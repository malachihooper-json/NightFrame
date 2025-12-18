/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║           NIGHTFRAME ORCHESTRATOR - THE MOTHERSHIP                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  The Command Node. The Switchboard. The Accountant.                        ║
 * ║  Thin-client design: No heavy compute, no storage. Only metadata routing.  ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using NIGHTFRAME.Orchestrator.Services;
using NIGHTFRAME.Orchestrator.Hubs;
using NIGHTFRAME.Orchestrator.Grpc;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════
//                              SERVICE REGISTRATION
// ═══════════════════════════════════════════════════════════════════════════════

// gRPC for drone communication
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 16 * 1024 * 1024; // 16MB for binary chunks
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// SignalR for web console real-time updates
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

// CORS for Vercel frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("VercelFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://*.vercel.app",
                "https://nightframe.vercel.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Core services
builder.Services.AddSingleton<DroneRegistry>();
builder.Services.AddSingleton<LedgerService>();
builder.Services.AddSingleton<ShardCoordinator>();
builder.Services.AddSingleton<CellularCoordinator>(); // v2.0
// VersionManager is static, no registration needed
builder.Services.AddSingleton<PromptQueue>();
builder.Services.AddHostedService<HeartbeatMonitor>();
builder.Services.AddHostedService<ConsensusChecker>();

// Minimal API endpoints
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════════════
//                              MIDDLEWARE PIPELINE
// ═══════════════════════════════════════════════════════════════════════════════

app.UseCors("VercelFrontend");

// Map gRPC service for drone connections
app.MapGrpcService<OrchestratorService>();

// Map SignalR hub for web console
app.MapHub<ConsoleHub>("/hub/console");

// ═══════════════════════════════════════════════════════════════════════════════
//                              REST API ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════════

var api = app.MapGroup("/api");

// Health check
api.MapGet("/health", () => Results.Ok(new { status = "online", timestamp = DateTime.UtcNow }));

// Global state (for frontends)
api.MapGet("/state", (DroneRegistry registry, LedgerService ledger) =>
{
    return Results.Ok(new
    {
        activeNodes = registry.ActiveNodeCount,
        totalComputeOps = ledger.TotalOperations,
        latestVersion = VersionManager.CurrentVersion,
        meshHealth = registry.CalculateMeshHealth(),
        timestamp = DateTime.UtcNow
    });
});

// Submit a prompt for processing
api.MapPost("/prompt", async (PromptRequest request, PromptQueue queue, ConsoleHub hub) =>
{
    var promptId = await queue.EnqueueAsync(request.Prompt, request.Priority);
    
    // Notify all connected web consoles
    await hub.BroadcastPromptReceived(promptId, request.Prompt);
    
    return Results.Accepted($"/api/prompt/{promptId}", new { promptId, status = "queued" });
});

// Get prompt status
api.MapGet("/prompt/{promptId}", (string promptId, PromptQueue queue) =>
{
    var status = queue.GetStatus(promptId);
    return status != null ? Results.Ok(status) : Results.NotFound();
});

// List active nodes
api.MapGet("/nodes", (DroneRegistry registry) =>
{
    return Results.Ok(registry.GetActiveNodes());
});

// Get node details
api.MapGet("/nodes/{nodeId}", (string nodeId, DroneRegistry registry) =>
{
    var node = registry.GetNode(nodeId);
    return node != null ? Results.Ok(node) : Results.NotFound();
});

// Ledger summary
api.MapGet("/ledger", (LedgerService ledger) =>
{
    return Results.Ok(ledger.GetSummary());
});

// Upload training data (routes to storage drone)
api.MapPost("/upload", async (HttpRequest request, DroneRegistry registry) =>
{
    // Find best storage drone
    var storageDrone = registry.GetBestNodeForRole(NodeRole.RoleStorage);
    
    if (storageDrone == null)
    {
        return Results.StatusCode(503); // Service Unavailable
    }
    
    // Generate upload token for direct drone connection
    var token = Guid.NewGuid().ToString("N");
    var dropZone = new
    {
        uploadToken = token,
        droneAddress = storageDrone.Address,
        droneNodeId = storageDrone.NodeId,
        expiresAt = DateTime.UtcNow.AddMinutes(15)
    };
    
    return Results.Ok(dropZone);
});

// ═══════════════════════════════════════════════════════════════════════════════
//                              STARTUP
// ═══════════════════════════════════════════════════════════════════════════════

app.Logger.LogInformation("═══════════════════════════════════════════════════════════════════");
app.Logger.LogInformation("  NIGHTFRAME ORCHESTRATOR v{Version} - COMMAND NODE ONLINE", VersionManager.CurrentVersion);
app.Logger.LogInformation("  gRPC: {GrpcPort} | HTTP: {HttpPort}", 5001, 5000);
app.Logger.LogInformation("═══════════════════════════════════════════════════════════════════");

app.Run();

// ═══════════════════════════════════════════════════════════════════════════════
//                              REQUEST MODELS
// ═══════════════════════════════════════════════════════════════════════════════

public record PromptRequest(string Prompt, int Priority = 5);
