# NFRAME Global Internet - Platform SSID Broadcaster
# Uses WiFi Direct with password embedded in SSID name

$ErrorActionPreference = 'Stop'

# SSID with password visible in name
$SSID = 'NFRAME Global Internet pw:00000000'
$Password = '00000000'

Write-Host ""
Write-Host "==========================================="
Write-Host "  NFRAME SSID BROADCASTER"
Write-Host "==========================================="
Write-Host "  SSID: $SSID"
Write-Host "  Password: $Password (visible in SSID!)"
Write-Host "==========================================="
Write-Host ""

try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime
    
    [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher, Windows.Devices.WiFiDirect, ContentType = WindowsRuntime] | Out-Null
    [Windows.Security.Credentials.PasswordCredential, Windows.Security.Credentials, ContentType = WindowsRuntime] | Out-Null
    
    $publisher = New-Object Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher
    $publisher.Advertisement.IsAutonomousGroupOwnerEnabled = $true
    $publisher.Advertisement.ListenStateDiscoverability = [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementListenStateDiscoverability]::Normal
    
    $legacySettings = $publisher.Advertisement.LegacySettings
    $legacySettings.IsEnabled = $true
    $legacySettings.Ssid = $SSID
    
    $credential = New-Object Windows.Security.Credentials.PasswordCredential
    $credential.Password = $Password
    $legacySettings.Passphrase = $credential
    
    Write-Host "Starting WiFi Direct..."
    $publisher.Start()
    Start-Sleep -Seconds 3
    
    $status = $publisher.Status
    Write-Host "Status: $status"
    
    if ($status -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {
        Write-Host ""
        Write-Host "SUCCESS! Network is broadcasting!"
        Write-Host ""
        Write-Host "On your phone:"
        Write-Host "  1. Open WiFi settings"
        Write-Host "  2. Find: $SSID"
        Write-Host "  3. Enter password: $Password"
        Write-Host ""
        Write-Host "Press Ctrl+C to stop..."
        
        while ($true) {
            Start-Sleep -Seconds 60
            if ($publisher.Status -ne [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {
                Write-Host "AP stopped unexpectedly"
                break
            }
        }
    }
    else {
        Write-Host "FAILED: Status = $status"
    }
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
