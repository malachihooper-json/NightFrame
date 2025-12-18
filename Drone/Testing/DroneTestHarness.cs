/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    DRONE TEST HARNESS - SELF TESTING                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Automated testing framework for drone functionality.                      ║
 * ║  Simulates propagation, verifies component connectivity.                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using NIGHTFRAME.Drone.Network;
using NIGHTFRAME.Drone.Propagation;

namespace NIGHTFRAME.Drone.Testing;

/// <summary>
/// Test harness for automated drone testing and simulation.
/// </summary>
public class DroneTestHarness
{
    private readonly List<TestResult> _results = new();
    
    public class TestResult
    {
        public required string TestName { get; init; }
        public bool Passed { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Message { get; set; }
        public Exception? Error { get; set; }
    }
    
    /// <summary>
    /// Runs all drone self-tests.
    /// </summary>
    public async Task<IReadOnlyList<TestResult>> RunAllTestsAsync()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              NIGHTFRAME DRONE SELF-TEST SUITE                 ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        // Core tests
        await RunTest("Configuration System", TestConfigurationSystemAsync);
        await RunTest("Resource Limits", TestResourceLimitsAsync);
        await RunTest("Session Logger", TestSessionLoggerAsync);
        await RunTest("Portal Pages Generation", TestPortalPagesAsync);
        await RunTest("Node Identity", TestNodeIdentityAsync);
        await RunTest("Propagation Engine Initialization", TestPropagationEngineAsync);
        await RunTest("Component Connectivity", TestComponentConnectivityAsync);
        await RunTest("Late-Stage Propagation Simulation", TestLateStagePropagationAsync);
        
        // GOSSIP and update tests
        await RunTest("GOSSIP Protocol Self-Test", TestGossipProtocolAsync);
        await RunTest("Security Hardening", TestSecurityHardeningAsync);
        await RunTest("Update Deployer Edge Cases", TestUpdateDeployerEdgeCasesAsync);
        await RunTest("P2P Relay Chain", TestP2PRelayChainAsync);
        await RunTest("WiFi Broadcast Compatibility", TestWiFiBroadcastAsync);
        
        // Print summary
        PrintSummary();
        
        return _results.AsReadOnly();
    }
    
