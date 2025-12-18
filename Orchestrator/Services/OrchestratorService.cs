/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    gRPC SERVICE - DRONE COMMUNICATION                      ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Implements the Mothership-side of the NIGHTFRAME mesh protocol.           ║
 * ║  Handles registration, bidirectional streaming, and compute sharding.      ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Grpc.Core;
using NIGHTFRAME.Orchestrator.Grpc;
using NIGHTFRAME.Orchestrator.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace NIGHTFRAME.Orchestrator.Services;

public class OrchestratorService : NightframeOrchestrator.NightframeOrchestratorBase
{
    private readonly DroneRegistry _registry;
    private readonly LedgerService _ledger;
    private readonly ShardCoordinator _shardCoordinator;
    private readonly IHubContext<ConsoleHub> _consoleHub;
    private readonly ILogger<OrchestratorService> _logger;
    
    public OrchestratorService(
        DroneRegistry registry,
        LedgerService ledger,
        ShardCoordinator shardCoordinator,
        IHubContext<ConsoleHub> consoleHub,
        ILogger<OrchestratorService> logger)
    {
        _registry = registry;
        _ledger = ledger;
        _shardCoordinator = shardCoordinator;
        _consoleHub = consoleHub;
        _logger = logger;
    }
    
    /// <summary>
    /// Handles initial drone registration (the Handshake of Truth).
    /// </summary>
    public override Task<RegistrationResponse> Register(DroneManifest manifest, ServerCallContext context)
    {
        var address = context.Peer ?? "unknown";
        _logger.LogInformation("◈ Registration request from {NodeId} @ {Address}", manifest.NodeId, address);
        
        var response = _registry.EvaluateManifest(manifest, address);
        
        // Notify web console
        _ = _consoleHub.Clients.All.SendAsync("NodeRegistered", new
        {
            nodeId = manifest.NodeId,
            role = response.AssignedRole.ToString(),
            accepted = response.Accepted,
            reason = response.Reason
        });
        
        return Task.FromResult(response);
    }
    
