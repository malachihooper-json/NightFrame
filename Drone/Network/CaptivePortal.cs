/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    PERSISTENT CAPTIVE PORTAL                               ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Wi-Fi hotspot with IMMEDIATE internet access.                             ║
 * ║  No disconnection after portal. Platform-specific install flows.           ║
 * ║  iOS users get internet without any install.                               ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace NIGHTFRAME.Drone.Network;

public class PersistentCaptivePortal
{
    private readonly InternetGateway _gateway;
    private HttpListener? _httpListener;
    private UdpClient? _dnsServer;
    private bool _isRunning = false;
    private readonly string _ssid;
    private readonly int _httpPort;
    private readonly int _dnsPort;
    private readonly string _localIp;
    
    // Track which clients have seen the portal
    private readonly HashSet<string> _portalShown = new();
    
    public PersistentCaptivePortal(
        InternetGateway gateway,
        string ssid = "NFRAME Global Internet", 
        int httpPort = 80, 
        int dnsPort = 53)
    {
        _gateway = gateway;
        _ssid = ssid;
        _httpPort = httpPort;
        _dnsPort = dnsPort;
        _localIp = GetLocalIpAddress();
    }
    
    /// <summary>
    /// Starts the persistent captive portal.
    /// </summary>
    public async Task StartAsync(CancellationToken ct)
    {
        if (_isRunning) return;
        
        Console.WriteLine("◈ Starting Persistent Captive Portal...");
        
        try
        {
            // Start Wi-Fi access point (open network)
            await StartAccessPointAsync();
            
            // Start DNS server
            _ = StartDnsServerAsync(ct);
            
            // Start HTTP server
            _ = StartHttpServerAsync(ct);
            
            // Start internet gateway
            await _gateway.StartAsync(8080, 5353, ct);
            
            _isRunning = true;
            Console.WriteLine($"◈ Portal active: {_ssid}");
            Console.WriteLine($"◈ Clients get IMMEDIATE internet access");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Failed to start portal: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Stops the portal.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        _httpListener?.Stop();
        _dnsServer?.Close();
        _gateway.Stop();
        
        await StopAccessPointAsync();
        
        Console.WriteLine("◎ Captive portal stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          WI-FI ACCESS POINT
    // ═══════════════════════════════════════════════════════════════════════════
    
    private AdaptiveWiFiHotspot? _adaptiveHotspot;
    private SelfHealingEngine? _selfHealing;
    private WiFiDirectSoftAP? _wifiDirectAP;
    private UniversalSSIDBroadcaster? _universalBroadcaster;
    private const string PORTAL_IP = "192.168.137.1";
    
    private async Task StartAccessPointAsync()
    {
        Console.WriteLine("◎ Starting Platform-Aware SSID Broadcaster...");
        
        // Use UniversalSSIDBroadcaster - evaluates platform and uses optimal method
        _universalBroadcaster = await UniversalSSIDBroadcaster.CreateAsync();
        
        var success = await _universalBroadcaster.StartAsync();
        
        if (success)
        {
            var config = _universalBroadcaster.Config;
            Console.WriteLine($"◈ SSID Broadcasting: {config.SSID}");
            
            if (config.IsOpenNetwork)
            {
                Console.WriteLine("◈ OPEN NETWORK - Zero friction, no password!");
            }
            else
            {
                Console.WriteLine($"◈ Password embedded in SSID name (00000000)");
            }
            
            // Configure IP on the network interface
            await ConfigureWiFiDirectIPAsync();
        }
        else
        {
            Console.WriteLine("⚠️  Platform broadcaster failed, trying WiFi Direct directly...");
            
            // Fallback to WiFi Direct with password
            var ssid = SSIDConfig.PASSWORD_SSID; // "NFRAME Global Internet pw:00000000"
            _wifiDirectAP = new WiFiDirectSoftAP(ssid, SSIDConfig.STANDARD_PASSWORD);
            
            var wifiDirectSuccess = await _wifiDirectAP.StartAsync();
            
            if (wifiDirectSuccess)
            {
                Console.WriteLine($"◈ WiFi Direct Started: {ssid}");
                await ConfigureWiFiDirectIPAsync();
            }
            else
            {
                Console.WriteLine("⚠️  All SSID methods failed.");
                Console.WriteLine("    Try: Windows Settings → Network → Mobile Hotspot → Turn On");
                Console.WriteLine($"    Set SSID to: {SSIDConfig.PASSWORD_SSID}");
            }
        }
    }
    
    private async Task ConfigureWiFiDirectIPAsync()
    {
        Console.WriteLine("◎ Configuring network interface...");
        
        try
        {
            // Find the WiFi Direct adapter
            var output = await RunCommandAsync("powershell", 
                "-Command \"(Get-NetAdapter | Where-Object { $_.InterfaceDescription -like '*Wi-Fi Direct*' -or $_.Name -like '*Local Area Connection*' } | Select-Object -First 1).Name\"");
            
            var adapterName = output.Trim();
            
            if (!string.IsNullOrEmpty(adapterName))
            {
                Console.WriteLine($"  Found adapter: {adapterName}");
                
                // Set static IP
                await RunCommandAsync("netsh", 
                    $"interface ip set address \"{adapterName}\" static {PORTAL_IP} 255.255.255.0");
                
                Console.WriteLine($"  Assigned IP: {PORTAL_IP}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  IP configuration warning: {ex.Message}");
        }
    }
    
    private async Task StopAccessPointAsync()
    {
        if (_universalBroadcaster != null)
        {
            await _universalBroadcaster.StopAsync();
        }
        
        if (_wifiDirectAP != null)
        {
            await _wifiDirectAP.StopAsync();
        }
        
        if (_adaptiveHotspot != null)
        {
            await _adaptiveHotspot.StopAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await RunCommandAsync("netsh", "wlan stop hostednetwork");
        }
        else
        {
            await RunCommandAsync("sudo", "systemctl stop hostapd");
        }
    }
    
    private async Task StartLinuxAccessPointAsync()
    {
        var config = $@"
interface=wlan0
driver=nl80211
ssid={_ssid}
hw_mode=g
channel=7
wmm_enabled=0
macaddr_acl=0
auth_algs=1
ignore_broadcast_ssid=0
# Open network - no WPA
";
        await File.WriteAllTextAsync("/tmp/hostapd.conf", config);
        await RunCommandAsync("sudo", "hostapd -B /tmp/hostapd.conf");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          DNS SERVER
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartDnsServerAsync(CancellationToken ct)
    {
        Console.WriteLine($"◎ Starting DNS server on port {_dnsPort}...");
        
        try
        {
            _dnsServer = new UdpClient(_dnsPort);
        }
        catch (SocketException)
        {
            Console.WriteLine($"∴ Could not bind DNS port {_dnsPort}");
            return;
        }
        
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                var result = await _dnsServer.ReceiveAsync(ct);
                var clientKey = result.RemoteEndPoint.Address.ToString();
                
                // Check if this client has seen the captive portal
                if (!_portalShown.Contains(clientKey))
                {
                    // First DNS query - redirect to our portal
                    // This triggers the captive portal detection
                    var captiveResponse = CreateDnsResponse(result.Buffer, _localIp);
                    await _dnsServer.SendAsync(captiveResponse, captiveResponse.Length, result.RemoteEndPoint);
                }
                else
                {
                    // Client has seen portal - forward DNS normally
                    var response = await ForwardDnsQueryAsync(result.Buffer);
                    await _dnsServer.SendAsync(response, response.Length, result.RemoteEndPoint);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ DNS error: {ex.Message}");
            }
        }
    }
    
    private async Task<byte[]> ForwardDnsQueryAsync(byte[] query)
    {
        try
        {
            using var forwarder = new UdpClient();
            await forwarder.SendAsync(query, query.Length, "8.8.8.8", 53);
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var result = await forwarder.ReceiveAsync(cts.Token);
            return result.Buffer;
        }
        catch
        {
            // Return NXDOMAIN on failure
            return query;
        }
    }
    
    private byte[] CreateDnsResponse(byte[] query, string redirectIp)
    {
        var response = new List<byte>();
        
        // Transaction ID
        response.Add(query[0]);
        response.Add(query[1]);
        
        // Flags: Standard response
        response.Add(0x81);
        response.Add(0x80);
        
        // Questions: 1, Answers: 1
        response.Add(0x00); response.Add(0x01);
        response.Add(0x00); response.Add(0x01);
        response.Add(0x00); response.Add(0x00);
        response.Add(0x00); response.Add(0x00);
        
        // Copy question
        int questionEnd = 12;
        while (query[questionEnd] != 0) questionEnd++;
        questionEnd += 5;
        
        for (int i = 12; i < questionEnd; i++)
            response.Add(query[i]);
        
        // Answer
        response.Add(0xc0); response.Add(0x0c);
        response.Add(0x00); response.Add(0x01);
        response.Add(0x00); response.Add(0x01);
        response.Add(0x00); response.Add(0x00);
        response.Add(0x00); response.Add(0x3c);
        response.Add(0x00); response.Add(0x04);
        
        foreach (var part in redirectIp.Split('.'))
            response.Add(byte.Parse(part));
        
        return response.ToArray();
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HTTP SERVER
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task StartHttpServerAsync(CancellationToken ct)
    {
        Console.WriteLine($"◎ Starting HTTP server on port {_httpPort}...");
        
        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://+:{_httpPort}/");
            _httpListener.Start();
        }
        catch (HttpListenerException)
        {
            Console.WriteLine($"∴ Could not bind HTTP port {_httpPort}");
            return;
        }
        
        while (!ct.IsCancellationRequested && _isRunning)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                _ = HandleHttpRequestAsync(context);
            }
            catch (HttpListenerException) { break; }
            catch (ObjectDisposedException) { break; }
        }
    }
    
    private async Task HandleHttpRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var clientIp = request.RemoteEndPoint.Address.ToString();
        var path = request.Url?.AbsolutePath ?? "/";
        
        Console.WriteLine($"◎ HTTP: {clientIp} -> {path}");
        
        // Detect platform from User-Agent
        var userAgent = request.UserAgent ?? "";
        var platform = DetectPlatform(userAgent);
        
        // Mark client as having seen portal
        _portalShown.Add(clientIp);
        
        // Route requests
        if (path.StartsWith("/assets/"))
        {
            await ServeAssetAsync(response, path);
            return;
        }
        
        if (path == "/download" || path.StartsWith("/download/"))
        {
            await ServeDownloadAsync(response, platform);
            return;
        }
        
        if (path == "/terms" || path == "/privacy")
        {
            await ServeHtmlAsync(response, PortalPages.GetTermsPageHtml());
            return;
        }
        
        if (path == "/success" || path == "/generate_204")
        {
            // Check if coming as guest
            var isGuest = request.QueryString["guest"] == "true";
            
            if (path == "/generate_204")
            {
                // Android captive portal check - return 204
                response.StatusCode = 204;
                response.Close();
                return;
            }
            
            await ServeHtmlAsync(response, PortalPages.GetSuccessPageHtml(isGuest));
            
            // Register client
            _gateway.GetOrCreateSession(clientIp, isMeshMember: !isGuest);
            return;
        }
        
        // Settings page - view/modify resource donations
        if (path == "/settings")
        {
            await ServeSettingsPageAsync(response);
            return;
        }
        
        if (path == "/settings/save" && request.HttpMethod == "POST")
        {
            await HandleSettingsSaveAsync(request, response);
            return;
        }
        
        if (path == "/settings/revoke")
        {
            await HandleConsentRevokeAsync(response);
            return;
        }
        
        // Serve the main portal page
        await ServePortalPageAsync(response, platform, clientIp);
    }
    
    private async Task ServeSettingsPageAsync(HttpListenerResponse response)
    {
        var configManager = new SharingConfigManager();
        var config = configManager.GetConfig();
        
        var html = PortalPages.GetSettingsPageHtml(
            currentBandwidthLimit: config.MonthlyBandwidthLimitGB,
            usedBandwidth: config.CurrentMonthUsageGB,
            cpuLimit: ResourceLimits.MaxCpuPercentage, // TODO: Add to config
            ramLimit: ResourceLimits.MaxRamMB,
            gpuLimit: ResourceLimits.MaxGpuPercentage,
            consentGranted: config.ConsentGranted,
            consentDate: config.ConsentTimestamp
        );
        
        await ServeHtmlAsync(response, html);
    }
    
    private async Task HandleSettingsSaveAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            // Read form data
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var formData = System.Web.HttpUtility.ParseQueryString(body);
            
            var configManager = new SharingConfigManager();
            var config = configManager.GetConfig();
            
            // Update settings (validate against hard caps)
            if (double.TryParse(formData["bandwidthLimit"], out var bwLimit))
            {
                config.MonthlyBandwidthLimitGB = Math.Min(bwLimit, ResourceLimits.DefaultMonthlyBandwidthGB);
            }
            
            configManager.SaveConfig();
            
            // Redirect back to settings
            response.StatusCode = 302;
            response.Headers["Location"] = "/settings";
            response.Close();
        }
        catch
        {
            response.StatusCode = 500;
            response.Close();
        }
    }
    
    private async Task HandleConsentRevokeAsync(HttpListenerResponse response)
    {
        var configManager = new SharingConfigManager();
        var config = configManager.GetConfig();
        
        config.ConsentGranted = false;
        config.IsEnabled = false;
        config.Acknowledgments = new ConsentAcknowledgments();
        configManager.SaveConfig();
        
        // Redirect back to settings
        response.StatusCode = 302;
        response.Headers["Location"] = "/settings";
        response.Close();
        await Task.CompletedTask;
    }
    
    private Platform DetectPlatform(string userAgent)
    {
        if (userAgent.Contains("iPhone") || userAgent.Contains("iPad"))
            return Platform.iOS;
        if (userAgent.Contains("Android"))
            return Platform.Android;
        if (userAgent.Contains("Mac OS"))
            return Platform.macOS;
        if (userAgent.Contains("Linux"))
            return Platform.Linux;
        return Platform.Windows;
    }
    
    private async Task ServePortalPageAsync(HttpListenerResponse response, Platform platform, string clientIp)
    {
        // Get full user agent for enhanced detection
        var html = GetPortalHtmlWithCapabilityCheck(platform);
        await ServeHtmlAsync(response, html);
        
        // Register client as guest (throttled internet)
        _gateway.GetOrCreateSession(clientIp, isMeshMember: false);
    }
    
    /// <summary>
    /// Gets appropriate portal HTML based on platform capability.
    /// </summary>
    private string GetPortalHtmlWithCapabilityCheck(Platform platform)
    {
        // Convert Platform enum to string for capability check
        var platformName = platform switch
        {
            Platform.iOS => "iOS",
            Platform.Android => "Android",
            Platform.macOS => "macOS",
            Platform.Linux => "Linux",
            Platform.Windows => "Windows",
            _ => "Unknown"
        };
        
        // Check if this platform can broadcast SSID
        var capability = OnboardingCapability.DetectFromUserAgent(platformName);
        
        if (capability.RecommendedPath == OnboardingPath.GuestOnly)
        {
            // Show guest-only page with explanation
            return PortalPages.GetGuestOnlyPageHtml(
                capability.Platform, 
                capability.GuestOnlyReason
            );
        }
        else if (capability.RecommendedPath == OnboardingPath.Unknown)
        {
            // Show unknown platform page with selection
            return PortalPages.GetUnknownPlatformPageHtml();
        }
        
        // Full member path - show standard platform-specific page
        return GetPortalHtml(platform);
    }
    
    private async Task ServeHtmlAsync(HttpListenerResponse response, string html)
    {
        response.ContentType = "text/html; charset=utf-8";
        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }
    
    private async Task ServeAssetAsync(HttpListenerResponse response, string path)
    {
        // Map path to local asset
        var assetName = path.Replace("/assets/", "");
        var assetPath = Path.Combine(AppContext.BaseDirectory, "Assets", assetName);
        
        if (!File.Exists(assetPath))
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }
        
        // Determine content type
        response.ContentType = assetName.EndsWith(".png") ? "image/png" :
                              assetName.EndsWith(".jpg") ? "image/jpeg" :
                              assetName.EndsWith(".svg") ? "image/svg+xml" :
                              assetName.EndsWith(".css") ? "text/css" :
                              assetName.EndsWith(".js") ? "application/javascript" :
                              "application/octet-stream";
        
        var bytes = await File.ReadAllBytesAsync(assetPath);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
    
    private async Task ServeDownloadAsync(HttpListenerResponse response, Platform platform)
    {
        var binaryPath = platform switch
        {
            Platform.Windows => Path.Combine(AppContext.BaseDirectory, "brain.exe"),
            Platform.macOS => Path.Combine(AppContext.BaseDirectory, "brain-osx"),
            Platform.Linux => Path.Combine(AppContext.BaseDirectory, "brain-linux"),
            Platform.Android => Path.Combine(AppContext.BaseDirectory, "scout.apk"),
            _ => null
        };
        
        if (binaryPath == null || !File.Exists(binaryPath))
        {
            response.StatusCode = 404;
            response.Close();
            return;
        }
        
        var filename = Path.GetFileName(binaryPath);
        response.ContentType = "application/octet-stream";
        response.AddHeader("Content-Disposition", $"attachment; filename=\"{filename}\"");
        
        var bytes = await File.ReadAllBytesAsync(binaryPath);
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }
    
    private string GetPortalHtml(Platform platform) => platform switch
    {
        Platform.iOS => PortalPages.GetIOSPortalHtml(),
        Platform.Android => PortalPages.GetAndroidPortalHtml(),
        Platform.macOS => PortalPages.GetMacOSPortalHtml(),
        Platform.Linux => PortalPages.GetLinuxPortalHtml(),
        _ => PortalPages.GetWindowsPortalHtml()
    };
    
    private string GetIosPortalHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Connected</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            background: linear-gradient(135deg, #0a0a0f 0%, #1a1a24 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            color: #f8fafc;
        }
        .container {
            text-align: center;
            max-width: 400px;
            padding: 48px;
        }
        .icon { font-size: 80px; margin-bottom: 24px; }
        h1 { font-size: 24px; margin-bottom: 16px; color: #7c3aed; }
        p { color: #94a3b8; line-height: 1.6; margin-bottom: 24px; }
        .status { 
            background: rgba(34, 197, 94, 0.2);
            border: 1px solid #22c55e;
            padding: 16px;
            border-radius: 12px;
            color: #22c55e;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="icon">🌐</div>
        <h1>You're Connected!</h1>
        <p>You now have free internet access via the NFRAME Global Network.</p>
        <div class="status">
            ✓ Internet Active<br>
            No installation required
        </div>
    </div>
    <script>
        // Trigger captive portal success
        setTimeout(() => { window.location.href = '/success'; }, 3000);
    </script>
</body>
</html>
""";
    
    private string GetAndroidPortalHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            background: linear-gradient(135deg, #0a0a0f 0%, #1a1a24 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: 'Roboto', sans-serif;
            color: #f8fafc;
        }
        .container { text-align: center; max-width: 400px; padding: 32px; }
        .icon { font-size: 64px; margin-bottom: 24px; }
        h1 { font-size: 24px; margin-bottom: 12px; color: #7c3aed; }
        p { color: #94a3b8; line-height: 1.6; margin-bottom: 24px; font-size: 14px; }
        .btn {
            display: block;
            background: linear-gradient(135deg, #7c3aed 0%, #06b6d4 100%);
            color: white;
            text-decoration: none;
            padding: 16px 32px;
            border-radius: 12px;
            font-weight: 600;
            margin-bottom: 16px;
        }
        .btn-secondary {
            display: block;
            background: transparent;
            border: 1px solid #374151;
            color: #9ca3af;
            text-decoration: none;
            padding: 12px 24px;
            border-radius: 8px;
        }
        .note { font-size: 12px; color: #6b7280; margin-top: 16px; }
    </style>
</head>
<body>
    <div class="container">
        <div class="icon">◈</div>
        <h1>NFRAME Global Network</h1>
        <p>Free unlimited internet. Install the app to get full speed and help the network grow.</p>
        <a href="/download/drone.apk" class="btn">Install for Full Speed</a>
        <a href="/success" class="btn-secondary">Continue with Limited Speed</a>
        <p class="note">Limited: 512 Kbps | Full: Unlimited</p>
    </div>
</body>
</html>
""";
    
    private string GetDesktopPortalHtml() => """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>NFRAME Network</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            background: linear-gradient(135deg, #0a0a0f 0%, #1a1a24 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
            color: #f8fafc;
        }
        .container { text-align: center; max-width: 500px; padding: 48px; }
        .logo { font-size: 80px; margin-bottom: 24px; animation: pulse 2s infinite; }
        h1 { font-size: 32px; margin-bottom: 16px; background: linear-gradient(135deg, #7c3aed, #06b6d4); -webkit-background-clip: text; -webkit-text-fill-color: transparent; }
        p { color: #94a3b8; line-height: 1.6; margin-bottom: 32px; }
        .btn {
            display: inline-block;
            background: linear-gradient(135deg, #7c3aed 0%, #06b6d4 100%);
            color: white;
            text-decoration: none;
            padding: 16px 48px;
            border-radius: 12px;
            font-weight: 600;
            font-size: 18px;
            margin-bottom: 16px;
        }
        .btn-secondary {
            display: block;
            margin-top: 16px;
            color: #6b7280;
            text-decoration: none;
        }
        @keyframes pulse { 0%, 100% { opacity: 1; } 50% { opacity: 0.7; } }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo">◈</div>
        <h1>NFRAME Global Network</h1>
        <p>Free unlimited internet, powered by the decentralized mesh. Install to get full speed and contribute compute.</p>
        <a href="/download" class="btn">Download & Install</a>
        <a href="/success" class="btn-secondary">Continue with Limited Speed (512 Kbps)</a>
    </div>
    <script>
        // Auto-detect platform
        var ua = navigator.userAgent;
        var link = document.querySelector('.btn');
        if (ua.includes('Mac')) link.href = '/download/brain-osx';
        else if (ua.includes('Linux')) link.href = '/download/brain-linux';
    </script>
</body>
</html>
""";
    
    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)socket.LocalEndPoint!).Address.ToString();
        }
        catch
        {
            return "192.168.137.1";
        }
    }
    
    private static async Task<string> RunCommandAsync(string command, string args)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        using var process = System.Diagnostics.Process.Start(psi);
        if (process != null)
        {
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            return output;
        }
        return "";
    }
}

public enum Platform
{
    Windows,
    macOS,
    Linux,
    Android,
    iOS
}
