/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    NIGHTFRAME DRONE - BRAIN.EXE v2.0                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  The Worker. The Repeater. The Brain.                                      ║
 * ║  Native AOT compiled binary for maximum portability and stealth.           ║
 * ║  Now with Cellular Intelligence for RF location and handover prediction.  ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using NIGHTFRAME.Drone;
using NIGHTFRAME.Drone.Core;
using NIGHTFRAME.Drone.Hardware;
using NIGHTFRAME.Drone.Network;
using NIGHTFRAME.Drone.Compute;
using NIGHTFRAME.Drone.Setup;
using NIGHTFRAME.Drone.Cellular;
using System.IO.Ports;

// ═══════════════════════════════════════════════════════════════════════════════
//                              PARSE ARGUMENTS
// ═══════════════════════════════════════════════════════════════════════════════

// Check for special commands first
if (args.Contains("train-cells") || args.Contains("--train-cells"))
{
    await CellTrainingTool.RunAsync(args);
    return;
}

if (args.Contains("--train-interactive"))
{
    await CellTrainingTool.RunInteractiveAsync();
    return;
}

if (args.Contains("--list-modems"))
{
    ListAvailableModems();
    return;
}

// Run self-tests
if (args.Contains("--test") || args.Contains("--self-test"))
{
    var exitCode = await NIGHTFRAME.Drone.Testing.TestRunner.RunAsync(args);
    Environment.ExitCode = exitCode;
    return;
}

// Run network simulations
if (args.Contains("--simulate"))
{
    var simArgs = args.SkipWhile(a => a != "--simulate").Skip(1).ToArray();
    var exitCode = await NIGHTFRAME.Drone.Testing.SimulationRunner.RunAsync(simArgs);
    Environment.ExitCode = exitCode;
    return;
}

var isSetup = args.Contains("--setup");
var isBackground = args.Contains("--background");
var enableHosting = !args.Contains("--no-hosting");
var enableCellular = !args.Contains("--no-cellular");

// Get modem port from args or auto-detect
var modemPort = GetModemPort(args);

// ═══════════════════════════════════════════════════════════════════════════════
//                              FIRST RUN DETECTION
// ═══════════════════════════════════════════════════════════════════════════════

var configDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "NIGHTFRAME");
var setupCompletePath = Path.Combine(configDir, ".setup_complete");
var isFirstRun = !File.Exists(setupCompletePath);

