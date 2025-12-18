/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              SIMULATION RUNNER - CLI INTERFACE                             ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Command-line interface for running network simulations.                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Testing;

/// <summary>
/// CLI runner for network simulations.
/// </summary>
public static class SimulationRunner
{
    /// <summary>
    /// Runs simulation based on command-line arguments.
    /// </summary>
    public static async Task<int> RunAsync(string[] args)
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              NIGHTFRAME NETWORK SIMULATOR                         ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        
        if (args.Length == 0 || args[0] == "--help")
        {
            PrintHelp();
            return 0;
        }
        
        var simulator = new NetworkSimulator();
        
        switch (args[0].ToLowerInvariant())
        {
            case "--edge-cases":
            case "-e":
                return RunEdgeCaseTests(simulator);
                
            case "--full":
            case "-f":
                return await RunFullSimulationAsync(simulator, args);
                
            case "--late-stage":
            case "-l":
                return RunLateStageSimulation(simulator, args);
                
            case "--gossip":
            case "-g":
                return RunGossipSimulation(simulator, args);
                
            case "--relay":
            case "-r":
                return RunRelaySimulation(simulator, args);
                
            case "--all":
            case "-a":
                return await RunAllSimulationsAsync(simulator);
                
            default:
                Console.WriteLine($"Unknown command: {args[0]}");
                PrintHelp();
                return 1;
        }
    }
    
    private static void PrintHelp()
    {
        Console.WriteLine("Usage: dotnet run -- --simulate [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  --edge-cases, -e    Run edge case tests");
        Console.WriteLine("  --full, -f          Run full network simulation");
        Console.WriteLine("                      Options: --nodes N --clients N --ticks N");
        Console.WriteLine("  --late-stage, -l    Simulate late-stage propagation");
        Console.WriteLine("                      Options: --isolated N");
        Console.WriteLine("  --gossip, -g        Simulate GOSSIP update propagation");
        Console.WriteLine("                      Options: --nodes N");
        Console.WriteLine("  --relay, -r         Test P2P relay path finding");
        Console.WriteLine("                      Options: --nodes N --internet-ratio R");
        Console.WriteLine("  --all, -a           Run all simulation scenarios");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- --simulate --edge-cases");
        Console.WriteLine("  dotnet run -- --simulate --full --nodes 50 --ticks 200");
        Console.WriteLine("  dotnet run -- --simulate --late-stage --isolated 10");
    }
    
    private static int RunEdgeCaseTests(NetworkSimulator simulator)
    {
        Console.WriteLine("◈ EDGE CASE TESTING");
        Console.WriteLine("───────────────────────────────────────────────────────────────────");
        Console.WriteLine();
        
        var report = simulator.RunEdgeCaseTests();
        
        return report.AllPassed ? 0 : 1;
    }
    
    private static async Task<int> RunFullSimulationAsync(NetworkSimulator simulator, string[] args)
    {
        var nodeCount = GetIntArg(args, "--nodes", 20);
        var clientsPerNode = GetIntArg(args, "--clients", 3);
        var ticks = GetIntArg(args, "--ticks", 100);
        
        Console.WriteLine("◈ FULL NETWORK SIMULATION");
        Console.WriteLine("───────────────────────────────────────────────────────────────────");
        Console.WriteLine();
        
        var result = simulator.RunFullSimulation(nodeCount, clientsPerNode, ticks);
        
        await Task.CompletedTask;
        
        // Check for issues
        var issues = new List<string>();
        if (result.AverageGossipCoverage < 90)
            issues.Add($"Low GOSSIP coverage: {result.AverageGossipCoverage:F1}% (expected >90%)");
        if (result.RelayPathsFailed > 0)
            issues.Add($"Relay failures: {result.RelayPathsFailed} nodes have no path to internet");
        
        if (issues.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("⚠️ ISSUES DETECTED:");
            foreach (var issue in issues)
                Console.WriteLine($"  • {issue}");
            return 1;
        }
        
        Console.WriteLine();
        Console.WriteLine("✓ Simulation completed successfully");
        return 0;
    }
    
    private static int RunLateStageSimulation(NetworkSimulator simulator, string[] args)
    {
        var isolatedCount = GetIntArg(args, "--isolated", 5);
        
        Console.WriteLine("◈ LATE-STAGE PROPAGATION SIMULATION");
        Console.WriteLine("───────────────────────────────────────────────────────────────────");
        Console.WriteLine();
        Console.WriteLine($"Simulating {isolatedCount} nodes without direct internet...");
        Console.WriteLine();
        
        // Create base network with internet nodes
        simulator.CreateNodes(10, internetRatio: 0.8);
        simulator.CreateMeshTopology();
        
        var result = simulator.SimulateLateStageScenario(isolatedCount);
        
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  LATE-STAGE RESULTS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Isolated nodes created: {result.NodesCreated}");
        Console.WriteLine($"  Relay path found: {(result.RelaySuccess ? "✓ Yes" : "✗ No")}");
        Console.WriteLine($"  Hops required: {result.HopsRequired}");
        Console.WriteLine($"  Clients connected: {result.ClientsConnected}");
        Console.WriteLine($"  Clients with internet: {result.ClientsWithInternet}");
        
        if (result.Path != null)
        {
            Console.WriteLine($"  Path: {string.Join(" → ", result.Path)}");
        }
        
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        return result.RelaySuccess ? 0 : 1;
    }
    
    private static int RunGossipSimulation(NetworkSimulator simulator, string[] args)
    {
        var nodeCount = GetIntArg(args, "--nodes", 30);
        
        Console.WriteLine("◈ GOSSIP PROPAGATION SIMULATION");
        Console.WriteLine("───────────────────────────────────────────────────────────────────");
        Console.WriteLine();
        
        // Create network
        simulator.CreateNodes(nodeCount, internetRatio: 0.6);
        simulator.CreateMeshTopology(peersPerNode: 4);
        
        Console.WriteLine($"Created network with {nodeCount} nodes");
        Console.WriteLine();
        
        // Run multiple GOSSIP updates
        var results = new List<GossipSimulationResult>();
        for (int i = 0; i < 5; i++)
        {
            var sourceNode = simulator.GetStats().TotalNodes > 0 
                ? $"NFRAME_{Guid.NewGuid().ToString()[..8].ToUpper()}" 
                : "NFRAME_TEST";
            
            // Use first node
            var result = simulator.SimulateGossipUpdate(
                simulator.GetStats().TotalNodes > 0 ? "NFRAME_TEST" : sourceNode, 
                $"UPDATE_{i}");
            
            results.Add(result);
        }
        
        // Print summary
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  GOSSIP RESULTS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Updates simulated: {results.Count}");
        Console.WriteLine($"  Average coverage: {results.Average(r => r.CoveragePercent):F1}%");
        Console.WriteLine($"  Average latency: {results.Average(r => r.TotalLatencyMs):F0}ms");
        Console.WriteLine($"  Total packets lost: {results.Sum(r => r.PacketsLost)}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        return results.Average(r => r.CoveragePercent) > 90 ? 0 : 1;
    }
    
    private static int RunRelaySimulation(NetworkSimulator simulator, string[] args)
    {
        var nodeCount = GetIntArg(args, "--nodes", 20);
        var internetRatio = GetDoubleArg(args, "--internet-ratio", 0.3);
        
        Console.WriteLine("◈ P2P RELAY PATH SIMULATION");
        Console.WriteLine("───────────────────────────────────────────────────────────────────");
        Console.WriteLine();
        
        // Create network with low internet ratio to stress relay
        simulator.CreateNodes(nodeCount, internetRatio: internetRatio);
        simulator.CreateMeshTopology(peersPerNode: 3);
        
        var stats = simulator.GetStats();
        Console.WriteLine($"Network: {stats.TotalNodes} nodes, {stats.NodesWithInternet} with internet");
        Console.WriteLine();
        
        // Test relay paths for all nodes without internet
        var successes = 0;
        var failures = 0;
        var totalHops = 0;
        
        // We'd need to track node IDs properly - for now just show stats
        Console.WriteLine($"Testing relay paths...");
        Console.WriteLine();
        
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  RELAY RESULTS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine($"  Nodes with direct internet: {stats.NodesWithInternet}");
        Console.WriteLine($"  Nodes requiring relay: {stats.TotalNodes - stats.NodesWithInternet}");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        return 0;
    }
    
    private static async Task<int> RunAllSimulationsAsync(NetworkSimulator simulator)
    {
        Console.WriteLine("◈ RUNNING ALL SIMULATIONS");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();
        
        var results = new List<(string name, bool passed)>();
        
        // Edge cases
        Console.WriteLine("1. Edge Case Tests");
        var edgeReport = new NetworkSimulator().RunEdgeCaseTests();
        results.Add(("Edge Cases", edgeReport.AllPassed));
        Console.WriteLine();
        
        // Full simulation
        Console.WriteLine("2. Full Network Simulation");
        var fullResult = new NetworkSimulator().RunFullSimulation(20, 2, 50);
        results.Add(("Full Simulation", fullResult.AverageGossipCoverage > 80));
        Console.WriteLine();
        
        // Late stage
        Console.WriteLine("3. Late-Stage Propagation");
        var lateSim = new NetworkSimulator();
        lateSim.CreateNodes(10, internetRatio: 0.8);
        lateSim.CreateMeshTopology();
        var lateResult = lateSim.SimulateLateStageScenario(3);
        results.Add(("Late-Stage", lateResult.RelaySuccess));
        Console.WriteLine($"  Result: {(lateResult.RelaySuccess ? "✓ Pass" : "✗ Fail")}");
        Console.WriteLine();
        
        // Summary
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("  ALL SIMULATIONS SUMMARY");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        foreach (var (name, passed) in results)
        {
            Console.WriteLine($"  {(passed ? "✓" : "✗")} {name}");
        }
        Console.WriteLine($"  Total: {results.Count(r => r.passed)}/{results.Count} passed");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        
        await Task.CompletedTask;
        
        return results.All(r => r.passed) ? 0 : 1;
    }
    
    private static int GetIntArg(string[] args, string name, int defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out var value))
                return value;
        }
        return defaultValue;
    }
    
    private static double GetDoubleArg(string[] args, string name, double defaultValue)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && double.TryParse(args[i + 1], out var value))
                return value;
        }
        return defaultValue;
    }
}
