/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    FORMATION SUITE - GENESIS BOOTSTRAP                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Single entry point for initializing and running NIGHTFRAME.              ║
 * ║  Supports Genesis (single PC), Formation (2-5), and Propagation modes.    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NIGHTFRAME.Orchestrator.Services;
using NIGHTFRAME.Orchestrator.Hubs;

namespace NIGHTFRAME.Orchestrator;

public enum FormationMode
{
    Genesis,       // Single PC, full stack
    Formation,     // 2-5 nodes, testing mesh
    Propagation    // 5+ nodes, viral growth enabled
}

public class FormationSuite
{
    private readonly FormationMode _mode;
    private readonly ILogger<FormationSuite> _logger;
    
    public FormationSuite(FormationMode mode, ILogger<FormationSuite> logger)
    {
        _mode = mode;
        _logger = logger;
    }
    
    /// <summary>
    /// Runs the full formation suite based on mode.
    /// </summary>
    public async Task RunAsync(string[] args, CancellationToken ct = default)
    {
        PrintBanner();
        
        _logger.LogInformation("◈ Formation Mode: {Mode}", _mode);
        
        switch (_mode)
        {
            case FormationMode.Genesis:
                await RunGenesisAsync(args, ct);
                break;
            
            case FormationMode.Formation:
                await RunFormationAsync(args, ct);
                break;
            
            case FormationMode.Propagation:
                await RunPropagationAsync(args, ct);
                break;
        }
    }
    
    /// <summary>
    /// Genesis Mode: Single PC bootstrap with embedded drone.
    /// </summary>
    private async Task RunGenesisAsync(string[] args, CancellationToken ct)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        _logger.LogInformation("  GENESIS MODE - Single PC Bootstrap");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        
        // Step 1: Initialize data directory
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NIGHTFRAME");
        Directory.CreateDirectory(dataDir);
        _logger.LogInformation("◎ Data directory: {DataDir}", dataDir);
        
        // Step 2: Start orchestrator in background
        _logger.LogInformation("◎ Starting Orchestrator...");
        var orchestratorTask = StartOrchestratorAsync(args, ct);
        
        // Step 3: Wait for orchestrator to be ready
        await Task.Delay(2000, ct);
        
        // Step 4: Start embedded drone (same process)
        _logger.LogInformation("◎ Starting embedded Drone...");
        var droneTask = StartEmbeddedDroneAsync("http://localhost:5000", ct);
        
        // Step 5: Show status
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        _logger.LogInformation("  GENESIS COMPLETE - System Ready");
        _logger.LogInformation("  Web Console: http://localhost:5000");
        _logger.LogInformation("  gRPC: http://localhost:5001");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        
        // Wait for both to complete
        await Task.WhenAll(orchestratorTask, droneTask);
    }
    
    /// <summary>
    /// Formation Mode: Testing mesh with 2-5 nodes.
    /// </summary>
    private async Task RunFormationAsync(string[] args, CancellationToken ct)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        _logger.LogInformation("  FORMATION MODE - Multi-Node Testing");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        
        // Just run orchestrator, drones connect externally
        await StartOrchestratorAsync(args, ct);
    }
    
    /// <summary>
    /// Propagation Mode: Viral growth with captive portal.
    /// </summary>
    private async Task RunPropagationAsync(string[] args, CancellationToken ct)
    {
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        _logger.LogInformation("  PROPAGATION MODE - Viral Growth Enabled");
        _logger.LogInformation("═══════════════════════════════════════════════════════════════════");
        
        // Run orchestrator with captive portal enabled
        // TODO: Enable captive portal on all connected drones
        await StartOrchestratorAsync(args, ct);
    }
    
    private async Task StartOrchestratorAsync(string[] args, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(args);
        
        // gRPC
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = 16 * 1024 * 1024;
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        });
        
        // SignalR
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.MaximumReceiveMessageSize = 1024 * 1024;
        });
        
        // CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });
        
        // Core services
        builder.Services.AddSingleton<DroneRegistry>();
        builder.Services.AddSingleton<LedgerService>();
        builder.Services.AddSingleton<ShardCoordinator>();
        builder.Services.AddSingleton<PromptQueue>();
        builder.Services.AddHostedService<HeartbeatMonitor>();
        
        var app = builder.Build();
        
        app.UseCors("AllowAll");
        app.MapGrpcService<OrchestratorService>();
        app.MapHub<ConsoleHub>("/hub/console");
        
        // Health endpoint
        app.MapGet("/api/health", () => new { status = "online", mode = _mode.ToString(), timestamp = DateTime.UtcNow });
        
        // State endpoint
        app.MapGet("/api/state", (DroneRegistry registry, LedgerService ledger) => new
        {
            mode = _mode.ToString(),
            activeNodes = registry.ActiveNodeCount,
            totalNodes = registry.TotalNodeCount,
            meshHealth = registry.CalculateMeshHealth(),
            totalCredits = ledger.TotalOperations,
            timestamp = DateTime.UtcNow
        });
        
        await app.RunAsync(ct);
    }
    
    private async Task StartEmbeddedDroneAsync(string orchestratorAddress, CancellationToken ct)
    {
        // Create a lightweight embedded drone for Genesis mode
        _logger.LogInformation("◎ Embedded drone connecting to {Address}", orchestratorAddress);
        
        // In Genesis mode, we run a simplified drone in the same process
        // This is just for testing - production drones run as separate processes
        
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            _logger.LogDebug("◎ Embedded drone heartbeat");
        }
    }
    
    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("███╗   ██╗██╗ ██████╗ ██╗  ██╗████████╗███████╗██████╗  █████╗ ███╗   ███╗███████╗");
        Console.WriteLine("████╗  ██║██║██╔════╝ ██║  ██║╚══██╔══╝██╔════╝██╔══██╗██╔══██╗████╗ ████║██╔════╝");
        Console.WriteLine("██╔██╗ ██║██║██║  ███╗███████║   ██║   █████╗  ██████╔╝███████║██╔████╔██║█████╗  ");
        Console.WriteLine("██║╚██╗██║██║██║   ██║██╔══██║   ██║   ██╔══╝  ██╔══██╗██╔══██║██║╚██╔╝██║██╔══╝  ");
        Console.WriteLine("██║ ╚████║██║╚██████╔╝██║  ██║   ██║   ██║     ██║  ██║██║  ██║██║ ╚═╝ ██║███████╗");
        Console.WriteLine("╚═╝  ╚═══╝╚═╝ ╚═════╝ ╚═╝  ╚═╝   ╚═╝   ╚═╝     ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝╚══════╝");
        Console.WriteLine("                     Decentralized AI Mesh Network v1.0.0");
        Console.WriteLine();
    }
}
