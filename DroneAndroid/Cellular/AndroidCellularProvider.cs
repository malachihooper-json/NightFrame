/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║           ANDROID CELLULAR PROVIDER - TELEPHONYMANAGER INTEGRATION         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Implements ICellularProvider using Android TelephonyManager APIs.         ║
 * ║  Provides cell tower info, signal measurements, and location estimates.    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Android.App;
using Android.Content;
using Android.OS;
using Android.Telephony;
using Android.Locations;
using AndroidX.Core.Content;
using Android.Content.PM;

namespace NIGHTFRAME.Drone.Android.Cellular;

/// <summary>
/// Android implementation of cellular intelligence using TelephonyManager.
/// </summary>
public class AndroidCellularProvider : IDisposable
{
    private readonly Context _context;
    private TelephonyManager? _telephonyManager;
    private CellInfoCallback? _cellInfoCallback;
    private LocationManager? _locationManager;
    private CellLocationListener? _locationListener;
    private Timer? _pollingTimer;
    
    private bool _isRunning = false;
    private CancellationTokenSource? _cts;
    
    // Current state
    public AndroidCellMeasurement? CurrentMeasurement { get; private set; }
    public List<AndroidNeighborCell> NeighborCells { get; private set; } = new();
    public AndroidSignalQuality? CurrentQuality { get; private set; }
    public GeoLocation? LastLocation { get; private set; }
    
    // Events
    public event Action<AndroidCellMeasurement>? OnMeasurement;
    public event Action<AndroidSignalQuality>? OnQualityChanged;
    public event Action<GeoLocation>? OnLocationUpdated;
    public event Action<HandoverRecommendation>? OnHandoverRecommended;
    
    // Configuration
    public int PollingIntervalMs { get; set; } = 1000; // 1 second
    public bool EnableRFFingerprinting { get; set; } = true;
    
    // Capabilities
    public bool IsAvailable => _telephonyManager != null;
    public bool Is5GCapable { get; private set; }
    public bool HasCarrierPrivileges { get; private set; }
    public string NetworkOperator { get; private set; } = "";
    public string SimOperator { get; private set; } = "";
    
    public AndroidCellularProvider(Context context)
    {
        _context = context;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Initializes and starts cellular monitoring.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        Console.WriteLine("◈ Starting Android Cellular Provider...");
        
        try
        {
            // Get TelephonyManager
            _telephonyManager = (TelephonyManager?)_context.GetSystemService(Context.TelephonyService);
            if (_telephonyManager == null)
            {
                Console.WriteLine("∴ TelephonyManager not available");
                return false;
            }
            
            // Get LocationManager for GPS fusion
            _locationManager = (LocationManager?)_context.GetSystemService(Context.LocationService);
            
            // Check permissions
            if (!HasRequiredPermissions())
            {
                Console.WriteLine("∴ Missing cellular permissions");
                return false;
            }
            
            // Detect capabilities
            DetectCapabilities();
            
            // Get carrier info
            NetworkOperator = _telephonyManager.NetworkOperatorName ?? "";
            SimOperator = _telephonyManager.SimOperatorName ?? "";
            
            Console.WriteLine($"◎ Carrier: {NetworkOperator}");
            Console.WriteLine($"◎ SIM: {SimOperator}");
            Console.WriteLine($"◎ 5G Capable: {Is5GCapable}");
            
            // Register callback for cell info updates (API 29+)
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            {
                RegisterCellInfoCallback();
            }
            
            // Start polling loop as fallback
            _cts = new CancellationTokenSource();
            _isRunning = true;
            _ = PollLoopAsync(_cts.Token);
            
            Console.WriteLine($"◈ Android Cellular Provider active");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Cellular provider start failed: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Stops cellular monitoring.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _pollingTimer?.Dispose();
        
        UnregisterCellInfoCallback();
        UnregisterLocationListener();
        
        Console.WriteLine("◎ Android Cellular Provider stopped");
    }
    
    private bool HasRequiredPermissions()
    {
        var fineLocation = ContextCompat.CheckSelfPermission(_context, 
            global::Android.Manifest.Permission.AccessFineLocation);
        var phoneState = ContextCompat.CheckSelfPermission(_context,
            global::Android.Manifest.Permission.ReadPhoneState);
        
        return fineLocation == Permission.Granted && 
               phoneState == Permission.Granted;
    }
    
    private void DetectCapabilities()
    {
        if (_telephonyManager == null) return;
        
        // Check for 5G capability
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            // Check if device supports NR (5G)
            var networkType = _telephonyManager.DataNetworkType;
            Is5GCapable = networkType == NetworkType.Nr;
            
            // Also check supported network types
            try
            {
                var supportedTypes = _telephonyManager.GetSupportedModemCount();
                Console.WriteLine($"◎ Modem count: {supportedTypes}");
            }
            catch { }
        }
        
        // Check carrier privileges (for advanced operations)
        try
        {
            HasCarrierPrivileges = _telephonyManager.HasCarrierPrivileges;
            if (HasCarrierPrivileges)
            {
                Console.WriteLine("◈ Carrier privileges available - advanced features enabled");
            }
        }
        catch
        {
            HasCarrierPrivileges = false;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              CELL INFO CALLBACK (API 29+)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private void RegisterCellInfoCallback()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.Q || _telephonyManager == null)
            return;
        
        try
        {
            _cellInfoCallback = new CellInfoCallback(this);
            _telephonyManager.RegisterTelephonyCallback(
                _context.MainExecutor!, 
                _cellInfoCallback);
            
            Console.WriteLine("◎ CellInfo callback registered");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Failed to register callback: {ex.Message}");
        }
    }
    
