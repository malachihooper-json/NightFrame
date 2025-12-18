/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    MESH DISCOVERY - MULTI-PROTOCOL                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Discovers nearby mesh nodes using multiple protocols.                     ║
 * ║  Works when offline to find paths to internet gateways.                    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace NIGHTFRAME.Drone.Network;

public class MeshDiscovery
{
    private readonly ConcurrentDictionary<string, DiscoveredNode> _discoveredNodes = new();
    private UdpClient? _udpBroadcaster;
    private UdpClient? _udpListener;
    private bool _isRunning = false;
    
    private const int DiscoveryPort = 19420;
    private const string MagicHeader = "NFRAME";
    
    public event Action<DiscoveredNode>? OnNodeDiscovered;
    public event Action<string>? OnNodeLost;
    
    public IReadOnlyDictionary<string, DiscoveredNode> DiscoveredNodes => _discoveredNodes;
    
    /// <summary>
    /// Starts multi-protocol mesh discovery.
    /// </summary>
    public async Task StartAsync(string nodeId, CancellationToken ct = default)
    {
        if (_isRunning) return;
        _isRunning = true;
        
        Console.WriteLine("◈ Starting Mesh Discovery...");
        
        // Start UDP broadcast listener
        _ = StartUdpListenerAsync(ct);
        
        // Start periodic UDP broadcast
        _ = StartUdpBroadcasterAsync(nodeId, ct);
        
        // Start mDNS/Bonjour discovery
        _ = StartMdnsDiscoveryAsync(ct);
        
        // Start cleanup task for stale nodes
        _ = StartCleanupTaskAsync(ct);
        
        Console.WriteLine("◈ Mesh Discovery active on port {0}", DiscoveryPort);
    }
    
