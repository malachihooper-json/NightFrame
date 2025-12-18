/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    INTERNET GATEWAY - BANDWIDTH SHARING                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  NAT/Proxy for sharing internet with mesh clients.                         ║
 * ║  Provides immediate internet access - no install required for consumers.   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace NIGHTFRAME.Drone.Network;

public class InternetGateway
{
    private readonly ConcurrentDictionary<string, ClientSession> _clients = new();
    private readonly SharingConfigManager _sharingConfig;
    private readonly SessionLogger _sessionLogger;
    private TcpListener? _tcpListener;
    private UdpClient? _dnsForwarder;
    private bool _isRunning = false;
    
    // Configurable settings (adjustable via web console)
    public int GuestBandwidthKbps { get; set; } = 512;
    public int MemberBandwidthKbps { get; set; } = 0; // 0 = unlimited
    public int MaxConcurrentClients { get; set; } = 50;
    
    /// <summary>
    /// Creates an internet gateway with safety features.
    /// </summary>
    public InternetGateway(SharingConfigManager? sharingConfig = null, SessionLogger? sessionLogger = null)
    {
        _sharingConfig = sharingConfig ?? new SharingConfigManager();
        _sessionLogger = sessionLogger ?? new SessionLogger();
    }
    
    /// <summary>
    /// Checks if this device has internet connectivity.
    /// </summary>
    public static async Task<bool> HasInternetAccessAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://clients3.google.com/generate_204");
            return response.StatusCode == HttpStatusCode.NoContent;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Starts the internet gateway services.
    /// </summary>
    public async Task StartAsync(int proxyPort = 8080, int dnsPort = 53, CancellationToken ct = default)
    {
        if (_isRunning) return;
        
        // Check sharing configuration
        var config = _sharingConfig.GetConfig();
        if (!config.CanShare)
        {
            if (!config.ConsentGranted)
            {
                Console.WriteLine("∴ Consent not granted - run consent flow first");
                return;
            }
            if (config.LimitReached)
            {
                Console.WriteLine($"∴ Monthly sharing limit reached ({config.MonthlyBandwidthLimitGB} GB)");
                return;
            }
            if (!config.IsEnabled)
            {
                Console.WriteLine("∴ Sharing is disabled");
                return;
            }
        }
        
        Console.WriteLine("◈ Starting Internet Gateway...");
        Console.WriteLine($"  Sharing limit: {config.CurrentMonthUsageGB:F2} / {config.MonthlyBandwidthLimitGB:F0} GB");
        
        // Check if we have internet
        if (!await HasInternetAccessAsync())
        {
            Console.WriteLine("∴ No internet access - cannot act as gateway");
            return;
        }
        
        _isRunning = true;
        
        // Start DNS forwarder (responds to DNS queries from clients)
        _ = StartDnsForwarderAsync(dnsPort, ct);
        
        // Start HTTP/HTTPS proxy
        _ = StartProxyAsync(proxyPort, ct);
        
        Console.WriteLine($"◈ Internet Gateway active on port {proxyPort}");
    }
    
