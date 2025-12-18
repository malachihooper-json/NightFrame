/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              MULTI-NODE NETWORK SIMULATOR                                  ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Simulates entire NIGHTFRAME mesh networks without physical hardware.      ║
 * ║  Tests GOSSIP propagation, P2P relay, update deployment, edge cases.       ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Collections.Concurrent;
using System.Security.Cryptography;
using NIGHTFRAME.Drone.Network;

namespace NIGHTFRAME.Drone.Testing;

/// <summary>
/// Simulates a complete NIGHTFRAME mesh network for testing.
/// </summary>
public class NetworkSimulator
{
    private readonly ConcurrentDictionary<string, SimulatedNode> _nodes = new();
    private readonly ConcurrentDictionary<string, SimulatedClient> _clients = new();
    private readonly List<SimulationEvent> _eventLog = new();
    private readonly Random _random = new();
    private int _tickCount;
    
    public event Action<SimulationEvent>? OnEvent;
    
    // Network parameters
    public int NetworkLatencyMs { get; set; } = 50;
    public double PacketLossRate { get; set; } = 0.01; // 1%
    public int GossipFanout { get; set; } = 3;
    public int MaxHops { get; set; } = 5;
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          NODE MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Creates a simulated node in the network.
    /// </summary>
    public SimulatedNode CreateNode(string? nodeId = null, bool hasInternet = true)
    {
        nodeId ??= $"NFRAME_{Guid.NewGuid().ToString()[..8].ToUpper()}";
        
        var node = new SimulatedNode
        {
            NodeId = nodeId,
            HasInternetAccess = hasInternet,
            CanBroadcastSSID = true,
            IsOnline = true,
            CreatedAt = DateTime.UtcNow,
            BandwidthLimitMbps = 50,
            CurrentBandwidthUsedMbps = 0
        };
        
        _nodes[nodeId] = node;
        LogEvent(SimEventType.NodeCreated, nodeId, $"Node created (internet={hasInternet})");
        
        return node;
    }
    
    /// <summary>
    /// Creates multiple nodes at once.
    /// </summary>
    public List<SimulatedNode> CreateNodes(int count, double internetRatio = 0.7)
    {
        var nodes = new List<SimulatedNode>();
        for (int i = 0; i < count; i++)
        {
            var hasInternet = _random.NextDouble() < internetRatio;
            nodes.Add(CreateNode(hasInternet: hasInternet));
        }
        return nodes;
    }
    
    /// <summary>
    /// Connects two nodes as peers.
    /// </summary>
    public void ConnectNodes(string nodeA, string nodeB)
    {
        if (_nodes.TryGetValue(nodeA, out var a) && _nodes.TryGetValue(nodeB, out var b))
        {
            a.Peers.Add(nodeB);
            b.Peers.Add(nodeA);
            LogEvent(SimEventType.PeerConnected, nodeA, $"Connected to {nodeB}");
        }
    }
    