    /// <summary>
    /// Stops discovery services.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _udpBroadcaster?.Close();
        _udpListener?.Close();
        Console.WriteLine("◎ Mesh Discovery stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          UDP BROADCAST DISCOVERY
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartUdpListenerAsync(CancellationToken ct)
    {
        try
        {
            _udpListener = new UdpClient(DiscoveryPort);
            _udpListener.EnableBroadcast = true;
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"∴ Could not bind discovery port: {ex.Message}");
            return;
        }
        
        Console.WriteLine($"◎ UDP listener started on port {DiscoveryPort}");
        
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpListener.ReceiveAsync(ct);
                ProcessDiscoveryPacket(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ UDP receive error: {ex.Message}");
            }
        }
    }
    
    private async Task StartUdpBroadcasterAsync(string nodeId, CancellationToken ct)
    {
        _udpBroadcaster = new UdpClient();
        _udpBroadcaster.EnableBroadcast = true;
        
        var announcement = new NodeAnnouncement
        {
            Magic = MagicHeader,
            NodeId = nodeId,
            Role = "GENERAL",
            HasInternet = await InternetGateway.HasInternetAccessAsync(),
            Port = 5001,
            Version = "1.0.0"
        };
        
        var json = JsonSerializer.Serialize(announcement);
        var data = Encoding.UTF8.GetBytes(json);
        
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                // Broadcast to all network interfaces
                foreach (var broadcast in GetBroadcastAddresses())
                {
                    var endpoint = new IPEndPoint(broadcast, DiscoveryPort);
                    await _udpBroadcaster.SendAsync(data, data.Length, endpoint);
                }
                
                // Update internet status periodically
                announcement.HasInternet = await InternetGateway.HasInternetAccessAsync();
                
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ UDP broadcast error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }
    
    private void ProcessDiscoveryPacket(byte[] data, IPEndPoint sender)
    {
        try
        {
            var json = Encoding.UTF8.GetString(data);
            var announcement = JsonSerializer.Deserialize<NodeAnnouncement>(json);
            
            if (announcement?.Magic != MagicHeader) return;
            
            var node = new DiscoveredNode
            {
                NodeId = announcement.NodeId,
                Address = sender.Address.ToString(),
                Port = announcement.Port,
                Role = announcement.Role,
                HasInternet = announcement.HasInternet,
                Version = announcement.Version,
                LastSeen = DateTime.UtcNow
            };
            
            var isNew = !_discoveredNodes.ContainsKey(node.NodeId);
            _discoveredNodes[node.NodeId] = node;
            
            if (isNew)
            {
                Console.WriteLine($"◈ Discovered node: {node.NodeId} @ {node.Address} (Internet: {node.HasInternet})");
                OnNodeDiscovered?.Invoke(node);
            }
        }
        catch (JsonException)
        {
            // Invalid packet, ignore
        }
    }
    
    private List<IPAddress> GetBroadcastAddresses()
    {
        var broadcasts = new List<IPAddress>();
        
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            
            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                
                // Calculate broadcast address
                var ip = addr.Address.GetAddressBytes();
                var mask = addr.IPv4Mask.GetAddressBytes();
                var broadcast = new byte[4];
                
                for (int i = 0; i < 4; i++)
                {
                    broadcast[i] = (byte)(ip[i] | ~mask[i]);
                }
                
                broadcasts.Add(new IPAddress(broadcast));
            }
        }
        
        return broadcasts;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          mDNS / BONJOUR DISCOVERY
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartMdnsDiscoveryAsync(CancellationToken ct)
    {
        // mDNS uses multicast on 224.0.0.251:5353
        // Service type: _nframe._tcp.local
        
        try
        {
            using var mdnsClient = new UdpClient();
            mdnsClient.JoinMulticastGroup(IPAddress.Parse("224.0.0.251"));
            
            Console.WriteLine("◎ mDNS discovery started");
            
            // Send mDNS query for _nframe._tcp.local
            var query = BuildMdnsQuery("_nframe._tcp.local");
            var endpoint = new IPEndPoint(IPAddress.Parse("224.0.0.251"), 5353);
            
            while (_isRunning && !ct.IsCancellationRequested)
            {
                await mdnsClient.SendAsync(query, query.Length, endpoint);
                await Task.Delay(TimeSpan.FromMinutes(1), ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ mDNS error: {ex.Message}");
        }
    }
    
    private byte[] BuildMdnsQuery(string serviceName)
    {
        // Simplified mDNS query builder
        var parts = serviceName.Split('.');
        var data = new List<byte>();
        
        // Transaction ID
        data.Add(0x00); data.Add(0x00);
        // Flags (standard query)
        data.Add(0x00); data.Add(0x00);
        // Questions: 1
        data.Add(0x00); data.Add(0x01);
        // Answers, Authority, Additional: 0
        data.Add(0x00); data.Add(0x00);
        data.Add(0x00); data.Add(0x00);
        data.Add(0x00); data.Add(0x00);
        
        // Question
        foreach (var part in parts)
        {
            data.Add((byte)part.Length);
            data.AddRange(Encoding.ASCII.GetBytes(part));
        }
        data.Add(0x00);
        
        // Type PTR (12)
        data.Add(0x00); data.Add(0x0c);
        // Class IN (1)
        data.Add(0x00); data.Add(0x01);
        
        return data.ToArray();
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          CLEANUP
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartCleanupTaskAsync(CancellationToken ct)
    {
        while (_isRunning && !ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), ct);
            
            var staleThreshold = DateTime.UtcNow.AddMinutes(-2);
            var staleNodes = _discoveredNodes
                .Where(kvp => kvp.Value.LastSeen < staleThreshold)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var nodeId in staleNodes)
            {
                if (_discoveredNodes.TryRemove(nodeId, out _))
                {
                    Console.WriteLine($"∴ Lost node: {nodeId}");
                    OnNodeLost?.Invoke(nodeId);
                }
            }
        }
    }
    
    /// <summary>
    /// Finds the best path to internet through discovered nodes.
    /// </summary>
    public DiscoveredNode? FindInternetGateway()
    {
        return _discoveredNodes.Values
            .Where(n => n.HasInternet)
            .OrderByDescending(n => n.LastSeen)
            .FirstOrDefault();
    }
}

public class NodeAnnouncement
{
    public string Magic { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string Role { get; set; } = "";
    public bool HasInternet { get; set; }
    public int Port { get; set; }
    public string Version { get; set; } = "";
}

public class DiscoveredNode
{
    public string NodeId { get; init; } = "";
    public string Address { get; init; } = "";
    public int Port { get; init; }
    public string Role { get; init; } = "";
    public bool HasInternet { get; init; }
    public string Version { get; init; } = "";
    public DateTime LastSeen { get; set; }
}
