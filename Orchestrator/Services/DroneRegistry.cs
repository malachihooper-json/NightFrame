/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    DRONE REGISTRY - NODE TRACKING v2.0                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Tracks all connected drones, their capabilities, and health status.       ║
 * ║  Implements the "Border Control" logic for connection admission.           ║
 * ║  Extended with cellular and location tracking for v2.0.                    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Collections.Concurrent;
using NIGHTFRAME.Orchestrator.Grpc;

namespace NIGHTFRAME.Orchestrator.Services;

public class DroneNode
{
    public required string NodeId { get; init; }
    public required string Address { get; set; }
    public required string BinaryVersion { get; set; }
    public required NodeRole Role { get; set; }
    public required HardwareSpecs Specs { get; set; }
    public required byte[] PublicKey { get; init; }
    public required List<string> CachedModels { get; set; }
    
    // Connection state
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public bool IsOnline => (DateTime.UtcNow - LastHeartbeat).TotalSeconds < 60;
    
    // Performance metrics
    public double CurrentCpuLoad { get; set; }
    public long RamUsedMb { get; set; }
    public long TotalTasksCompleted { get; set; }
    public long TotalCreditsEarned { get; set; }
    
    // Probation state (Shadow Mode)
    public bool IsProbationary { get; set; } = true;
    public int ShadowTestsPassed { get; set; }
    public const int RequiredShadowTests = 10;
    
    // Cellular fields (v2.0)
    public bool HasCellular { get; set; }
    public string? CellularTechnology { get; set; }
    public int? SignalStrength { get; set; } // RSRP in dBm
    
    // Neural fields (v2.0)
    public bool HasGpu { get; set; }
    public string? GpuName { get; set; }
    public double? EstimatedFlops { get; set; }
    
    // Location (v2.0)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? LocationConfidence { get; set; }
}

public class DroneRegistry
{
    private readonly ConcurrentDictionary<string, DroneNode> _nodes = new();
    private readonly ILogger<DroneRegistry> _logger;
    
    // Hardware thresholds for role assignment
    private const long MinRamForCompute = 8192;   // 8GB
    private const long MinDiskForStorage = 102400; // 100GB
    private const long MinRamForRelay = 1024;      // 1GB (anything can relay)
    
    public int ActiveNodeCount => _nodes.Values.Count(n => n.IsOnline);
    public int TotalNodeCount => _nodes.Count;
    
