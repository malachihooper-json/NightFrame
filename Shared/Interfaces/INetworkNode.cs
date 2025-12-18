/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SHARED INTERFACES - NETWORK NODE                        ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Unified node abstraction for mesh network participation.                  ║
 * ║  Consolidates Agent3.NetworkCore and Drone.MeshDiscovery patterns.         ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Shared.Interfaces;

/// <summary>
/// Represents a node in the NIGHTFRAME mesh network.
/// </summary>
public interface INetworkNode
{
    /// <summary>
    /// Unique node identifier (derived from public key).
    /// </summary>
    string NodeId { get; }
    
    /// <summary>
    /// Current node status.
    /// </summary>
    NodeStatus Status { get; }
    
    /// <summary>
    /// Assigned role in the mesh.
    /// </summary>
    NodeRole Role { get; }
    
    /// <summary>
    /// Network addresses this node is reachable at.
    /// </summary>
    IReadOnlyList<string> Addresses { get; }
    
    /// <summary>
    /// Hardware capabilities.
    /// </summary>
    NodeCapabilities Capabilities { get; }
    
    /// <summary>
    /// Cellular capabilities (null if unavailable).
    /// </summary>
    CellularCapabilities? CellularInfo { get; }
    
    /// <summary>
    /// Discovered peer nodes.
    /// </summary>
    IReadOnlyDictionary<string, PeerInfo> Peers { get; }
    
    /// <summary>
    /// Event raised when a peer is discovered.
    /// </summary>
    event Action<PeerInfo>? OnPeerDiscovered;
    
    /// <summary>
    /// Event raised when connection to orchestrator changes.
    /// </summary>
    event Action<bool>? OnOrchestratorConnectionChanged;
}

public enum NodeStatus
{
    Offline,
    Initializing,
    Searching,
    Connected,
    Working,
    Updating,
    Error
}

public enum NodeRole
{
    Unknown = 0,
    Compute = 1,
    Storage = 2,
    Relay = 3,
    General = 4,
    Infiltration = 5,
    Scout = 6
}

/// <summary>
/// Hardware capabilities for load balancing.
/// </summary>
public record NodeCapabilities
{
    public required long RamMb { get; init; }
    public required int CpuCores { get; init; }
    public required long DiskFreeMb { get; init; }
    public required bool HasGpu { get; init; }
    public string? GpuName { get; init; }
    public long GpuVramMb { get; init; }
    public double CurrentCpuLoad { get; init; }
    public long EstimatedFlops { get; init; }
    public IReadOnlyList<string> CachedModels { get; init; } = [];
}

/// <summary>
/// Cellular network capabilities.
/// </summary>
public record CellularCapabilities
{
    public required bool Available { get; init; }
    public long? ServingCellId { get; init; }
    public float? RSRP { get; init; }
    public float? RSRQ { get; init; }
    public float? SINR { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public float? LocationConfidence { get; init; }
    public int NeighborCellCount { get; init; }
    public string RadioType { get; init; } = "LTE";
    public string? CarrierName { get; init; }
}

/// <summary>
/// Information about a discovered peer.
/// </summary>
public record PeerInfo
{
    public required string NodeId { get; init; }
    public required string Address { get; init; }
    public required NodeRole Role { get; init; }
    public required DateTime LastSeen { get; init; }
    public byte[]? PublicKey { get; init; }
    public bool HasInternet { get; init; }
    public CellularCapabilities? CellularInfo { get; init; }
}