if (isFirstRun || isSetup)
{
    // Run the unified initialization wizard
    var wizard = new InitializationWizard(enableHosting);
    var completed = await wizard.RunAsync();
    
    if (!completed)
    {
        Console.WriteLine("Setup cancelled. NIGHTFRAME will run in receive-only mode.");
    }
    
    // Mark setup as complete
    Directory.CreateDirectory(configDir);
    await File.WriteAllTextAsync(setupCompletePath, DateTime.UtcNow.ToString("o"));
    
    if (!completed)
    {
        // User cancelled - still start but without sharing
        enableHosting = false;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              STARTUP SEQUENCE
// ═══════════════════════════════════════════════════════════════════════════════

if (!isBackground)
{
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
    Console.WriteLine("  NIGHTFRAME DRONE v2.0.0 - AUTONOMOUS COMPUTE NODE");
    Console.WriteLine("═══════════════════════════════════════════════════════════════════");
}

// Generate or load identity
var identity = NodeIdentity.LoadOrCreate();
if (!isBackground)
    Console.WriteLine($"◈ Node ID: {identity.NodeId}");

// Run hardware audit
if (!isBackground)
    Console.WriteLine("◎ Running hardware audit...");
var specs = HardwareAudit.Scan();
if (!isBackground)
{
    Console.WriteLine($"  RAM: {specs.RamMb}MB | CPU: {specs.CpuCores} cores | GPU: {(specs.HasGpu ? specs.GpuName : "None")}");
    Console.WriteLine($"  Estimated FLOPS: {specs.EstimatedFlops / 1_000_000_000.0:F1} GFLOPS");
}

// Detect cellular modem
if (enableCellular && !string.IsNullOrEmpty(modemPort))
{
    if (!isBackground)
        Console.WriteLine($"◎ Cellular modem: {modemPort}");
}
else if (enableCellular && !isBackground)
{
    Console.WriteLine("◎ Cellular modem: Not detected");
}

// Determine orchestrator address
var orchestratorAddress = DetermineOrchestratorAddress(args);
if (!isBackground)
    Console.WriteLine($"◎ Orchestrator: {orchestratorAddress}");

// Create drone with cellular support
var drone = new DroneCore(
    identity, 
    specs, 
    orchestratorAddress,
    enableCellular ? modemPort : null);
var cts = new CancellationTokenSource();

// Start mesh discovery
var discovery = new MeshDiscovery();
_ = discovery.StartAsync(identity.NodeId, cts.Token);

// Start internet gateway and captive portal if hosting enabled
InternetGateway? gateway = null;
PersistentCaptivePortal? portal = null;

if (enableHosting)
{
    gateway = new InternetGateway();
    portal = new PersistentCaptivePortal(gateway);
    
    // Start captive portal in background
    _ = Task.Run(async () =>
    {
        try
        {
            await portal.StartAsync(cts.Token);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Captive portal error: {ex.Message}");
        }
    });
    
    if (!isBackground)
        Console.WriteLine("◈ Hosting mode: ENABLED (captive portal active)");
}
else
{
    if (!isBackground)
        Console.WriteLine("◎ Hosting mode: disabled");
}

// Handle shutdown signals
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    Console.WriteLine("∴ Shutdown signal received...");
    cts.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (s, e) =>
{
    if (!cts.IsCancellationRequested)
    {
        Console.WriteLine("∴ Process exit detected...");
        cts.Cancel();
    }
};

// Run main loop
try
{
    await drone.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    if (!isBackground)
        Console.WriteLine("◈ Drone shutdown complete.");
}
catch (Exception ex)
{
    Console.WriteLine($"∴ Fatal error: {ex.Message}");
    Environment.ExitCode = 1;
}
finally
{
    // Cleanup
    discovery.Stop();
    if (portal != null)
        await portal.StopAsync();
    drone.Dispose();
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              HELPER FUNCTIONS
// ═══════════════════════════════════════════════════════════════════════════════

static string DetermineOrchestratorAddress(string[] args)
{
    // Check command line
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--orchestrator" || args[i] == "-o")
            return args[i + 1];
    }
    
    // Check environment
    var envAddr = Environment.GetEnvironmentVariable("NIGHTFRAME_ORCHESTRATOR");
    if (!string.IsNullOrEmpty(envAddr))
        return envAddr;
    
    // Check local config file
    var configPath = Path.Combine(AppContext.BaseDirectory, "config.txt");
    if (File.Exists(configPath))
    {
        var lines = File.ReadAllLines(configPath);
        foreach (var line in lines)
        {
            if (line.StartsWith("orchestrator=", StringComparison.OrdinalIgnoreCase))
                return line.Substring("orchestrator=".Length).Trim();
        }
    }
    
    // Default to localhost for development
    return "http://localhost:5000";
}

static string? GetModemPort(string[] args)
{
    // Check command line
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--modem" || args[i] == "-m")
            return args[i + 1];
    }
    
    // Check environment
    var envPort = Environment.GetEnvironmentVariable("NIGHTFRAME_MODEM_PORT");
    if (!string.IsNullOrEmpty(envPort))
        return envPort;
    
    // Check local config file
    var configPath = Path.Combine(AppContext.BaseDirectory, "config.txt");
    if (File.Exists(configPath))
    {
        var lines = File.ReadAllLines(configPath);
        foreach (var line in lines)
        {
            if (line.StartsWith("modem=", StringComparison.OrdinalIgnoreCase))
                return line.Substring("modem=".Length).Trim();
        }
    }
    
    // Auto-detect modem on common ports
    return AutoDetectModemPort();
}

static string? AutoDetectModemPort()
{
    try
    {
        var ports = SerialPort.GetPortNames();
        
        foreach (var port in ports)
        {
            // Common modem patterns
            if (port.Contains("USB", StringComparison.OrdinalIgnoreCase) ||
                port.Contains("ACM", StringComparison.OrdinalIgnoreCase) ||
                port.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                // Try to verify it's a modem
                if (VerifyModemPort(port))
                {
                    return port;
                }
            }
        }
    }
    catch
    {
        // Serial port access may fail
    }
    
    return null;
}

static bool VerifyModemPort(string port)
{
    try
    {
        using var serial = new SerialPort(port, 115200);
        serial.ReadTimeout = 1000;
        serial.WriteTimeout = 1000;
        serial.Open();
        
        // Send AT command
        serial.WriteLine("AT");
        Thread.Sleep(200);
        
        var response = serial.ReadExisting();
        serial.Close();
        
        // Check for modem response
        return response.Contains("OK", StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
        return false;
    }
}

static void ListAvailableModems()
{
    Console.WriteLine("Available serial ports:");
    Console.WriteLine();
    
    try
    {
        var ports = SerialPort.GetPortNames();
        
        if (ports.Length == 0)
        {
            Console.WriteLine("  No serial ports found.");
            return;
        }
        
        foreach (var port in ports)
        {
            var isModem = VerifyModemPort(port);
            var status = isModem ? "✓ Modem" : "? Unknown";
            Console.WriteLine($"  {port}: {status}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error scanning ports: {ex.Message}");
    }
}