    public DroneRegistry(ILogger<DroneRegistry> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Evaluates a drone manifest and decides admission.
    /// Implements the "Border Control" pattern from architecture doc.
    /// </summary>
    public RegistrationResponse EvaluateManifest(DroneManifest manifest, string address)
    {
        _logger.LogInformation("◈ Evaluating manifest from {NodeId} @ {Address}", manifest.NodeId, address);
        
        // Check 1: Is the code current?
        if (manifest.BinaryVersion != VersionManager.CurrentVersion)
        {
            _logger.LogWarning("∴ Node {NodeId} outdated: {Current} vs required {Required}", 
                manifest.NodeId, manifest.BinaryVersion, VersionManager.CurrentVersion);
            
            return new RegistrationResponse
            {
                Accepted = false,
                Reason = "OUTDATED",
                AssignedRole = NodeRole.RoleUnknown,
                GlobalState = GetGlobalState()
            };
        }
        
        // Check 2: Does it have required models?
        var requiredModel = GetCurrentModelHash();
        bool hasModel = manifest.CachedModels.Contains(requiredModel);
        
        // Check 3: Hardware capabilities
        var assignedRole = DetermineRole(manifest.Specs, hasModel);
        
        // Register the node
        var node = new DroneNode
        {
            NodeId = manifest.NodeId,
            Address = address,
            BinaryVersion = manifest.BinaryVersion,
            Role = assignedRole,
            Specs = manifest.Specs,
            PublicKey = manifest.PublicKey.ToByteArray(),
            CachedModels = manifest.CachedModels.ToList(),
            IsProbationary = true, // Always start in shadow mode
            HasGpu = manifest.Specs.HasGpu,
            GpuName = manifest.Specs.GpuName
        };
        
        _nodes[manifest.NodeId] = node;
        
        _logger.LogInformation("◈ Node {NodeId} registered as {Role}", manifest.NodeId, assignedRole);
        
        return new RegistrationResponse
        {
            Accepted = true,
            AssignedRole = assignedRole,
            Reason = hasModel ? "ACTIVE" : "NEEDS_HYDRATION",
            GlobalState = GetGlobalState()
        };
    }
    
    /// <summary>
    /// Determines the optimal role based on hardware specs.
    /// Implements the Hardware Agnosticism pattern.
    /// </summary>
    private NodeRole DetermineRole(HardwareSpecs specs, bool hasModel)
    {
        // GPU + High RAM = Compute
        if (specs.HasGpu && specs.RamMb >= MinRamForCompute)
        {
            return NodeRole.RoleCompute;
        }
        
        // High disk space = Storage
        if (specs.DiskFreeMb >= MinDiskForStorage)
        {
            return NodeRole.RoleStorage;
        }
        
        // Decent RAM but no GPU = General
        if (specs.RamMb >= MinRamForCompute / 2)
        {
            return NodeRole.RoleGeneral;
        }
        
        // Low specs = Relay only
        return NodeRole.RoleRelay;
    }
    
    /// <summary>
    /// Updates heartbeat from a node.
    /// </summary>
    public void RecordHeartbeat(string nodeId, double cpuLoad, long ramUsedMb)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.LastHeartbeat = DateTime.UtcNow;
            node.CurrentCpuLoad = cpuLoad;
            node.RamUsedMb = ramUsedMb;
        }
    }
    
    /// <summary>
    /// Updates cellular info for a node (v2.0).
    /// </summary>
    public void UpdateCellularInfo(string nodeId, string technology, int rsrp)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.HasCellular = true;
            node.CellularTechnology = technology;
            node.SignalStrength = rsrp;
        }
    }
    
    /// <summary>
    /// Updates location for a node (v2.0).
    /// </summary>
    public void UpdateLocation(string nodeId, double latitude, double longitude, double confidence)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.Latitude = latitude;
            node.Longitude = longitude;
            node.LocationConfidence = confidence;
        }
    }
    
    /// <summary>
    /// Gets the best node for a specific role.
    /// </summary>
    public DroneNode? GetBestNodeForRole(NodeRole role)
    {
        return _nodes.Values
            .Where(n => n.IsOnline && n.Role == role && !n.IsProbationary)
            .OrderBy(n => n.CurrentCpuLoad)  // Prefer least loaded
            .ThenByDescending(n => n.TotalTasksCompleted)  // Then most reliable
            .FirstOrDefault();
    }
    
    /// <summary>
    /// Gets all nodes matching a role, sorted by availability.
    /// </summary>
    public IEnumerable<DroneNode> GetNodesForRole(NodeRole role)
    {
        return _nodes.Values
            .Where(n => n.IsOnline && n.Role == role)
            .OrderBy(n => n.CurrentCpuLoad);
    }
    
    /// <summary>
    /// Gets nodes with cellular capability.
    /// </summary>
    public IEnumerable<DroneNode> GetCellularNodes()
    {
        return _nodes.Values.Where(n => n.IsOnline && n.HasCellular);
    }
    
    /// <summary>
    /// Promotes a node from probationary status.
    /// </summary>
    public void PromoteFromProbation(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.IsProbationary = false;
            _logger.LogInformation("◈ Node {NodeId} promoted to ACTIVE status", nodeId);
        }
    }
    
    /// <summary>
    /// Records a successful shadow test.
    /// </summary>
    public bool RecordShadowTestResult(string nodeId, bool passed)
    {
        if (!_nodes.TryGetValue(nodeId, out var node)) return false;
        
        if (passed)
        {
            node.ShadowTestsPassed++;
            
            if (node.ShadowTestsPassed >= DroneNode.RequiredShadowTests)
            {
                PromoteFromProbation(nodeId);
                return true;
            }
        }
        else
        {
            // Failed shadow test - terminate connection
            _nodes.TryRemove(nodeId, out _);
            _logger.LogWarning("∴ Node {NodeId} FAILED shadow test, connection terminated", nodeId);
        }
        
        return false;
    }
    
    public DroneNode? GetNode(string nodeId) => 
        _nodes.TryGetValue(nodeId, out var node) ? node : null;
    
    public IEnumerable<object> GetActiveNodes() => 
        _nodes.Values
            .Where(n => n.IsOnline)
            .Select(n => new
            {
                n.NodeId,
                Role = n.Role.ToString(),
                n.BinaryVersion,
                n.CurrentCpuLoad,
                n.RamUsedMb,
                n.IsProbationary,
                n.TotalTasksCompleted,
                LastHeartbeat = n.LastHeartbeat.ToString("o"),
                // v2.0 fields
                n.HasCellular,
                n.CellularTechnology,
                n.SignalStrength,
                n.HasGpu,
                n.GpuName,
                n.EstimatedFlops,
                n.Latitude,
                n.Longitude,
                n.LocationConfidence
            });
    
    public double CalculateMeshHealth()
    {
        if (_nodes.IsEmpty) return 0;
        
        var onlineNodes = _nodes.Values.Where(n => n.IsOnline).ToList();
        if (!onlineNodes.Any()) return 0;
        
        var onlineRatio = (double)onlineNodes.Count / TotalNodeCount;
        var avgLoad = onlineNodes.Average(n => n.CurrentCpuLoad);
        
        // Health = % online * (1 - avg load)
        return onlineRatio * (1 - Math.Min(avgLoad, 1.0));
    }
    
    private GlobalState GetGlobalState() => new()
    {
        LatestVersion = VersionManager.CurrentVersion,
        LatestBinaryHash = Google.Protobuf.ByteString.CopyFrom(VersionManager.CurrentBinaryHash),
        CurrentModelHash = GetCurrentModelHash(),
        ActiveNodeCount = ActiveNodeCount,
        TotalComputeOps = 0 // TODO: Get from ledger
    };
    
    private string GetCurrentModelHash() => "model-v1.0-sha256"; // TODO: Implement properly
}
