/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║           CELLULAR SCOUT SERVICE - ANDROID BACKGROUND SERVICE              ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Background service for cellular signal collection, handover prediction,  ║
 * ║  and RF fingerprinting data gathering.                                    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using NIGHTFRAME.Drone.Android.Cellular;

namespace NIGHTFRAME.Drone.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeDataSync)]
public class CellularScoutService : Service
{
    private const int NotificationId = 1002;
    private const string ChannelId = "nframe_cellular_scout";
    
    private AndroidCellularProvider? _cellularProvider;
    private bool _isRunning = false;
    
    // Data collection state
    private readonly List<CellMeasurementRecord> _measurementHistory = new();
    private const int MaxHistorySize = 10000;
    private Timer? _reportTimer;
    
    // Configuration
    public int MeasurementIntervalMs { get; set; } = 1000;
    public int ReportIntervalMinutes { get; set; } = 5;
    public bool CollectForTraining { get; set; } = true;
    
    public override IBinder? OnBind(Intent? intent) => new CellularServiceBinder(this);
    
    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        CreateNotificationChannel();
        StartForeground(NotificationId, CreateNotification("Initializing..."));
        
        if (!_isRunning)
        {
            _isRunning = true;
            _ = InitializeAsync();
        }
        
        return StartCommandResult.Sticky;
    }
    
    public override void OnDestroy()
    {
        _isRunning = false;
        _cellularProvider?.Dispose();
        _reportTimer?.Dispose();
        base.OnDestroy();
    }
    
    private async Task InitializeAsync()
    {
        Console.WriteLine("◈ CellularScoutService initializing...");
        
        _cellularProvider = new AndroidCellularProvider(this)
        {
            PollingIntervalMs = MeasurementIntervalMs
        };
        
        // Subscribe to events
        _cellularProvider.OnMeasurement += OnCellMeasurement;
        _cellularProvider.OnQualityChanged += OnQualityChanged;
        _cellularProvider.OnHandoverRecommended += OnHandoverRecommended;
        _cellularProvider.OnLocationUpdated += OnLocationUpdated;
        
        // Start cellular monitoring
        var started = await _cellularProvider.StartAsync();
        
        if (started)
        {
            UpdateNotification($"Active - {_cellularProvider.NetworkOperator}");
            
            // Start periodic reporting
            _reportTimer = new Timer(
                _ => ReportToOrchestrator(),
                null,
                TimeSpan.FromMinutes(ReportIntervalMinutes),
                TimeSpan.FromMinutes(ReportIntervalMinutes));
            
            Console.WriteLine("◈ CellularScoutService running");
        }
        else
        {
            UpdateNotification("Failed to start - check permissions");
            Console.WriteLine("∴ CellularScoutService failed to start");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private void OnCellMeasurement(AndroidCellMeasurement measurement)
    {
        // Store for training data collection
        if (CollectForTraining)
        {
            var record = new CellMeasurementRecord
            {
                Measurement = measurement,
                Location = _cellularProvider?.LastLocation
            };
            
            lock (_measurementHistory)
            {
                _measurementHistory.Add(record);
                
                // Trim old data
                if (_measurementHistory.Count > MaxHistorySize)
                {
                    _measurementHistory.RemoveRange(0, _measurementHistory.Count - MaxHistorySize);
                }
            }
        }
        
        // Update notification with current signal
        var quality = _cellularProvider?.CurrentQuality;
        if (quality != null)
        {
            var bars = quality.Level;
            var tech = measurement.Technology;
            UpdateNotification($"{tech} | {bars}/4 bars | {measurement.RSRP} dBm");
        }
    }
    
    private void OnQualityChanged(AndroidSignalQuality quality)
    {
        // Log significant quality changes
        Console.WriteLine($"◎ Signal: {quality.Strength} ({quality.Score:F0}%) - {quality.EstimatedThroughputMbps:F1} Mbps");
    }
    
    private void OnHandoverRecommended(HandoverRecommendation recommendation)
    {
        Console.WriteLine($"◈ Handover recommended: {recommendation.Reason}");
        
        if (recommendation.Urgent)
        {
            // Show urgent notification
            ShowHandoverNotification(recommendation);
        }
    }
    
    private void OnLocationUpdated(GeoLocation location)
    {
        Console.WriteLine($"◎ Location: {location.Latitude:F6}, {location.Longitude:F6} " +
                         $"(±{location.Accuracy:F0}m, {location.Source})");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              ORCHESTRATOR REPORTING
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private void ReportToOrchestrator()
    {
        if (_cellularProvider == null) return;
        
        try
        {
            var report = _cellularProvider.GetStateReport();
            
            // Add training data summary
            int trainingDataPoints;
            lock (_measurementHistory)
            {
                trainingDataPoints = _measurementHistory.Count;
            }
            
            Console.WriteLine($"◎ Reporting to Orchestrator:");
            Console.WriteLine($"   Operator: {report.NetworkOperator}");
            Console.WriteLine($"   Technology: {report.CurrentMeasurement?.Technology ?? "Unknown"}");
            Console.WriteLine($"   Signal: {report.SignalQuality?.Strength ?? SignalStrength.NoSignal}");
            Console.WriteLine($"   Training data points: {trainingDataPoints}");
            Console.WriteLine($"   Neighbors: {report.NeighborCount}");
            
            // TODO: Send to Orchestrator via gRPC
            // This would use the DroneCore to send a CellularStatusUpdate message
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Report failed: {ex.Message}");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              TRAINING DATA EXPORT
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Exports collected training data for RF fingerprinting model training.
    /// </summary>
    public TrainingDataExport ExportTrainingData()
    {
        lock (_measurementHistory)
        {
            return new TrainingDataExport
            {
                DeviceInfo = new DeviceInfo
                {
                    Manufacturer = Build.Manufacturer ?? "",
                    Model = Build.Model ?? "",
                    SdkVersion = (int)Build.VERSION.SdkInt,
                    Is5GCapable = _cellularProvider?.Is5GCapable ?? false
                },
                Records = _measurementHistory.ToList(),
                ExportTime = DateTime.UtcNow
            };
        }
    }
    
    /// <summary>
    /// Clears collected training data after export.
    /// </summary>
    public void ClearTrainingData()
    {
        lock (_measurementHistory)
        {
            _measurementHistory.Clear();
        }
        Console.WriteLine("◎ Training data cleared");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              NOTIFICATIONS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "Cellular Scout",
                NotificationImportance.Low)
            {
                Description = "Cellular signal monitoring and collection"
            };
            
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }
    
    private Notification CreateNotification(string text)
    {
        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("NFRAME Cellular Scout")
            .SetContentText(text)
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow);
        
        return builder.Build();
    }
    
    private void UpdateNotification(string text)
    {
        var notification = CreateNotification(text);
        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(NotificationId, notification);
    }
    
    private void ShowHandoverNotification(HandoverRecommendation recommendation)
    {
        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("Signal Handover Recommended")
            .SetContentText(recommendation.Reason)
            .SetSmallIcon(Resource.Drawable.ic_notification)
            .SetPriority(NotificationCompat.PriorityDefault)
            .SetAutoCancel(true);
        
        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(NotificationId + 100, builder.Build());
    }
}

/// <summary>
/// Binder for local binding to service.
/// </summary>
public class CellularServiceBinder : Binder
{
    public CellularScoutService Service { get; }
    
    public CellularServiceBinder(CellularScoutService service)
    {
        Service = service;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

public class CellMeasurementRecord
{
    public AndroidCellMeasurement Measurement { get; set; } = new();
    public GeoLocation? Location { get; set; }
}

public class TrainingDataExport
{
    public DeviceInfo DeviceInfo { get; set; } = new();
    public List<CellMeasurementRecord> Records { get; set; } = new();
    public DateTime ExportTime { get; set; }
}

public class DeviceInfo
{
    public string Manufacturer { get; set; } = "";
    public string Model { get; set; } = "";
    public int SdkVersion { get; set; }
    public bool Is5GCapable { get; set; }
}