    /// <summary>
    /// Bidirectional streaming connection for continuous drone communication.
    /// Drones send heartbeats and status; Mothership sends commands.
    /// </summary>
    public override async Task Connect(
        IAsyncStreamReader<DroneMessage> requestStream,
        IServerStreamWriter<MothershipCommand> responseStream,
        ServerCallContext context)
    {
        string? nodeId = null;
        
        try
        {
            await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
            {
                switch (message.PayloadCase)
                {
                    case DroneMessage.PayloadOneofCase.Heartbeat:
                        nodeId = message.Heartbeat.NodeId;
                        _registry.RecordHeartbeat(
                            message.Heartbeat.NodeId,
                            message.Heartbeat.CpuLoad,
                            message.Heartbeat.RamUsedMb);
                        break;
                    
                    case DroneMessage.PayloadOneofCase.TaskStatus:
                        await HandleTaskStatus(message.TaskStatus, responseStream);
                        break;
                    
                    case DroneMessage.PayloadOneofCase.PeerDiscovery:
                        await HandlePeerDiscovery(message.PeerDiscovery);
                        break;
                    
                    case DroneMessage.PayloadOneofCase.LogEntry:
                        await HandleLogEntry(message.LogEntry);
                        break;
                }
                
                // Check if we need to send any commands to this drone
                if (nodeId != null)
                {
                    await TrySendCommandsAsync(nodeId, responseStream, context.CancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException || 
                                   ex.Message.Contains("cancelled"))
        {
            _logger.LogInformation("∴ Connection closed for {NodeId}", nodeId ?? "unknown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "∴ Connection error for {NodeId}", nodeId ?? "unknown");
        }
    }
    
    /// <summary>
    /// Handles a drone requesting a compute shard.
    /// </summary>
    public override Task<ComputeShard> RequestShard(ShardRequest request, ServerCallContext context)
    {
        var shard = _shardCoordinator.GetNextShardForNode(request.NodeId);
        
        if (shard == null)
        {
            // No work available
            return Task.FromResult(new ComputeShard
            {
                ShardId = "",
                ModelHash = "",
                StartLayer = -1,
                EndLayer = -1
            });
        }
        
        _logger.LogDebug("◎ Assigned shard {ShardId} to {NodeId}", shard.ShardId, request.NodeId);
        return Task.FromResult(shard);
    }
    
    /// <summary>
    /// Handles compute result submission.
    /// </summary>
    public override Task<ResultAck> SubmitResult(ShardResult result, ServerCallContext context)
    {
        _shardCoordinator.RecordShardCompletion(
            result.ShardId,
            result.NodeId,
            result.OutputData.ToByteArray(),
            result.ComputeTimeMs);
        
        // TODO: Verify signature for anti-fraud
        
        var balance = _ledger.GetBalance(result.NodeId);
        
        return Task.FromResult(new ResultAck
        {
            Accepted = true,
            CreditsEarned = LedgerService.CreditsPerComputeShard
        });
    }
    
    /// <summary>
    /// Streams binary update to drone.
    /// </summary>
    public override async Task RequestUpdate(
        UpdateRequest request,
        IServerStreamWriter<BinaryChunk> responseStream,
        ServerCallContext context)
    {
        var (version, _) = VersionManager.GetVersionForNode(request.NodeId);
        
        _logger.LogInformation("◈ Streaming update {Version} to {NodeId}", version, request.NodeId);
        
        int chunkIndex = 0;
        var chunks = VersionManager.StreamBinaryAsync(version).ToBlockingEnumerable().ToList();
        int totalChunks = chunks.Count;
        
        foreach (var chunkData in chunks)
        {
            var chunk = new BinaryChunk
            {
                ChunkIndex = chunkIndex++,
                TotalChunks = totalChunks,
                Data = Google.Protobuf.ByteString.CopyFrom(chunkData),
                ChunkHash = Google.Protobuf.ByteString.CopyFrom(
                    System.Security.Cryptography.SHA256.HashData(chunkData))
            };
            
            await responseStream.WriteAsync(chunk);
        }
    }
    
    /// <summary>
    /// Handles peer discovery reports for mesh building.
    /// </summary>
    public override Task<Empty> ReportPeer(PeerInfo request, ServerCallContext context)
    {
        _logger.LogDebug("◎ Peer report from {From}: discovered {Peer} @ {Address}",
            request.FromNodeId, request.PeerNodeId, request.PeerAddress);
        
        // TODO: Record for orchestrated P2P introductions
        
        return Task.FromResult(new Empty());
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                              HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task HandleTaskStatus(Grpc.TaskStatus status, IServerStreamWriter<MothershipCommand> responseStream)
    {
        _logger.LogDebug("◎ Task {TaskId} status: {State} ({Progress}%)",
            status.TaskId, status.State, status.Progress * 100);
        
        // Notify web console
        await _consoleHub.Clients.All.SendAsync("TaskProgress", new
        {
            taskId = status.TaskId,
            state = status.State.ToString(),
            progress = status.Progress
        });
    }
    
    private async Task HandlePeerDiscovery(PeerDiscovery discovery)
    {
        _logger.LogDebug("◎ {NodeId} discovered peer {Peer} @ {Address}",
            discovery.DiscoveredNodeId, discovery.DiscoveredNodeId, discovery.DiscoveredAddress);
        
        // Record for mesh topology
        await _consoleHub.Clients.All.SendAsync("PeerDiscovered", new
        {
            discoveredNodeId = discovery.DiscoveredNodeId,
            address = discovery.DiscoveredAddress,
            role = discovery.DiscoveredRole.ToString()
        });
    }
    
    private async Task HandleLogEntry(LogEntry log)
    {
        // Forward to web console
        await _consoleHub.Clients.All.SendAsync("DroneLog", new
        {
            nodeId = log.NodeId,
            level = log.Level,
            message = log.Message,
            timestamp = DateTimeOffset.FromUnixTimeSeconds(log.TimestampUnix)
        });
    }
    
    private async Task TrySendCommandsAsync(
        string nodeId, 
        IServerStreamWriter<MothershipCommand> stream,
        CancellationToken ct)
    {
        var node = _registry.GetNode(nodeId);
        if (node == null) return;
        
        // Check if node needs update
        if (node.BinaryVersion != VersionManager.CurrentVersion)
        {
            var (targetVersion, hash) = VersionManager.GetVersionForNode(nodeId);
            
            // Find a peer that has the update (P2P distribution)
            var peerWithUpdate = _registry.GetNodesForRole(Grpc.NodeRole.RoleGeneral)
                .FirstOrDefault(n => n.BinaryVersion == targetVersion && n.NodeId != nodeId);
            
            await stream.WriteAsync(new MothershipCommand
            {
                Update = new UpdateCommand
                {
                    NewVersion = targetVersion,
                    PeerWithBinary = peerWithUpdate?.Address ?? "", // Empty = download from mothership
                    ExpectedHash = Google.Protobuf.ByteString.CopyFrom(hash)
                }
            }, ct);
        }
        
        // Check if node needs model hydration
        // TODO: Implement model cache checking
    }
}
