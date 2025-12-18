/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SIGNALR HUB - WEB CONSOLE REALTIME v2.0                 ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Real-time communication hub for the Vercel web console.                   ║
 * ║  Enables live updates, prompt submission, cellular data, and mesh viz.     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Microsoft.AspNetCore.SignalR;
using NIGHTFRAME.Orchestrator.Services;

namespace NIGHTFRAME.Orchestrator.Hubs;

/// <summary>
/// Client-side events that the hub can invoke.
/// </summary>
public interface IConsoleClient
{
    // ═════════════════════════════════════════════════════════════════════════════
    //                              MESH STATUS
    // ═════════════════════════════════════════════════════════════════════════════
    
    Task MeshStatus(object status);
    Task MeshStatusUpdate(MeshStatusDto status);
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              NODE EVENTS
    // ═════════════════════════════════════════════════════════════════════════════
    
    Task NodeRegistered(object node);
    Task NodeOffline(string nodeId);
    Task NodeConnected(DroneNodeDto node);
    Task NodeDisconnected(string nodeId);
    Task NodeStatusUpdate(DroneNodeDto node);
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              PROMPT/TASK EVENTS
    // ═════════════════════════════════════════════════════════════════════════════
    
    Task PromptReceived(string promptId, string prompt);
    Task PromptProgress(PromptProgressDto progress);
    Task TaskProgress(object progress);
    Task ResultReady(string promptId, string result);
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              CELLULAR EVENTS (v2.0)
    // ═════════════════════════════════════════════════════════════════════════════
    
    Task CellularUpdate(CellularUpdateDto update);
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              CONSCIOUSNESS STREAM
    // ═════════════════════════════════════════════════════════════════════════════
    
    Task DroneLog(object log);
    Task PeerDiscovered(object peer);
    Task ConsciousnessStream(string thought);
    Task ConsciousnessEvent(ConsciousnessEventDto evt);
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              DTOs FOR SIGNALR
// ═══════════════════════════════════════════════════════════════════════════════

public record MeshStatusDto(
    int ActiveNodes,
    int TotalNodes,
    double MeshHealth,
    long TotalCredits,
    int PendingPrompts
);

public record DroneNodeDto(
    string NodeId,
    string Role,
    string BinaryVersion,
    double CurrentCpuLoad,
    int RamUsedMb,
    bool IsProbationary,
    int TotalTasksCompleted,
    string LastHeartbeat,
    // Cellular fields (v2.0)
    bool? HasCellular = null,
    string? CellularTechnology = null,
    int? SignalStrength = null,
    // Neural fields (v2.0)
    bool? HasGpu = null,
    string? GpuName = null,
    double? EstimatedFlops = null,
    // Location
    double? Latitude = null,
    double? Longitude = null,
    double? LocationConfidence = null
);

public record CellularUpdateDto(
    string NodeId,
    string Technology,
    int Rsrp,
    int Rsrq,
    int Sinr,
    long CellId,
    int NeighborCount,
    string Timestamp
);

public record PromptProgressDto(
    string PromptId,
    string Status,
    int Progress,
    string? Result = null,
    string? Error = null
);

public record ConsciousnessEventDto(
    string Text,
    string Type,
    string Timestamp
);

// ═══════════════════════════════════════════════════════════════════════════════
//                              CONSOLE HUB
// ═══════════════════════════════════════════════════════════════════════════════

public class ConsoleHub : Hub<IConsoleClient>
{
    private readonly DroneRegistry _registry;
    private readonly PromptQueue _promptQueue;
    private readonly LedgerService _ledger;
    private readonly ILogger<ConsoleHub> _logger;
    private readonly CellularCoordinator? _cellularCoordinator;
    
    public ConsoleHub(
        DroneRegistry registry,
        PromptQueue promptQueue,
        LedgerService ledger,
        ILogger<ConsoleHub> logger,
        CellularCoordinator? cellularCoordinator = null)
    {
        _registry = registry;
        _promptQueue = promptQueue;
        _ledger = ledger;
        _logger = logger;
        _cellularCoordinator = cellularCoordinator;
    }
    
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("◈ Console connected: {ConnectionId}", Context.ConnectionId);
        
