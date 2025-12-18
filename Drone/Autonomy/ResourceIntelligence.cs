/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              RESOURCE INTELLIGENCE - BACKEND RESOURCE MANAGEMENT           ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Provides intimate and specific resource information for autonomous ops.   ║
 * ║  Monitors, allocates, and optimizes system resources across the mesh.      ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NIGHTFRAME.Drone.Hardware;
using NIGHTFRAME.Drone.Cellular;

namespace NIGHTFRAME.Drone.Autonomy;

/// <summary>
/// Resource Intelligence - provides detailed resource insights for autonomous decision-making.
/// </summary>
public class ResourceIntelligence : IDisposable
{
    private readonly Timer? _monitorTimer;
    private readonly StealthController _stealth;
    private CellularIntelligence? _cellular;
    
    // Current resource state
    public ResourceSnapshot CurrentSnapshot { get; private set; } = new();
    public ResourceTrends Trends { get; private set; } = new();
    public List<ResourceSnapshot> History { get; } = new();
    
    // Events
    public event Action<ResourceSnapshot>? OnResourceUpdate;
    public event Action<ResourceAlert>? OnResourceAlert;
    public event Action<ResourceOpportunity>? OnOpportunityDetected;
    
    // Configuration
    public int MonitorIntervalMs { get; set; } = 5000;
    public int HistoryRetentionMinutes { get; set; } = 60;
    
    public ResourceIntelligence(StealthController stealth, CellularIntelligence? cellular = null)
    {
        _stealth = stealth;
        _cellular = cellular;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public void Start()
    {
        Console.WriteLine("◈ Starting Resource Intelligence...");
        
        // Initial scan
        CurrentSnapshot = CaptureSnapshot();
        History.Add(CurrentSnapshot);
        
        // Start monitoring timer
        var timer = new Timer(
            _ => MonitorTick(),
            null,
            MonitorIntervalMs,
            MonitorIntervalMs);
    }
    
    public void Stop()
    {
        _monitorTimer?.Dispose();
        Console.WriteLine("◎ Resource Intelligence stopped");
    }
    
    private void MonitorTick()
    {
        try
        {
            var snapshot = CaptureSnapshot();
            
            // Update trends
            UpdateTrends(snapshot);
            
            // Check for alerts
            CheckAlerts(snapshot);
            
            // Check for opportunities
            CheckOpportunities(snapshot);
            
            // Store in history
            History.Add(snapshot);
            CurrentSnapshot = snapshot;
            
            // Trim old history
            var cutoff = DateTime.UtcNow.AddMinutes(-HistoryRetentionMinutes);
            History.RemoveAll(s => s.Timestamp < cutoff);
            
            OnResourceUpdate?.Invoke(snapshot);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Resource monitor error: {ex.Message}");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              SNAPSHOT CAPTURE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Captures comprehensive resource snapshot.
    /// </summary>
    public ResourceSnapshot CaptureSnapshot()
    {
        var snapshot = new ResourceSnapshot
        {
            Timestamp = DateTime.UtcNow,
            
            // CPU
            Cpu = CaptureCpuResources(),
            
            // Memory
            Memory = CaptureMemoryResources(),
            
            // Storage
            Storage = CaptureStorageResources(),
            
            // Network
            Network = CaptureNetworkResources(),
            
            // Cellular
            Cellular = CaptureCellularResources(),
            
            // Power
            Power = CapturePowerResources(),
            
            // GPU
            Gpu = CaptureGpuResources()
        };
        
        // Calculate overall health score
        snapshot.HealthScore = CalculateHealthScore(snapshot);
        snapshot.AutonomyCapability = AssessAutonomyCapability(snapshot);
        
        return snapshot;
    }
    
    private CpuResources CaptureCpuResources()
    {
        var cpuLoad = _stealth.CurrentCpuLoad;
        var processCount = Process.GetProcesses().Length;
        
        return new CpuResources
        {
            CoreCount = Environment.ProcessorCount,
            CurrentLoadPercent = cpuLoad,
            AvailablePercent = 100 - cpuLoad,
            ProcessCount = processCount,
            IdleThrottled = cpuLoad < 5,
            ThermalState = EstimateThermalState(cpuLoad)
        };
    }
    
    private MemoryResources CaptureMemoryResources()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        
        return new MemoryResources
        {
            TotalMb = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024),
            UsedMb = (gcInfo.TotalAvailableMemoryBytes - gcInfo.HighMemoryLoadThresholdBytes) / (1024 * 1024),
            AvailableMb = gcInfo.HighMemoryLoadThresholdBytes / (1024 * 1024),
            ProcessMemoryMb = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024),
            HighLoad = gcInfo.MemoryLoadBytes > gcInfo.HighMemoryLoadThresholdBytes
        };
    }
    