    /// <summary>
    /// Stops the gateway.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _tcpListener?.Stop();
        _dnsForwarder?.Close();
        Console.WriteLine("◎ Internet Gateway stopped");
    }
    
    /// <summary>
    /// Registers a client and returns their session.
    /// </summary>
    public ClientSession GetOrCreateSession(string clientId, bool isMeshMember = false)
    {
        return _clients.GetOrAdd(clientId, id => new ClientSession
        {
            ClientId = id,
            IsMeshMember = isMeshMember,
            BandwidthLimitKbps = isMeshMember ? MemberBandwidthKbps : GuestBandwidthKbps,
            ConnectedAt = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Upgrades a guest to mesh member (unlimited bandwidth).
    /// </summary>
    public void UpgradeToMember(string clientId)
    {
        if (_clients.TryGetValue(clientId, out var session))
        {
            session.IsMeshMember = true;
            session.BandwidthLimitKbps = MemberBandwidthKbps;
            Console.WriteLine($"◈ Client {clientId} upgraded to mesh member");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          DNS FORWARDER
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartDnsForwarderAsync(int port, CancellationToken ct)
    {
        try
        {
            _dnsForwarder = new UdpClient(port);
            Console.WriteLine($"◎ DNS forwarder started on port {port}");
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"∴ Could not start DNS forwarder: {ex.Message}");
            return;
        }
        
        // Use Google DNS as upstream
        var upstreamDns = new IPEndPoint(IPAddress.Parse("8.8.8.8"), 53);
        
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                var result = await _dnsForwarder.ReceiveAsync(ct);
                
                // Forward to upstream DNS
                using var forwarder = new UdpClient();
                await forwarder.SendAsync(result.Buffer, result.Buffer.Length, upstreamDns);
                
                var response = await forwarder.ReceiveAsync(ct);
                
                // Send response back to client
                await _dnsForwarder.SendAsync(response.Buffer, response.Buffer.Length, result.RemoteEndPoint);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ DNS error: {ex.Message}");
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HTTP/HTTPS PROXY
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartProxyAsync(int port, CancellationToken ct)
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Any, port);
            _tcpListener.Start();
            Console.WriteLine($"◎ HTTP proxy started on port {port}");
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"∴ Could not start proxy: {ex.Message}");
            return;
        }
        
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                var client = await _tcpListener.AcceptTcpClientAsync(ct);
                _ = HandleClientAsync(client, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ Proxy accept error: {ex.Message}");
            }
        }
    }
    
    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        var session = GetOrCreateSession(clientEndpoint);
        
        // Log guest session for liability protection
        var guestSession = _sessionLogger.GetActiveSession(clientEndpoint) 
            ?? _sessionLogger.StartSession(clientEndpoint, client.Client.RemoteEndPoint?.ToString());
        
        try
        {
            using (client)
            await using (var clientStream = client.GetStream())
            {
                // Check if we've hit the sharing limit mid-session
                var config = _sharingConfig.GetConfig();
                if (config.LimitReached)
                {
                    Console.WriteLine($"◎ Sharing limit reached, closing connection to {clientEndpoint}");
                    return;
                }
                
                // Read the HTTP request
                var buffer = new byte[8192];
                var bytesRead = await clientStream.ReadAsync(buffer, ct);
                
                if (bytesRead == 0) return;
                
                var request = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                var lines = request.Split('\n');
                
                if (lines.Length == 0) return;
                
                var firstLine = lines[0].Trim();
                var parts = firstLine.Split(' ');
                
                if (parts.Length < 2) return;
                
                var method = parts[0];
                var target = parts[1];
                
                if (method == "CONNECT")
                {
                    // HTTPS tunnel
                    await HandleConnectAsync(clientStream, target, session, ct);
                }
                else
                {
                    // HTTP proxy
                    await HandleHttpAsync(clientStream, request, session, ct);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Client error ({clientEndpoint}): {ex.Message}");
        }
        finally
        {
            // Record bandwidth to both session tracking systems
            var bytesUsed = session.CurrentRequestBytes;
            session.BytesTransferred += bytesUsed;
            session.CurrentRequestBytes = 0;
            
            // Only count non-member traffic against sharing limit
            if (!session.IsMeshMember && bytesUsed > 0)
            {
                _sharingConfig.RecordUsage(bytesUsed);
                _sessionLogger.RecordBandwidth(clientEndpoint, bytesUsed, 0);
                
                // Check if we just hit the limit
                var config = _sharingConfig.GetConfig();
                if (config.LimitReached && _isRunning)
                {
                    Console.WriteLine($"◎ Monthly sharing limit of {config.MonthlyBandwidthLimitGB} GB reached!");
                    Console.WriteLine("◎ Gateway will continue for existing sessions but new guests will be rejected.");
                }
            }
        }
    }
    
    private async Task HandleConnectAsync(NetworkStream clientStream, string target, ClientSession session, CancellationToken ct)
    {
        // Parse host:port
        var hostPort = target.Split(':');
        var host = hostPort[0];
        var port = hostPort.Length > 1 ? int.Parse(hostPort[1]) : 443;
        
        try
        {
            // Connect to remote server
            using var remote = new TcpClient();
            await remote.ConnectAsync(host, port, ct);
            
            // Send 200 Connection Established
            var response = "HTTP/1.1 200 Connection Established\r\n\r\n"u8.ToArray();
            await clientStream.WriteAsync(response, ct);
            
            // Tunnel data bidirectionally
            await using var remoteStream = remote.GetStream();
            
            var clientToRemote = CopyWithThrottleAsync(clientStream, remoteStream, session, ct);
            var remoteToClient = CopyWithThrottleAsync(remoteStream, clientStream, session, ct);
            
            await Task.WhenAny(clientToRemote, remoteToClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ CONNECT error to {target}: {ex.Message}");
        }
    }
    
    private async Task HandleHttpAsync(NetworkStream clientStream, string request, ClientSession session, CancellationToken ct)
    {
        // Extract URL from request
        var lines = request.Split('\n');
        var firstLine = lines[0].Trim();
        var parts = firstLine.Split(' ');
        
        if (parts.Length < 2) return;
        
        var url = parts[1];
        
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                // Relative URL, need Host header
                var hostLine = lines.FirstOrDefault(l => l.StartsWith("Host:", StringComparison.OrdinalIgnoreCase));
                if (hostLine != null)
                {
                    var host = hostLine.Split(':')[1].Trim();
                    uri = new Uri($"http://{host}{url}");
                }
                else return;
            }
            
            // Forward request
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(uri, ct);
            
            // Build response
            var statusLine = $"HTTP/1.1 {(int)response.StatusCode} {response.ReasonPhrase}\r\n";
            await clientStream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(statusLine), ct);
            
            foreach (var header in response.Headers)
            {
                var headerLine = $"{header.Key}: {string.Join(",", header.Value)}\r\n";
                await clientStream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(headerLine), ct);
            }
            
            await clientStream.WriteAsync("\r\n"u8.ToArray(), ct);
            
            // Copy body with throttling
            await using var responseStream = await response.Content.ReadAsStreamAsync(ct);
            await CopyWithThrottleAsync(responseStream, clientStream, session, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ HTTP proxy error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Copies data with bandwidth throttling.
    /// </summary>
    private async Task CopyWithThrottleAsync(Stream source, Stream destination, ClientSession session, CancellationToken ct)
    {
        var buffer = new byte[8192];
        var bytesPerSecond = session.BandwidthLimitKbps * 1024 / 8;
        var startTime = DateTime.UtcNow;
        long totalBytes = 0;
        
        while (true)
        {
            var bytesRead = await source.ReadAsync(buffer, ct);
            if (bytesRead == 0) break;
            
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalBytes += bytesRead;
            session.CurrentRequestBytes += bytesRead;
            
            // Apply throttling if bandwidth limit is set
            if (bytesPerSecond > 0)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                var expectedBytes = bytesPerSecond * elapsed;
                
                if (totalBytes > expectedBytes)
                {
                    var waitTime = (totalBytes - expectedBytes) / bytesPerSecond;
                    await Task.Delay(TimeSpan.FromSeconds(waitTime), ct);
                }
            }
        }
    }
    
    /// <summary>
    /// Gets current gateway statistics.
    /// </summary>
    public GatewayStats GetStats()
    {
        return new GatewayStats
        {
            IsRunning = _isRunning,
            ConnectedClients = _clients.Count,
            MeshMembers = _clients.Values.Count(c => c.IsMeshMember),
            Guests = _clients.Values.Count(c => !c.IsMeshMember),
            TotalBytesServed = _clients.Values.Sum(c => c.BytesTransferred),
            GuestBandwidthKbps = GuestBandwidthKbps
        };
    }
}

public class ClientSession
{
    public required string ClientId { get; init; }
    public bool IsMeshMember { get; set; }
    public int BandwidthLimitKbps { get; set; }
    public DateTime ConnectedAt { get; init; }
    public long BytesTransferred { get; set; }
    public long CurrentRequestBytes { get; set; }
}

public class GatewayStats
{
    public bool IsRunning { get; init; }
    public int ConnectedClients { get; init; }
    public int MeshMembers { get; init; }
    public int Guests { get; init; }
    public long TotalBytesServed { get; init; }
    public int GuestBandwidthKbps { get; init; }
}
