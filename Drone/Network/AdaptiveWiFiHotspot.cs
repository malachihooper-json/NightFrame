/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              ADAPTIVE WIFI HOTSPOT - MULTI-METHOD SSID                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Automatically evaluates and selects the best SSID broadcast method.       ║
 * ║  Falls back through multiple strategies until one works.                   ║
 * ║  Self-healing: reports issues for neural network training.                 ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// SSID broadcast method with capability info.
/// </summary>
public enum SSIDMethod
{
    /// <summary>Windows 10/11 Mobile Hotspot API</summary>
    MobileHotspot,
    
    /// <summary>Legacy netsh hosted network</summary>
    HostedNetwork,
    
    /// <summary>WiFi Direct (P2P)</summary>
    WiFiDirect,
    
    /// <summary>Linux hostapd</summary>
    Hostapd,
    
    /// <summary>macOS Internet Sharing</summary>
    MacOSSharing,
    
    /// <summary>No method available</summary>
    None
}

/// <summary>
/// Result of SSID capability evaluation.
/// </summary>
public class SSIDCapabilityResult
{
    public SSIDMethod RecommendedMethod { get; set; }
    public List<SSIDMethod> AvailableMethods { get; set; } = new();
    public Dictionary<SSIDMethod, string> MethodStatus { get; set; } = new();
    public string? FailureReason { get; set; }
    public bool CanBroadcast => RecommendedMethod != SSIDMethod.None;
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Diagnostic info for self-healing training.
/// </summary>
public class SSIDDiagnostic
{
    public string NodeId { get; set; } = "";
    public string OSVersion { get; set; } = "";
    public string WiFiAdapter { get; set; } = "";
    public string WiFiDriver { get; set; } = "";
    public bool HostedNetworkSupported { get; set; }
    public bool MobileHotspotAvailable { get; set; }
    public SSIDMethod AttemptedMethod { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Resolution { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Adaptive WiFi hotspot with multi-method fallback.
/// </summary>
public class AdaptiveWiFiHotspot
{
    private SSIDMethod _currentMethod = SSIDMethod.None;
    private Process? _hotspotProcess;
    private readonly string _ssid;
    private readonly string _password;
    private readonly List<SSIDDiagnostic> _diagnostics = new();
    
    public event Action<SSIDDiagnostic>? OnDiagnostic;
    
    public AdaptiveWiFiHotspot(string ssid = "NFRAME Global Internet", string? password = null)
    {
        _ssid = ssid;
        _password = password ?? GeneratePassword();
    }
    
    /// <summary>
    /// Evaluates all available SSID broadcast methods.
    /// </summary>
    public async Task<SSIDCapabilityResult> EvaluateCapabilitiesAsync()
    {
        var result = new SSIDCapabilityResult();
        
        Console.WriteLine("◈ Evaluating SSID broadcast capabilities...");
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Check Mobile Hotspot (Windows 10/11)
            var mobileHotspot = await CheckMobileHotspotAsync();
            result.MethodStatus[SSIDMethod.MobileHotspot] = mobileHotspot.status;
            if (mobileHotspot.available)
            {
                result.AvailableMethods.Add(SSIDMethod.MobileHotspot);
                Console.WriteLine("  ✓ Mobile Hotspot: Available");
            }
            else
            {
                Console.WriteLine($"  ✗ Mobile Hotspot: {mobileHotspot.status}");
            }
            
            // Check Hosted Network (Legacy)
            var hostedNetwork = await CheckHostedNetworkAsync();
            result.MethodStatus[SSIDMethod.HostedNetwork] = hostedNetwork.status;
            if (hostedNetwork.available)
            {
                result.AvailableMethods.Add(SSIDMethod.HostedNetwork);
                Console.WriteLine("  ✓ Hosted Network: Available");
            }
            else
            {
                Console.WriteLine($"  ✗ Hosted Network: {hostedNetwork.status}");
            }
            
            // Check WiFi Direct
            var wifiDirect = await CheckWiFiDirectAsync();
            result.MethodStatus[SSIDMethod.WiFiDirect] = wifiDirect.status;
            if (wifiDirect.available)
            {
                result.AvailableMethods.Add(SSIDMethod.WiFiDirect);
                Console.WriteLine("  ✓ WiFi Direct: Available");
            }
            else
            {
                Console.WriteLine($"  ✗ WiFi Direct: {wifiDirect.status}");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var hostapd = await CheckHostapdAsync();
            result.MethodStatus[SSIDMethod.Hostapd] = hostapd.status;
            if (hostapd.available)
            {
                result.AvailableMethods.Add(SSIDMethod.Hostapd);
                Console.WriteLine("  ✓ hostapd: Available");
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            result.MethodStatus[SSIDMethod.MacOSSharing] = "Available (manual setup)";
            result.AvailableMethods.Add(SSIDMethod.MacOSSharing);
            Console.WriteLine("  ✓ macOS Internet Sharing: Available (manual)");
        }
        
        // Select best method
        result.RecommendedMethod = result.AvailableMethods.FirstOrDefault();
        
        if (result.RecommendedMethod == SSIDMethod.None)
        {
            result.FailureReason = "No SSID broadcast method available on this system";
            Console.WriteLine("  ⚠ No broadcast method available");
            
            // Generate diagnostic for training
            await GenerateDiagnosticAsync(SSIDMethod.None, false, result.FailureReason);
        }
        else
        {
            Console.WriteLine($"  → Selected method: {result.RecommendedMethod}");
        }
        
        return result;
    }
    
    /// <summary>
    /// Starts the hotspot using the best available method.
    /// Uses ForcedWiFiAP first (no cellular), then falls back to other methods.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        // Try ForcedWiFiAP first - this uses Virtual Adapter, Registry hacks, WiFi Direct
        // These methods don't require cellular or Mobile Hotspot
        Console.WriteLine("◈ Attempting forced WiFi AP (no cellular required)...");
        
        var forcedAP = new ForcedWiFiAP(_ssid, _password);
        forcedAP.OnLog += msg => Console.WriteLine($"  {msg}");
        
        if (await forcedAP.StartAsync())
        {
            _currentMethod = SSIDMethod.WiFiDirect; // Mark as WiFi Direct since it's similar
            Console.WriteLine($"  ✓ Forced WiFi AP succeeded!");
            Console.WriteLine($"  ✓ SSID: {_ssid}");
            Console.WriteLine($"  ✓ Password: {_password}");
            return true;
        }
        
        Console.WriteLine("  Forced AP failed, evaluating fallback methods...");
        
        // Fall back to capability evaluation
        var capabilities = await EvaluateCapabilitiesAsync();
        
        if (!capabilities.CanBroadcast)
        {
            await GenerateDiagnosticAsync(SSIDMethod.None, false, 
                "No broadcast method available", 
                "Try: (1) Update WiFi drivers, (2) Enable Mobile Hotspot in Windows Settings");
            return false;
        }
        
        // Try methods in order of preference
        foreach (var method in capabilities.AvailableMethods)
        {
            Console.WriteLine($"◈ Attempting {method}...");
            
            var success = method switch
            {
                SSIDMethod.MobileHotspot => await StartMobileHotspotAsync(),
                SSIDMethod.HostedNetwork => await StartHostedNetworkAsync(),
                SSIDMethod.WiFiDirect => await StartWiFiDirectAsync(),
                SSIDMethod.Hostapd => await StartHostapdAsync(),
                SSIDMethod.MacOSSharing => await StartMacOSSharingAsync(),
                _ => false
            };
            
            await GenerateDiagnosticAsync(method, success, 
                success ? null : $"Failed to start {method}");
            
            if (success)
            {
                _currentMethod = method;
                Console.WriteLine($"  ✓ {method} started successfully");
                Console.WriteLine($"  ✓ SSID: {_ssid}");
                return true;
            }
            
            Console.WriteLine($"  ✗ {method} failed, trying next...");
        }
        
        return false;
    }
    
    /// <summary>
    /// Stops the hotspot.
    /// </summary>
    public async Task StopAsync()
    {
        switch (_currentMethod)
        {
            case SSIDMethod.MobileHotspot:
                await StopMobileHotspotAsync();
                break;
            case SSIDMethod.HostedNetwork:
                await RunCommandAsync("netsh", "wlan stop hostednetwork");
                break;
            case SSIDMethod.Hostapd:
                _hotspotProcess?.Kill();
                break;
        }
        
        _currentMethod = SSIDMethod.None;
        Console.WriteLine("◎ Hotspot stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          CAPABILITY CHECKS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<(bool available, string status)> CheckMobileHotspotAsync()
    {
        try
        {
            // Check if Mobile Hotspot feature is available (Windows 10 1607+)
            var result = await RunCommandAsync("powershell", 
                "-Command \"(Get-NetAdapter | Where-Object {$_.MediaType -eq 'Native 802.11'}).Status\"");
            
            if (result.Contains("Up") || result.Contains("Disconnected"))
            {
                // Check if we can access the Mobile Hotspot settings
                var hotspotCheck = await RunCommandAsync("powershell",
                    "-Command \"try { [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null; 'Available' } catch { 'Not Available' }\"");
                
                if (hotspotCheck.Contains("Available"))
                {
                    return (true, "Windows Mobile Hotspot available");
                }
            }
            
            return (false, "Mobile Hotspot not supported");
        }
        catch (Exception ex)
        {
            return (false, $"Error checking: {ex.Message}");
        }
    }
    
    private async Task<(bool available, string status)> CheckHostedNetworkAsync()
    {
        try
        {
            var result = await RunCommandAsync("netsh", "wlan show drivers");
            
            if (result.Contains("Hosted network supported  : Yes"))
            {
                return (true, "Hosted network supported");
            }
            
            return (false, "Hosted network not supported by driver");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }
    
    private async Task<(bool available, string status)> CheckWiFiDirectAsync()
    {
        try
        {
            // WiFi Direct is available on most modern adapters
            var result = await RunCommandAsync("powershell",
                "-Command \"Get-NetAdapter | Where-Object {$_.InterfaceDescription -like '*Wi-Fi Direct*'} | Select-Object -First 1\"");
            
            if (!string.IsNullOrWhiteSpace(result))
            {
                return (true, "WiFi Direct adapter found");
            }
            
            // Check in device manager
            var devCheck = await RunCommandAsync("powershell",
                "-Command \"Get-PnpDevice | Where-Object {$_.FriendlyName -like '*Wi-Fi Direct*' -and $_.Status -eq 'OK'} | Select-Object -First 1\"");
            
            if (!string.IsNullOrWhiteSpace(devCheck))
            {
                return (true, "WiFi Direct device available");
            }
            
            return (false, "WiFi Direct not available");
        }
        catch (Exception ex)
        {
            return (false, $"Error: {ex.Message}");
        }
    }
    
    private async Task<(bool available, string status)> CheckHostapdAsync()
    {
        try
        {
            var result = await RunCommandAsync("which", "hostapd");
            if (!string.IsNullOrWhiteSpace(result))
            {
                // Check if we have an AP-capable interface
                var iwCheck = await RunCommandAsync("iw", "list");
                if (iwCheck.Contains("* AP"))
                {
                    return (true, "hostapd available with AP support");
                }
                return (false, "hostapd installed but no AP support");
            }
            return (false, "hostapd not installed");
        }
        catch
        {
            return (false, "hostapd not available");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          START METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> StartMobileHotspotAsync()
    {
        try
        {
            // Use PowerShell to start Mobile Hotspot via Windows API
            var script = $@"
Add-Type -AssemblyName System.Runtime.WindowsRuntime

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]
Function Await($WinRtTask, $ResultType) {{
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}}

[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null

$connectionProfile = [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]::GetInternetConnectionProfile()
$tetheringManager = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($connectionProfile)

# Configure SSID
$config = $tetheringManager.GetCurrentAccessPointConfiguration()
$config.Ssid = '{_ssid}'
$config.Passphrase = '{_password}'

$tetheringManager.ConfigureAccessPointAsync($config) | Out-Null
Start-Sleep -Milliseconds 500

# Start hotspot
$result = Await ($tetheringManager.StartTetheringAsync()) ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult])

if ($result.Status -eq 'Success') {{
    Write-Output 'SUCCESS'
}} else {{
    Write-Output ""FAILED: $($result.Status)""
}}
";
            
            var result = await RunCommandAsync("powershell", $"-ExecutionPolicy Bypass -Command \"{script.Replace("\"", "`\"")}\"");
            
            return result.Contains("SUCCESS");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Mobile Hotspot error: {ex.Message}");
            return false;
        }
    }
    
    private async Task<bool> StartHostedNetworkAsync()
    {
        try
        {
            // Configure hosted network
            await RunCommandAsync("netsh", $"wlan set hostednetwork mode=allow ssid=\"{_ssid}\" key=\"{_password}\"");
            
            // Start it
            var result = await RunCommandAsync("netsh", "wlan start hostednetwork");
            
            return result.Contains("started") || result.Contains("已启动");
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> StartWiFiDirectAsync()
    {
        // WiFi Direct requires more complex setup
        // For now, return false and suggest Mobile Hotspot
        await Task.CompletedTask;
        return false;
    }
    
    private async Task<bool> StartHostapdAsync()
    {
        try
        {
            // Find wireless interface
            var iface = await RunCommandAsync("bash", "-c \"iw dev | grep Interface | head -1 | awk '{print $2}'\"");
            iface = iface.Trim();
            
            if (string.IsNullOrEmpty(iface))
            {
                return false;
            }
            
            // Create hostapd config
            var configPath = "/tmp/nframe_hostapd.conf";
            var config = $@"
interface={iface}
driver=nl80211
ssid={_ssid}
hw_mode=g
channel=7
wmm_enabled=0
macaddr_acl=0
auth_algs=1
ignore_broadcast_ssid=0
wpa=2
wpa_passphrase={_password}
wpa_key_mgmt=WPA-PSK
rsn_pairwise=CCMP
";
            await File.WriteAllTextAsync(configPath, config);
            
            // Start hostapd
            var psi = new ProcessStartInfo
            {
                FileName = "hostapd",
                Arguments = configPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            _hotspotProcess = Process.Start(psi);
            await Task.Delay(2000); // Wait for it to start
            
            return _hotspotProcess != null && !_hotspotProcess.HasExited;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task<bool> StartMacOSSharingAsync()
    {
        // macOS requires manual setup via System Preferences
        Console.WriteLine("  ℹ️  macOS: Enable Internet Sharing in System Preferences");
        Console.WriteLine("       → System Preferences → Sharing → Internet Sharing");
        Console.WriteLine("       → Share from: [Your internet connection]");
        Console.WriteLine("       → To: Wi-Fi");
        Console.WriteLine("       → Wi-Fi Options: Set SSID to 'NFRAME Global Internet'");
        
        await Task.CompletedTask;
        return false; // Manual setup required
    }
    
    private async Task StopMobileHotspotAsync()
    {
        var script = @"
[Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null
$connectionProfile = [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime]::GetInternetConnectionProfile()
$tetheringManager = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($connectionProfile)
$tetheringManager.StopTetheringAsync() | Out-Null
";
        await RunCommandAsync("powershell", $"-ExecutionPolicy Bypass -Command \"{script}\"");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          DIAGNOSTICS & SELF-HEALING
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task GenerateDiagnosticAsync(SSIDMethod method, bool success, string? error, string? resolution = null)
    {
        var diagnostic = new SSIDDiagnostic
        {
            NodeId = NodeIdentity.LoadOrCreate().NodeId,
            OSVersion = Environment.OSVersion.ToString(),
            WiFiAdapter = await GetWiFiAdapterInfoAsync(),
            WiFiDriver = await GetWiFiDriverInfoAsync(),
            HostedNetworkSupported = (await CheckHostedNetworkAsync()).available,
            MobileHotspotAvailable = (await CheckMobileHotspotAsync()).available,
            AttemptedMethod = method,
            Success = success,
            ErrorMessage = error,
            Resolution = resolution
        };
        
        _diagnostics.Add(diagnostic);
        OnDiagnostic?.Invoke(diagnostic);
        
        // Save diagnostic for training
        await SaveDiagnosticForTrainingAsync(diagnostic);
    }
    
    private async Task<string> GetWiFiAdapterInfoAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await RunCommandAsync("powershell", 
                    "-Command \"(Get-NetAdapter | Where-Object {$_.MediaType -eq 'Native 802.11'}).InterfaceDescription\"");
            }
            return "Unknown";
        }
        catch { return "Unknown"; }
    }
    
    private async Task<string> GetWiFiDriverInfoAsync()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await RunCommandAsync("powershell",
                    "-Command \"(Get-NetAdapter | Where-Object {$_.MediaType -eq 'Native 802.11'} | Get-NetAdapterAdvancedProperty | Where-Object {$_.RegistryKeyword -eq 'DriverVersion'}).RegistryValue\"");
            }
            return "Unknown";
        }
        catch { return "Unknown"; }
    }
    
    private async Task SaveDiagnosticForTrainingAsync(SSIDDiagnostic diagnostic)
    {
        try
        {
            var trainingDir = Path.Combine(AppContext.BaseDirectory, "training_data", "ssid_diagnostics");
            Directory.CreateDirectory(trainingDir);
            
            var filename = $"ssid_diag_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
            var json = JsonSerializer.Serialize(diagnostic, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(Path.Combine(trainingDir, filename), json);
        }
        catch { /* Ignore save errors */ }
    }
    
    /// <summary>
    /// Gets all diagnostics for training.
    /// </summary>
    public IReadOnlyList<SSIDDiagnostic> GetDiagnostics() => _diagnostics.AsReadOnly();
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HELPERS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private static async Task<string> RunCommandAsync(string command, string args)
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
            
            return output.Trim();
        }
        catch
        {
            return "";
        }
    }
    
    private static string GeneratePassword()
    {
        var chars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 12).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }
}
