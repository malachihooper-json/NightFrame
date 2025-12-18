/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              PLATFORM SSID EVALUATOR - SMART NETWORK BROADCAST             ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Evaluates platform capabilities in real-time and selects the optimal      ║
 * ║  SSID broadcast method. Uses open networks where possible, falls back      ║
 * ║  to password-in-name approach for platforms that require it.               ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Runtime.InteropServices;
using System.Diagnostics;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Platform capability for SSID broadcasting.
/// </summary>
public enum SSIDCapability
{
    /// <summary>Can create open networks (Android, Linux)</summary>
    OpenNetwork,
    
    /// <summary>Requires password (Windows, macOS)</summary>
    PasswordRequired,
    
    /// <summary>Cannot broadcast at all (iOS)</summary>
    CannotBroadcast
}

/// <summary>
/// SSID configuration based on platform evaluation.
/// </summary>
public class SSIDConfig
{
    /// <summary>Open network SSID (for Android/Linux)</summary>
    public const string OPEN_SSID = "NFRAME Global Internet";
    
    /// <summary>Password-in-name SSID (for Windows/Mac)</summary>
    public const string PASSWORD_SSID = "NFRAME Global Internet pw:00000000";
    
    /// <summary>Standard password for all nodes</summary>
    public const string STANDARD_PASSWORD = "00000000";
    
    public string SSID { get; set; } = "";
    public string? Password { get; set; }
    public bool IsOpenNetwork => Password == null;
    public SSIDCapability Capability { get; set; }
    public string Platform { get; set; } = "";
    public string BroadcastMethod { get; set; } = "";
}

/// <summary>
/// Real-time platform evaluator that determines optimal SSID broadcast.
/// </summary>
public class PlatformSSIDEvaluator
{
    /// <summary>
    /// Evaluates current platform and returns optimal SSID configuration.
    /// </summary>
    public static async Task<SSIDConfig> EvaluateAsync()
    {
        Console.WriteLine("◈ Evaluating platform SSID capabilities...");
        
        var config = new SSIDConfig();
        
        // Detect platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            config = await EvaluateWindowsAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            config = await EvaluateMacOSAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            config = await EvaluateLinuxAsync();
        }
        else
        {
            // Check for Android (running via .NET MAUI or Xamarin)
            config = await EvaluateAndroidAsync();
        }
        
        // Log result
        Console.WriteLine($"  Platform: {config.Platform}");
        Console.WriteLine($"  Capability: {config.Capability}");
        Console.WriteLine($"  SSID: {config.SSID}");
        Console.WriteLine($"  Open Network: {config.IsOpenNetwork}");
        Console.WriteLine($"  Method: {config.BroadcastMethod}");
        