    private void UnregisterCellInfoCallback()
    {
        if (_cellInfoCallback != null && _telephonyManager != null)
        {
            try
            {
                _telephonyManager.UnregisterTelephonyCallback(_cellInfoCallback);
            }
            catch { }
        }
    }
    
    private void UnregisterLocationListener()
    {
        if (_locationListener != null && _locationManager != null)
        {
            try
            {
                _locationManager.RemoveUpdates(_locationListener);
            }
            catch { }
        }
    }
    
    /// <summary>
    /// Callback class for receiving cell info updates.
    /// </summary>
    private class CellInfoCallback : TelephonyCallback, TelephonyCallback.ICellInfoListener
    {
        private readonly AndroidCellularProvider _provider;
        
        public CellInfoCallback(AndroidCellularProvider provider)
        {
            _provider = provider;
        }
        
        public void OnCellInfoChanged(IList<CellInfo> cellInfo)
        {
            _provider.ProcessCellInfo(cellInfo);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              POLLING LOOP (FALLBACK)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (_isRunning && !ct.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ Poll error: {ex.Message}");
            }
            
            await Task.Delay(PollingIntervalMs, ct);
        }
    }
    
    private async Task PollOnceAsync()
    {
        if (_telephonyManager == null) return;
        
        try
        {
            // Get cell info list
            var cellInfoList = _telephonyManager.AllCellInfo;
            if (cellInfoList != null)
            {
                ProcessCellInfo(cellInfoList);
            }
        }
        catch (SecurityException)
        {
            Console.WriteLine("∴ Permission denied for cell info");
        }
        
        await Task.CompletedTask;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              CELL INFO PROCESSING
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private void ProcessCellInfo(IList<CellInfo>? cellInfoList)
    {
        if (cellInfoList == null || cellInfoList.Count == 0) return;
        
        AndroidCellMeasurement? servingCell = null;
        var neighbors = new List<AndroidNeighborCell>();
        
        foreach (var cellInfo in cellInfoList)
        {
            var measurement = ParseCellInfo(cellInfo);
            if (measurement == null) continue;
            
            if (cellInfo.IsRegistered)
            {
                servingCell = measurement;
            }
            else
            {
                neighbors.Add(new AndroidNeighborCell
                {
                    CellId = measurement.CellId,
                    Technology = measurement.Technology,
                    RSRP = measurement.RSRP,
                    RSRQ = measurement.RSRQ,
                    Level = measurement.SignalLevel
                });
            }
        }
        
        if (servingCell != null)
        {
            CurrentMeasurement = servingCell;
            NeighborCells = neighbors;
            
            // Calculate signal quality
            CurrentQuality = ClassifySignal(servingCell);
            
            // Trigger events
            OnMeasurement?.Invoke(servingCell);
            OnQualityChanged?.Invoke(CurrentQuality);
            
            // Check for handover recommendation
            CheckHandoverNeed(servingCell, neighbors);
        }
    }
    
    private AndroidCellMeasurement? ParseCellInfo(CellInfo cellInfo)
    {
        var measurement = new AndroidCellMeasurement
        {
            Timestamp = DateTime.UtcNow,
            IsRegistered = cellInfo.IsRegistered
        };
        
        switch (cellInfo)
        {
            case CellInfoLte lte:
                ParseLteCellInfo(lte, measurement);
                break;
                
            case CellInfoNr nr:
                ParseNrCellInfo(nr, measurement);
                break;
                
            case CellInfoWcdma wcdma:
                ParseWcdmaCellInfo(wcdma, measurement);
                break;
                
            case CellInfoGsm gsm:
                ParseGsmCellInfo(gsm, measurement);
                break;
                
            default:
                return null;
        }
        
        return measurement;
    }
    
    private void ParseLteCellInfo(CellInfoLte lte, AndroidCellMeasurement measurement)
    {
        var identity = lte.CellIdentity;
        var signal = lte.CellSignalStrength;
        
        measurement.Technology = "LTE";
        measurement.CellId = identity?.Ci ?? 0;
        measurement.PhysicalCellId = identity?.Pci ?? 0;
        measurement.TAC = identity?.Tac ?? 0;
        measurement.EARFCN = identity?.Earfcn ?? 0;
        measurement.MCC = identity?.MccString ?? "";
        measurement.MNC = identity?.MncString ?? "";
        measurement.Bandwidth = identity?.Bandwidth ?? 0;
        
        measurement.RSRP = signal?.Rsrp ?? int.MinValue;
        measurement.RSRQ = signal?.Rsrq ?? int.MinValue;
        measurement.RSSI = signal?.Rssi ?? int.MinValue;
        measurement.CQI = signal?.Cqi ?? 0;
        measurement.SignalLevel = signal?.Level ?? 0;
        
        // Timing advance (distance estimation)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            measurement.TimingAdvance = signal?.TimingAdvance ?? int.MaxValue;
        }
    }
    
