/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              FORCED WIFI ACCESS POINT - NO CELLULAR REQUIRED               ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Creates a true WiFi AP using WiFi Direct, Virtual Adapter, or forced     ║
 * ║  hosted network. Does NOT use Mobile Hotspot (cellular tethering).        ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Forces WiFi AP creation without cellular dependency.
/// </summary>
public class ForcedWiFiAP
{
    private readonly string _ssid;
    private readonly string _password;
    private Process? _apProcess;
    private bool _isRunning;
    
    public event Action<string>? OnLog;
    public event Action<bool, string>? OnStatusChange;
    
    public ForcedWiFiAP(string ssid = "NFRAME Global Internet", string? password = null)
    {
        _ssid = ssid;
        _password = password ?? "nframe123"; // Min 8 chars for WPA2
    }
    
    /// <summary>
    /// Starts the WiFi AP using the best available method.
    /// Tries multiple approaches until one succeeds.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        Log("◈ Starting Forced WiFi AP (no cellular)...");
        
        // Method 1: Try to enable Virtual WiFi Adapter
        if (await TryVirtualWiFiAdapterAsync())
        {
            Log("✓ Virtual WiFi Adapter method succeeded");
            return true;
        }
        
        // Method 2: Force hosted network via registry tweak
        if (await TryForceHostedNetworkAsync())
        {
            Log("✓ Forced Hosted Network method succeeded");
            return true;
        }
        
        // Method 3: WiFi Direct Autonomous Group Owner (SoftAP)
        if (await TryWiFiDirectSoftAPAsync())
        {
            Log("✓ WiFi Direct SoftAP method succeeded");
            return true;
        }
        
        // Method 4: Use third-party tool (create-ap equivalent for Windows)
        if (await TryCreateAPToolAsync())
        {
            Log("✓ CreateAP tool method succeeded");
            return true;
        }
        
        Log("✗ All WiFi AP methods failed");
        Log("  → Your WiFi adapter may need driver update for AP mode");
        Log("  → Try: Device Manager → WiFi Adapter → Update Driver");
        
