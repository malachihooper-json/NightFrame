/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    P2P RELAY CHAIN - MULTI-HOP CONNECTIVITY                ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Manages peer-to-peer relay chains for late-stage propagation.             ║
 * ║  Enables internet access through complex relay networks.                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 * 
 * ARCHITECTURE:
 * User (no internet) → Drone1 → Drone2 → Drone3 → Internet
 * 
 * Each hop in the chain:
 * 1. Receives traffic from upstream
 * 2. Forwards to downstream (if available)
 * 3. Maintains health metrics
 * 4. Can be replaced if failing
 */

using System.Collections.Concurrent;
using System.Net;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Manages peer-to-peer relay chains for multi-hop internet access.
/// </summary>
public class P2PRelayChain
{
    private readonly ConcurrentDictionary<string, RelayNode> _knownNodes = new();
    private readonly List<RelayPath> _activePaths = new();
    private bool _hasDirectInternet;
    private string _localNodeId = "";
    
    public event Action<RelayPath>? OnPathEstablished;
    public event Action<string>? OnPathFailed;
    
    /// <summary>
    /// Whether this node has direct internet access (is an origin).
    /// </summary>
    public bool HasDirectInternet => _hasDirectInternet;
    
    /// <summary>
    /// Number of active relay paths.
    /// </summary>
    public int ActivePathCount => _activePaths.Count;
    
    /// <summary>
    /// Maximum hops allowed in a relay chain.
    /// </summary>
    public const int MaxHops = 5;
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          INITIALIZATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Initializes the relay chain manager.
    /// </summary>
    public async Task InitializeAsync(string nodeId)
    {
        _localNodeId = nodeId;
        
        // Check if we have direct internet
        _hasDirectInternet = await CheckDirectInternetAsync();
        
        Console.WriteLine($"◈ P2P Relay: Direct internet = {_hasDirectInternet}");
        
        // If we don't have internet, start looking for relay paths
        if (!_hasDirectInternet)
        {
            await DiscoverRelayPathsAsync();
        }
    }
    