        return config;
    }
    
    /// <summary>
    /// Windows: Requires password via WiFi Direct.
    /// </summary>
    private static async Task<SSIDConfig> EvaluateWindowsAsync()
    {
        Console.WriteLine("  Detected: Windows");
        
        var config = new SSIDConfig
        {
            Platform = "Windows",
            Capability = SSIDCapability.PasswordRequired,
            SSID = SSIDConfig.PASSWORD_SSID,
            Password = SSIDConfig.STANDARD_PASSWORD,
            BroadcastMethod = "WiFi Direct Legacy"
        };
        
        // Check if we can use WiFi Direct
        try
        {
            var checkResult = await RunCommandAsync("powershell", 
                "-Command \"[Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher, Windows.Devices.WiFiDirect, ContentType=WindowsRuntime] | Out-Null; 'OK'\"");
            
            if (checkResult.Contains("OK"))
            {
                config.BroadcastMethod = "WiFi Direct Legacy (confirmed)";
            }
        }
        catch
        {
            config.BroadcastMethod = "WiFi Direct (needs verification)";
        }
        
        return config;
    }
    
    /// <summary>
    /// macOS: Requires password via Internet Sharing.
    /// </summary>
    private static async Task<SSIDConfig> EvaluateMacOSAsync()
    {
        Console.WriteLine("  Detected: macOS");
        
        var config = new SSIDConfig
        {
            Platform = "macOS",
            Capability = SSIDCapability.PasswordRequired,
            SSID = SSIDConfig.PASSWORD_SSID,
            Password = SSIDConfig.STANDARD_PASSWORD,
            BroadcastMethod = "Internet Sharing"
        };
        
        // Check for WiFi capability
        try
        {
            var result = await RunCommandAsync("system_profiler", "SPAirPortDataType");
            if (result.Contains("Wi-Fi"))
            {
                config.BroadcastMethod = "Internet Sharing (WiFi available)";
            }
        }
        catch { }
        
        return config;
    }
    
    /// <summary>
    /// Linux: Can create open networks via hostapd!
    /// </summary>
    private static async Task<SSIDConfig> EvaluateLinuxAsync()
    {
        Console.WriteLine("  Detected: Linux");
        
        var config = new SSIDConfig
        {
            Platform = "Linux",
            Capability = SSIDCapability.OpenNetwork,
            SSID = SSIDConfig.OPEN_SSID,  // OPEN - no password!
            Password = null,
            BroadcastMethod = "hostapd"
        };
        
        // Check for hostapd
        try
        {
            var result = await RunCommandAsync("which", "hostapd");
            if (!string.IsNullOrEmpty(result))
            {
                config.BroadcastMethod = "hostapd (installed)";
                
                // Check for AP-capable interface
                var iwResult = await RunCommandAsync("iw", "list");
                if (iwResult.Contains("* AP"))
                {
                    config.BroadcastMethod = "hostapd (AP mode supported)";
                }
            }
            else
            {
                config.BroadcastMethod = "hostapd (needs install: apt install hostapd)";
            }
        }
        catch { }
        
        return config;
    }
    
    /// <summary>
    /// Android: Can create open networks via Hotspot API!
    /// </summary>
    private static async Task<SSIDConfig> EvaluateAndroidAsync()
    {
        Console.WriteLine("  Detected: Android (or unknown platform)");
        
        // Android can create open hotspots!
        var config = new SSIDConfig
        {
            Platform = "Android",
            Capability = SSIDCapability.OpenNetwork,
            SSID = SSIDConfig.OPEN_SSID,  // OPEN - no password!
            Password = null,
            BroadcastMethod = "WifiManager.LocalOnlyHotspotReservation"
        };
        
        // Note: On Android, we'd use Android-specific APIs
        // This evaluation is for when running on Android via .NET MAUI/Xamarin
        
        await Task.CompletedTask;
        return config;
    }
    
    /// <summary>
    /// Checks if current platform can broadcast open networks.
    /// </summary>
    public static bool CanBroadcastOpenNetwork()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return true;
            
        // Android detection would go here in a MAUI app
        // For now, only Linux confirmed
        
        return false;
    }
    
    /// <summary>
    /// Gets the appropriate SSID for the current platform.
    /// </summary>
    public static string GetSSID()
    {
        if (CanBroadcastOpenNetwork())
            return SSIDConfig.OPEN_SSID;
        else
            return SSIDConfig.PASSWORD_SSID;
    }
    
    /// <summary>
    /// Gets the password (null for open networks).
    /// </summary>
    public static string? GetPassword()
    {
        if (CanBroadcastOpenNetwork())
            return null;
        else
            return SSIDConfig.STANDARD_PASSWORD;
    }
    
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
}

/// <summary>
/// Cross-platform SSID broadcaster that uses optimal method per platform.
/// </summary>
public class UniversalSSIDBroadcaster
{
    private readonly SSIDConfig _config;
    private WiFiDirectSoftAP? _windowsAP;
    private Process? _linuxAP;
    private bool _isRunning;
    
    public bool IsRunning => _isRunning;
    public SSIDConfig Config => _config;
    
    public UniversalSSIDBroadcaster(SSIDConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Creates broadcaster after evaluating platform.
    /// </summary>
    public static async Task<UniversalSSIDBroadcaster> CreateAsync()
    {
        var config = await PlatformSSIDEvaluator.EvaluateAsync();
        return new UniversalSSIDBroadcaster(config);
    }
    
    /// <summary>
    /// Starts broadcasting SSID using the appropriate method.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        Console.WriteLine("◈ Starting Universal SSID Broadcaster...");
        Console.WriteLine($"  SSID: {_config.SSID}");
        Console.WriteLine($"  Open Network: {_config.IsOpenNetwork}");
        
        bool success = false;
        
        if (_config.Platform == "Windows")
        {
            success = await StartWindowsAsync();
        }
        else if (_config.Platform == "macOS")
        {
            success = await StartMacOSAsync();
        }
        else if (_config.Platform == "Linux")
        {
            success = await StartLinuxAsync();
        }
        else if (_config.Platform == "Android")
        {
            success = await StartAndroidAsync();
        }
        
        if (success)
        {
            _isRunning = true;
            Console.WriteLine($"◈ SSID Broadcasting: {_config.SSID}");
            
            if (_config.IsOpenNetwork)
            {
                Console.WriteLine("◈ OPEN NETWORK - No password required!");
            }
            else
            {
                Console.WriteLine($"◈ Password visible in SSID name");
            }
        }
        
        return success;
    }
    