        return false;
    }
    
    /// <summary>
    /// Stops the WiFi AP.
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning) return;
        
        // Stop hosted network
        await RunCommandAsync("netsh", "wlan stop hostednetwork");
        
        // Kill any AP process
        _apProcess?.Kill();
        _apProcess = null;
        
        _isRunning = false;
        Log("◎ WiFi AP stopped");
        OnStatusChange?.Invoke(false, "Stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          METHOD 1: VIRTUAL WIFI ADAPTER
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> TryVirtualWiFiAdapterAsync()
    {
        Log("  Trying Virtual WiFi Adapter...");
        
        try
        {
            // Check if we can enable Microsoft Virtual WiFi Miniport
            var checkResult = await RunCommandAsync("netsh", "wlan show drivers");
            
            // Even if "Hosted network supported: No", we can try to force it
            // by enabling the virtual adapter
            
            // Try to find and enable virtual adapter
            var adapters = await RunCommandAsync("powershell", @"
                Get-NetAdapter -IncludeHidden | 
                Where-Object { $_.InterfaceDescription -like '*Virtual*' -or 
                               $_.InterfaceDescription -like '*Microsoft Hosted*' -or
                               $_.InterfaceDescription -like '*Wi-Fi Direct*' } | 
                Select-Object -Property Name, InterfaceDescription, Status
            ");
            
            Log($"    Found adapters: {adapters.Replace("\n", " ").Trim()}");
            
            // Try to enable any virtual WiFi adapter
            await RunCommandAsync("powershell", @"
                Get-NetAdapter -IncludeHidden | 
                Where-Object { $_.InterfaceDescription -like '*Virtual*' -or 
                               $_.InterfaceDescription -like '*Hosted*' } | 
                Enable-NetAdapter -Confirm:$false 2>$null
            ");
            
            // Configure and start hosted network
            var setResult = await RunCommandAsync("netsh", 
                $"wlan set hostednetwork mode=allow ssid=\"{_ssid}\" key=\"{_password}\"");
            
            if (setResult.Contains("successfully") || setResult.Contains("changed"))
            {
                var startResult = await RunCommandAsync("netsh", "wlan start hostednetwork");
                
                if (startResult.ContainsAny("started", "已启动", "démarré"))
                {
                    _isRunning = true;
                    OnStatusChange?.Invoke(true, "Virtual WiFi Adapter");
                    await ConfigureDHCPAsync();
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Log($"    Virtual WiFi failed: {ex.Message}");
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          METHOD 2: FORCE HOSTED NETWORK
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> TryForceHostedNetworkAsync()
    {
        Log("  Trying Force Hosted Network via registry...");
        
        try
        {
            // Get WiFi adapter GUID
            var adapterGuid = await GetWiFiAdapterGuidAsync();
            if (string.IsNullOrEmpty(adapterGuid))
            {
                Log("    Could not find WiFi adapter GUID");
                return false;
            }
            
            Log($"    WiFi Adapter GUID: {adapterGuid}");
            
            // Try to enable hosted network support via registry
            // WARNING: This modifies driver settings
            var regPath = $@"SYSTEM\CurrentControlSet\Services\Wlansvc\Parameters\HostedNetworkSettings";
            
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(regPath);
                if (key != null)
                {
                    // Set hosted network to enabled
                    key.SetValue("HostedNetworkEnabled", 1, RegistryValueKind.DWord);
                    Log("    Registry: Enabled HostedNetwork flag");
                }
            }
            catch (Exception regEx)
            {
                Log($"    Registry modification failed (need admin): {regEx.Message}");
            }
            
            // Restart WLAN service to apply
            await RunCommandAsync("net", "stop wlansvc");
            await Task.Delay(1000);
            await RunCommandAsync("net", "start wlansvc");
            await Task.Delay(2000);
            
            // Now try hosted network again
            await RunCommandAsync("netsh", 
                $"wlan set hostednetwork mode=allow ssid=\"{_ssid}\" key=\"{_password}\"");
            
            var result = await RunCommandAsync("netsh", "wlan start hostednetwork");
            
            if (result.ContainsAny("started", "已启动"))
            {
                _isRunning = true;
                OnStatusChange?.Invoke(true, "Forced Hosted Network");
                await ConfigureDHCPAsync();
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Log($"    Force hosted network failed: {ex.Message}");
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          METHOD 3: WIFI DIRECT SOFTAP
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> TryWiFiDirectSoftAPAsync()
    {
        Log("  Trying WiFi Direct SoftAP (Autonomous Group Owner)...");
        
        try
        {
            // WiFi Direct can create an Autonomous Group Owner (AGO) that acts like an AP
            // This is different from Mobile Hotspot - it's a local network only
            
            var script = $@"
# Load Windows Runtime assemblies
Add-Type -AssemblyName System.Runtime.WindowsRuntime

# Helper function to await WinRT async operations
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | 
    Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and 
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]

Function Await($WinRtTask, $ResultType) {{
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}}

try {{
    # Load WiFi Direct types
    [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher, Windows.Devices.WiFiDirect, ContentType=WindowsRuntime] | Out-Null
    [Windows.Devices.WiFiDirect.WiFiDirectConnectionListener, Windows.Devices.WiFiDirect, ContentType=WindowsRuntime] | Out-Null
    
    # Create publisher for WiFi Direct advertisement
    $publisher = New-Object Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher
    
    # Configure as Autonomous Group Owner (acts like AP)
    $publisher.Advertisement.IsAutonomousGroupOwnerEnabled = $true
    $publisher.Advertisement.ListenStateDiscoverability = [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementListenStateDiscoverability]::Normal
    
    # Set legacy mode to allow non-WiFi Direct devices to connect
    $legacySettings = $publisher.Advertisement.LegacySettings
    $legacySettings.IsEnabled = $true
    $legacySettings.Ssid = '{_ssid}'
    
    # Set passphrase
    $passphrase = [Windows.Security.Credentials.PasswordCredential]::new()
    $passphrase.Password = '{_password}'
    $legacySettings.Passphrase = $passphrase
    
    # Start advertising
    $publisher.Start()
    
    Start-Sleep -Seconds 2
    
    if ($publisher.Status -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {{
        Write-Output 'SUCCESS'
        Write-Output 'SSID: {_ssid}'
        # Keep running
        while ($true) {{ Start-Sleep -Seconds 60 }}
    }} else {{
        Write-Output ""FAILED: $($publisher.Status)""
    }}
}} catch {{
    Write-Output ""ERROR: $($_.Exception.Message)""
}}
";
            
            // Run PowerShell script in background
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-ExecutionPolicy Bypass -Command \"{script.Replace("\"", "`\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            _apProcess = Process.Start(psi);
            
            // Wait for output
            await Task.Delay(4000);
            
            if (_apProcess != null && !_apProcess.HasExited)
            {
                // Try to read output
                var output = await _apProcess.StandardOutput.ReadLineAsync() ?? "";
                
                if (output.Contains("SUCCESS"))
                {
                    _isRunning = true;
                    OnStatusChange?.Invoke(true, "WiFi Direct SoftAP");
                    Log($"    ✓ SSID broadcasting: {_ssid}");
                    return true;
                }
            }
            
            // Check if process exited with error
            if (_apProcess?.HasExited == true)
            {
                var error = await _apProcess.StandardOutput.ReadToEndAsync();
                Log($"    WiFi Direct SoftAP output: {error.Trim()}");
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Log($"    WiFi Direct SoftAP failed: {ex.Message}");
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          METHOD 4: CREATE-AP TOOL
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task<bool> TryCreateAPToolAsync()
    {
        Log("  Trying alternative AP creation methods...");
        
        try
        {
            // Check if we have any external tools
            // For now, try netsh with different parameters
            
            // Some drivers respond to different parameters
            var commands = new[]
            {
                $"wlan set hostednetwork mode=allow ssid=\"{_ssid}\" key=\"{_password}\" keyUsage=persistent",
                $"wlan set hostednetwork mode=allow ssid=\"{_ssid}\" key=\"{_password}\""
            };
            
            foreach (var cmd in commands)
            {
                await RunCommandAsync("netsh", cmd);
                var result = await RunCommandAsync("netsh", "wlan start hostednetwork");
                
                if (result.ContainsAny("started", "已启动", "The hosted network started"))
                {
                    _isRunning = true;
                    OnStatusChange?.Invoke(true, "Hosted Network (Alternative)");
                    await ConfigureDHCPAsync();
                    return true;
                }
            }
            
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task ConfigureDHCPAsync()
    {
        Log("  Configuring DHCP for connected clients...");
        
        try
        {
            // Get the hosted network adapter
            var hostedAdapter = await RunCommandAsync("powershell", @"
                (Get-NetAdapter | Where-Object { 
                    $_.InterfaceDescription -like '*Microsoft Hosted*' -or 
                    $_.InterfaceDescription -like '*Virtual WiFi*' -or
                    $_.Name -like '*Local*'
                } | Select-Object -First 1).Name
            ");
            
            hostedAdapter = hostedAdapter.Trim();
            
            if (!string.IsNullOrEmpty(hostedAdapter))
            {
                // Set static IP for the hosted network adapter
                await RunCommandAsync("netsh", 
                    $"interface ip set address \"{hostedAdapter}\" static 192.168.137.1 255.255.255.0");
                
                Log($"    Set IP 192.168.137.1 on {hostedAdapter}");
            }
            
            // Windows ICS can provide DHCP - try to enable it
            await TryEnableICSAsync();
        }
        catch (Exception ex)
        {
            Log($"    DHCP config warning: {ex.Message}");
        }
    }
    
    private async Task TryEnableICSAsync()
    {
        try
        {
            // Get internet-connected adapter
            var internetAdapter = await RunCommandAsync("powershell", @"
                (Get-NetRoute -DestinationPrefix '0.0.0.0/0' | 
                 Get-NetAdapter | 
                 Where-Object { $_.Status -eq 'Up' } | 
                 Select-Object -First 1).Name
            ");
            
            internetAdapter = internetAdapter.Trim();
            
            if (!string.IsNullOrEmpty(internetAdapter))
            {
                Log($"    Will share internet from: {internetAdapter}");
                
                // Enable ICS via netsh (Windows feature)
                // Note: This may require additional permissions
                await RunCommandAsync("netsh", 
                    $"routing ip nat install");
                await RunCommandAsync("netsh", 
                    $"routing ip nat add interface \"{internetAdapter}\" full");
            }
        }
        catch
        {
            // ICS may not be available, clients can still connect
        }
    }
    
    private async Task<string> GetWiFiAdapterGuidAsync()
    {
        var result = await RunCommandAsync("powershell", @"
            (Get-NetAdapter | Where-Object { 
                $_.InterfaceDescription -like '*Wi-Fi*' -or 
                $_.InterfaceDescription -like '*Wireless*' 
            } | Select-Object -First 1).InterfaceGuid
        ");
        
        return result.Trim().Replace("{", "").Replace("}", "");
    }
    
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
    
    private void Log(string message)
    {
        Console.WriteLine(message);
        OnLog?.Invoke(message);
    }
}

/// <summary>
/// Extension method for string contains any.
/// </summary>
public static class StringExtensions
{
    public static bool ContainsAny(this string s, params string[] values)
    {
        foreach (var v in values)
        {
            if (s.Contains(v, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
