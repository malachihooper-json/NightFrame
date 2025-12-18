/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SHARD COORDINATOR - DISTRIBUTED COMPUTE                 ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Coordinates pipeline-parallel inference across the drone mesh.            ║
 * ║  Implements the "Quantum Threshold" pattern for large model sharding.      ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Collections.Concurrent;
using NIGHTFRAME.Orchestrator.Grpc;

namespace NIGHTFRAME.Orchestrator.Services;

public class InferenceJob
{
    public required string JobId { get; init; }
    public required string PromptId { get; init; }
    public required byte[] InputData { get; init; }
    public required int TotalLayers { get; init; }
    public required int LayersPerShard { get; init; }
    public required DateTime CreatedAt { get; init; }
    
    public List<ShardAssignment> Shards { get; } = new();
    public ConcurrentDictionary<int, byte[]> CompletedShards { get; } = new();
    public byte[]? FinalResult { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ShardAssignment
{
    public required string ShardId { get; init; }
    public required string NodeId { get; set; }
    public required int StartLayer { get; init; }
    public required int EndLayer { get; init; }
    public required string NextHopAddress { get; set; }
    public ShardStatus Status { get; set; } = ShardStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum ShardStatus { Pending, Assigned, Running, Completed, Failed, Retrying }

public class ShardCoordinator
{
    private readonly DroneRegistry _registry;
    private readonly LedgerService _ledger;
    private readonly ILogger<ShardCoordinator> _logger;
    
    private readonly ConcurrentDictionary<string, InferenceJob> _activeJobs = new();
    private readonly ConcurrentQueue<InferenceJob> _pendingJobs = new();
    
    // Model architecture (configurable)
    private const int DefaultTotalLayers = 48;
    private const int DefaultLayersPerShard = 12;
    
    public ShardCoordinator(
        DroneRegistry registry, 
        LedgerService ledger,
        ILogger<ShardCoordinator> logger)
    {
        _registry = registry;
        _ledger = ledger;
        _logger = logger;
    }
    
    /// <summary>
    /// Creates an inference job and shards it across available compute nodes.
    /// Implements Pipeline Parallelism.
    /// </summary>
    public async Task<string> CreateInferenceJobAsync(string promptId, byte[] inputData)
    {
        var job = new InferenceJob
        {
            JobId = $"JOB_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..20],
            PromptId = promptId,
            InputData = inputData,
            TotalLayers = DefaultTotalLayers,
            LayersPerShard = DefaultLayersPerShard,
            CreatedAt = DateTime.UtcNow
        };
        
        // Get available compute nodes
        var computeNodes = _registry.GetNodesForRole(NodeRole.RoleCompute)
            .Concat(_registry.GetNodesForRole(NodeRole.RoleGeneral))
            .Where(n => !n.IsProbationary)
            .ToList();
        
        if (computeNodes.Count == 0)
        {
            // No nodes available - queue the job
            _pendingJobs.Enqueue(job);
            _logger.LogWarning("∴ No compute nodes available, job {JobId} queued", job.JobId);
            return job.JobId;
        }
        
        // Create shard assignments
        int shardCount = (int)Math.Ceiling((double)job.TotalLayers / job.LayersPerShard);
        
        for (int i = 0; i < shardCount; i++)
        {
            int startLayer = i * job.LayersPerShard;
            int endLayer = Math.Min(startLayer + job.LayersPerShard - 1, job.TotalLayers - 1);
            
            // Select node (round-robin with load balancing)
            var node = computeNodes[i % computeNodes.Count];
            var nextNode = i < shardCount - 1 
                ? computeNodes[(i + 1) % computeNodes.Count]
                : null;
            
            var shard = new ShardAssignment
            {
                ShardId = $"{job.JobId}_SHARD_{i:D2}",
                NodeId = node.NodeId,
                StartLayer = startLayer,
                EndLayer = endLayer,
                NextHopAddress = nextNode?.Address ?? "RETURN_TO_ORCHESTRATOR",
                Status = ShardStatus.Assigned
            };
            
            job.Shards.Add(shard);
        }
        
        _activeJobs[job.JobId] = job;
        
        _logger.LogInformation("◈ Created job {JobId} with {ShardCount} shards across {NodeCount} nodes",
            job.JobId, shardCount, Math.Min(shardCount, computeNodes.Count));
        
        return job.JobId;
    }
    
    /// <summary>
    /// Gets the next shard for a node to process.
    /// Called by drones requesting work.
    /// </summary>
    public ComputeShard? GetNextShardForNode(string nodeId)
    {
        // Find any shard assigned to this node that hasn't started
        foreach (var job in _activeJobs.Values)
        {
            var shard = job.Shards.FirstOrDefault(s => 
                s.NodeId == nodeId && s.Status == ShardStatus.Assigned);
            
            if (shard != null)
            {
                shard.Status = ShardStatus.Running;
                shard.StartedAt = DateTime.UtcNow;
                
                // Determine input data
                byte[] inputData;
                if (shard.StartLayer == 0)
                {
                    // First shard gets original input
                    inputData = job.InputData;
                }
                else
                {
                    // Later shards wait for previous shard output
                    int prevShardIndex = job.Shards.IndexOf(shard) - 1;
                    if (!job.CompletedShards.TryGetValue(prevShardIndex, out var prevOutput))
                    {
                        // Previous shard not ready yet
                        shard.Status = ShardStatus.Assigned;
                        shard.StartedAt = null;
                        return null;
                    }
                    inputData = prevOutput;
                }
                
                return new ComputeShard
                {
                    ShardId = shard.ShardId,
                    ModelHash = "model-v1.0-sha256",
                    StartLayer = shard.StartLayer,
                    EndLayer = shard.EndLayer,
                    InputData = Google.Protobuf.ByteString.CopyFrom(inputData),
                    NextHopAddress = shard.NextHopAddress
                };
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Records shard completion and handles pipeline flow.
    /// </summary>
    public void RecordShardCompletion(string shardId, string nodeId, byte[] outputData, long computeTimeMs)
    {
        // Find the job containing this shard
        foreach (var job in _activeJobs.Values)
        {
            var shard = job.Shards.FirstOrDefault(s => s.ShardId == shardId);
            if (shard == null) continue;
            
            shard.Status = ShardStatus.Completed;
            shard.CompletedAt = DateTime.UtcNow;
            
            int shardIndex = job.Shards.IndexOf(shard);
            job.CompletedShards[shardIndex] = outputData;
            
            // Credit the node
            _ledger.CreditComputeWork(nodeId, shardId, computeTimeMs);
            
            _logger.LogDebug("◎ Shard {ShardId} completed by {NodeId} in {Time}ms",
                shardId, nodeId, computeTimeMs);
            
            // Check if all shards are complete
            if (job.Shards.All(s => s.Status == ShardStatus.Completed))
            {
                // Get final output from last shard
                int lastIndex = job.Shards.Count - 1;
                if (job.CompletedShards.TryGetValue(lastIndex, out var finalOutput))
                {
                    job.FinalResult = finalOutput;
                    job.CompletedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("◈ Job {JobId} COMPLETED in {Duration}ms",
                        job.JobId, (job.CompletedAt.Value - job.CreatedAt).TotalMilliseconds);
                }
            }
            
            return;
        }
        
        _logger.LogWarning("∴ Received result for unknown shard {ShardId}", shardId);
    }
    
    /// <summary>
    /// Gets job status.
    /// </summary>
    public object? GetJobStatus(string jobId)
    {
        if (!_activeJobs.TryGetValue(jobId, out var job))
            return null;
        
        return new
        {
            job.JobId,
            job.PromptId,
            status = job.FinalResult != null ? "completed" : "processing",
            shardsCompleted = job.Shards.Count(s => s.Status == ShardStatus.Completed),
            shardsTotal = job.Shards.Count,
            createdAt = job.CreatedAt,
            completedAt = job.CompletedAt,
            hasResult = job.FinalResult != null
        };
    }
    
    /// <summary>
    /// Gets result of completed job.
    /// </summary>
    public byte[]? GetJobResult(string jobId)
    {
        return _activeJobs.TryGetValue(jobId, out var job) ? job.FinalResult : null;
    }
    
    /// <summary>
    /// Processes pending jobs when new nodes become available.
    /// </summary>
    public async Task ProcessPendingJobsAsync()
    {
        while (_pendingJobs.TryDequeue(out var job))
        {
            var computeNodes = _registry.GetNodesForRole(NodeRole.RoleCompute)
                .Concat(_registry.GetNodesForRole(NodeRole.RoleGeneral))
                .Where(n => !n.IsProbationary)
                .ToList();
            
            if (computeNodes.Count == 0)
            {
                // Still no nodes, put it back
                _pendingJobs.Enqueue(job);
                break;
            }
            
            // Re-create as a new job
            await CreateInferenceJobAsync(job.PromptId, job.InputData);
        }
    }
}
