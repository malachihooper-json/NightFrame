# Open WiFi Direct Network - No Password
Add-Type -AssemblyName System.Runtime.WindowsRuntime

try {
    [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher, Windows.Devices.WiFiDirect, ContentType = WindowsRuntime] | Out-Null
    
    $publisher = New-Object Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher
    $publisher.Advertisement.IsAutonomousGroupOwnerEnabled = $true
    $publisher.Advertisement.ListenStateDiscoverability = [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementListenStateDiscoverability]::Normal
    
    $legacySettings = $publisher.Advertisement.LegacySettings
    $legacySettings.IsEnabled = $true
    $legacySettings.Ssid = 'NFRAME Global Internet'
    # No passphrase = open network
    
    Write-Host "Starting OPEN WiFi Direct network..."
    $publisher.Start()
    Start-Sleep -Seconds 3
    
    $status = $publisher.Status
    Write-Host "Status: $status"
    
    if ($status -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {
        Write-Host "SUCCESS! Open network active: NFRAME Global Internet"
        Write-Host "NO PASSWORD - Just connect!"
        Write-Host "Running for 3 minutes..."
        Start-Sleep -Seconds 180
        $publisher.Stop()
        Write-Host "Stopped"
    }
    else {
        Write-Host "FAILED: Status = $status"
    }
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