    private void ParseNrCellInfo(CellInfoNr nr, AndroidCellMeasurement measurement)
    {
        var identity = (CellIdentityNr?)nr.CellIdentity;
        var signal = (CellSignalStrengthNr?)nr.CellSignalStrength;
        
        measurement.Technology = "5G NR";
        measurement.CellId = (long)(identity?.Nci ?? 0);
        measurement.PhysicalCellId = identity?.Pci ?? 0;
        measurement.TAC = identity?.Tac ?? 0;
        measurement.NRARFCN = identity?.Nrarfcn ?? 0;
        measurement.MCC = identity?.MccString ?? "";
        measurement.MNC = identity?.MncString ?? "";
        
        // 5G NR signal measurements
        measurement.SsRsrp = signal?.SsRsrp ?? int.MinValue;
        measurement.SsRsrq = signal?.SsRsrq ?? int.MinValue;
        measurement.SsSinr = signal?.SsSinr ?? int.MinValue;
        measurement.CsiRsrp = signal?.CsiRsrp ?? int.MinValue;
        measurement.CsiRsrq = signal?.CsiRsrq ?? int.MinValue;
        measurement.CsiSinr = signal?.CsiSinr ?? int.MinValue;
        measurement.SignalLevel = signal?.Level ?? 0;
        
        // Map SS-RSRP to RSRP for unified interface
        measurement.RSRP = measurement.SsRsrp;
        measurement.RSRQ = measurement.SsRsrq;
        measurement.SINR = measurement.SsSinr;
    }
    
