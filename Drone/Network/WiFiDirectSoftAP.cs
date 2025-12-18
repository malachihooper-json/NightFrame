/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              WIFI DIRECT SOFTAP - FORCED SSID BROADCAST                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Creates a visible WiFi network using WiFi Direct Legacy Mode.             ║
 * ║  Works on Intel WiFi 6 and similar adapters that don't support Hosted Net. ║
 * ║  NO cellular required. NO user intervention. Just works.                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Creates a WiFi Direct SoftAP that appears as a normal WiFi network.
/// This bypasses the "Hosted Network Not Supported" limitation.
/// </summary>
public class WiFiDirectSoftAP
{
    private readonly string _ssid;
    private readonly string _passphrase;
    private Process? _apProcess;
    private bool _isRunning;
    
    public bool IsRunning => _isRunning;
    public string SSID => _ssid;
    public string Password => _passphrase;
    
    public WiFiDirectSoftAP(string ssid = "NFRAME Global Internet", string passphrase = "nframe123")
    {
        _ssid = ssid;
        // Passphrase must be 8-63 characters for WPA2
        _passphrase = passphrase.Length >= 8 ? passphrase : "nframe123";
    }
    
    /// <summary>
    /// Starts the WiFi Direct SoftAP. This will create a visible WiFi network.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("WiFi Direct SoftAP is Windows-only");
            return false;
        }
        
        Console.WriteLine("◈ Starting WiFi Direct SoftAP...");
        Console.WriteLine($"  SSID: {_ssid}");
        Console.WriteLine($"  Password: {_passphrase}");
        
        // Create the PowerShell script that runs the WiFi Direct AP
        var script = CreateWiFiDirectScript();
        
        // Save script to temp file
        var scriptPath = Path.Combine(Path.GetTempPath(), "nframe_wifidirect.ps1");
        await File.WriteAllTextAsync(scriptPath, script);
        
        // Start PowerShell with the script (running indefinitely to keep AP alive)
        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        try
        {
            _apProcess = Process.Start(psi);
            
            if (_apProcess == null)
            {
                Console.WriteLine("  ✗ Failed to start PowerShell process");
                return false;
            }
            
            // Wait for initialization
            Console.WriteLine("  Initializing WiFi Direct...");
            await Task.Delay(5000);
            
            // Check if process is still running (good sign)
            if (!_apProcess.HasExited)
            {
                // Read any output
                var output = "";
                if (_apProcess.StandardOutput.Peek() > -1)
                {
                    output = await _apProcess.StandardOutput.ReadLineAsync() ?? "";
                }
                
                if (output.Contains("FAILED") || output.Contains("ERROR"))
                {
                    Console.WriteLine($"  ✗ {output}");
                    return false;
                }
                
                _isRunning = true;
                Console.WriteLine("  ✓ WiFi Direct SoftAP started!");
                Console.WriteLine($"  ✓ Network visible as: {_ssid}");
                Console.WriteLine($"  ✓ Connect with password: {_passphrase}");
                return true;
            }
            else
            {
                // Process exited - read error
                var errorOutput = await _apProcess.StandardError.ReadToEndAsync();
                var stdOutput = await _apProcess.StandardOutput.ReadToEndAsync();
                Console.WriteLine($"  ✗ Process exited: {stdOutput} {errorOutput}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Exception: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Stops the WiFi Direct SoftAP.
    /// </summary>
    public async Task StopAsync()
    {
        if (_apProcess != null && !_apProcess.HasExited)
        {
            _apProcess.Kill();
            await _apProcess.WaitForExitAsync();
        }
        _isRunning = false;
        Console.WriteLine("◎ WiFi Direct SoftAP stopped");
    }
    
    private string CreateWiFiDirectScript()
    {
        return $@"
# WiFi Direct Autonomous Group Owner (SoftAP) Script
# Creates a visible WiFi network using WiFi Direct Legacy Mode

$ErrorActionPreference = 'Stop'

function Write-Log($msg) {{
    Write-Output $msg
    [Console]::Out.Flush()
}}

try {{
    Write-Log 'Loading Windows Runtime assemblies...'
    
    # Load required WinRT types
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    
    # Define async helper
    $null = [Windows.Foundation.IAsyncOperation`1, Windows.Foundation, ContentType=WindowsRuntime]
    
    $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | 
        Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and 
        $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]
    
    function Await($WinRtTask, $ResultType) {{
        $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
        $netTask = $asTask.Invoke($null, @($WinRtTask))
        $netTask.Wait(-1) | Out-Null
        $netTask.Result
    }}
    
    function AwaitAction($WinRtAction) {{
        $asTask = [System.WindowsRuntimeSystemExtensions].GetMethods() | 
            Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncAction' }}
        $netTask = $asTask.Invoke($null, @($WinRtAction))
        $netTask.Wait(-1) | Out-Null
    }}

    # Load WiFi Direct types
    Write-Log 'Loading WiFi Direct types...'
    [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher, Windows.Devices.WiFiDirect, ContentType=WindowsRuntime] | Out-Null
    [Windows.Devices.WiFiDirect.WiFiDirectConnectionListener, Windows.Devices.WiFiDirect, ContentType=WindowsRuntime] | Out-Null
    [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementListenStateDiscoverability, Windows.Devices.WiFiDirect, ContentType=WindowsRuntime] | Out-Null
    [Windows.Security.Credentials.PasswordCredential, Windows.Security.Credentials, ContentType=WindowsRuntime] | Out-Null
    
    # Create the publisher
    Write-Log 'Creating WiFi Direct publisher...'
    $publisher = New-Object Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher
    
    # Configure as Autonomous Group Owner (this makes it act like an AP)
    $publisher.Advertisement.IsAutonomousGroupOwnerEnabled = $true
    
    # Set discoverability to Normal so all devices can see it
    $publisher.Advertisement.ListenStateDiscoverability = [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementListenStateDiscoverability]::Normal
    
    # CRITICAL: Enable Legacy Settings - this makes it visible to non-WiFi-Direct devices!
    Write-Log 'Configuring Legacy Settings (visible to all devices)...'
    $legacySettings = $publisher.Advertisement.LegacySettings
    $legacySettings.IsEnabled = $true
    $legacySettings.Ssid = '{_ssid}'
    
    # Set passphrase
    $passphrase = New-Object Windows.Security.Credentials.PasswordCredential
    $passphrase.Password = '{_passphrase}'
    $legacySettings.Passphrase = $passphrase
    
    # Register status change handler
    $statusHandler = {{
        param($sender, $args)
        $status = $sender.Status
        Write-Log ""STATUS: $status""
        
        if ($status -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {{
            Write-Log 'SUCCESS: WiFi Direct AP is broadcasting!'
            Write-Log 'SSID: {_ssid}'
        }} elseif ($status -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Aborted) {{
            Write-Log ""FAILED: Publisher aborted - Error: $($args.Error)""
        }}
    }}
    
    Register-ObjectEvent -InputObject $publisher -EventName StatusChanged -Action $statusHandler | Out-Null
    
    # Start advertising!
    Write-Log 'Starting WiFi Direct advertisement...'
    $publisher.Start()
    
    # Wait a moment for status
    Start-Sleep -Seconds 3
    
    $currentStatus = $publisher.Status
    Write-Log ""Current status: $currentStatus""
    
    if ($currentStatus -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {{
        Write-Log 'SUCCESS'
        
        # Keep running to maintain the AP
        Write-Log 'AP is running. Press Ctrl+C to stop.'
        while ($true) {{
            Start-Sleep -Seconds 60
            # Heartbeat
            if ($publisher.Status -ne [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {{
                Write-Log ""AP stopped unexpectedly: $($publisher.Status)""
                break
            }}
        }}
    }} else {{
        Write-Log ""FAILED: Status is $currentStatus""
        
        # Try to get more info
        if ($currentStatus -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Aborted) {{
            Write-Log 'The WiFi adapter may not support WiFi Direct Group Owner mode.'
            Write-Log 'Try: Update WiFi drivers or use a different WiFi adapter.'
        }}
    }}
    
}} catch {{
    Write-Log ""ERROR: $($_.Exception.Message)""
    Write-Log $_.ScriptStackTrace
}}
";
    }
}
