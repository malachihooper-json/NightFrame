/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║             CELLULAR ACCESS POINT - 5G/4G LTE INTERNET PROVISION           ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Provisions cellular internet access to end users via:                     ║
 * ║  - USB tethering for direct device connection                              ║
 * ║  - Wi-Fi hotspot bridging (cellular → Wi-Fi)                              ║
 * ║  - Ethernet bridging for fixed installations                              ║
 * ║  Implements intelligent band selection and signal optimization.            ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace NIGHTFRAME.Drone.Cellular;

/// <summary>
/// Cellular Access Point Manager - provisions 5G/4G LTE internet to end users.
/// Handles modem management, connection establishment, and traffic routing.
/// </summary>
public class CellularAccessPoint : IDisposable
{
    private readonly ModemController _modem;
    private readonly CellularIntelligence? _intelligence;
    
    private bool _isRunning = false;
    private string? _cellularInterface;
    private string? _apnProfile;
    private Process? _natProcess;
    
    // Connection state
    public bool IsConnected { get; private set; }
    public bool IsProvisioningActive { get; private set; }
    public CellularConnectionInfo? ConnectionInfo { get; private set; }
    public long BytesTransferred { get; private set; }
    public int ConnectedClients { get; private set; }
    
    // Events
    public event Action<CellularConnectionInfo>? OnConnectionEstablished;
    public event Action<string>? OnConnectionLost;
    public event Action<int>? OnClientCountChanged;
    public event Action<BandInfo>? OnBandChanged;
    