    private StorageResources CaptureStorageResources()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .ToList();
        
        var totalBytes = drives.Sum(d => d.TotalSize);
        var freeBytes = drives.Sum(d => d.AvailableFreeSpace);
        
        return new StorageResources
        {
            TotalMb = totalBytes / (1024 * 1024),
            AvailableMb = freeBytes / (1024 * 1024),
            UsedPercent = 100.0 * (totalBytes - freeBytes) / Math.Max(1, totalBytes),
            DriveCount = drives.Count,
            ModelCacheMb = GetModelCacheSize(),
            TrainingDataMb = GetTrainingDataSize()
        };
    }
    
    private NetworkResources CaptureNetworkResources()
    {
        var interfaces = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                        ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .ToList();
        
        var wifiInterface = interfaces.FirstOrDefault(ni => 
            ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
        var ethernetInterface = interfaces.FirstOrDefault(ni =>
            ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
        
        var totalBytesSent = interfaces.Sum(ni => ni.GetIPStatistics().BytesSent);
        var totalBytesReceived = interfaces.Sum(ni => ni.GetIPStatistics().BytesReceived);
        
        return new NetworkResources
        {
            InterfaceCount = interfaces.Count,
            WifiAvailable = wifiInterface != null,
            WifiInterface = wifiInterface?.Name,
            EthernetAvailable = ethernetInterface != null,
            EthernetInterface = ethernetInterface?.Name,
            BytesSent = totalBytesSent,
            BytesReceived = totalBytesReceived,
            InternetAvailable = CheckInternetConnectivity()
        };
    }
    
    private CellularResources? CaptureCellularResources()
    {
        if (_cellular == null || !_cellular.IsRunning)
            return null;
        
        var measurement = _cellular.LastMeasurement;
        var location = _cellular.LastLocation;
        var quality = _cellular.CurrentQuality;
        
        return new CellularResources
        {
            Available = true,
            Connected = _cellular.IsRunning,
            Technology = measurement?.RadioType ?? "Unknown",
            SignalStrength = quality?.Strength.ToString() ?? "Unknown",
            RSRP = measurement?.RSRP ?? 0,
            RSRQ = measurement?.RSRQ ?? 0,
            SINR = measurement?.SINR ?? 0,
            ServingCellId = measurement?.CellId ?? 0,
            NeighborCount = _cellular.NeighborCells?.Count ?? 0,
            LocationAvailable = location != null,
            Latitude = location?.Latitude,
            Longitude = location?.Longitude,
            LocationConfidence = location?.Confidence
        };
    }
    
    private PowerResources CapturePowerResources()
    {
        // Platform-specific power info
        var isOnBattery = false;
        var batteryPercent = 100.0;
        var estimatedMinutesRemaining = int.MaxValue;
        
        try
        {
            // This is Windows-specific
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Would use GetSystemPowerStatus
            }
        }
        catch { }
        
        return new PowerResources
        {
            OnBattery = isOnBattery,
            BatteryPercent = batteryPercent,
            EstimatedMinutesRemaining = estimatedMinutesRemaining,
            PowerSourceStable = !isOnBattery
        };
    }
    
    private GpuResources? CaptureGpuResources()
    {
        // Try to get GPU info
        var specs = HardwareAudit.Scan();
        
        if (!specs.HasGpu)
            return null;
        
        return new GpuResources
        {
            Available = true,
            Name = specs.GpuName ?? "Unknown",
            VramMb = specs.GpuVramMb,
            EstimatedFlops = specs.EstimatedFlops,
            ExecutionProvider = specs.ExecutionProvider
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              ANALYSIS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private void UpdateTrends(ResourceSnapshot current)
    {
        if (History.Count < 2) return;
        
        var previous = History[^1];
        var oldestRelevant = History.Count > 10 ? History[^10] : History[0];
        var timeSpan = (current.Timestamp - oldestRelevant.Timestamp).TotalMinutes;
        
        if (timeSpan < 1) return;
        
        Trends = new ResourceTrends
        {
            CpuTrendPercent = (current.Cpu.CurrentLoadPercent - oldestRelevant.Cpu.CurrentLoadPercent) / timeSpan,
            MemoryTrendMbPerMin = (current.Memory.UsedMb - oldestRelevant.Memory.UsedMb) / timeSpan,
            NetworkTrendBytesPerMin = (current.Network.BytesSent + current.Network.BytesReceived - 
                                      oldestRelevant.Network.BytesSent - oldestRelevant.Network.BytesReceived) / timeSpan,
            HealthTrend = (current.HealthScore - oldestRelevant.HealthScore) / timeSpan
        };
    }
    
    private void CheckAlerts(ResourceSnapshot snapshot)
    {
        var alerts = new List<ResourceAlert>();
        
        // CPU overload
        if (snapshot.Cpu.CurrentLoadPercent > 90)
        {
            alerts.Add(new ResourceAlert
            {
                Level = AlertLevel.Warning,
                Resource = "CPU",
                Message = $"High CPU load: {snapshot.Cpu.CurrentLoadPercent:F0}%",
                RecommendedAction = "Reduce compute intensity or wait for idle"
            });
        }
        
        // Memory pressure
        if (snapshot.Memory.AvailableMb < 512)
        {
            alerts.Add(new ResourceAlert
            {
                Level = AlertLevel.Warning,
                Resource = "Memory",
                Message = $"Low memory: {snapshot.Memory.AvailableMb}MB available",
                RecommendedAction = "Unload cached models"
            });
        }
        
        // Storage low
        if (snapshot.Storage.UsedPercent > 90)
        {
            alerts.Add(new ResourceAlert
            {
                Level = AlertLevel.Warning,
                Resource = "Storage",
                Message = $"Low disk space: {snapshot.Storage.AvailableMb / 1024:F1}GB free",
                RecommendedAction = "Clean training data cache"
            });
        }
        
        // Cellular signal weak
        if (snapshot.Cellular != null && snapshot.Cellular.RSRP < -110)
        {
            alerts.Add(new ResourceAlert
            {
                Level = AlertLevel.Info,
                Resource = "Cellular",
                Message = $"Weak cellular signal: {snapshot.Cellular.RSRP:F0} dBm",
                RecommendedAction = "Consider handover or location change"
            });
        }
        
        // Battery low
        if (snapshot.Power.OnBattery && snapshot.Power.BatteryPercent < 20)
        {
            alerts.Add(new ResourceAlert
            {
                Level = AlertLevel.Critical,
                Resource = "Power",
                Message = $"Low battery: {snapshot.Power.BatteryPercent:F0}%",
                RecommendedAction = "Reduce power consumption, prepare for shutdown"
            });
        }
        
        foreach (var alert in alerts)
        {
            OnResourceAlert?.Invoke(alert);
        }
    }
    
    private void CheckOpportunities(ResourceSnapshot snapshot)
    {
        var opportunities = new List<ResourceOpportunity>();
        
        // Idle compute available
        if (snapshot.Cpu.AvailablePercent > 80 && snapshot.Memory.AvailableMb > 2048)
        {
            opportunities.Add(new ResourceOpportunity
            {
                Type = OpportunityType.ComputeAvailable,
                Score = (float)snapshot.Cpu.AvailablePercent,
                Description = "High compute capacity available - can process larger shards",
                ExpiresIn = TimeSpan.FromMinutes(1) // May change quickly
            });
        }
        
        // Strong cellular signal
        if (snapshot.Cellular?.RSRP > -75)
        {
            opportunities.Add(new ResourceOpportunity
            {
                Type = OpportunityType.HighBandwidth,
                Score = 90,
                Description = $"Excellent cellular signal ({snapshot.Cellular.RSRP:F0} dBm) - ideal for data transfer",
                ExpiresIn = TimeSpan.FromMinutes(5)
            });
        }
        
        // GPU available
        if (snapshot.Gpu?.Available == true)
        {
            opportunities.Add(new ResourceOpportunity
            {
                Type = OpportunityType.GpuAvailable,
                Score = 100,
                Description = $"GPU available: {snapshot.Gpu.Name} - can accelerate inference",
                ExpiresIn = TimeSpan.FromHours(1) // Stable
            });
        }
        
        foreach (var opportunity in opportunities)
        {
            OnOpportunityDetected?.Invoke(opportunity);
        }
    }
    
    private float CalculateHealthScore(ResourceSnapshot snapshot)
    {
        float score = 100;
        
        // CPU penalty
        if (snapshot.Cpu.CurrentLoadPercent > 80) score -= 20;
        else if (snapshot.Cpu.CurrentLoadPercent > 60) score -= 10;
        
        // Memory penalty
        if (snapshot.Memory.HighLoad) score -= 25;
        else if (snapshot.Memory.AvailableMb < 1024) score -= 15;
        
        // Storage penalty
        if (snapshot.Storage.UsedPercent > 90) score -= 20;
        
        // Network penalty
        if (!snapshot.Network.InternetAvailable) score -= 30;
        
        // Power penalty
        if (snapshot.Power.OnBattery && snapshot.Power.BatteryPercent < 30) score -= 20;
        
        return Math.Max(0, score);
    }
    
    private AutonomyLevel AssessAutonomyCapability(ResourceSnapshot snapshot)
    {
        // Full autonomy requires all resources healthy
        if (snapshot.HealthScore > 80 && 
            snapshot.Network.InternetAvailable &&
            snapshot.Cpu.AvailablePercent > 30 &&
            snapshot.Memory.AvailableMb > 1024)
        {
            return AutonomyLevel.Full;
        }
        
        // Limited autonomy if resources constrained
        if (snapshot.HealthScore > 50)
        {
            return AutonomyLevel.Limited;
        }
        
        // Minimal autonomy if resources critical
        if (snapshot.HealthScore > 20)
        {
            return AutonomyLevel.Minimal;
        }
        
        // Survival mode
        return AutonomyLevel.Survival;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              UTILITY
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private bool CheckInternetConnectivity()
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("8.8.8.8", 1000);
            return reply?.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }
    
    private ThermalState EstimateThermalState(double cpuLoad)
    {
        // Estimate based on CPU load (actual thermal data requires platform-specific APIs)
        if (cpuLoad > 90) return ThermalState.Hot;
        if (cpuLoad > 70) return ThermalState.Warm;
        return ThermalState.Normal;
    }
    
    private long GetModelCacheSize()
    {
        var modelDir = Path.Combine(AppContext.BaseDirectory, "models");
        if (!Directory.Exists(modelDir)) return 0;
        
        return Directory.GetFiles(modelDir, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length) / (1024 * 1024);
    }
    
    private long GetTrainingDataSize()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "training_data");
        if (!Directory.Exists(dataDir)) return 0;
        
        return Directory.GetFiles(dataDir, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length) / (1024 * 1024);
    }
    
    public void Dispose()
    {
        Stop();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

public record ResourceSnapshot
{
    public DateTime Timestamp { get; init; }
    public float HealthScore { get; set; }
    public AutonomyLevel AutonomyCapability { get; set; }
    public CpuResources Cpu { get; init; } = new();
    public MemoryResources Memory { get; init; } = new();
    public StorageResources Storage { get; init; } = new();
    public NetworkResources Network { get; init; } = new();
    public CellularResources? Cellular { get; init; }
    public PowerResources Power { get; init; } = new();
    public GpuResources? Gpu { get; init; }
}

public record CpuResources
{
    public int CoreCount { get; init; }
    public double CurrentLoadPercent { get; init; }
    public double AvailablePercent { get; init; }
    public int ProcessCount { get; init; }
    public bool IdleThrottled { get; init; }
    public ThermalState ThermalState { get; init; }
}

public record MemoryResources
{
    public long TotalMb { get; init; }
    public long UsedMb { get; init; }
    public long AvailableMb { get; init; }
    public long ProcessMemoryMb { get; init; }
    public bool HighLoad { get; init; }
}

public record StorageResources
{
    public long TotalMb { get; init; }
    public long AvailableMb { get; init; }
    public double UsedPercent { get; init; }
    public int DriveCount { get; init; }
    public long ModelCacheMb { get; init; }
    public long TrainingDataMb { get; init; }
}

public record NetworkResources
{
    public int InterfaceCount { get; init; }
    public bool WifiAvailable { get; init; }
    public string? WifiInterface { get; init; }
    public bool EthernetAvailable { get; init; }
    public string? EthernetInterface { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public bool InternetAvailable { get; init; }
}

public record CellularResources
{
    public bool Available { get; init; }
    public bool Connected { get; init; }
    public string Technology { get; init; } = "";
    public string SignalStrength { get; init; } = "";
    public float RSRP { get; init; }
    public float RSRQ { get; init; }
    public float SINR { get; init; }
    public long ServingCellId { get; init; }
    public int NeighborCount { get; init; }
    public bool LocationAvailable { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public float? LocationConfidence { get; init; }
}

public record PowerResources
{
    public bool OnBattery { get; init; }
    public double BatteryPercent { get; init; }
    public int EstimatedMinutesRemaining { get; init; }
    public bool PowerSourceStable { get; init; }
}

public record GpuResources
{
    public bool Available { get; init; }
    public string Name { get; init; } = "";
    public long VramMb { get; init; }
    public long EstimatedFlops { get; init; }
    public string ExecutionProvider { get; init; } = "";
}

public record ResourceTrends
{
    public double CpuTrendPercent { get; init; }       // Change per minute
    public double MemoryTrendMbPerMin { get; init; }   // MB change per minute
    public double NetworkTrendBytesPerMin { get; init; }
    public double HealthTrend { get; init; }
}

public record ResourceAlert
{
    public AlertLevel Level { get; init; }
    public required string Resource { get; init; }
    public required string Message { get; init; }
    public string? RecommendedAction { get; init; }
}

public record ResourceOpportunity
{
    public OpportunityType Type { get; init; }
    public float Score { get; init; }
    public required string Description { get; init; }
    public TimeSpan ExpiresIn { get; init; }
}

public enum AutonomyLevel
{
    Full,      // All systems nominal
    Limited,   // Some resources constrained
    Minimal,   // Critical resources constrained
    Survival   // Emergency mode
}

public enum ThermalState
{
    Normal,
    Warm,
    Hot,
    Throttling
}

public enum AlertLevel
{
    Info,
    Warning,
    Critical
}

public enum OpportunityType
{
    ComputeAvailable,
    HighBandwidth,
    GpuAvailable,
    StorageAvailable,
    LowLatencyConnection
}
