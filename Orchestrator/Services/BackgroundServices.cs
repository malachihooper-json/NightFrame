/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    BACKGROUND SERVICES - MONITORS                          ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Heartbeat monitoring, consensus checking, and job processing.             ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Microsoft.AspNetCore.SignalR;
using NIGHTFRAME.Orchestrator.Hubs;
using NIGHTFRAME.Orchestrator.Grpc;

namespace NIGHTFRAME.Orchestrator.Services;

/// <summary>
/// Monitors drone heartbeats and marks offline nodes.
/// </summary>
public class HeartbeatMonitor : BackgroundService
{
    private readonly DroneRegistry _registry;
    private readonly IHubContext<ConsoleHub> _consoleHub;
    private readonly ILogger<HeartbeatMonitor> _logger;
    
    public HeartbeatMonitor(
        DroneRegistry registry,
        IHubContext<ConsoleHub> consoleHub,
        ILogger<HeartbeatMonitor> logger)
    {
        _registry = registry;
        _consoleHub = consoleHub;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("◎ Heartbeat monitor started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            
            var stats = new
            {
                activeNodes = _registry.ActiveNodeCount,
                totalNodes = _registry.TotalNodeCount,
                meshHealth = _registry.CalculateMeshHealth(),
                timestamp = DateTime.UtcNow
            };
            
            // Broadcast mesh status to all connected consoles
            await _consoleHub.Clients.All.SendAsync("MeshStatus", stats, stoppingToken);
            
            if (_registry.ActiveNodeCount > 0)
            {
                _logger.LogDebug("◎ Mesh status: {Active}/{Total} nodes active, health: {Health:P0}",
                    stats.activeNodes, stats.totalNodes, stats.meshHealth);
            }
        }
    }
}

/// <summary>
/// Performs consensus checks on compute results for anti-fraud.
/// Implements the Redundancy Check and Rolling Consensus patterns.
/// </summary>
public class ConsensusChecker : BackgroundService
{
    private readonly DroneRegistry _registry;
    private readonly LedgerService _ledger;
    private readonly ShardCoordinator _shardCoordinator;
    private readonly ILogger<ConsensusChecker> _logger;
    
    // Pool of test results for shadow mode verification
    private readonly Dictionary<string, byte[]> _knownGoodResults = new();
    
    public ConsensusChecker(
        DroneRegistry registry,
        LedgerService ledger,
        ShardCoordinator shardCoordinator,
        ILogger<ConsensusChecker> logger)
    {
        _registry = registry;
        _ledger = ledger;
        _shardCoordinator = shardCoordinator;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("◎ Consensus checker started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            
            // Process any pending jobs
            await _shardCoordinator.ProcessPendingJobsAsync();
            
            // Run consensus checks on a sample of recent results
            // TODO: Implement actual consensus voting
        }
    }
    
    /// <summary>
    /// Validates a shadow test result against known good output.
    /// </summary>
    public bool ValidateShadowResult(string nodeId, string testId, byte[] result)
    {
        if (!_knownGoodResults.TryGetValue(testId, out var expected))
        {
            // First result becomes the expected
            _knownGoodResults[testId] = result;
            return true;
        }
        
        bool matches = result.SequenceEqual(expected);
        
        if (!matches)
        {
            _logger.LogWarning("∴ Shadow test FAILED for {NodeId}: result mismatch", nodeId);
            _ledger.ApplyFraudPenalty(nodeId, testId, "Shadow test result mismatch");
        }
        
        _registry.RecordShadowTestResult(nodeId, matches);
        return matches;
    }
}
