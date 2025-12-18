# WiFi Direct with Simple Password
Add-Type -AssemblyName System.Runtime.WindowsRuntime

try {
    [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher, Windows.Devices.WiFiDirect, ContentType = WindowsRuntime] | Out-Null
    [Windows.Security.Credentials.PasswordCredential, Windows.Security.Credentials, ContentType = WindowsRuntime] | Out-Null
    
    $publisher = New-Object Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisher
    $publisher.Advertisement.IsAutonomousGroupOwnerEnabled = $true
    $publisher.Advertisement.ListenStateDiscoverability = [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementListenStateDiscoverability]::Normal
    
    $legacySettings = $publisher.Advertisement.LegacySettings
    $legacySettings.IsEnabled = $true
    $legacySettings.Ssid = 'NFRAME Global Internet'
    
    # Set simple password - required for WiFi Direct
    $password = '12345678'
    $credential = New-Object Windows.Security.Credentials.PasswordCredential
    $credential.Password = $password
    $legacySettings.Passphrase = $credential
    
    Write-Host ""
    Write-Host "==========================================="
    Write-Host "  NFRAME Global Internet"
    Write-Host "==========================================="
    Write-Host "  Password: $password"
    Write-Host "==========================================="
    Write-Host ""
    Write-Host "Starting..."
    
    $publisher.Start()
    Start-Sleep -Seconds 3
    
    $status = $publisher.Status
    Write-Host "Status: $status"
    
    if ($status -eq [Windows.Devices.WiFiDirect.WiFiDirectAdvertisementPublisherStatus]::Started) {
        Write-Host ""
        Write-Host "SUCCESS! Connect with password: $password"
        Write-Host ""
        Write-Host "Running for 3 minutes... Press Ctrl+C to stop."
        Start-Sleep -Seconds 180
        $publisher.Stop()
    }
    else {
        Write-Host "FAILED: $status"
    }
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
}