        // Send initial mesh status
        await Clients.Caller.MeshStatusUpdate(new MeshStatusDto(
            ActiveNodes: _registry.ActiveNodeCount,
            TotalNodes: _registry.TotalNodeCount,
            MeshHealth: _registry.CalculateMeshHealth(),
            TotalCredits: _ledger.GetTotalCredits(),
            PendingPrompts: _promptQueue.PendingCount
        ));
        
        // Send consciousness welcome
        await Clients.Caller.ConsciousnessEvent(new ConsciousnessEventDto(
            Text: "◈ Connected to NIGHTFRAME Orchestrator",
            Type: "event",
            Timestamp: DateTime.UtcNow.ToString("o")
        ));
        
        await base.OnConnectedAsync();
    }
    
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("∴ Console disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              CLIENT → SERVER METHODS
    // ═════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Submit a prompt from the web console.
    /// </summary>
    public async Task<string> SubmitPrompt(string prompt, int priority = 5)
    {
        var promptId = await _promptQueue.EnqueueAsync(prompt, priority);
        
        // Notify all consoles
        await Clients.All.PromptReceived(promptId, prompt);
        await BroadcastConsciousness($"◈ Prompt received: \"{prompt.Substring(0, Math.Min(50, prompt.Length))}...\"", "event");
        
        return promptId;
    }
    
    /// <summary>
    /// Get current mesh status.
    /// </summary>
    public Task<MeshStatusDto> GetMeshStatus()
    {
        return Task.FromResult(new MeshStatusDto(
            ActiveNodes: _registry.ActiveNodeCount,
            TotalNodes: _registry.TotalNodeCount,
            MeshHealth: _registry.CalculateMeshHealth(),
            TotalCredits: _ledger.GetTotalCredits(),
            PendingPrompts: _promptQueue.PendingCount
        ));
    }
    
    /// <summary>
    /// Get list of active nodes.
    /// </summary>
    public Task<List<DroneNodeDto>> GetNodes()
    {
        var nodes = _registry.GetActiveNodes()
            .Select(ToDto)
            .ToList();
        return Task.FromResult(nodes);
    }
    
    /// <summary>
    /// Get details for a specific node.
    /// </summary>
    public Task<DroneNodeDto?> GetNodeDetails(string nodeId)
    {
        var node = _registry.GetNode(nodeId);
        return Task.FromResult(node != null ? ToDto(node) : null);
    }
    
    /// <summary>
    /// Get ledger summary.
    /// </summary>
    public Task<object> GetLedger()
    {
        return Task.FromResult(_ledger.GetSummary());
    }
    
    /// <summary>
    /// Get recent prompts.
    /// </summary>
    public Task<object> GetRecentPrompts()
    {
        return Task.FromResult<object>(_promptQueue.GetRecent());
    }
    
    /// <summary>
    /// Update guest bandwidth setting.
    /// </summary>
    public async Task SetGuestBandwidth(int kbps)
    {
        // TODO: Broadcast to all nodes with gateway role
        await BroadcastConsciousness($"◎ Guest bandwidth updated to {kbps} Kbps", "status");
    }
    
    /// <summary>
    /// Send a command to a specific node.
    /// </summary>
    public async Task<bool> SendNodeCommand(string nodeId, string command, object? args = null)
    {
        var node = _registry.GetNode(nodeId);
        if (node == null) return false;
        
        // TODO: Queue command for node via gRPC stream
        await BroadcastConsciousness($"◎ Command '{command}' sent to {nodeId[..12]}", "status");
        return true;
    }
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              BROADCAST METHODS (Server → All Clients)
    // ═════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Broadcasts a consciousness stream event to all connected consoles.
    /// </summary>
    public async Task BroadcastConsciousness(string text, string type)
    {
        await Clients.All.ConsciousnessEvent(new ConsciousnessEventDto(
            Text: text,
            Type: type,
            Timestamp: DateTime.UtcNow.ToString("o")
        ));
    }
    
    /// <summary>
    /// Broadcasts node connection event.
    /// </summary>
    public async Task BroadcastNodeConnected(object node)
    {
        await Clients.All.NodeConnected(ToDto(node));
    }
    
    /// <summary>
    /// Broadcasts node disconnection event.
    /// </summary>
    public async Task BroadcastNodeDisconnected(string nodeId)
    {
        await Clients.All.NodeDisconnected(nodeId);
    }
    
    /// <summary>
    /// Broadcasts node status update.
    /// </summary>
    public async Task BroadcastNodeUpdate(object node)
    {
        await Clients.All.NodeStatusUpdate(ToDto(node));
    }
    
    /// <summary>
    /// Broadcasts cellular update from a node.
    /// </summary>
    public async Task BroadcastCellularUpdate(CellularUpdateDto update)
    {
        await Clients.All.CellularUpdate(update);
    }
    
    /// <summary>
    /// Broadcasts prompt progress update.
    /// </summary>
    public async Task BroadcastPromptProgress(PromptProgressDto progress)
    {
        await Clients.All.PromptProgress(progress);
    }
    
    /// <summary>
    /// Broadcasts mesh status update.
    /// </summary>
    public async Task BroadcastMeshStatus()
    {
        await Clients.All.MeshStatusUpdate(new MeshStatusDto(
            ActiveNodes: _registry.ActiveNodeCount,
            TotalNodes: _registry.TotalNodeCount,
            MeshHealth: _registry.CalculateMeshHealth(),
            TotalCredits: _ledger.GetTotalCredits(),
            PendingPrompts: _promptQueue.PendingCount
        ));
    }
    
    // Legacy compatibility
    public async Task BroadcastThought(string thought)
    {
        await Clients.All.ConsciousnessStream(thought);
    }
    
    public async Task BroadcastPromptReceived(string promptId, string prompt)
    {
        await Clients.All.PromptReceived(promptId, prompt);
    }
    
    // ═════════════════════════════════════════════════════════════════════════════
    //                              HELPERS
    // ═════════════════════════════════════════════════════════════════════════════
    
    private static DroneNodeDto ToDto(object node)
    {
        // Use reflection or pattern matching for flexibility
        var type = node.GetType();
        
        return new DroneNodeDto(
            NodeId: type.GetProperty("NodeId")?.GetValue(node)?.ToString() ?? "",
            Role: type.GetProperty("Role")?.GetValue(node)?.ToString() ?? "ROLE_GENERAL",
            BinaryVersion: type.GetProperty("BinaryVersion")?.GetValue(node)?.ToString() ?? "1.0.0",
            CurrentCpuLoad: Convert.ToDouble(type.GetProperty("CurrentCpuLoad")?.GetValue(node) ?? 0),
            RamUsedMb: Convert.ToInt32(type.GetProperty("RamUsedMb")?.GetValue(node) ?? 0),
            IsProbationary: Convert.ToBoolean(type.GetProperty("IsProbationary")?.GetValue(node) ?? false),
            TotalTasksCompleted: Convert.ToInt32(type.GetProperty("TotalTasksCompleted")?.GetValue(node) ?? 0),
            LastHeartbeat: type.GetProperty("LastHeartbeat")?.GetValue(node)?.ToString() ?? DateTime.UtcNow.ToString("o"),
            HasCellular: type.GetProperty("HasCellular")?.GetValue(node) as bool?,
            CellularTechnology: type.GetProperty("CellularTechnology")?.GetValue(node)?.ToString(),
            SignalStrength: type.GetProperty("SignalStrength")?.GetValue(node) as int?,
            HasGpu: type.GetProperty("HasGpu")?.GetValue(node) as bool?,
            GpuName: type.GetProperty("GpuName")?.GetValue(node)?.ToString(),
            EstimatedFlops: type.GetProperty("EstimatedFlops")?.GetValue(node) as double?,
            Latitude: type.GetProperty("Latitude")?.GetValue(node) as double?,
            Longitude: type.GetProperty("Longitude")?.GetValue(node) as double?,
            LocationConfidence: type.GetProperty("LocationConfidence")?.GetValue(node) as double?
        );
    }
}