    /// <summary>
    /// Checks if this node has direct internet connectivity.
    /// </summary>
    private async Task<bool> CheckDirectInternetAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://clients3.google.com/generate_204");
            return response.StatusCode == HttpStatusCode.NoContent;
        }
        catch
        {
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          PATH DISCOVERY
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Discovers available relay paths through nearby nodes.
    /// </summary>
    public async Task DiscoverRelayPathsAsync()
    {
        Console.WriteLine("◈ Discovering relay paths...");
        
        // In a real implementation, this would:
        // 1. Scan for nearby NFRAME WiFi networks
        // 2. Connect to each and query their relay capability
        // 3. Build paths through nodes that have internet access
        
        // For now, simulate path discovery
        foreach (var node in _knownNodes.Values.Where(n => n.HasInternet))
        {
            var path = await TryBuildPathAsync(node);
            if (path != null)
            {
                _activePaths.Add(path);
                OnPathEstablished?.Invoke(path);
            }
        }
    }
    
    /// <summary>
    /// Attempts to build a relay path through a node.
    /// </summary>
    private async Task<RelayPath?> TryBuildPathAsync(RelayNode targetNode)
    {
        // Simple direct path for now
        var path = new RelayPath
        {
            PathId = Guid.NewGuid().ToString()[..8],
            Hops = new List<string> { _localNodeId, targetNode.NodeId },
            EstablishedAt = DateTime.UtcNow,
            LastHealthCheck = DateTime.UtcNow,
            Latency = await MeasureLatencyAsync(targetNode)
        };
        
        if (path.Latency < TimeSpan.FromSeconds(10))
        {
            path.IsHealthy = true;
            return path;
        }
        
        return null;
    }
    
    /// <summary>
    /// Measures latency to a relay node.
    /// </summary>
    private async Task<TimeSpan> MeasureLatencyAsync(RelayNode node)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // In production, this would ping the node
        await Task.Delay(50); // Simulated
        
        sw.Stop();
        return sw.Elapsed;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          NODE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Registers a discovered relay node.
    /// </summary>
    public void RegisterNode(RelayNode node)
    {
        _knownNodes.TryAdd(node.NodeId, node);
    }
    
    /// <summary>
    /// Updates node health status.
    /// </summary>
    public void UpdateNodeHealth(string nodeId, bool isHealthy)
    {
        if (_knownNodes.TryGetValue(nodeId, out var node))
        {
            node.IsHealthy = isHealthy;
            node.LastSeen = DateTime.UtcNow;
            
            // Check if any paths are affected
            foreach (var path in _activePaths.Where(p => p.Hops.Contains(nodeId)))
            {
                if (!isHealthy)
                {
                    path.IsHealthy = false;
                    OnPathFailed?.Invoke(path.PathId);
                }
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          TRAFFIC ROUTING
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets the best available relay path.
    /// </summary>
    public RelayPath? GetBestPath()
    {
        return _activePaths
            .Where(p => p.IsHealthy)
            .OrderBy(p => p.Hops.Count) // Prefer shorter paths
            .ThenBy(p => p.Latency)     // Then by latency
            .FirstOrDefault();
    }
    
    /// <summary>
    /// Routes traffic through a relay path.
    /// </summary>
    public async Task<byte[]?> RouteRequestAsync(byte[] request, RelayPath path)
    {
        if (!path.IsHealthy)
        {
            return null;
        }
        
        // In production, this would forward the request through each hop
        // For now, simulate routing
        foreach (var hop in path.Hops.Skip(1)) // Skip self
        {
            if (_knownNodes.TryGetValue(hop, out var node))
            {
                // Forward to next hop
                await Task.Delay(10); // Simulated network delay
            }
        }
        
        // Simulated response
        return System.Text.Encoding.UTF8.GetBytes("RELAY_RESPONSE");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HEALTH MONITORING
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Performs health checks on all active paths.
    /// </summary>
    public async Task PerformHealthChecksAsync()
    {
        foreach (var path in _activePaths.ToList())
        {
            var isHealthy = await CheckPathHealthAsync(path);
            
            if (!isHealthy && path.IsHealthy)
            {
                path.IsHealthy = false;
                OnPathFailed?.Invoke(path.PathId);
            }
            
            path.LastHealthCheck = DateTime.UtcNow;
        }
        
        // If no healthy paths, try to discover new ones
        if (!_hasDirectInternet && !_activePaths.Any(p => p.IsHealthy))
        {
            await DiscoverRelayPathsAsync();
        }
    }
    
    private async Task<bool> CheckPathHealthAsync(RelayPath path)
    {
        // Check each hop in the path
        foreach (var hop in path.Hops.Skip(1))
        {
            if (_knownNodes.TryGetValue(hop, out var node))
            {
                if (!node.IsHealthy || (DateTime.UtcNow - node.LastSeen).TotalMinutes > 5)
                {
                    return false;
                }
            }
        }
        
        // Verify end-to-end connectivity
        var latency = await MeasurePathLatencyAsync(path);
        return latency < TimeSpan.FromSeconds(30);
    }
    
    private async Task<TimeSpan> MeasurePathLatencyAsync(RelayPath path)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        // Would ping through the entire path
        await Task.Delay(path.Hops.Count * 20); // Simulated
        
        sw.Stop();
        return sw.Elapsed;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          STATISTICS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets relay chain statistics.
    /// </summary>
    public RelayChainStats GetStats()
    {
        return new RelayChainStats
        {
            HasDirectInternet = _hasDirectInternet,
            KnownNodes = _knownNodes.Count,
            ActivePaths = _activePaths.Count(p => p.IsHealthy),
            TotalPaths = _activePaths.Count,
            AverageHops = _activePaths.Any() 
                ? _activePaths.Average(p => p.Hops.Count) 
                : 0,
            AverageLatencyMs = _activePaths.Any() 
                ? _activePaths.Average(p => p.Latency.TotalMilliseconds) 
                : 0
        };
    }
    
    /// <summary>
    /// Validates that relay chains work for late-stage scenarios.
    /// </summary>
    public async Task<bool> ValidateLateStageScenarioAsync()
    {
        Console.WriteLine("◈ Validating late-stage relay scenario...");
        
        // Scenario: User has no internet, connects to NFRAME SSID
        // They should be able to reach the internet through relays
        
        if (_hasDirectInternet)
        {
            Console.WriteLine("  ✓ This node has direct internet (can be relay origin)");
            return true;
        }
        
        if (_activePaths.Any(p => p.IsHealthy))
        {
            Console.WriteLine($"  ✓ {_activePaths.Count(p => p.IsHealthy)} healthy relay path(s) available");
            var bestPath = GetBestPath();
            if (bestPath != null)
            {
                Console.WriteLine($"  ✓ Best path: {bestPath.Hops.Count} hops, {bestPath.Latency.TotalMilliseconds:F0}ms latency");
            }
            return true;
        }
        
        Console.WriteLine("  ✗ No relay paths available - isolated node");
        return false;
    }
}

/// <summary>
/// Represents a node that can participate in relay chains.
/// </summary>
public class RelayNode
{
    public required string NodeId { get; init; }
    public string? IPAddress { get; init; }
    public bool HasInternet { get; set; }
    public bool IsHealthy { get; set; } = true;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public int HopsToInternet { get; set; }
}

/// <summary>
/// Represents a relay path through multiple nodes.
/// </summary>
public class RelayPath
{
    public required string PathId { get; init; }
    public List<string> Hops { get; init; } = new();
    public DateTime EstablishedAt { get; init; }
    public DateTime LastHealthCheck { get; set; }
    public TimeSpan Latency { get; set; }
    public bool IsHealthy { get; set; }
}

/// <summary>
/// Statistics for the relay chain system.
/// </summary>
public class RelayChainStats
{
    public bool HasDirectInternet { get; init; }
    public int KnownNodes { get; init; }
    public int ActivePaths { get; init; }
    public int TotalPaths { get; init; }
    public double AverageHops { get; init; }
    public double AverageLatencyMs { get; init; }
    
    public override string ToString()
    {
        return $@"
P2P Relay Chain Stats
─────────────────────
Direct Internet: {(HasDirectInternet ? "Yes" : "No")}
Known Nodes: {KnownNodes}
Active Paths: {ActivePaths}/{TotalPaths}
Average Hops: {AverageHops:F1}
Average Latency: {AverageLatencyMs:F0}ms
";
    }
}