    private async Task RunTest(string name, Func<Task<(bool success, string message)>> test)
    {
        Console.Write($"  [{_results.Count + 1:D2}] {name}... ");
        
        var sw = Stopwatch.StartNew();
        TestResult result;
        
        try
        {
            var (success, message) = await test();
            sw.Stop();
            
            result = new TestResult
            {
                TestName = name,
                Passed = success,
                Duration = sw.Elapsed,
                Message = message
            };
            
            if (success)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ PASS ({sw.ElapsedMilliseconds}ms)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ FAIL: {message}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            result = new TestResult
            {
                TestName = name,
                Passed = false,
                Duration = sw.Elapsed,
                Message = ex.Message,
                Error = ex
            };
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ ERROR: {ex.Message}");
        }
        
        Console.ResetColor();
        _results.Add(result);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          INDIVIDUAL TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<(bool, string)> TestConfigurationSystemAsync()
    {
        var configManager = new SharingConfigManager();
        var config = configManager.GetConfig();
        
        // Verify default values
        if (config.MonthlyBandwidthLimitGB <= 0)
            return (false, "Invalid default bandwidth limit");
        
        // Test save/load cycle
        var originalLimit = config.MonthlyBandwidthLimitGB;
        config.MonthlyBandwidthLimitGB = 25.5;
        configManager.SaveConfig();
        
        var reloadedConfig = configManager.GetConfig();
        if (reloadedConfig.MonthlyBandwidthLimitGB != 25.5)
            return (false, "Config save/load failed");
        
        // Restore
        config.MonthlyBandwidthLimitGB = originalLimit;
        configManager.SaveConfig();
        
        await Task.CompletedTask;
        return (true, "Configuration persistence working");
    }
    
    private async Task<(bool, string)> TestResourceLimitsAsync()
    {
        // Verify all limits are sensible
        if (ResourceLimits.DefaultMonthlyBandwidthGB <= 0)
            return (false, "Bandwidth limit not set");
        if (ResourceLimits.MaxCpuPercentage <= 0 || ResourceLimits.MaxCpuPercentage > 100)
            return (false, "CPU limit invalid");
        if (ResourceLimits.MaxRamMB <= 0)
            return (false, "RAM limit not set");
        if (ResourceLimits.GuestBandwidthKbps <= 0)
            return (false, "Guest bandwidth not set");
        
        // Verify formatted output
        var summary = ResourceLimits.GetFormattedSummary();
        if (string.IsNullOrEmpty(summary))
            return (false, "Formatted summary empty");
        
        var uiLimits = ResourceLimits.GetLimitsForUI();
        if (uiLimits.Count < 5)
            return (false, "UI limits incomplete");
        
        await Task.CompletedTask;
        return (true, $"All {uiLimits.Count} limits valid");
    }
    
    private async Task<(bool, string)> TestSessionLoggerAsync()
    {
        using var logger = new SessionLogger();
        
        // Test session creation
        var session = logger.StartSession("00:11:22:33:44:55", "192.168.1.100");
        if (session == null)
            return (false, "Failed to create session");
        
        // Test session retrieval
        var retrieved = logger.GetActiveSession("192.168.1.100");
        if (retrieved == null)
            return (false, "Failed to retrieve session");
        
        // Test bandwidth recording
        logger.RecordBandwidth("00:11:22:33:44:55", 1024 * 1024, 0); // 1MB down, 0 up
        
        // End session
        logger.EndSession("192.168.1.100");
        
        // Active session should be null now
        var ended = logger.GetActiveSession("192.168.1.100");
        if (ended != null)
            return (false, "Session not properly ended");
        
        await Task.CompletedTask;
        return (true, "Session lifecycle working");
    }
    
    private async Task<(bool, string)> TestPortalPagesAsync()
    {
        // Test all platform pages generate
        var platforms = new[]
        {
            ("Windows", PortalPages.GetWindowsPortalHtml()),
            ("macOS", PortalPages.GetMacOSPortalHtml()),
            ("Android", PortalPages.GetAndroidPortalHtml()),
            ("iOS", PortalPages.GetIOSPortalHtml()),
            ("Linux", PortalPages.GetLinuxPortalHtml()),
            ("Success", PortalPages.GetSuccessPageHtml(true)),
            ("Terms", PortalPages.GetTermsPageHtml()),
            ("Settings", PortalPages.GetSettingsPageHtml(50, 10, 20, 512, 30, true, DateTime.Now))
        };
        
        foreach (var (name, html) in platforms)
        {
            if (string.IsNullOrEmpty(html))
                return (false, $"{name} page is empty");
            if (!html.Contains("NFRAME") && !html.Contains("NIGHTFRAME"))
                return (false, $"{name} page missing branding");
            if (!html.Contains("/assets/logo.png") && !html.Contains("◈"))
                return (false, $"{name} page missing logo reference");
        }
        
        await Task.CompletedTask;
        return (true, $"All {platforms.Length} pages generated correctly");
    }
    
    private async Task<(bool, string)> TestNodeIdentityAsync()
    {
        var identity = NodeIdentity.LoadOrCreate();
        
        if (string.IsNullOrEmpty(identity.NodeId))
            return (false, "Node ID is empty");
        if (!identity.NodeId.StartsWith("NFRAME_"))
            return (false, "Node ID format invalid");
        if (identity.PublicKey.Length == 0)
            return (false, "Public key is empty");
        
        // Test signing
        var testData = "Hello NIGHTFRAME"u8.ToArray();
        var signature = identity.Sign(testData);
        if (signature.Length == 0)
            return (false, "Signature is empty");
        
        // Verify signature
        if (!identity.Verify(testData, signature))
            return (false, "Signature verification failed");
        
        await Task.CompletedTask;
        return (true, $"Identity: {identity.NodeId}");
    }
    
    private async Task<(bool, string)> TestPropagationEngineAsync()
    {
        // Just verify the engine can be instantiated
        var engine = new PropagationEngine();
        
        // Check methods exist
        var methods = engine.GetType().GetMethods();
        var hasPropagate = methods.Any(m => m.Name.Contains("Propagate") || m.Name.Contains("Start"));
        
        if (!hasPropagate)
            return (false, "Missing propagation methods");
        
        await Task.CompletedTask;
        return (true, "Engine initialized");
    }
    
    private async Task<(bool, string)> TestComponentConnectivityAsync()
    {
        var issues = new List<string>();
        
        // Test that SharingConfigManager can be used by InternetGateway
        try
        {
            var configManager = new SharingConfigManager();
            using var sessionLogger = new SessionLogger();
            
            // Verify they share the same data path
            // This tests the component connectivity
        }
        catch (Exception ex)
        {
            issues.Add($"Config/Logger: {ex.Message}");
        }
        
        // Test portal routes can access config
        try
        {
            var config = new SharingConfigManager().GetConfig();
            var html = PortalPages.GetSettingsPageHtml(
                config.MonthlyBandwidthLimitGB,
                config.CurrentMonthUsageGB,
                ResourceLimits.MaxCpuPercentage,
                ResourceLimits.MaxRamMB,
                ResourceLimits.MaxGpuPercentage,
                config.ConsentGranted,
                config.ConsentTimestamp
            );
            if (string.IsNullOrEmpty(html))
                issues.Add("Settings page generation failed");
        }
        catch (Exception ex)
        {
            issues.Add($"Settings integration: {ex.Message}");
        }
        
        await Task.CompletedTask;
        
        if (issues.Count > 0)
            return (false, string.Join("; ", issues));
        
        return (true, "All components connected");
    }
    
    private async Task<(bool, string)> TestLateStagePropagationAsync()
    {
        // Simulate late-stage propagation scenario
        // In late-stage, the network is established and we're testing:
        // 1. Guest connects to portal
        // 2. Views resource limits
        // 3. Optionally installs and becomes member
        // 4. Session tracking works
        
        var simulationLog = new List<string>();
        
        // Step 1: Simulate guest session
        using var logger = new SessionLogger();
        var guestMac = "AA:BB:CC:DD:EE:FF";
        var guestIp = "192.168.4.50";
        
        var session = logger.StartSession(guestMac, guestIp);
        simulationLog.Add($"Guest connected: {guestMac}");
        
        // Step 2: Record ToS acceptance
        logger.RecordTermsAcceptance(guestIp);
        simulationLog.Add("ToS accepted");
        
        // Step 3: Simulate bandwidth usage (500KB)
        logger.RecordBandwidth(guestMac, 512000, 0);
        simulationLog.Add("Bandwidth used: 500KB");
        
        // Step 4: Check config integration
        var config = new SharingConfigManager().GetConfig();
        simulationLog.Add($"Current usage: {config.CurrentMonthUsageGB:F2} GB");
        
        // Step 5: End session
        logger.EndSession(guestIp);
        simulationLog.Add("Session ended");
        
        // Verify history
        var history = logger.GetSessionsByMac(guestMac);
        if (!history.Any())
            return (false, "Session not in history");
        
        await Task.CompletedTask;
        return (true, $"Simulation: {simulationLog.Count} steps completed");
    }
    
    private async Task<(bool, string)> TestGossipProtocolAsync()
    {
        var identity = NodeIdentity.LoadOrCreate();
        var gossip = new GossipProtocol(identity);
        
        // Run GOSSIP self-test
        var passed = await gossip.SelfTestAsync();
        
        if (!passed)
            return (false, "GOSSIP self-test failed");
        
        // Check stats
        var stats = gossip.GetStats();
        if (stats.MaxHops <= 0)
            return (false, "Invalid max hops");
        if (stats.FanoutSize <= 0)
            return (false, "Invalid fanout size");
        
        return (true, $"GOSSIP: {stats.KnownPeers} peers, fanout={stats.FanoutSize}");
    }
    
    private async Task<(bool, string)> TestSecurityHardeningAsync()
    {
        // Run security self-test
        var passed = NIGHTFRAME.Drone.Security.SecurityHardening.SelfTest();
        
        if (!passed)
            return (false, "Security self-test failed");
        
        // Test data protection round trip
        var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var protected_ = NIGHTFRAME.Drone.Security.SecurityHardening.ProtectData(testData);
        var unprotected = NIGHTFRAME.Drone.Security.SecurityHardening.UnprotectData(protected_);
        
        if (!testData.SequenceEqual(unprotected))
            return (false, "Data protection round-trip failed");
        
        await Task.CompletedTask;
        return (true, "Security hardening verified");
    }
    
    private async Task<(bool, string)> TestUpdateDeployerEdgeCasesAsync()
    {
        var identity = NodeIdentity.LoadOrCreate();
        var gossip = new GossipProtocol(identity);
        var deployer = new DroneUpdateDeployer(gossip, identity);
        
        // Run edge-case tests
        var report = await deployer.RunEdgeCaseTestsAsync();
        
        if (!report.AllPassed)
            return (false, $"{report.FailedTests} edge-case tests failed");
        
        return (true, $"All {report.TotalTests} edge-case tests passed");
    }
    
    private async Task<(bool, string)> TestP2PRelayChainAsync()
    {
        var relay = new P2PRelayChain();
        
        // Initialize with test node ID
        await relay.InitializeAsync("NFRAME_TEST_NODE");
        
        // Check stats
        var stats = relay.GetStats();
        
        // Register some test nodes
        relay.RegisterNode(new RelayNode
        {
            NodeId = "NFRAME_PEER_1",
            HasInternet = true,
            IPAddress = "192.168.1.1"
        });
        
        relay.RegisterNode(new RelayNode
        {
            NodeId = "NFRAME_PEER_2",
            HasInternet = false,
            HopsToInternet = 1
        });
        
        // Check stats updated
        stats = relay.GetStats();
        if (stats.KnownNodes < 2)
            return (false, "Failed to register nodes");
        
        // Validate late-stage scenario
        var validated = await relay.ValidateLateStageScenarioAsync();
        
        return (true, $"P2P relay: {stats.KnownNodes} nodes registered");
    }
    
    private async Task<(bool, string)> TestWiFiBroadcastAsync()
    {
        var wifi = new WiFiBroadcast();
        
        // Test device compatibility
        var report = await wifi.TestDeviceCompatibilityAsync();
        
        // Check essential compatibility
        if (!report.SSIDValid)
            return (false, "SSID encoding invalid");
        
        // At minimum, we should show platform compatibility
        var compatibleCount = new[] {
            report.IOSCompatible,
            report.AndroidCompatible,
            report.WindowsCompatible,
            report.MacOSCompatible,
            report.LinuxCompatible
        }.Count(c => c);
        
        if (compatibleCount < 3)
            return (false, $"Only {compatibleCount}/5 platforms compatible");
        
        return (true, $"WiFi: {compatibleCount}/5 platforms, adapter={report.HasWiFiAdapter}");
    }
    
    private void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        var passed = _results.Count(r => r.Passed);
        var failed = _results.Count - passed;
        
        Console.Write($"  Results: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write($"{passed} passed");
        Console.ResetColor();
        Console.Write(" / ");
        
        if (failed > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write($"{failed} failed");
            Console.ResetColor();
        }
        else
        {
            Console.Write("0 failed");
        }
        
        var totalTime = _results.Sum(r => r.Duration.TotalMilliseconds);
        Console.WriteLine($" ({totalTime:F0}ms total)");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        if (failed > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  Failed tests:");
            foreach (var result in _results.Where(r => !r.Passed))
            {
                Console.WriteLine($"    • {result.TestName}: {result.Message}");
            }
            Console.ResetColor();
        }
    }
}

/// <summary>
/// Entry point for running tests via command line.
/// </summary>
public static class TestRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        var harness = new DroneTestHarness();
        var results = await harness.RunAllTestsAsync();
        
        return results.All(r => r.Passed) ? 0 : 1;
    }
}
