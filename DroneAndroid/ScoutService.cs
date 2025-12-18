/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    ANDROID SCOUT SERVICE                                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Background service for Wi-Fi scanning, AP discovery, and propagation.    ║
 * ║  Runs continuously, even when app is closed.                               ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Android.App;
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using Android.Locations;
using AndroidX.Core.App;

namespace NIGHTFRAME.Drone.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeDataSync)]
public class ScoutService : Service
{
    private const int NotificationId = 1001;
    private const string ChannelId = "nframe_scout";
    
    private WifiManager? _wifiManager;
    private LocationManager? _locationManager;
    private Timer? _scanTimer;
    private bool _isRunning = false;
    
    // Scout settings
    private const int ScanIntervalMinutes = 5;
    private const float MinBatteryForHotspot = 0.5f;
    
    public override IBinder? OnBind(Intent? intent) => null;
    
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        StartForeground(NotificationId, CreateNotification());
        
        if (!_isRunning)
        {
            _isRunning = true;
            InitializeScout();
            StartScanning();
        }
        
        return StartCommandResult.Sticky;
    }
    
    public override void OnDestroy()
    {
        _isRunning = false;
        _scanTimer?.Dispose();
        base.OnDestroy();
    }
    
    private void InitializeScout()
    {
        _wifiManager = (WifiManager?)GetSystemService(WifiService);
        _locationManager = (LocationManager?)GetSystemService(LocationService);
        
        Console.WriteLine("◈ Android Scout initialized");
    }
    
    private void StartScanning()
    {
        _scanTimer = new Timer(async _ => await PerformScanAsync(), null, 
            TimeSpan.Zero, 
            TimeSpan.FromMinutes(ScanIntervalMinutes));
    }
    
    private async Task PerformScanAsync()
    {
        if (!_isRunning) return;
        
        Console.WriteLine("◎ Performing Wi-Fi scan...");
        
        try
        {
            // Get current location
            var location = await GetCurrentLocationAsync();
            
            // Scan for Wi-Fi networks
            var networks = ScanWifiNetworks();
            
            foreach (var network in networks)
            {
                // Check for NFRAME mesh nodes
                if (network.Ssid?.StartsWith("NFRAME") == true)
                {
                    Console.WriteLine($"◈ Found mesh node: {network.Ssid}");
                    ReportMeshNode(network, location);
                    continue;
                }
                
                // Check if network has internet (for gateway detection)
                // Note: Can't connect and test without user permission
            }
            
            // Check if we should start propagation mode
            await CheckPropagationModeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Scan error: {ex.Message}");
        }
    }
    
    private List<ScanResult> ScanWifiNetworks()
    {
        if (_wifiManager == null) return new List<ScanResult>();
        
        _wifiManager.StartScan();
        return _wifiManager.ScanResults?.ToList() ?? new List<ScanResult>();
    }
    
    private async Task<Location?> GetCurrentLocationAsync()
    {
        if (_locationManager == null) return null;
        
        try
        {
            var providers = _locationManager.GetProviders(true);
            foreach (var provider in providers)
            {
                var location = _locationManager.GetLastKnownLocation(provider);
                if (location != null) return location;
            }
        }
        catch { }
        
        return null;
    }
    
    private void ReportMeshNode(ScanResult network, Location? location)
    {
        // TODO: Report to orchestrator
        var report = new
        {
            Ssid = network.Ssid,
            Bssid = network.Bssid,
            SignalStrength = network.Level,
            Latitude = location?.Latitude,
            Longitude = location?.Longitude,
            Timestamp = DateTime.UtcNow
        };
        
        Console.WriteLine($"◎ Mesh node report: {report.Ssid} @ ({report.Latitude}, {report.Longitude})");
    }
    
    private async Task CheckPropagationModeAsync()
    {
        // Check battery level
        var batteryManager = (BatteryManager?)GetSystemService(BatteryService);
        var batteryLevel = batteryManager?.GetIntProperty(BatteryPropertyId.Capacity) ?? 0;
        
        if (batteryLevel / 100.0f >= MinBatteryForHotspot)
        {
            Console.WriteLine($"◎ Battery {batteryLevel}% - eligible for propagation mode");
            // TODO: Start hotspot if configured
        }
    }
    
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "NFRAME Scout",
                NotificationImportance.Low)
            {
                Description = "Background mesh network scanning"
            };
            
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }
    
    private Notification CreateNotification()
    {
        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("NFRAME Scout Active")
            .SetContentText("Scanning for mesh networks...")
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow);
        
        return builder.Build();
    }
}

/// <summary>
/// Starts scout services on device boot.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { Intent.ActionBootCompleted, "android.intent.action.QUICKBOOT_POWERON" })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action == Intent.ActionBootCompleted || 
            intent?.Action == "android.intent.action.QUICKBOOT_POWERON")
        {
            Console.WriteLine("◈ Boot completed - starting Scout services");
            
            // Start Wi-Fi Scout
            var wifiServiceIntent = new Intent(context, typeof(ScoutService));
            
            // Start Cellular Scout  
            var cellularServiceIntent = new Intent(context, typeof(Cellular.CellularScoutService));
            
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context?.StartForegroundService(wifiServiceIntent);
                context?.StartForegroundService(cellularServiceIntent);
            }
            else
            {
                context?.StartService(wifiServiceIntent);
                context?.StartService(cellularServiceIntent);
            }
        }
    }
}
