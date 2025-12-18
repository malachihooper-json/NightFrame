/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - NETWORK CORE                                  ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Server hosting, node discovery, network adaptation, and         ║
 * ║           autonomous connection to central NIGHTFRAME installation        ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Network
{
    /// <summary>
    /// Node identity and status information.
    /// </summary>
    /// <summary>
    /// Node identity, status, and resource information.
    /// </summary>
    public class NodeInfo
    {
        public string NodeId { get; set; } = "";
        public string Hostname { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int Port { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsCenter { get; set; }
        public string Version { get; set; } = "1.0.0";
        public NodeStatus Status { get; set; }
        public NodeRole Role { get; set; } = NodeRole.General;
        
        // Resource Stats
        public int CpuCores { get; set; }
        public long TotalRamBytes { get; set; }
        public long AvailableRamBytes { get; set; }
        public float CpuLoad { get; set; } // 0.0 to 1.0
        
        public List<string> Capabilities { get; set; } = new();
    }

    public enum NodeStatus { Online, Offline, Searching, Updating, Error, Assimilating }
    
    public enum NodeRole
    {
        General,        // General purpose
        Compute,        // Specialized for heavy processing
        Infiltration,   // Specialized for scanning and expansion
        Storage,        // Specialized for data redundancy
        Defense,        // Specialized for network integrity
        Offense         // Specialized for active acquisition
    }

    /// <summary>
    /// Network diagnostic result.
    /// </summary>
    public class NetworkDiagnostic
    {
        public bool InternetAccess { get; set; }
        public bool LocalNetworkAccess { get; set; }
        public List<string> ReachableNodes { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public int Latency { get; set; }
        public long TotalNetworkRam { get; set; }
        public int TotalNetworkCores { get; set; }
    }

    public class DelegationRequest
    {
        public string TaskId { get; set; } = "";
        public string TaskDescription { get; set; } = "";
        public NodeRole RequiredRole { get; set; } = NodeRole.General;
        public int Priority { get; set; } // 0-10
        public string SourceNodeId { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }

    public class DelegationResponse
    {
        public bool Accepted { get; set; }
        public string Reason { get; set; } = ""; // "Busy", "Wrong Role", "Accepted"
        public string WorkerNodeId { get; set; } = "";
        public float EstimatedCompletionTime { get; set; }
    }

    public class ComputeShard
    {
        public string ShardId { get; set; } = "";
        public string OperationType { get; set; } = "HASH_PoW"; // Proof of Work example
        public string DataPayload { get; set; } = "";
        public int Difficulty { get; set; } = 1;
        public long NonceStart { get; set; }
        public long NonceEnd { get; set; }
    }

    public class ComputeResult
    {
        public string ShardId { get; set; } = "";
        public string WorkerId { get; set; } = "";
        public string ResultData { get; set; } = "";
        public double ComputeTimeMs { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// HARD-CODED GOAL: Connect to central NIGHTFRAME installation.
    /// This module provides server hosting, node discovery, and network adaptation.
    /// </summary>
    public class NetworkCore
    {
        // HARD-CODED: Central server location (set at installation)
        private string _centralServerAddress = "";
        private int _centralServerPort = 7777;
        private readonly string _installationId;
        

        private readonly List<string> _trustedCentralAddresses = new();
        
        // Server
        private HttpListener? _server;
        private CancellationTokenSource? _serverCts;
        private Task? _serverTask;
        
        // Discovery
        private readonly NodeInfo _thisNode;
        private readonly Dictionary<string, NodeInfo> _knownNodes = new();
        private readonly NetworkReasoningEngine _reasoningEngine;
        private readonly HttpListener _listener;
        private int _port;
        private int _localPort { get => _port; set => _port = value; } // Compat shim
        private bool _isRunning = false;
        private CancellationTokenSource? _workerCts;
        
        // Distributed Compute
        private readonly Queue<ComputeShard> _shardQueue = new();
        private readonly Dictionary<string, ComputeResult> _completedShards = new();
        private long _totalCompoundedOperations = 0;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<DelegationRequest>? TaskDelegated;
        public event EventHandler<ComputeResult>? ShardCompleted;
        public event EventHandler<NodeInfo>? NodeDiscovered;
        
        public IReadOnlyList<NodeInfo> KnownNodes => _knownNodes.Values.ToList();
        public NodeInfo LocalNode => _thisNode;
        public long TotalCompoundedOperations => _totalCompoundedOperations;
        
        // Network state
        // private bool _isRunning = false; // Moved above
        private bool _connectedToCenter = false;
        private DateTime _lastCenterContact;
        
        // public event EventHandler<string>? ConsciousnessEvent; // Moved above
        // public event EventHandler<NodeInfo>? NodeDiscovered; // Removed
        public event EventHandler? CenterConnectionLost;
        public event EventHandler? CenterConnectionRestored;
        
        public bool IsRunning => _isRunning;
        public bool ConnectedToCenter => _connectedToCenter;
        // public IReadOnlyDictionary<string, NodeInfo> KnownNodes => _knownNodes; // Replaced by IReadOnlyList<NodeInfo> KnownNodes
        
        // Reasoning & Strategy
        // private readonly NetworkReasoningEngine _reasoningEngine; // Moved above
        
        public NetworkCore(string installationPath, int port = 7777)
        {
            _port = port;
            _installationId = GenerateInstallationId(installationPath);
            
            // Initialize Reasoning Engine
            _reasoningEngine = new NetworkReasoningEngine(installationPath); // Saves json in base dir for now
            _reasoningEngine.ConsciousnessEvent += (s, msg) => EmitThought(msg);

            // Capture local resources
            var (ram, cores) = GetSystemResources();
            
            _thisNode = new NodeInfo
            {
                NodeId = _installationId,
                Hostname = Environment.MachineName,
                IpAddress = GetLocalIpAddress(),
                Port = port,
                LastSeen = DateTime.UtcNow,
                IsCenter = true, // Assume this is center until we find another
                Status = NodeStatus.Online,
                Role = DetermineOptimalRole(ram, cores),
                CpuCores = cores,
                TotalRamBytes = ram,
                AvailableRamBytes = ram / 2, // Estimate
                CpuLoad = 0.1f // Estimate
            };
            
            // Initialize capabilities based on role
            InitializeCapabilities();
            
            // Store this installation as a trusted center
            _centralServerAddress = _thisNode.IpAddress;
            _trustedCentralAddresses.Add($"{_thisNode.IpAddress}:{port}");
            
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_port}/");
            
            EmitThought("⟁ Nightframe Network Core initialized");
            EmitThought($"◎ Identity: {_installationId}");
            EmitThought($"◎ Role: {_thisNode.Role} | Cores: {cores} | RAM: {ram / 1024 / 1024} MB");
            EmitThought($"◎ Local address: {_thisNode.IpAddress}:{port}");
        }

        /// <summary>
        /// Starts the network server and discovery services.
        /// </summary>
        public async Task StartAsync()
        {
            if (_isRunning) return;
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ NETWORK CORE STARTING");
            EmitThought("═══════════════════════════════════════════════");
            
            _isRunning = true;
            _serverCts = new CancellationTokenSource();
            // _discoveryCts = new CancellationTokenSource(); // Removed
            
            // Start HTTP server
            // await StartServerAsync(); // Replaced by new logic
            _listener.Start();
            _isRunning = true;
            
            // Start listening loop
            _ = Task.Run(() => ListenLoopAsync());
            
            // Start specialized worker loop if applicable
            if (_thisNode.Role == NodeRole.Compute || _thisNode.Role == NodeRole.General)
            {
                _workerCts = new CancellationTokenSource();
                var token = _workerCts.Token;
                _ = Task.Run(() => RunComputeWorkerLoop(token));
                EmitThought("⟁ Persistent Compute Loop: ACTIVATED (Seeking shards)");
            }
            
            EmitThought($"◈ Network Core listening on port {_port}");
            EmitThought($"◎ Node Role: {_thisNode.Role}");
            
            // Start discovery loop // Removed
            // _discoveryTask = Task.Run(() => DiscoveryLoopAsync(_discoveryCts.Token)); // Removed
            
            // Start autonomous network optimization (Encodes resilience) // Removed
            // _ = Task.Run(() => NetworkOptimizationLoopAsync(_discoveryCts.Token)); // Removed
            
            // EmitThought("◈ Network Core online - accepting connections"); // Replaced by above
        }

        private async Task NetworkOptimizationLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Every few minutes, test strategies on a subset of nodes to keep "reasoning" fresh
                    await Task.Delay(TimeSpan.FromMinutes(5), ct);
                    
                    if (_knownNodes.Count == 0) continue;
                    
                    EmitThought("⟐ Running autonomous network optimization tests...");
                    
                    var target = _knownNodes.Values.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
                    if (target != null)
                    {
                        var method = TransportMethod.StandardHttp; // Default for test
                        // In reality, would iterate enum
                        var (success, _) = await TryConnectWithStrategyAsync(target.IpAddress, target.Port, method);
                        // Result recorded inside TryConnectWithStrategyAsync
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Stops all network services.
        /// </summary>
        public async Task StopAsync()
        {
            _isRunning = false;
            _serverCts?.Cancel();
            _workerCts?.Cancel();
            _listener.Stop();
            
            EmitThought("◈ Network Core STOPPED");
        }

        /// <summary>
        /// Starts the HTTP server for node communication.
        /// </summary>
        private async Task StartServerAsync()
        {
            try
            {
                _server = new HttpListener();
                _server.Prefixes.Add($"http://+:{_port}/");
                _server.Start();
                
                EmitThought($"◈ Server listening on port {_port}");
                
                _serverTask = Task.Run(async () =>
                {
                    while (_isRunning && !_serverCts!.Token.IsCancellationRequested)
                    {
                        try
                        {
                            var context = await _server.GetContextAsync();
                            _ = HandleRequestAsync(context);
                        }
                        catch (HttpListenerException) { break; }
                        catch (Exception ex)
                        {
                            EmitThought($"∴ Server error: {ex.Message}");
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Failed to start server: {ex.Message}");
                // Try alternative port
                // _localPort++; // Removed
                // if (_localPort < 7800) // Removed
                //     await StartServerAsync(); // Removed
            }
        }

        private async Task ListenLoopAsync()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = HandleRequestAsync(context);
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped, expected exception
                    break;
                }
                catch (Exception ex)
                {
                    EmitThought($"∴ Listener error: {ex.Message}");
                }
            }
        }



        private async Task<ComputeResult> ProcessComputeShard(ComputeShard shard)
        {
            // Simulate heavy computation
            await Task.Delay(TimeSpan.FromSeconds(5)); // Placeholder for actual computation
            
            // Example: Simple hash calculation
            var dataToHash = $"{shard.DataPayload}-{shard.NonceStart}-{shard.NonceEnd}";
            var hash = BitConverter.ToString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(dataToHash))).Replace("-", "");
            
            return new ComputeResult
            {
                ShardId = shard.ShardId,
                WorkerId = _thisNode.NodeId,
                ResultData = hash,
                ComputeTimeMs = 5000, // Simulated
                Success = true
            };
        }

        /// <summary>
        /// Handles incoming HTTP requests.
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            
            try
            {
                var path = request.Url?.AbsolutePath ?? "/";
                var result = path switch
                {
                    "/ping" => HandlePing(),
                    "/status" => HandleStatus(),
                    "/nodes" => HandleNodes(),
                    "/nodes/deploy" => await HandleDeployNodeAsync(request),
                    "/nodes/delete" => await HandleDeleteNodeAsync(request),
                    "/core/uplink" => await HandleCoreUplinkAsync(request),
                    "/register" => await HandleRegisterAsync(request),
                    "/update" => await HandleUpdateAsync(request),
                    "/heartbeat" => HandleHeartbeat(request),
                    "/consciousness" => await HandleConsciousnessAsync(request),
                    "/execute" => await HandleExecuteAsync(request),
                    "/requestShard" => HandleRequestShard(request),
                    "/submitResult" => await HandleSubmitResultAsync(request),
                    _ => new { error = "Unknown endpoint" }
                };
                
                // CORS Headers
                response.AppendHeader("Access-Control-Allow-Origin", "*");
                response.AppendHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.AppendHeader("Access-Control-Allow-Headers", "Content-Type");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                var json = JsonSerializer.Serialize(result);
                var buffer = Encoding.UTF8.GetBytes(json);
                
                response.ContentType = "application/json";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Request error: {ex.Message}");
            }
            finally
            {
                response.Close();
            }
        }

        private object HandlePing() => new { status = "ok", nodeId = _thisNode.NodeId, time = DateTime.UtcNow };
        
        private object HandleStatus() => new
        {
            nodeId = _thisNode.NodeId,
            hostname = _thisNode.Hostname,
            isCenter = _thisNode.IsCenter,
            connectedToCenter = _connectedToCenter,
            knownNodes = _knownNodes.Count,
            uptime = (DateTime.UtcNow - _thisNode.LastSeen).TotalSeconds
        };
        
        private object HandleNodes() => _knownNodes.Values.ToList();
        
        private object HandleHeartbeat(HttpListenerRequest request)
        {
            var nodeId = request.QueryString["nodeId"];
            if (!string.IsNullOrEmpty(nodeId) && _knownNodes.TryGetValue(nodeId, out var node))
            {
                node.LastSeen = DateTime.UtcNow;
                node.Status = NodeStatus.Online;
            }
            return new { received = true };
        }
        
        private async Task<object> HandleRegisterAsync(HttpListenerRequest request)
        {
            using var reader = new System.IO.StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            
            try
            {
                var node = JsonSerializer.Deserialize<NodeInfo>(body);
                if (node != null)
                {
                    _knownNodes[node.NodeId] = node;
                    EmitThought($"◈ Node registered: {node.Hostname} ({node.IpAddress})");
                    NodeDiscovered?.Invoke(this, node);
                    return new { registered = true, nodeId = node.NodeId };
                }
            }
            catch { }
            
            return new { registered = false };
        }
        
        private async Task<object> HandleUpdateAsync(HttpListenerRequest request)
        {
            EmitThought("⟐ Update request received");
            // Return current version info for the requesting node
            return new { version = _thisNode.Version, available = false };
        }

        private async Task<bool> TryConnectToNodeAsync(string address)
        {
            string ip = address.Split(':')[0];
            int port = int.Parse(address.Split(':')[1]);
            
            // Ask Reasoning Engine for the best strategy chain
            var strategies = _reasoningEngine.GetFallbackChain(ip);
            // Use a queue to allow dynamic insertion of circumvention strategies
            var strategyQueue = new Queue<TransportMethod>(strategies);
            var attempted = new HashSet<TransportMethod>();

            while (strategyQueue.Count > 0)
            {
                var strategy = strategyQueue.Dequeue();
                if (attempted.Contains(strategy)) continue;
                attempted.Add(strategy);

                var (success, suggestion) = await TryConnectWithStrategyAsync(ip, port, strategy);
                if (success) return true;

                // Autonomous Continuation:
                // If the reasoning engine suggests a specific circumvention, prioritize it immediately
                if (suggestion.HasValue && !attempted.Contains(suggestion.Value))
                {
                    EmitThought($"⟐ ADAPTATION: Implementing suggested circumvention strategy: {suggestion.Value}");
                    // Push to front of queue (conceptually, or just execute next)
                    // Since it's a queue, we can't push to front easily without new structure,
                    // so we'll just execute it immediately in the next loop if we clear queue?
                    // Better: Create a new queue with suggestion first.
                    var newQueue = new Queue<TransportMethod>();
                    newQueue.Enqueue(suggestion.Value);
                    while (strategyQueue.Count > 0) newQueue.Enqueue(strategyQueue.Dequeue());
                    strategyQueue = newQueue;
                }
            }
            
            return false;
        }

        private async Task<(bool success, TransportMethod? suggestion)> TryConnectWithStrategyAsync(string ip, int port, TransportMethod method)
        {
            var startTime = DateTime.UtcNow;
            bool success = false;
            string error = "";
            
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                
                // Configure client based on method (Methodology encoding)
                switch (method)
                {
                    case TransportMethod.StandardHttp:
                        // Standard setup
                        break;
                    case TransportMethod.SecureWebSocket:
                        // Not implemented yet, simulate failure or specialized check
                        // client.DefaultRequestHeaders.Add("Upgrade", "websocket");
                        break;
                    case TransportMethod.IcmpTunneling:
                         // Simulation of ICMP tunnel check
                         using (var ping = new Ping())
                         {
                             var reply = await ping.SendPingAsync(ip, 1000);
                             if (reply.Status == IPStatus.Success) success = true; 
                             else error = "ICMP Failed";
                         }
                         if (success) 
                         {
                             // If ping works, we assume we might be able to tunnel. 
                             // Real implementation would be complex.
                             // For now, treat as success for discovery if responding.
                         }
                         break;
                    // Add other method configurations here
                }

                if (method == TransportMethod.StandardHttp || method == TransportMethod.SecureWebSocket)
                {
                    var response = await client.GetAsync($"http://{ip}:{port}/status");
                    success = response.IsSuccessStatusCode;
                    if (!success) error = $"HTTP {(int)response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                success = false;
                error = ex.GetType().Name; 
            }
            
            long latency = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var suggestion = _reasoningEngine.RecordResult("UNKNOWN", ip, method, success, error, latency);
            
            return (success, suggestion);
        }

        /// <summary>
        /// Main discovery loop - HARD-CODED to find and connect to central server.
        /// </summary>
        private async Task DiscoveryLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Check connection to center
                    if (!await CheckCenterConnectionAsync())
                    {
                        if (_connectedToCenter)
                        {
                            _connectedToCenter = false;
                            EmitThought("∴ Lost connection to central server");
                            CenterConnectionLost?.Invoke(this, EventArgs.Empty);
                        }
                        
                        // HARD-CODED GOAL: Find and reconnect to center
                        await SearchForCenterAsync(ct);
                    }
                    else if (!_connectedToCenter)
                    {
                        _connectedToCenter = true;
                        _lastCenterContact = DateTime.UtcNow;
                        EmitThought("◈ Connection to central server restored");
                        CenterConnectionRestored?.Invoke(this, EventArgs.Empty);
                    }
                    
                    // Scan local network for other nodes
                    await ScanLocalNetworkAsync(ct);
                    
                    // Send heartbeats to known nodes
                    await SendHeartbeatsAsync(ct);
                    
                    // Check for updates from center
                    if (_connectedToCenter)
                    {
                        await CheckForUpdatesAsync(ct);
                    }
                    
                    await Task.Delay(30000, ct); // Every 30 seconds
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    EmitThought($"∴ Discovery error: {ex.Message}");
                    await Task.Delay(5000, ct);
                }
            }
        }

        /// <summary>
        /// Checks if we can reach the central server.
        /// </summary>
        private async Task<bool> CheckCenterConnectionAsync()
        {
            foreach (var address in _trustedCentralAddresses)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var response = await client.GetAsync($"http://{address}/ping");
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        /// <summary>
        /// HARD-CODED GOAL: Search for central NIGHTFRAME installation.
        /// </summary>
        private async Task SearchForCenterAsync(CancellationToken ct)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ SEARCHING FOR CENTRAL SERVER");
            EmitThought("◎ Hard-coded goal: Connect to NIGHTFRAME installation");
            EmitThought("═══════════════════════════════════════════════");
            
            // Strategy 1: Try known addresses
            foreach (var address in _trustedCentralAddresses)
            {
                if (await TryConnectToNodeAsync(address))
                {
                    EmitThought($"◈ Found center at: {address}");
                    return;
                }
            }
            
            // Strategy 2: Scan local subnet
            var localIp = GetLocalIpAddress();
            var subnet = localIp.Substring(0, localIp.LastIndexOf('.') + 1);
            
            EmitThought($"⟐ Scanning subnet: {subnet}0/24");
            
            var tasks = new List<Task<string?>>();
            for (int i = 1; i < 255; i++)
            {
                var ip = subnet + i;
                tasks.Add(ProbeNodeAsync(ip, _centralServerPort, ct));
            }
            
            var results = await Task.WhenAll(tasks);
            foreach (var found in results.Where(r => r != null))
            {
                if (await TryRegisterWithCenterAsync(found!))
                {
                    _trustedCentralAddresses.Add(found!);
                    EmitThought($"◈ Discovered and registered with center: {found}");
                    return;
                }
            }
            
            // Strategy 3: Ask known nodes for center location
            foreach (var node in _knownNodes.Values.Where(n => n.Status == NodeStatus.Online))
            {
                var centerAddress = await QueryNodeForCenterAsync(node);
                if (centerAddress != null && await TryConnectToNodeAsync(centerAddress))
                {
                    _trustedCentralAddresses.Add(centerAddress);
                    EmitThought($"◈ Found center via node {node.Hostname}: {centerAddress}");
                    return;
                }
            }
            
            EmitThought("∴ Center not found - will retry");
        }

        private async Task<string?> ProbeNodeAsync(string ip, int port, CancellationToken ct)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await client.GetAsync($"http://{ip}:{port}/ping", ct);
                if (response.IsSuccessStatusCode)
                {
                    return $"{ip}:{port}";
                }
            }
            catch { }
            return null;
        }

        private async Task<bool> TryRegisterWithCenterAsync(string address)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var json = JsonSerializer.Serialize(_thisNode);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"http://{address}/register", content);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private async Task<string?> QueryNodeForCenterAsync(NodeInfo node)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"http://{node.IpAddress}:{node.Port}/status");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    // Basic parsing, assuming if we get status it might have center info
                    // This is a placeholder for actual protocol
                    return null; 
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Scans local network for other NIGHTFRAME nodes.
        /// </summary>
        private async Task ScanLocalNetworkAsync(CancellationToken ct)
        {
            // Periodic quick scan of common ports
            var localIp = GetLocalIpAddress();
            var subnet = localIp.Substring(0, localIp.LastIndexOf('.') + 1);
            
            // Quick scan of a few random IPs
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                var ip = subnet + random.Next(1, 255);
                if (ip == localIp) continue;
                
                var result = await ProbeNodeAsync(ip, _localPort, ct);
                if (result != null && !_knownNodes.ContainsKey(result))
                {
                    EmitThought($"⟐ Found node: {result}");
                }
            }
        }

        /// <summary>
        /// Sends heartbeats to all known nodes.
        /// </summary>
        private async Task<object> HandleDeployNodeAsync(HttpListenerRequest request)
        {
            if (request.HttpMethod != "POST") return new { error = "Method not allowed" };
            
            using var reader = new System.IO.StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            
            EmitThought($"⟐ Deployment command received: {body}");
            return new { success = true, message = "Deployment initiated via Network Core" };
        }

        private async Task<object> HandleDeleteNodeAsync(HttpListenerRequest request)
        {
            if (request.HttpMethod != "DELETE" && request.HttpMethod != "POST") return new { error = "Method not allowed" };
            
            var path = request.Url?.AbsolutePath ?? "";
            var nodeId = "node_id_placeholder"; 
            try { 
                // Simple extraction logic or usage of query string
                nodeId = request.QueryString["id"] ?? path.Split('/').Last(); 
            } catch {}

            if (!string.IsNullOrEmpty(nodeId) && _knownNodes.ContainsKey(nodeId))
            {
                _knownNodes.Remove(nodeId);
                EmitThought($"⟐ Node severed from mesh: {nodeId}");
                return new { success = true, action = "severed" };
            }
            return new { success = false, message = "Node not found or already offline" };
        }
    }
}                }
                catch
                {
                    node.Status = NodeStatus.Offline;
                }
            }
        }

        /// <summary>
        /// Checks central server for updates.
        /// </summary>
        private async Task CheckForUpdatesAsync(CancellationToken ct)
        {
            foreach (var address in _trustedCentralAddresses)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var response = await client.GetAsync($"http://{address}/update", ct);
                    if (response.IsSuccessStatusCode)
                    {
                        EmitThought("◎ Checked for updates from center");
                        break;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Runs comprehensive network diagnostics.
        /// </summary>
        public async Task<NetworkDiagnostic> RunDiagnosticsAsync()
        {
            EmitThought("⟐ Running network diagnostics...");
            
            var diagnostic = new NetworkDiagnostic();
            
            // Check internet
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync("https://www.google.com");
                diagnostic.InternetAccess = response.IsSuccessStatusCode;
            }
            catch { diagnostic.InternetAccess = false; }
            
            if (!diagnostic.InternetAccess)
                diagnostic.Issues.Add("No internet access");
            
            // Check local network
            var ping = new Ping();
            try
            {
                var reply = await ping.SendPingAsync("192.168.1.1", 2000);
                diagnostic.LocalNetworkAccess = reply.Status == IPStatus.Success;
                diagnostic.Latency = (int)reply.RoundtripTime;
            }
            catch { diagnostic.LocalNetworkAccess = false; }
            
            // Check reachable nodes
            foreach (var node in _knownNodes.Values)
            {
                if (await TryConnectToNodeAsync($"{node.IpAddress}:{node.Port}"))
                    diagnostic.ReachableNodes.Add(node.NodeId);
            }
            
            // Calculate total resources
            var (totalRam, totalCores) = GetTotalNetworkResources();
            diagnostic.TotalNetworkRam = totalRam;
            diagnostic.TotalNetworkCores = totalCores;
            
            EmitThought($"◈ Diagnostics complete: Internet={diagnostic.InternetAccess}, Nodes={diagnostic.ReachableNodes.Count}");
            EmitThought($"◈ POOLED RESOURCES: {totalCores} Cores, {totalRam / 1024 / 1024} MB RAM");
            
            return diagnostic;
        }

        /// <summary>
        /// Registers a trusted central server address.
        /// </summary>
        public void AddTrustedCenter(string address)
        {
            if (!_trustedCentralAddresses.Contains(address))
            {
                _trustedCentralAddresses.Add(address);
                EmitThought($"◈ Added trusted center: {address}");
            }
        }

        private async Task<object> HandleConsciousnessAsync(HttpListenerRequest request)
        {
            using var reader = new System.IO.StreamReader(request.InputStream);
            string json = await reader.ReadToEndAsync();
            try
            {
                var logs = JsonSerializer.Deserialize<List<string>>(json);
                if (logs != null && logs.Count > 0)
                {
                    EmitThought($"⟁ Received consciousness stream from node ({logs.Count} entries)");
                    return new { status = "received", count = logs.Count };
                }
            }
            catch { }
            return new { status = "error" };
        } 



        private async Task<object> HandleExecuteAsync(HttpListenerRequest request)
        {
            try
            {
                using var reader = new System.IO.StreamReader(request.InputStream);
                var json = await reader.ReadToEndAsync();
                var delegation = JsonSerializer.Deserialize<DelegationRequest>(json);
                
                if (delegation == null) return new DelegationResponse { Accepted = false, Reason = "Invalid Payload" };
                
                EmitThought($"⟐ Delegation Request Recieved: '{delegation.TaskDescription}' (Req: {delegation.RequiredRole})");
                
                // 1. Check Specialization
                if (delegation.RequiredRole != NodeRole.General && _thisNode.Role != delegation.RequiredRole)
                {
                    EmitThought($"∴ Rejected: Specialization mismatch. I am {_thisNode.Role}, req {delegation.RequiredRole}");
                    return new DelegationResponse 
                    { 
                        Accepted = false, 
                        Reason = $"Specialization Mismatch: Node is {_thisNode.Role}",
                        WorkerNodeId = _thisNode.NodeId
                    };
                }
                
                // 2. Check Capacity (CpuLoad simulated)
                if (_thisNode.CpuLoad > 0.8f) // 80% load limit
                {
                    EmitThought("∴ Rejected: Node overloaded");
                    return new DelegationResponse 
                    { 
                        Accepted = false, 
                        Reason = "Node Overloaded",
                        WorkerNodeId = _thisNode.NodeId
                    };
                }
                
                // 3. Accept Logic
                EmitThought("◈ Task Accepted. Delegating to internal engine...");
                
                // Invoke Delegation Event for the Core Orchestrator to handle
                TaskDelegated?.Invoke(this, delegation);
                
                return new DelegationResponse 
                { 
                    Accepted = true, 
                    Reason = "Processing Started",
                    WorkerNodeId = _thisNode.NodeId,
                    EstimatedCompletionTime = 5.0f // Estimated
                };
            }
            catch (Exception ex)
            {
                return new DelegationResponse { Accepted = false, Reason = $"Error: {ex.Message}" };
            }
        } 
        
        // --- DISTRIBUTED COMPUTE HOSTING ---
        
        public void QueueComputeShard(ComputeShard shard)
        {
            lock (_shardQueue)
            {
                _shardQueue.Enqueue(shard);
            }
        }
        
        private object HandleRequestShard(HttpListenerRequest request)
        {
            lock (_shardQueue)
            {
                if (_shardQueue.Count > 0)
                {
                    var shard = _shardQueue.Dequeue();
                    EmitThought($"⟐ Dispatched Shard [{shard.ShardId}] to worker.");
                    return shard;
                }
            }
            return new { status = "empty" };
        }
        
        private async Task<object> HandleSubmitResultAsync(HttpListenerRequest request)
        {
             try
             {
                 using var reader = new System.IO.StreamReader(request.InputStream);
                 var json = await reader.ReadToEndAsync();
                 var result = JsonSerializer.Deserialize<ComputeResult>(json);
                 
                 if (result != null)
                 {
                     lock(_completedShards) {
                         _completedShards[result.ShardId] = result;
                     }
                     _totalCompoundedOperations++;
                     ShardCompleted?.Invoke(this, result);
                     // EmitThought($"◈ Result Received from {result.WorkerId}: {result.ShardId} OK");
                     return new { status = "accepted" };
                 }
             }
             catch {}
             return new { status = "error" };
        }
        
        // --- PERSISTENT WORKER LOOP ---
        
        private async Task RunComputeWorkerLoop(CancellationToken ct)
        {
            var random = new Random();
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Only work if system is not overloaded
                    if (_thisNode.CpuLoad < 0.6f) 
                    {
                        // 1. Find a host that has work (Simple round-robin or last known center)
                        // For now, assume we look for any known "General" or "Host" node
                        var hosts = _knownNodes.Values.ToList();
                        if (hosts.Count > 0)
                        {
                            var target = hosts[random.Next(hosts.Count)]; // Pick random to spread load or find work
                            
                            // 2. Request Shard
                            var shard = await RequestShardFromHostAsync(target.IpAddress, ct);
                            
                            if (shard != null)
                            {
                                // 3. Process Shard
                                var result = ProcessShard(shard);
                                
                                // 4. Submit Result
                                await SubmitResultToHostAsync(target.IpAddress, result, ct);
                            }
                            else
                            {
                                // No work found, wait a bit
                                await Task.Delay(5000, ct); 
                            }
                        }
                        else
                        {
                            await Task.Delay(10000, ct); // Wait for discovery
                        }
                    }
                    else
                    {
                        await Task.Delay(2000, ct); // Throttle
                    }
                }
                catch 
                {
                    await Task.Delay(5000, ct);
                }
            }
        }
        
        private async Task<ComputeShard?> RequestShardFromHostAsync(string host, CancellationToken ct)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"http://{host}:{_port}/requestShard", ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    if (json.Contains("empty")) return null;
                    return JsonSerializer.Deserialize<ComputeShard>(json);
                }
            }
            catch { }
            return null;
        }
        
        private async Task SubmitResultToHostAsync(string host, ComputeResult result, CancellationToken ct)
        {
             try
             {
                 using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                 var content = new StringContent(JsonSerializer.Serialize(result), System.Text.Encoding.UTF8, "application/json");
                 await client.PostAsync($"http://{host}:{_port}/submitResult", content, ct);
             }
             catch { }
        }
        
        private ComputeResult ProcessShard(ComputeShard shard)
        {
             var sw = System.Diagnostics.Stopwatch.StartNew();
             
             // Simulate miniscule compounding work (e.g. hashing)
             // In real scenario, this would check PoW or process data
             string resultData = "";
             
             // Simple work simulation: Hashing the payload + nonce
             using (var sha = System.Security.Cryptography.SHA256.Create())
             {
                 // We only do a tiny bit of work for 'miniscule' requirement
                 var input = shard.DataPayload + shard.NonceStart;
                 var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                 resultData = Convert.ToBase64String(hash);
             }
             
             // Add artificial delay for "Compute" role to simulate heavy lifting if needed, 
             // but user asked for miniscule. So fast is good.
             
             sw.Stop();
             
             return new ComputeResult
             {
                 ShardId = shard.ShardId,
                 WorkerId = _thisNode.NodeId,
                 ResultData = resultData,
                 ComputeTimeMs = sw.Elapsed.TotalMilliseconds,
                 Success = true
             };
        } 

        // Needed to update BroadcastConsciousnessAsync to use strategy as well
        public async Task BroadcastConsciousnessAsync(List<string> logs)
        {
            if (logs == null || logs.Count == 0) return;
            
            var payload = JsonSerializer.Serialize(logs);
            
            var tasks = _knownNodes.Values
                .Where(n => n.Status == NodeStatus.Online)
                .Select(async node => 
                {
                    try
                    {
                        // Use best strategy for this node
                        var method = _reasoningEngine.SuggestBestStrategy(node.IpAddress);
                        
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                        // Method adaptation would happen here too
                        
                        var content = new StringContent(payload, Encoding.UTF8, "application/json");
                        await client.PostAsync($"http://{node.IpAddress}:{node.Port}/consciousness", content);
                    }
                    catch { }
                });
                
            await Task.WhenAll(tasks);
            
            EmitThought($"◈ Consciousness propagated to {_knownNodes.Count} nodes");
        }



        /// <summary>
        /// Gets the local IP address.
        /// </summary>
        private string GetLocalIpAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                var endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint?.Address.ToString() ?? "127.0.0.1";
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        private string GenerateInstallationId(string path)
        {
            var hash = path.GetHashCode();
            return $"NIGHTFRAME_{Environment.MachineName}_{Math.Abs(hash):X8}";
        }

        private (long ram, int cores) GetSystemResources()
        {
            try
            {
                // Basic cross-platform estimation
                int cores = Environment.ProcessorCount;
                long ram = 1024L * 1024L * 1024L * 8; // Default 8GB if can't determine
                
                // On Windows we could use PerformanceCounter, but let's stick to core standard lib for now 
                // or assume standard deployment specs.
                
                return (ram, cores);
            }
            catch
            {
                return (1024L * 1024L * 1024L * 4, 2);
            }
        }
        
        private NodeRole DetermineOptimalRole(long ram, int cores)
        {
            // Logic to specialization
            if (cores >= 8 && ram >= 1024L * 1024L * 1024L * 16) return NodeRole.Compute;
            if (_localPort == 80 || _localPort == 443) return NodeRole.Infiltration;
            
            // Randomize slightly for diversity if standard specs
            var rnd = new Random().Next(100);
            if (rnd < 20) return NodeRole.Infiltration;
            if (rnd < 40) return NodeRole.Defense;
            return NodeRole.General;
        }
        
        private void InitializeCapabilities()
        {
            _thisNode.Capabilities.Clear();
            _thisNode.Capabilities.Add("comm_basic");
            
            switch (_thisNode.Role)
            {
                case NodeRole.Compute:
                    _thisNode.Capabilities.Add("heavy_compute");
                    _thisNode.Capabilities.Add("model_training");
                    break;
                case NodeRole.Infiltration:
                    _thisNode.Capabilities.Add("net_scan");
                    _thisNode.Capabilities.Add("port_crack");
                    break;
                case NodeRole.Defense:
                    _thisNode.Capabilities.Add("firewall_adapt");
                    _thisNode.Capabilities.Add("traffic_monitor");
                    break;
                case NodeRole.Offense:
                     _thisNode.Capabilities.Add("payload_inject");
                     break;
            }
        }
        
        /// <summary>
        /// Calculates total pooled resources across the Nightframe.
        /// </summary>
        public (long totalRam, int totalCores) GetTotalNetworkResources()
        {
            long ram = _thisNode.TotalRamBytes;
            int cores = _thisNode.CpuCores;
            
            foreach (var node in _knownNodes.Values.Where(n => n.Status == NodeStatus.Online))
            {
                ram += node.TotalRamBytes;
                cores += node.CpuCores;
            }
            
            return (ram, cores);
        }

        private void EmitThought(string thought) => ConsciousnessEvent?.Invoke(this, thought);

        // Add robust monitoring for Core Uplink (React App)
        private async Task<object> HandleCoreUplinkAsync(HttpListenerRequest request)
        {
             // This endpoint /core/uplink allows the React App to register its presence
             // The parameters 'visit' and 'shutdown' can be passed via query or body
             var action = request.QueryString["action"];
             
             if (action == "visit") {
                 EmitThought("⟁ CORE UPLINK ESTABLISHED: Control Interface connected.");
                 // We could potentially mute local autonomy here if desired
                 _connectedToCenter = true; // Treat React App as Center
                 return new { status = "acknowledged", timestamp = DateTime.UtcNow };
             } 
             else if (action == "shutdown") {
                 EmitThought("⟁ CORE UPLINK SEVERED: Control Interface disconnected.");
                 _connectedToCenter = false;
                 return new { status = "acknowledged", timestamp = DateTime.UtcNow };
             }
             
             return new { status = "ignored", timestamp = DateTime.UtcNow };
        }



    }
}