    private void ParseWcdmaCellInfo(CellInfoWcdma wcdma, AndroidCellMeasurement measurement)
    {
        var identity = wcdma.CellIdentity;
        var signal = wcdma.CellSignalStrength;
        
        measurement.Technology = "WCDMA";
        measurement.CellId = identity?.Cid ?? 0;
        measurement.LAC = identity?.Lac ?? 0;
        measurement.UARFCN = identity?.Uarfcn ?? 0;
        measurement.MCC = identity?.MccString ?? "";
        measurement.MNC = identity?.MncString ?? "";
        measurement.PSC = identity?.Psc ?? 0;
        
        measurement.RSCP = signal?.Dbm ?? int.MinValue;
        measurement.EcNo = signal?.EcNo ?? int.MinValue;
        measurement.SignalLevel = signal?.Level ?? 0;
    }
    
    private void ParseGsmCellInfo(CellInfoGsm gsm, AndroidCellMeasurement measurement)
    {
        var identity = gsm.CellIdentity;
        var signal = gsm.CellSignalStrength;
        
        measurement.Technology = "GSM";
        measurement.CellId = identity?.Cid ?? 0;
        measurement.LAC = identity?.Lac ?? 0;
        measurement.ARFCN = identity?.Arfcn ?? 0;
        measurement.MCC = identity?.MccString ?? "";
        measurement.MNC = identity?.MncString ?? "";
        measurement.BSIC = identity?.Bsic ?? 0;
        
        measurement.RSSI = signal?.Dbm ?? int.MinValue;
        measurement.BitErrorRate = signal?.BitErrorRate ?? 0;
        measurement.SignalLevel = signal?.Level ?? 0;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              SIGNAL ANALYSIS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private AndroidSignalQuality ClassifySignal(AndroidCellMeasurement measurement)
    {
        var quality = new AndroidSignalQuality
        {
            Technology = measurement.Technology,
            Level = measurement.SignalLevel,
            Timestamp = DateTime.UtcNow
        };
        
        // Classify based on technology
        if (measurement.Technology == "LTE" || measurement.Technology == "5G NR")
        {
            // Use RSRP for LTE/NR
            var rsrp = measurement.RSRP;
            if (rsrp >= -80)
            {
                quality.Strength = SignalStrength.Excellent;
                quality.Score = 100;
                quality.Description = "Excellent signal";
            }
            else if (rsrp >= -90)
            {
                quality.Strength = SignalStrength.Good;
                quality.Score = 75;
                quality.Description = "Good signal";
            }
            else if (rsrp >= -100)
            {
                quality.Strength = SignalStrength.Fair;
                quality.Score = 50;
                quality.Description = "Fair signal";
            }
            else if (rsrp >= -110)
            {
                quality.Strength = SignalStrength.Poor;
                quality.Score = 25;
                quality.Description = "Poor signal";
            }
            else
            {
                quality.Strength = SignalStrength.NoSignal;
                quality.Score = 0;
                quality.Description = "No usable signal";
            }
            
            // Adjust for SINR
            if (measurement.SINR < 0)
            {
                quality.Score *= 0.7f;
                quality.Description += " (high interference)";
            }
        }
        else
        {
            // Use RSSI for GSM/WCDMA
            var rssi = measurement.RSSI != int.MinValue ? measurement.RSSI : 
                       measurement.RSCP != int.MinValue ? measurement.RSCP : -100;
            
            if (rssi >= -70)
            {
                quality.Strength = SignalStrength.Excellent;
                quality.Score = 100;
            }
            else if (rssi >= -85)
            {
                quality.Strength = SignalStrength.Good;
                quality.Score = 75;
            }
            else if (rssi >= -100)
            {
                quality.Strength = SignalStrength.Fair;
                quality.Score = 50;
            }
            else
            {
                quality.Strength = SignalStrength.Poor;
                quality.Score = 25;
            }
        }
        
        // Estimate throughput based on technology and signal
        quality.EstimatedThroughputMbps = EstimateThroughput(measurement, quality);
        
        return quality;
    }
    
    private float EstimateThroughput(AndroidCellMeasurement measurement, AndroidSignalQuality quality)
    {
        // Base throughput by technology
        float baseThroughput = measurement.Technology switch
        {
            "5G NR" => 200,   // 200 Mbps base for 5G
            "LTE" => 50,      // 50 Mbps base for LTE
            "WCDMA" => 10,    // 10 Mbps base for HSPA
            "GSM" => 0.2f,    // 0.2 Mbps for EDGE
            _ => 1
        };
        
        // Scale by signal quality
        return baseThroughput * (quality.Score / 100f);
    }
    
    private void CheckHandoverNeed(AndroidCellMeasurement serving, List<AndroidNeighborCell> neighbors)
    {
        // Check if any neighbor is significantly stronger
        foreach (var neighbor in neighbors)
        {
            if (neighbor.RSRP > serving.RSRP + 6) // 6 dB hysteresis
            {
                var recommendation = new HandoverRecommendation
                {
                    Reason = $"Neighbor cell {neighbor.CellId} is {neighbor.RSRP - serving.RSRP} dB stronger",
                    RecommendedCellId = neighbor.CellId,
                    CurrentRSRP = serving.RSRP,
                    TargetRSRP = neighbor.RSRP,
                    Urgent = serving.RSRP < -100
                };
                
                OnHandoverRecommended?.Invoke(recommendation);
                break;
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              LOCATION FROM CELL
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Estimates location using cell tower info (RF fingerprinting would enhance this).
    /// </summary>
    public async Task<GeoLocation?> EstimateLocationAsync()
    {
        // First try GPS for ground truth
        if (_locationManager != null)
        {
            try
            {
                var providers = _locationManager.GetProviders(true);
                foreach (var provider in providers)
                {
                    var androidLocation = _locationManager.GetLastKnownLocation(provider);
                    if (androidLocation != null)
                    {
                        LastLocation = new GeoLocation
                        {
                            Latitude = androidLocation.Latitude,
                            Longitude = androidLocation.Longitude,
                            Accuracy = androidLocation.Accuracy,
                            Source = "GPS",
                            Timestamp = DateTime.UtcNow
                        };
                        
                        OnLocationUpdated?.Invoke(LastLocation.Value);
                        return LastLocation;
                    }
                }
            }
            catch { }
        }
        
        // Fall back to cell-based location (coarse)
        // This would ideally use RF fingerprinting with a trained model
        if (CurrentMeasurement != null)
        {
            // Without a database, we can only provide cell ID
            // Real implementation would query OpenCellID or similar
            Console.WriteLine($"◎ Cell-based location: MCC={CurrentMeasurement.MCC}, " +
                            $"MNC={CurrentMeasurement.MNC}, " +
                            $"CellId={CurrentMeasurement.CellId}");
        }
        
        return null;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              DATA EXPORT
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets current state for reporting to Orchestrator.
    /// </summary>
    public CellularStateReport GetStateReport()
    {
        return new CellularStateReport
        {
            Timestamp = DateTime.UtcNow,
            IsAvailable = IsAvailable,
            Is5GCapable = Is5GCapable,
            NetworkOperator = NetworkOperator,
            SimOperator = SimOperator,
            CurrentMeasurement = CurrentMeasurement,
            NeighborCount = NeighborCells.Count,
            SignalQuality = CurrentQuality,
            Location = LastLocation
        };
    }
    
    public void Dispose()
    {
        Stop();
    }
}

/// <summary>
/// Location listener for continuous location updates.
/// </summary>
internal class CellLocationListener : Java.Lang.Object, ILocationListener
{
    private readonly AndroidCellularProvider _provider;
    
    public CellLocationListener(AndroidCellularProvider provider)
    {
        _provider = provider;
    }
    
    public void OnLocationChanged(Location location)
    {
        _provider.LastLocation = new GeoLocation
        {
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Accuracy = location.Accuracy,
            Source = location.Provider ?? "Unknown",
            Timestamp = DateTime.UtcNow
        };
        
        _provider.OnLocationUpdated?.Invoke(_provider.LastLocation.Value);
    }
    
    public void OnProviderDisabled(string provider) { }
    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

public class AndroidCellMeasurement
{
    public DateTime Timestamp { get; set; }
    public bool IsRegistered { get; set; }
    public string Technology { get; set; } = "";
    
    // Cell identity
    public long CellId { get; set; }
    public int PhysicalCellId { get; set; }
    public int TAC { get; set; }  // Tracking Area Code (LTE)
    public int LAC { get; set; }  // Location Area Code (GSM/WCDMA)
    public string MCC { get; set; } = "";
    public string MNC { get; set; } = "";
    
    // Frequency info
    public int EARFCN { get; set; }   // LTE
    public int NRARFCN { get; set; }  // 5G NR
    public int UARFCN { get; set; }   // WCDMA
    public int ARFCN { get; set; }    // GSM
    public int Bandwidth { get; set; }
    
    // LTE signal measurements
    public int RSRP { get; set; } = int.MinValue;
    public int RSRQ { get; set; } = int.MinValue;
    public int RSSI { get; set; } = int.MinValue;
    public int SINR { get; set; } = int.MinValue;
    public int CQI { get; set; }
    public int TimingAdvance { get; set; } = int.MaxValue;
    
    // 5G NR specific
    public int SsRsrp { get; set; } = int.MinValue;
    public int SsRsrq { get; set; } = int.MinValue;
    public int SsSinr { get; set; } = int.MinValue;
    public int CsiRsrp { get; set; } = int.MinValue;
    public int CsiRsrq { get; set; } = int.MinValue;
    public int CsiSinr { get; set; } = int.MinValue;
    
    // WCDMA specific
    public int RSCP { get; set; } = int.MinValue;
    public int EcNo { get; set; } = int.MinValue;
    public int PSC { get; set; }
    
    // GSM specific
    public int BSIC { get; set; }
    public int BitErrorRate { get; set; }
    
    // Signal level (0-4)
    public int SignalLevel { get; set; }
}

public class AndroidNeighborCell
{
    public long CellId { get; set; }
    public string Technology { get; set; } = "";
    public int RSRP { get; set; }
    public int RSRQ { get; set; }
    public int Level { get; set; }
}

public class AndroidSignalQuality
{
    public string Technology { get; set; } = "";
    public SignalStrength Strength { get; set; }
    public float Score { get; set; }
    public string Description { get; set; } = "";
    public int Level { get; set; }
    public float EstimatedThroughputMbps { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum SignalStrength
{
    NoSignal,
    Poor,
    Fair,
    Good,
    Excellent
}

public struct GeoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Accuracy { get; set; }
    public string Source { get; set; }
    public DateTime Timestamp { get; set; }
}

public class HandoverRecommendation
{
    public string Reason { get; set; } = "";
    public long RecommendedCellId { get; set; }
    public int CurrentRSRP { get; set; }
    public int TargetRSRP { get; set; }
    public bool Urgent { get; set; }
}

public class CellularStateReport
{
    public DateTime Timestamp { get; set; }
    public bool IsAvailable { get; set; }
    public bool Is5GCapable { get; set; }
    public string NetworkOperator { get; set; } = "";
    public string SimOperator { get; set; } = "";
    public AndroidCellMeasurement? CurrentMeasurement { get; set; }
    public int NeighborCount { get; set; }
    public AndroidSignalQuality? SignalQuality { get; set; }
    public GeoLocation? Location { get; set; }
}