    /// <summary>
    /// Creates a Cellular Access Point manager.
    /// </summary>
    /// <param name="modem">Modem controller for AT commands</param>
    /// <param name="intelligence">Optional cellular intelligence for optimization</param>
    public CellularAccessPoint(ModemController modem, CellularIntelligence? intelligence = null)
    {
        _modem = modem;
        _intelligence = intelligence;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Initializes the cellular access point and establishes data connection.
    /// </summary>
    public async Task<bool> StartAsync(CellularAPConfig config, CancellationToken ct = default)
    {
        Console.WriteLine("◈ Starting Cellular Access Point...");
        
        try
        {
            // Step 1: Connect to modem
            if (!_modem.IsConnected)
            {
                var connected = await _modem.ConnectAsync();
                if (!connected)
                {
                    Console.WriteLine("∴ Failed to connect to modem");
                    return false;
                }
            }
            
            // Step 2: Configure APN
            _apnProfile = config.APN ?? await DetectAPNAsync();
            Console.WriteLine($"◎ Using APN: {_apnProfile}");
            
            var apnSet = await SetAPNAsync(_apnProfile, config.Username, config.Password);
            if (!apnSet)
            {
                Console.WriteLine("∴ Failed to configure APN");
                return false;
            }
            
            // Step 3: Select optimal band (if intelligence available)
            if (_intelligence != null && config.OptimizeBand)
            {
                await OptimizeBandSelectionAsync();
            }
            
            // Step 4: Establish data connection
            var dataConnected = await EstablishDataConnectionAsync();
            if (!dataConnected)
            {
                Console.WriteLine("∴ Failed to establish data connection");
                return false;
            }
            
            // Step 5: Detect cellular interface
            _cellularInterface = await DetectCellularInterfaceAsync();
            if (string.IsNullOrEmpty(_cellularInterface))
            {
                Console.WriteLine("∴ Failed to detect cellular network interface");
                return false;
            }
            
            Console.WriteLine($"◎ Cellular interface: {_cellularInterface}");
            
            // Step 6: Configure NAT/routing based on mode
            switch (config.ProvisionMode)
            {
                case CellularProvisionMode.USBTethering:
                    await ConfigureUSBTetheringAsync();
                    break;
                    
                case CellularProvisionMode.WifiBridge:
                    await ConfigureWifiBridgeAsync(config.WifiInterface);
                    break;
                    
                case CellularProvisionMode.EthernetBridge:
                    await ConfigureEthernetBridgeAsync(config.EthernetInterface);
                    break;
                    
                case CellularProvisionMode.FullNAT:
                    await ConfigureFullNATAsync();
                    break;
            }
            
            _isRunning = true;
            IsProvisioningActive = true;
            
            // Start monitoring loop
            _ = MonitorConnectionAsync(ct);
            
            Console.WriteLine("◈ Cellular Access Point ACTIVE");
            Console.WriteLine($"  Mode: {config.ProvisionMode}");
            Console.WriteLine($"  Technology: {ConnectionInfo?.Technology ?? "Unknown"}");
            Console.WriteLine($"  IP: {ConnectionInfo?.IPAddress ?? "Acquiring..."}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Cellular AP start failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Stops the cellular access point and disconnects.
    /// </summary>
    public async Task StopAsync()
    {
        Console.WriteLine("◎ Stopping Cellular Access Point...");
        
        _isRunning = false;
        IsProvisioningActive = false;
        
        // Stop NAT
        _natProcess?.Kill();
        _natProcess?.Dispose();
        _natProcess = null;
        
        // Tear down routing
        await TearDownRoutingAsync();
        
        // Disconnect data
        await _modem.SendCommandAsync("AT+CGACT=0,1");
        
        IsConnected = false;
        Console.WriteLine("◎ Cellular Access Point stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              APN CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Auto-detects the APN based on carrier.
    /// </summary>
    private async Task<string> DetectAPNAsync()
    {
        // Get carrier info
        var response = await _modem.SendCommandAsync("AT+COPS?");
        var carrier = ATCommandParser.ParseCarrier(response);
        
        // Common US carrier APNs
        var apnDatabase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // US Carriers
            { "AT&T", "broadband" },
            { "T-Mobile", "fast.t-mobile.com" },
            { "Verizon", "vzwinternet" },
            { "Sprint", "cinet.spcs" },
            { "US Cellular", "usccinternet" },
            
            // European Carriers
            { "Vodafone", "web.vodafone.de" },
            { "O2", "mobile.o2.co.uk" },
            { "EE", "everywhere" },
            { "Three", "three.co.uk" },
            
            // Asian Carriers
            { "NTT DoCoMo", "spmode.ne.jp" },
            { "SoftBank", "plus.softbank" },
            { "SK Telecom", "lte.sktelecom.com" },
            
            // Default for unknown carriers
            { "default", "internet" }
        };
        
        foreach (var kvp in apnDatabase)
        {
            if (carrier.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        
        return apnDatabase["default"];
    }
    
    /// <summary>
    /// Configures the APN on the modem.
    /// </summary>
    private async Task<bool> SetAPNAsync(string apn, string? username = null, string? password = null)
    {
        try
        {
            // Set PDP context with APN
            var pdpCmd = $"AT+CGDCONT=1,\"IPV4V6\",\"{apn}\"";
            var response = await _modem.SendCommandAsync(pdpCmd);
            
            if (!response.Contains("OK"))
                return false;
            
            // Set authentication if provided
            if (!string.IsNullOrEmpty(username))
            {
                var authType = string.IsNullOrEmpty(password) ? 0 : 1; // 0=None, 1=PAP
                var authCmd = $"AT+CGAUTH=1,{authType},\"{username}\",\"{password ?? ""}\"";
                await _modem.SendCommandAsync(authCmd);
            }
            
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              BAND OPTIMIZATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Optimizes band selection based on signal analysis.
    /// </summary>
    private async Task OptimizeBandSelectionAsync()
    {
        Console.WriteLine("◎ Analyzing available bands...");
        
        // Get available bands
        var bandsResponse = await _modem.SendCommandAsync("AT+QNWPREFCFG=\"lte_band\"");
        var bands = ATCommandParser.ParseAvailableBands(bandsResponse);
        
        // Get current measurements
        var measurements = _intelligence?.LastMeasurement;
        if (measurements == null)
            return;
        
        // Prioritize bands based on signal and throughput potential
        var prioritizedBands = bands
            .OrderByDescending(b => GetBandScore(b, measurements))
            .ToList();
        
        if (prioritizedBands.Count > 0)
        {
            var bestBand = prioritizedBands.First();
            Console.WriteLine($"◎ Optimizing for Band {bestBand.BandNumber} ({bestBand.Technology})");
            
            // Lock to optimal band
            await _modem.SendCommandAsync($"AT+QNWPREFCFG=\"lte_band\",{bestBand.BandNumber}");
            
            OnBandChanged?.Invoke(bestBand);
        }
    }
    
    private float GetBandScore(BandInfo band, CellTowerMeasurement measurement)
    {
        float score = 50;
        
        // 5G NR bands get priority
        if (band.Technology == "5G NR")
            score += 20;
        
        // Higher frequency = more bandwidth potential (but less range)
        if (band.FrequencyMHz > 2000)
            score += 10;
        
        // If we have good signal, prefer high-bandwidth bands
        if (measurement.RSRP > -85)
            score += band.BandwidthMHz / 2;
        
        // If signal is weak, prefer low-frequency bands
        if (measurement.RSRP < -100)
            score -= band.FrequencyMHz / 500;
        
        return score;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              DATA CONNECTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Establishes the data connection (PDP context activation).
    /// </summary>
    private async Task<bool> EstablishDataConnectionAsync()
    {
        Console.WriteLine("◎ Establishing data connection...");
        
        try
        {
            // Check registration
            var regResponse = await _modem.SendCommandAsync("AT+CREG?");
            var registration = ATCommandParser.ParseRegistration(regResponse);
            
            if (!registration.Registered)
            {
                Console.WriteLine("∴ Not registered to network");
                return false;
            }
            
            Console.WriteLine($"◎ Registered: {registration.Technology}");
            
            // Activate PDP context
            var activateResponse = await _modem.SendCommandAsync("AT+CGACT=1,1");
            if (!activateResponse.Contains("OK"))
            {
                Console.WriteLine("∴ PDP activation failed");
                return false;
            }
            
            // Get IP address
            var ipResponse = await _modem.SendCommandAsync("AT+CGPADDR=1");
            var ipAddress = ATCommandParser.ParseIPAddress(ipResponse);
            
            // Get connection details
            ConnectionInfo = new CellularConnectionInfo
            {
                Technology = registration.Technology,
                IPAddress = ipAddress,
                APN = _apnProfile ?? "",
                IsIPv6 = ipAddress?.Contains(':') ?? false,
                ConnectedAt = DateTime.UtcNow
            };
            
            IsConnected = true;
            OnConnectionEstablished?.Invoke(ConnectionInfo);
            
            Console.WriteLine($"◈ Data connection established");
            Console.WriteLine($"   IP: {ConnectionInfo.IPAddress}");
            Console.WriteLine($"   Technology: {ConnectionInfo.Technology}");
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Data connection failed: {ex.Message}");
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              INTERFACE DETECTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Detects the cellular network interface.
    /// </summary>
    private async Task<string?> DetectCellularInterfaceAsync()
    {
        // Wait for interface to come up
        await Task.Delay(2000);
        
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
            .ToList();
        
        // Look for cellular interface patterns
        var cellularPatterns = new[] 
        { 
            "wwan", "rmnet", "usb", "cdc_", "enx", "wwp", 
            "Cellular", "Mobile Broadband", "WWAN"
        };
        
        foreach (var ni in interfaces)
        {
            var name = ni.Name.ToLowerInvariant();
            var desc = ni.Description.ToLowerInvariant();
            
            if (cellularPatterns.Any(p => name.Contains(p.ToLower()) || desc.Contains(p.ToLower())))
            {
                // Verify it has an IP address
                var props = ni.GetIPProperties();
                if (props.UnicastAddresses.Any(addr => 
                    addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                {
                    return ni.Name;
                }
            }
            
            // Also check for non-local IPv4 that's not our LAN
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Ppp ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Wwanpp ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Wwanpp2)
            {
                return ni.Name;
            }
        }
        
        // Fallback: find interface with internet-routable IP that's not local
        foreach (var ni in interfaces)
        {
            var props = ni.GetIPProperties();
            var gateway = props.GatewayAddresses.FirstOrDefault();
            
            if (gateway != null && 
                !gateway.Address.ToString().StartsWith("192.168.") &&
                ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            {
                return ni.Name;
            }
        }
        
        return null;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              NAT/ROUTING CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private async Task ConfigureUSBTetheringAsync()
    {
        Console.WriteLine("◎ Configuring USB tethering...");
        
        // Enable USB tethering mode on modem
        await _modem.SendCommandAsync("AT+QCFG=\"usbnet\",0");
        
        // The modem will appear as a network interface
        // NAT is typically handled by the host OS
    }
    
    private async Task ConfigureWifiBridgeAsync(string? wifiInterface)
    {
        Console.WriteLine("◎ Configuring Wi-Fi bridge...");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await ConfigureWindowsNAT(wifiInterface);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await ConfigureLinuxNAT(wifiInterface);
        }
    }
    
    private async Task ConfigureEthernetBridgeAsync(string? ethInterface)
    {
        Console.WriteLine("◎ Configuring Ethernet bridge...");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Create bridge
            await RunCommandAsync("brctl", $"addbr br0");
            await RunCommandAsync("brctl", $"addif br0 {_cellularInterface}");
            await RunCommandAsync("brctl", $"addif br0 {ethInterface}");
            await RunCommandAsync("ip", "link set br0 up");
        }
    }
    
    private async Task ConfigureFullNATAsync()
    {
        Console.WriteLine("◎ Configuring full NAT routing...");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await ConfigureWindowsNAT(null);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await ConfigureLinuxNAT(null);
        }
    }
    
    private async Task ConfigureWindowsNAT(string? shareToInterface)
    {
        // Enable IP forwarding
        await RunCommandAsync("netsh", "interface ipv4 set interface \"" + _cellularInterface + "\" forwarding=enabled");
        
        if (!string.IsNullOrEmpty(shareToInterface))
        {
            // Enable ICS (Internet Connection Sharing)
            // This requires admin and manipulating registry/COM objects
            // For now, use netsh to share
            await RunCommandAsync("netsh", $"wlan set hostednetwork mode=allow");
        }
    }
    
    private async Task ConfigureLinuxNAT(string? shareToInterface)
    {
        // Enable IP forwarding
        await RunCommandAsync("sysctl", "-w net.ipv4.ip_forward=1");
        
        // Configure iptables NAT
        await RunCommandAsync("iptables", $"-t nat -A POSTROUTING -o {_cellularInterface} -j MASQUERADE");
        
        if (!string.IsNullOrEmpty(shareToInterface))
        {
            await RunCommandAsync("iptables", $"-A FORWARD -i {shareToInterface} -o {_cellularInterface} -j ACCEPT");
            await RunCommandAsync("iptables", $"-A FORWARD -i {_cellularInterface} -o {shareToInterface} -m state --state RELATED,ESTABLISHED -j ACCEPT");
        }
    }
    
    private async Task TearDownRoutingAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await RunCommandAsync("iptables", $"-t nat -D POSTROUTING -o {_cellularInterface} -j MASQUERADE");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              CONNECTION MONITORING
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private async Task MonitorConnectionAsync(CancellationToken ct)
    {
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                // Check connection status
                var response = await _modem.SendCommandAsync("AT+CGACT?");
                var isActive = response.Contains("+CGACT: 1,1");
                
                if (!isActive && IsConnected)
                {
                    Console.WriteLine("∴ Cellular connection lost");
                    IsConnected = false;
                    OnConnectionLost?.Invoke("Connection deactivated");
                    
                    // Attempt reconnection
                    await Task.Delay(5000, ct);
                    await EstablishDataConnectionAsync();
                }
                
                // Update signal quality
                if (_intelligence != null)
                {
                    var signal = _intelligence.CurrentQuality;
                    if (signal != null && ConnectionInfo != null)
                    {
                        ConnectionInfo = ConnectionInfo with { SignalQuality = signal.Score };
                    }
                }
                
                // Update bytes transferred (platform-specific)
                await UpdateTrafficStatsAsync();
                
                await Task.Delay(10000, ct); // Check every 10 seconds
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ Monitor error: {ex.Message}");
                await Task.Delay(30000, ct);
            }
        }
    }
    
    private async Task UpdateTrafficStatsAsync()
    {
        if (string.IsNullOrEmpty(_cellularInterface))
            return;
        
        try
        {
            var ni = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name == _cellularInterface);
            
            if (ni != null)
            {
                var stats = ni.GetIPStatistics();
                BytesTransferred = stats.BytesSent + stats.BytesReceived;
            }
        }
        catch { }
        
        await Task.CompletedTask;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              UTILITY
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private async Task<string> RunCommandAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return "";
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output;
        }
        catch
        {
            return "";
        }
    }
    
    public void Dispose()
    {
        _natProcess?.Kill();
        _natProcess?.Dispose();
    }
}

/// <summary>
/// Configuration for cellular access point.
/// </summary>
public record CellularAPConfig
{
    public string? APN { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public CellularProvisionMode ProvisionMode { get; init; } = CellularProvisionMode.WifiBridge;
    public string? WifiInterface { get; init; }
    public string? EthernetInterface { get; init; }
    public bool OptimizeBand { get; init; } = true;
    public int MaxClients { get; init; } = 10;
    public long BandwidthLimitKbps { get; init; } = 0; // 0 = unlimited
}

/// <summary>
/// Cellular provisioning mode.
/// </summary>
public enum CellularProvisionMode
{
    USBTethering,    // Share via USB tethering
    WifiBridge,      // Bridge cellular to WiFi hotspot
    EthernetBridge,  // Bridge cellular to Ethernet
    FullNAT          // Full NAT for all interfaces
}

/// <summary>
/// Information about the cellular data connection.
/// </summary>
public record CellularConnectionInfo
{
    public required string Technology { get; init; }
    public string? IPAddress { get; init; }
    public required string APN { get; init; }
    public bool IsIPv6 { get; init; }
    public DateTime ConnectedAt { get; init; }
    public float SignalQuality { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
}

/// <summary>
/// Information about a cellular band.
/// </summary>
public record BandInfo
{
    public required int BandNumber { get; init; }
    public required string Technology { get; init; }  // LTE, 5G NR
    public required int FrequencyMHz { get; init; }
    public required int BandwidthMHz { get; init; }
}