    /// <summary>
    /// Stops broadcasting.
    /// </summary>
    public async Task StopAsync()
    {
        if (_windowsAP != null)
        {
            await _windowsAP.StopAsync();
        }
        
        if (_linuxAP != null && !_linuxAP.HasExited)
        {
            _linuxAP.Kill();
        }
        
        _isRunning = false;
        Console.WriteLine("◎ SSID Broadcaster stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          PLATFORM-SPECIFIC STARTERS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> StartWindowsAsync()
    {
        // Use WiFi Direct with password (required on Windows)
        _windowsAP = new WiFiDirectSoftAP(_config.SSID, _config.Password ?? SSIDConfig.STANDARD_PASSWORD);
        return await _windowsAP.StartAsync();
    }
    
    private async Task<bool> StartMacOSAsync()
    {
        Console.WriteLine("  macOS: Starting Internet Sharing...");
        
        // Create sharing plist
        var plistPath = "/tmp/nframe_sharing.plist";
        var plist = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!DOCTYPE plist PUBLIC ""-//Apple//DTD PLIST 1.0//EN"" ""http://www.apple.com/DTDs/PropertyList-1.0.dtd"">
<plist version=""1.0"">
<dict>
    <key>SSID</key>
    <string>{_config.SSID}</string>
    <key>Password</key>
    <string>{_config.Password}</string>
    <key>Mode</key>
    <integer>2</integer>
</dict>
</plist>";
        
        try
        {
            await File.WriteAllTextAsync(plistPath, plist);
            
            // macOS sharing requires manual or elevated setup
            Console.WriteLine("  ℹ️  macOS: Enable Internet Sharing in System Preferences");
            Console.WriteLine($"     Set network name to: {_config.SSID}");
            Console.WriteLine($"     Set password to: {_config.Password}");
            
            return false; // Manual setup needed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  macOS error: {ex.Message}");
            return false;
        }
    }
    
    private async Task<bool> StartLinuxAsync()
    {
        Console.WriteLine("  Linux: Starting OPEN network via hostapd...");
        
        // Find wireless interface
        var iface = await RunCommandAsync("bash", 
            "-c \"iw dev | grep Interface | head -1 | awk '{print $2}'\"");
        iface = iface.Trim();
        
        if (string.IsNullOrEmpty(iface))
        {
            Console.WriteLine("  ✗ No wireless interface found");
            return false;
        }
        
        Console.WriteLine($"  Interface: {iface}");
        
        // Create hostapd config for OPEN network
        var configPath = "/tmp/nframe_hostapd.conf";
        var config = $@"
interface={iface}
driver=nl80211
ssid={_config.SSID}
hw_mode=g
channel=7
wmm_enabled=0
macaddr_acl=0
auth_algs=1
ignore_broadcast_ssid=0
# OPEN NETWORK - NO AUTHENTICATION
# No wpa settings = open network
";
        
        try
        {
            await File.WriteAllTextAsync(configPath, config);
            
            // Stop any existing hostapd
            await RunCommandAsync("sudo", "killall hostapd 2>/dev/null");
            await Task.Delay(500);
            
            // Start hostapd
            var psi = new ProcessStartInfo
            {
                FileName = "sudo",
                Arguments = $"hostapd {configPath}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            _linuxAP = Process.Start(psi);
            await Task.Delay(2000);
            
            if (_linuxAP != null && !_linuxAP.HasExited)
            {
                Console.WriteLine("  ✓ OPEN network started!");
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Linux error: {ex.Message}");
            return false;
        }
    }
    
    private async Task<bool> StartAndroidAsync()
    {
        Console.WriteLine("  Android: Open hotspot API");
        Console.WriteLine("  ℹ️  Use Android Hotspot API (LocalOnlyHotspot)");
        
        // On actual Android, this would use:
        // WifiManager.startLocalOnlyHotspot() - creates open network
        
        // For now, provide instructions
        Console.WriteLine("  Android device can create open hotspots natively.");
        Console.WriteLine("  Integrate with Android SDK for automatic broadcast.");
        
        await Task.CompletedTask;
        return false; // Needs Android-specific implementation
    }
    
    private static async Task<string> RunCommandAsync(string command, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
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
}
