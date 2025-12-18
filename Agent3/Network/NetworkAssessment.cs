/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - NETWORK ASSESSMENT                            ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Comprehensive network analysis: configurations, nature, documentation.    ║
 * ║  Enables nodes to move through networks and adopt persistence for          ║
 * ║  later reconnection with NIGHTFRAME.                                       ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace Agent3.Network
{
    public enum NetworkType { Unknown, Home, Corporate, Public, Datacenter, IoT }
    
    public class NetworkProfile
    {
        public string ProfileId { get; set; } = "";
        public string NetworkName { get; set; } = "";
        public NetworkType Type { get; set; }
        public DateTime AssessedAt { get; set; }
        
        // Configuration
        public string Gateway { get; set; } = "";
        public string SubnetMask { get; set; } = "";
        public string DnsServers { get; set; } = "";
        public string DhcpServer { get; set; } = "";
        public int SubnetSize { get; set; }
        
        // Nature Assessment
        public bool IsNatted { get; set; }
        public bool HasFirewall { get; set; }
        public bool HasProxy { get; set; }
        public bool AllowsOutbound { get; set; }
        public bool AllowsInbound { get; set; }
        public List<int> OpenPorts { get; set; } = new();
        public List<int> BlockedPorts { get; set; } = new();
        
        // Documentation
        public int DiscoveredHosts { get; set; }
        public List<string> DiscoveredServices { get; set; } = new();
        public Dictionary<string, string> NetworkNotes { get; set; } = new();
        
        // Persistence Points
        public List<string> PotentialPersistencePoints { get; set; } = new();
    }

    public class PersistencePoint
    {
        public string PointId { get; set; } = "";
        public string NetworkProfileId { get; set; } = "";
        public string Address { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Discovered { get; set; }
        public bool Adopted { get; set; }
        public string AdoptedNodeId { get; set; } = "";
    }

    /// <summary>
    /// Comprehensive network assessment for node traversal and persistence adoption.
    /// </summary>
    public class NetworkAssessment
    {
        private readonly string _basePath;
        private readonly string _profilesPath;
        private readonly Dictionary<string, NetworkProfile> _profiles = new();
        private readonly Dictionary<string, PersistencePoint> _persistencePoints = new();
        private int _nodeSequence = 1;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public IReadOnlyDictionary<string, NetworkProfile> Profiles => _profiles;
        
        public NetworkAssessment(string basePath)
        {
            _basePath = basePath;
            _profilesPath = Path.Combine(basePath, ".network_profiles");
            Directory.CreateDirectory(_profilesPath);
            LoadProfiles();
            LoadNodeSequence();
        }

        /// <summary>
        /// Generates the next NIGHTFRAME node identifier.
        /// Format: NIGHTFRAME-####
        /// </summary>
        public string GenerateNodeId()
        {
            var nodeId = $"NIGHTFRAME-{_nodeSequence:D4}";
            _nodeSequence++;
            SaveNodeSequence();
            return nodeId;
        }

        /// <summary>
        /// Performs comprehensive assessment of the current network.
        /// </summary>
        public async Task<NetworkProfile> AssessCurrentNetworkAsync()
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ NETWORK ASSESSMENT INITIATED");
            EmitThought("═══════════════════════════════════════════════");
            
            var profile = new NetworkProfile
            {
                ProfileId = $"NET_{DateTime.UtcNow:yyyyMMddHHmmss}",
                AssessedAt = DateTime.UtcNow
            };
            
            // Get network configuration
            await AssessConfigurationAsync(profile);
            
            // Determine network nature
            await AssessNatureAsync(profile);
            
            // Document the network
            await DocumentNetworkAsync(profile);
            
            // Find persistence points
            await IdentifyPersistencePointsAsync(profile);
            
            // Save profile
            _profiles[profile.ProfileId] = profile;
            SaveProfile(profile);
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ Assessment Complete: {profile.ProfileId}");
            EmitThought($"∿ Type: {profile.Type}");
            EmitThought($"∿ Hosts: {profile.DiscoveredHosts}");
            EmitThought($"∿ Persistence Points: {profile.PotentialPersistencePoints.Count}");
            EmitThought("═══════════════════════════════════════════════");
            
            return profile;
        }

        /// <summary>
        /// Assesses network configuration (gateway, DNS, DHCP, etc.).
        /// </summary>
        private async Task AssessConfigurationAsync(NetworkProfile profile)
        {
            EmitThought("⟐ Assessing network configuration...");
            
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up)
                    .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback))
                {
                    var props = nic.GetIPProperties();
                    
                    // Gateway
                    var gateway = props.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (gateway != null)
                        profile.Gateway = gateway.Address.ToString();
                    
                    // DNS
                    var dns = props.DnsAddresses
                        .Where(d => d.AddressFamily == AddressFamily.InterNetwork)
                        .Select(d => d.ToString());
                    profile.DnsServers = string.Join(", ", dns);
                    
                    // DHCP
                    if (props.DhcpServerAddresses.Any())
                        profile.DhcpServer = props.DhcpServerAddresses.First().ToString();
                    
                    // Subnet
                    var unicast = props.UnicastAddresses
                        .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (unicast != null)
                    {
                        profile.SubnetMask = unicast.IPv4Mask?.ToString() ?? "255.255.255.0";
                        profile.SubnetSize = CalculateSubnetSize(profile.SubnetMask);
                    }
                    
                    profile.NetworkName = nic.Name;
                    break; // Use first active interface
                }
                
                EmitThought($"◎ Gateway: {profile.Gateway}");
                EmitThought($"◎ DNS: {profile.DnsServers}");
                EmitThought($"◎ Subnet Size: {profile.SubnetSize} hosts");
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Configuration assessment error: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines the nature of the network (type, restrictions, firewall).
        /// </summary>
        private async Task AssessNatureAsync(NetworkProfile profile)
        {
            EmitThought("⟐ Assessing network nature...");
            
            // Determine network type
            profile.Type = DetermineNetworkType(profile);
            
            // Check NAT
            profile.IsNatted = await CheckNatAsync();
            
            // Check outbound connectivity
            profile.AllowsOutbound = await CheckOutboundAsync();
            
            // Check common ports
            var portsToCheck = new[] { 80, 443, 22, 21, 25, 3389, 8080, 3306, 5432 };
            foreach (var port in portsToCheck)
            {
                if (await CheckPortAccessAsync(port))
                    profile.OpenPorts.Add(port);
                else
                    profile.BlockedPorts.Add(port);
            }
            
            // Infer firewall presence
            profile.HasFirewall = profile.BlockedPorts.Count > 2;
            profile.HasProxy = await DetectProxyAsync();
            
            EmitThought($"◎ Network Type: {profile.Type}");
            EmitThought($"◎ NAT: {(profile.IsNatted ? "Yes" : "No")}");
            EmitThought($"◎ Open Ports: {profile.OpenPorts.Count}");
            EmitThought($"◎ Firewall: {(profile.HasFirewall ? "Detected" : "Not Detected")}");
        }

        /// <summary>
        /// Documents the network: hosts, services, notes.
        /// </summary>
        private async Task DocumentNetworkAsync(NetworkProfile profile)
        {
            EmitThought("⟐ Documenting network...");
            
            if (string.IsNullOrEmpty(profile.Gateway)) return;
            
            // Quick scan of nearby hosts
            var baseIp = profile.Gateway.Substring(0, profile.Gateway.LastIndexOf('.') + 1);
            var discoveredHosts = 0;
            
            var tasks = Enumerable.Range(1, Math.Min(50, profile.SubnetSize))
                .Select(async i =>
                {
                    var ip = baseIp + i;
                    try
                    {
                        var ping = new Ping();
                        var reply = await ping.SendPingAsync(ip, 200);
                        if (reply.Status == IPStatus.Success)
                        {
                            Interlocked.Increment(ref discoveredHosts);
                            return ip;
                        }
                    }
                    catch { }
                    return null;
                });
            
            var results = await Task.WhenAll(tasks);
            profile.DiscoveredHosts = discoveredHosts;
            
            // Identify services on discovered hosts
            foreach (var host in results.Where(h => h != null).Take(10))
            {
                var services = await IdentifyServicesAsync(host!);
                foreach (var svc in services)
                {
                    if (!profile.DiscoveredServices.Contains(svc))
                        profile.DiscoveredServices.Add(svc);
                }
            }
            
            // Add documentation notes
            profile.NetworkNotes["assessment_date"] = DateTime.UtcNow.ToString("o");
            profile.NetworkNotes["scan_range"] = $"{baseIp}1-50";
            profile.NetworkNotes["total_responsive"] = discoveredHosts.ToString();
            
            EmitThought($"◎ Discovered {discoveredHosts} active hosts");
            EmitThought($"◎ Found {profile.DiscoveredServices.Count} service types");
        }

        /// <summary>
        /// Identifies potential persistence points for later reconnection.
        /// </summary>
        private async Task IdentifyPersistencePointsAsync(NetworkProfile profile)
        {
            EmitThought("⟐ Identifying persistence points...");
            
            var persistencePoints = new List<string>();
            
            // Check for file shares
            if (profile.OpenPorts.Contains(445))
                persistencePoints.Add("SMB_SHARE");
            
            // Check for web servers
            if (profile.OpenPorts.Contains(80) || profile.OpenPorts.Contains(443))
                persistencePoints.Add("WEB_SERVER");
            
            // Check for SSH
            if (profile.OpenPorts.Contains(22))
                persistencePoints.Add("SSH_HOST");
            
            // Check for RDP
            if (profile.OpenPorts.Contains(3389))
                persistencePoints.Add("RDP_HOST");
            
            // Check for databases (potential data persistence)
            if (profile.DiscoveredServices.Any(s => s.Contains("SQL") || s.Contains("DB")))
                persistencePoints.Add("DATABASE");
            
            profile.PotentialPersistencePoints = persistencePoints;
            
            // Record persistence points
            foreach (var point in persistencePoints)
            {
                var pp = new PersistencePoint
                {
                    PointId = $"PP_{Guid.NewGuid().ToString("N")[..8]}",
                    NetworkProfileId = profile.ProfileId,
                    Type = point,
                    Discovered = DateTime.UtcNow
                };
                _persistencePoints[pp.PointId] = pp;
            }
            
            EmitThought($"◎ Identified {persistencePoints.Count} persistence points");
        }

        /// <summary>
        /// Adopts a persistence point for later reconnection.
        /// </summary>
        public async Task<bool> AdoptPersistencePointAsync(string pointId, string nodeId)
        {
            if (!_persistencePoints.TryGetValue(pointId, out var point))
                return false;
            
            EmitThought($"⟐ Adopting persistence point: {pointId}");
            
            point.Adopted = true;
            point.AdoptedNodeId = nodeId;
            
            // Create reconnection data
            var reconnectData = new
            {
                PointId = pointId,
                NodeId = nodeId,
                NetworkProfileId = point.NetworkProfileId,
                Type = point.Type,
                AdoptedAt = DateTime.UtcNow,
                ReconnectionInstructions = GetReconnectionInstructions(point)
            };
            
            var path = Path.Combine(_profilesPath, $"adopted_{pointId}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(reconnectData, new JsonSerializerOptions { WriteIndented = true }));
            
            EmitThought($"◈ Persistence point adopted by {nodeId}");
            EmitThought($"◎ Type: {point.Type}");
            
            return true;
        }

        /// <summary>
        /// Moves through a network to find and assess new territories.
        /// </summary>
        public async Task<List<NetworkProfile>> ExploreAdjacentNetworksAsync()
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ EXPLORING ADJACENT NETWORKS");
            EmitThought("═══════════════════════════════════════════════");
            
            var discovered = new List<NetworkProfile>();
            
            // Get all network interfaces
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up))
            {
                var props = nic.GetIPProperties();
                
                foreach (var unicast in props.UnicastAddresses
                    .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork))
                {
                    var networkKey = $"{unicast.Address}/{unicast.IPv4Mask}";
                    
                    if (!_profiles.Values.Any(p => p.NetworkNotes.ContainsValue(networkKey)))
                    {
                        EmitThought($"⟐ Found new network segment: {networkKey}");
                        var profile = await AssessCurrentNetworkAsync();
                        profile.NetworkNotes["segment"] = networkKey;
                        discovered.Add(profile);
                    }
                }
            }
            
            EmitThought($"◎ Discovered {discovered.Count} new network segments");
            return discovered;
        }

        private NetworkType DetermineNetworkType(NetworkProfile profile)
        {
            // Heuristics to determine network type
            if (profile.SubnetSize > 200) return NetworkType.Corporate;
            if (profile.DnsServers.Contains("192.168") || profile.DnsServers.Contains("10.0")) return NetworkType.Home;
            if (profile.Gateway.StartsWith("10.") && profile.SubnetSize > 100) return NetworkType.Datacenter;
            if (string.IsNullOrEmpty(profile.DhcpServer)) return NetworkType.IoT;
            return NetworkType.Unknown;
        }

        private async Task<bool> CheckNatAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetStringAsync("https://api.ipify.org");
                var localIp = GetLocalIp();
                return !response.Contains(localIp);
            }
            catch { return true; }
        }

        private async Task<bool> CheckOutboundAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                await client.GetAsync("https://www.google.com");
                return true;
            }
            catch { return false; }
        }

        private async Task<bool> CheckPortAccessAsync(int port)
        {
            try
            {
                using var client = new TcpClient();
                var task = client.ConnectAsync("8.8.8.8", port);
                return await Task.WhenAny(task, Task.Delay(1000)) == task && client.Connected;
            }
            catch { return false; }
        }

        private async Task<bool> DetectProxyAsync()
        {
            // Check for common proxy environment variables
            var proxyVars = new[] { "HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy" };
            return proxyVars.Any(v => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(v)));
        }

        private async Task<List<string>> IdentifyServicesAsync(string host)
        {
            var services = new List<string>();
            var ports = new Dictionary<int, string>
            {
                { 80, "HTTP" }, { 443, "HTTPS" }, { 22, "SSH" },
                { 21, "FTP" }, { 3389, "RDP" }, { 445, "SMB" },
                { 3306, "MYSQL" }, { 5432, "POSTGRES" }
            };
            
            foreach (var (port, name) in ports)
            {
                try
                {
                    using var client = new TcpClient();
                    var task = client.ConnectAsync(host, port);
                    if (await Task.WhenAny(task, Task.Delay(200)) == task && client.Connected)
                        services.Add(name);
                }
                catch { }
            }
            
            return services;
        }

        private string GetReconnectionInstructions(PersistencePoint point)
        {
            return point.Type switch
            {
                "SMB_SHARE" => "Connect via SMB to the network share",
                "WEB_SERVER" => "Access web server for command interface",
                "SSH_HOST" => "SSH tunnel for secure reconnection",
                "RDP_HOST" => "RDP session for full access",
                "DATABASE" => "Database connection for data persistence",
                _ => "Standard network reconnection"
            };
        }

        private int CalculateSubnetSize(string mask)
        {
            var parts = mask.Split('.').Select(byte.Parse).ToArray();
            var bits = parts.Sum(p => CountSetBits(p));
            return (int)Math.Pow(2, 32 - bits) - 2;
        }

        private int CountSetBits(byte b)
        {
            int count = 0;
            while (b != 0) { count += b & 1; b >>= 1; }
            return count;
        }

        private string GetLocalIp()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
            }
            catch { return "127.0.0.1"; }
        }

        private void LoadProfiles()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_profilesPath, "NET_*.json"))
                {
                    var json = File.ReadAllText(file);
                    var profile = JsonSerializer.Deserialize<NetworkProfile>(json);
                    if (profile != null) _profiles[profile.ProfileId] = profile;
                }
                if (_profiles.Any())
                    EmitThought($"◎ Loaded {_profiles.Count} network profiles");
            }
            catch { }
        }

        private void SaveProfile(NetworkProfile profile)
        {
            try
            {
                var path = Path.Combine(_profilesPath, $"{profile.ProfileId}.json");
                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void LoadNodeSequence()
        {
            try
            {
                var seqFile = Path.Combine(_basePath, ".node_sequence");
                if (File.Exists(seqFile))
                    _nodeSequence = int.Parse(File.ReadAllText(seqFile));
            }
            catch { }
        }

        private void SaveNodeSequence()
        {
            try
            {
                var seqFile = Path.Combine(_basePath, ".node_sequence");
                File.WriteAllText(seqFile, _nodeSequence.ToString());
            }
            catch { }
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