    /// <summary>
    /// Creates a mesh topology where nodes connect to nearby peers.
    /// </summary>
    public void CreateMeshTopology(int peersPerNode = 4)
    {
        var nodeList = _nodes.Values.ToList();
        foreach (var node in nodeList)
        {
            var potentialPeers = nodeList
                .Where(n => n.NodeId != node.NodeId && !node.Peers.Contains(n.NodeId))
                .OrderBy(_ => _random.Next())
                .Take(peersPerNode);
            
            foreach (var peer in potentialPeers)
            {
                ConnectNodes(node.NodeId, peer.NodeId);
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          CLIENT SIMULATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Simulates a client connecting to a node's hotspot.
    /// </summary>
    public SimulatedClient ConnectClient(string nodeId, string platform = "iOS")
    {
        var client = new SimulatedClient
        {
            ClientId = $"CLIENT_{Guid.NewGuid().ToString()[..6]}",
            ConnectedToNode = nodeId,
            Platform = platform,
            IsGuest = platform is "iOS" or "Android",
            ConnectedAt = DateTime.UtcNow,
            BandwidthUsedBytes = 0
        };
        
        _clients[client.ClientId] = client;
        
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.ConnectedClients.Add(client.ClientId);
        }
        
        LogEvent(SimEventType.ClientConnected, nodeId, 
            $"Client {client.ClientId} ({platform}) connected as {(client.IsGuest ? "guest" : "member")}");
        
        return client;
    }
    
    /// <summary>
    /// Simulates client browsing activity.
    /// </summary>
    public void SimulateClientActivity(string clientId, long bytesDown, long bytesUp)
    {
        if (_clients.TryGetValue(clientId, out var client))
        {
            client.BandwidthUsedBytes += bytesDown + bytesUp;
            
            if (_nodes.TryGetValue(client.ConnectedToNode, out var node))
            {
                node.TotalBytesServed += bytesDown + bytesUp;
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          GOSSIP SIMULATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Simulates GOSSIP update propagation across the network.
    /// </summary>
    public GossipSimulationResult SimulateGossipUpdate(string sourceNodeId, string updateId)
    {
        var result = new GossipSimulationResult
        {
            UpdateId = updateId,
            SourceNodeId = sourceNodeId,
            StartTime = DateTime.UtcNow
        };
        
        var receivedNodes = new HashSet<string> { sourceNodeId };
        var pendingNodes = new Queue<(string nodeId, int hops)>();
        pendingNodes.Enqueue((sourceNodeId, 0));
        
        while (pendingNodes.Count > 0)
        {
            var (currentNodeId, hops) = pendingNodes.Dequeue();
            
            if (hops >= MaxHops)
            {
                result.DroppedAtMaxHops++;
                continue;
            }
            
            if (!_nodes.TryGetValue(currentNodeId, out var currentNode))
                continue;
            
            // Select random peers to propagate to (GOSSIP fanout)
            var eligiblePeers = currentNode.Peers
                .Where(p => !receivedNodes.Contains(p))
                .OrderBy(_ => _random.Next())
                .Take(GossipFanout)
                .ToList();
            
            foreach (var peerId in eligiblePeers)
            {
                // Simulate packet loss
                if (_random.NextDouble() < PacketLossRate)
                {
                    result.PacketsLost++;
                    continue;
                }
                
                // Simulate latency
                result.TotalLatencyMs += NetworkLatencyMs;
                
                if (!_nodes.TryGetValue(peerId, out var peerNode) || !peerNode.IsOnline)
                {
                    result.OfflineNodes++;
                    continue;
                }
                
                receivedNodes.Add(peerId);
                pendingNodes.Enqueue((peerId, hops + 1));
                result.HopDistribution[hops + 1] = result.HopDistribution.GetValueOrDefault(hops + 1) + 1;
            }
        }
        
        result.NodesReached = receivedNodes.Count;
        result.TotalNodes = _nodes.Count;
        result.CoveragePercent = (double)receivedNodes.Count / _nodes.Count * 100;
        result.EndTime = DateTime.UtcNow;
        
        LogEvent(SimEventType.GossipComplete, sourceNodeId, 
            $"Update {updateId} reached {result.NodesReached}/{result.TotalNodes} nodes ({result.CoveragePercent:F1}%)");
        
        return result;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          P2P RELAY SIMULATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Finds the shortest path from a node without internet to one with internet.
    /// </summary>
    public RelayPathResult FindRelayPath(string sourceNodeId)
    {
        var result = new RelayPathResult
        {
            SourceNodeId = sourceNodeId
        };
        
        if (!_nodes.TryGetValue(sourceNodeId, out var sourceNode))
        {
            result.Success = false;
            result.FailureReason = "Source node not found";
            return result;
        }
        
        if (sourceNode.HasInternetAccess)
        {
            result.Success = true;
            result.Path = new List<string> { sourceNodeId };
            result.HopCount = 0;
            return result;
        }
        
        // BFS to find shortest path to internet
        var visited = new HashSet<string> { sourceNodeId };
        var queue = new Queue<(string nodeId, List<string> path)>();
        queue.Enqueue((sourceNodeId, new List<string> { sourceNodeId }));
        
        while (queue.Count > 0)
        {
            var (currentId, path) = queue.Dequeue();
            
            if (!_nodes.TryGetValue(currentId, out var currentNode))
                continue;
            
            foreach (var peerId in currentNode.Peers)
            {
                if (visited.Contains(peerId))
                    continue;
                
                visited.Add(peerId);
                var newPath = new List<string>(path) { peerId };
                
                if (_nodes.TryGetValue(peerId, out var peerNode) && peerNode.HasInternetAccess)
                {
                    result.Success = true;
                    result.Path = newPath;
                    result.HopCount = newPath.Count - 1;
                    result.InternetNodeId = peerId;
                    result.EstimatedLatencyMs = result.HopCount * NetworkLatencyMs;
                    
                    LogEvent(SimEventType.RelayPathFound, sourceNodeId,
                        $"Relay path found: {string.Join(" → ", newPath)} ({result.HopCount} hops)");
                    
                    return result;
                }
                
                if (newPath.Count < MaxHops)
                {
                    queue.Enqueue((peerId, newPath));
                }
            }
        }
        
        result.Success = false;
        result.FailureReason = "No path to internet found within max hops";
        LogEvent(SimEventType.RelayPathFailed, sourceNodeId, result.FailureReason);
        
        return result;
    }
    
    /// <summary>
    /// Simulates late-stage propagation scenario.
    /// </summary>
    public LateStageResult SimulateLateStageScenario(int nodesWithoutInternet = 5)
    {
        var result = new LateStageResult();
        
        // Create isolated nodes (no direct internet)
        var isolatedNodes = new List<SimulatedNode>();
        for (int i = 0; i < nodesWithoutInternet; i++)
        {
            isolatedNodes.Add(CreateNode(hasInternet: false));
        }
        
        // Connect them in a chain
        for (int i = 0; i < isolatedNodes.Count - 1; i++)
        {
            ConnectNodes(isolatedNodes[i].NodeId, isolatedNodes[i + 1].NodeId);
        }
        
        // Connect last isolated node to a node with internet
        var internetNode = _nodes.Values.FirstOrDefault(n => n.HasInternetAccess);
        if (internetNode != null)
        {
            ConnectNodes(isolatedNodes.Last().NodeId, internetNode.NodeId);
        }
        
        // Test if first isolated node can reach internet
        var relayResult = FindRelayPath(isolatedNodes.First().NodeId);
        
        result.NodesCreated = nodesWithoutInternet;
        result.RelaySuccess = relayResult.Success;
        result.HopsRequired = relayResult.HopCount;
        result.Path = relayResult.Path;
        
        // Simulate clients connecting to isolated nodes
        foreach (var node in isolatedNodes)
        {
            var client = ConnectClient(node.NodeId, "iPhone");
            result.ClientsConnected++;
            
            // Can this client reach internet?
            var clientRelay = FindRelayPath(node.NodeId);
            if (clientRelay.Success)
                result.ClientsWithInternet++;
        }
        
        return result;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          EDGE CASE SCENARIOS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Runs comprehensive edge case tests.
    /// </summary>
    public EdgeCaseReport RunEdgeCaseTests()
    {
        var report = new EdgeCaseReport();
        
        Console.WriteLine("◈ Running Network Simulation Edge Cases...");
        Console.WriteLine();
        
        // Test 1: Single node network
        RunEdgeCase(report, "Single node (no peers)", () =>
        {
            var sim = new NetworkSimulator();
            var node = sim.CreateNode(hasInternet: true);
            var client = sim.ConnectClient(node.NodeId, "iOS");
            return client.IsGuest && sim._nodes.Count == 1;
        });
        
        // Test 2: All nodes offline
        RunEdgeCase(report, "All nodes offline", () =>
        {
            var sim = new NetworkSimulator();
            sim.CreateNodes(5, internetRatio: 0.5);
            sim.CreateMeshTopology();
            
            foreach (var node in sim._nodes.Values)
                node.IsOnline = false;
            
            var result = sim.SimulateGossipUpdate(sim._nodes.Keys.First(), "TEST");
            
            return result.NodesReached == 1; // Only source
        });
        
        // Test 3: High packet loss
        RunEdgeCase(report, "50% packet loss", () =>
        {
            var sim = new NetworkSimulator();
            sim.CreateNodes(10, internetRatio: 0.5);
            sim.CreateMeshTopology();
            sim.PacketLossRate = 0.5;
            
            var result = sim.SimulateGossipUpdate(sim._nodes.Keys.First(), "TEST");
            
            // With 50% packet loss, we expect some packets lost OR limited reach
            return result.PacketsLost > 0 || result.CoveragePercent < 100;
        });
        
        // Test 4: No internet in network
        RunEdgeCase(report, "No internet nodes", () =>
        {
            var sim = new NetworkSimulator();
            sim.CreateNodes(5, internetRatio: 0.0);
            sim.CreateMeshTopology();
            
            var relay = sim.FindRelayPath(sim._nodes.Keys.First());
            return !relay.Success;
        });
        
        // Test 5: Chain topology (worst case relay)
        RunEdgeCase(report, "Chain topology (4 hops)", () =>
        {
            var sim = new NetworkSimulator();
            var nodes = new List<SimulatedNode>();
            for (int i = 0; i < 5; i++)
            {
                nodes.Add(sim.CreateNode(hasInternet: i == 4));
            }
            for (int i = 0; i < 4; i++)
            {
                sim.ConnectNodes(nodes[i].NodeId, nodes[i + 1].NodeId);
            }
            
            var relay = sim.FindRelayPath(nodes[0].NodeId);
            return relay.Success && relay.HopCount == 4;
        });
        
        // Test 6: Exceed max hops
        RunEdgeCase(report, "Exceed max hops (6 hops)", () =>
        {
            var sim = new NetworkSimulator();
            sim.MaxHops = 5;
            var nodes = new List<SimulatedNode>();
            for (int i = 0; i < 7; i++)
            {
                nodes.Add(sim.CreateNode(hasInternet: i == 6));
            }
            for (int i = 0; i < 6; i++)
            {
                sim.ConnectNodes(nodes[i].NodeId, nodes[i + 1].NodeId);
            }
            
            var relay = sim.FindRelayPath(nodes[0].NodeId);
            return !relay.Success; // Should fail - too many hops
        });
        
        // Test 7: Platform detection
        RunEdgeCase(report, "iOS guest detection", () =>
        {
            var cap = OnboardingCapability.DetectFromUserAgent("Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X)");
            return cap.RecommendedPath == OnboardingPath.GuestOnly;
        });
        
        // Test 8: Windows full member
        RunEdgeCase(report, "Windows full member", () =>
        {
            var cap = OnboardingCapability.DetectFromUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            return cap.RecommendedPath == OnboardingPath.FullMember;
        });
        
        // Test 9: Unknown platform
        RunEdgeCase(report, "Unknown platform handling", () =>
        {
            var cap = OnboardingCapability.DetectFromUserAgent("SomeBizarreBot/1.0");
            return cap.RecommendedPath == OnboardingPath.Unknown;
        });
        
        // Test 10: Concurrent clients
        RunEdgeCase(report, "100 concurrent clients", () =>
        {
            var sim = new NetworkSimulator();
            var node = sim.CreateNode(hasInternet: true);
            for (int i = 0; i < 100; i++)
            {
                sim.ConnectClient(node.NodeId, i % 2 == 0 ? "iOS" : "Android");
            }
            return node.ConnectedClients.Count >= 100;
        });
        
        report.PrintSummary();
        return report;
    }
    
    private void RunEdgeCase(EdgeCaseReport report, string name, Func<bool> test)
    {
        try
        {
            var passed = test();
            report.AddResult(name, passed);
            Console.WriteLine($"  {(passed ? "✓" : "✗")} {name}");
        }
        catch (Exception ex)
        {
            report.AddResult(name, false, ex.Message);
            Console.WriteLine($"  ✗ {name}: {ex.Message}");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          FULL SIMULATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Runs a full network simulation scenario.
    /// </summary>
    public FullSimulationResult RunFullSimulation(int nodeCount = 20, int clientsPerNode = 3, int ticks = 100)
    {
        var result = new FullSimulationResult
        {
            NodeCount = nodeCount,
            StartTime = DateTime.UtcNow
        };
        
        Console.WriteLine($"◈ Running Full Network Simulation");
        Console.WriteLine($"  Nodes: {nodeCount}");
        Console.WriteLine($"  Clients per node: {clientsPerNode}");
        Console.WriteLine($"  Simulation ticks: {ticks}");
        Console.WriteLine();
        
        // Create network
        Console.Write("  Creating nodes... ");
        CreateNodes(nodeCount, internetRatio: 0.6);
        Console.WriteLine($"✓ {_nodes.Count} nodes");
        
        // Create mesh
        Console.Write("  Building mesh topology... ");
        CreateMeshTopology(peersPerNode: 4);
        var totalConnections = _nodes.Values.Sum(n => n.Peers.Count) / 2;
        Console.WriteLine($"✓ {totalConnections} connections");
        
        // Connect clients
        Console.Write("  Connecting clients... ");
        var platforms = new[] { "iOS", "Android", "Windows", "macOS" };
        foreach (var node in _nodes.Values.Take(nodeCount / 2))
        {
            for (int i = 0; i < clientsPerNode; i++)
            {
                ConnectClient(node.NodeId, platforms[_random.Next(platforms.Length)]);
            }
        }
        Console.WriteLine($"✓ {_clients.Count} clients");
        
        // Run simulation ticks
        Console.WriteLine("  Running simulation...");
        for (int tick = 0; tick < ticks; tick++)
        {
            _tickCount = tick;
            
            // Simulate client activity
            foreach (var client in _clients.Values.Where(c => _random.NextDouble() > 0.3))
            {
                SimulateClientActivity(client.ClientId, 
                    _random.Next(10000, 1000000),  // 10KB - 1MB down
                    _random.Next(1000, 100000));   // 1KB - 100KB up
            }
            
            // Occasionally simulate GOSSIP update
            if (tick % 10 == 0)
            {
                var sourceNode = _nodes.Keys.ElementAt(_random.Next(_nodes.Count));
                var gossipResult = SimulateGossipUpdate(sourceNode, $"UPDATE_{tick}");
                result.GossipResults.Add(gossipResult);
            }
            
            // Occasionally simulate node going offline/online
            if (tick % 20 == 0)
            {
                var randomNode = _nodes.Values.ElementAt(_random.Next(_nodes.Count));
                randomNode.IsOnline = !randomNode.IsOnline;
            }
        }
        
        // Calculate results
        result.TotalBandwidthBytes = _nodes.Values.Sum(n => n.TotalBytesServed);
        result.AverageGossipCoverage = result.GossipResults.Average(g => g.CoveragePercent);
        result.NodesWithInternet = _nodes.Values.Count(n => n.HasInternetAccess);
        result.NodesWithoutInternet = _nodes.Values.Count(n => !n.HasInternetAccess);
        
        // Test relay paths for all offline nodes
        result.RelayPathsValid = 0;
        result.RelayPathsFailed = 0;
        foreach (var node in _nodes.Values.Where(n => !n.HasInternetAccess))
        {
            var relay = FindRelayPath(node.NodeId);
            if (relay.Success)
                result.RelayPathsValid++;
            else
                result.RelayPathsFailed++;
        }
        
        result.EndTime = DateTime.UtcNow;
        
        // Print summary
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  SIMULATION RESULTS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Duration: {(result.EndTime - result.StartTime).TotalSeconds:F2}s");
        Console.WriteLine($"  Total bandwidth: {result.TotalBandwidthBytes / 1024 / 1024:F2} MB");
        Console.WriteLine($"  Nodes with internet: {result.NodesWithInternet}");
        Console.WriteLine($"  Nodes relaying: {result.NodesWithoutInternet}");
        Console.WriteLine($"  Valid relay paths: {result.RelayPathsValid}/{result.NodesWithoutInternet}");
        Console.WriteLine($"  Average GOSSIP coverage: {result.AverageGossipCoverage:F1}%");
        Console.WriteLine($"  Total clients: {_clients.Count}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        return result;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          LOGGING
    // ═══════════════════════════════════════════════════════════════════════════
    
    private void LogEvent(SimEventType type, string nodeId, string message)
    {
        var evt = new SimulationEvent
        {
            Tick = _tickCount,
            Timestamp = DateTime.UtcNow,
            Type = type,
            NodeId = nodeId,
            Message = message
        };
        
        _eventLog.Add(evt);
        OnEvent?.Invoke(evt);
    }
    
    public IReadOnlyList<SimulationEvent> GetEventLog() => _eventLog.AsReadOnly();
    
    /// <summary>
    /// Gets network statistics.
    /// </summary>
    public NetworkStats GetStats() => new()
    {
        TotalNodes = _nodes.Count,
        OnlineNodes = _nodes.Values.Count(n => n.IsOnline),
        NodesWithInternet = _nodes.Values.Count(n => n.HasInternetAccess),
        TotalClients = _clients.Count,
        GuestClients = _clients.Values.Count(c => c.IsGuest),
        MemberClients = _clients.Values.Count(c => !c.IsGuest),
        TotalBandwidthBytes = _nodes.Values.Sum(n => n.TotalBytesServed),
        TotalConnections = _nodes.Values.Sum(n => n.Peers.Count) / 2
    };
}

// ═══════════════════════════════════════════════════════════════════════════
//                          DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════

public class SimulatedNode
{
    public required string NodeId { get; init; }
    public bool HasInternetAccess { get; set; }
    public bool CanBroadcastSSID { get; set; }
    public bool IsOnline { get; set; }
    public DateTime CreatedAt { get; init; }
    public double BandwidthLimitMbps { get; set; }
    public double CurrentBandwidthUsedMbps { get; set; }
    public long TotalBytesServed { get; set; }
    public HashSet<string> Peers { get; } = new();
    public List<string> ConnectedClients { get; } = new();
}

public class SimulatedClient
{
    public required string ClientId { get; init; }
    public required string ConnectedToNode { get; init; }
    public required string Platform { get; init; }
    public bool IsGuest { get; set; }
    public DateTime ConnectedAt { get; init; }
    public long BandwidthUsedBytes { get; set; }
}

public enum SimEventType
{
    NodeCreated,
    NodeOnline,
    NodeOffline,
    PeerConnected,
    PeerDisconnected,
    ClientConnected,
    ClientDisconnected,
    GossipStarted,
    GossipComplete,
    RelayPathFound,
    RelayPathFailed,
    UpdateApplied
}

public class SimulationEvent
{
    public int Tick { get; init; }
    public DateTime Timestamp { get; init; }
    public SimEventType Type { get; init; }
    public required string NodeId { get; init; }
    public required string Message { get; init; }
}

public class GossipSimulationResult
{
    public required string UpdateId { get; init; }
    public required string SourceNodeId { get; init; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int NodesReached { get; set; }
    public int TotalNodes { get; set; }
    public double CoveragePercent { get; set; }
    public int PacketsLost { get; set; }
    public int DroppedAtMaxHops { get; set; }
    public int OfflineNodes { get; set; }
    public int TotalLatencyMs { get; set; }
    public Dictionary<int, int> HopDistribution { get; } = new();
}

public class RelayPathResult
{
    public required string SourceNodeId { get; init; }
    public bool Success { get; set; }
    public List<string> Path { get; set; } = new();
    public int HopCount { get; set; }
    public string? InternetNodeId { get; set; }
    public int EstimatedLatencyMs { get; set; }
    public string? FailureReason { get; set; }
}

public class LateStageResult
{
    public int NodesCreated { get; set; }
    public bool RelaySuccess { get; set; }
    public int HopsRequired { get; set; }
    public List<string>? Path { get; set; }
    public int ClientsConnected { get; set; }
    public int ClientsWithInternet { get; set; }
}

public class FullSimulationResult
{
    public int NodeCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long TotalBandwidthBytes { get; set; }
    public int NodesWithInternet { get; set; }
    public int NodesWithoutInternet { get; set; }
    public int RelayPathsValid { get; set; }
    public int RelayPathsFailed { get; set; }
    public double AverageGossipCoverage { get; set; }
    public List<GossipSimulationResult> GossipResults { get; } = new();
}

public class NetworkStats
{
    public int TotalNodes { get; init; }
    public int OnlineNodes { get; init; }
    public int NodesWithInternet { get; init; }
    public int TotalClients { get; init; }
    public int GuestClients { get; init; }
    public int MemberClients { get; init; }
    public long TotalBandwidthBytes { get; init; }
    public int TotalConnections { get; init; }
}

public class EdgeCaseReport
{
    private readonly List<(string Name, bool Passed, string? Error)> _results = new();
    
    public void AddResult(string name, bool passed, string? error = null)
    {
        _results.Add((name, passed, error));
    }
    
    public int TotalTests => _results.Count;
    public int PassedTests => _results.Count(r => r.Passed);
    public int FailedTests => TotalTests - PassedTests;
    public bool AllPassed => FailedTests == 0;
    
    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine($"◈ Edge-Case Results: {PassedTests}/{TotalTests} passed");
        
        if (FailedTests > 0)
        {
            Console.WriteLine("  Failed tests:");
            foreach (var (name, passed, error) in _results.Where(r => !r.Passed))
            {
                Console.WriteLine($"    • {name}: {error ?? "Failed"}");
            }
        }
    }
}
