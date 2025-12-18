/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    CELLULAR INTELLIGENCE SERVICE                           ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Main orchestrator for cellular signal analysis and tower management.      ║
 * ║  Combines modem control, RF fingerprinting, and handover prediction.       ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Cellular;

/// <summary>
/// Cellular Intelligence Service - combines all cellular capabilities.
/// </summary>
public class CellularIntelligence : IDisposable
{
    private readonly ModemController _modem;
    private readonly RFLocatorModel _locator;
    private readonly HandoverPredictor _handover;
    
    private Timer? _pollingTimer;
    private bool _isRunning = false;
    private CancellationTokenSource? _cts;
    
    // Current state
    public CellTowerMeasurement? CurrentMeasurement { get; private set; }
    public List<NeighborCell> CurrentNeighbors { get; private set; } = new();
    public LocationPrediction? LastLocation { get; private set; }
    public HandoverPrediction? LastHandoverPrediction { get; private set; }
    public SignalQuality? CurrentQuality { get; private set; }
    
    // Aliases for DroneCore integration
    public bool IsRunning => _isRunning;
    public CellTowerMeasurement? LastMeasurement => CurrentMeasurement;
    public List<NeighborCell>? NeighborCells => CurrentNeighbors;
    public bool RFLocationAvailable => _locator.IsModelLoaded;
    public bool HandoverPredictionAvailable => _handover.IsModelLoaded || true; // Rule-based always available
    
    // Events
    public event Action<CellTowerMeasurement>? OnMeasurement;
    public event Action<LocationPrediction>? OnLocationUpdate;
    public event Action<LocationPrediction>? OnLocationUpdated; // Alias for DroneCore
    public event Action<HandoverPrediction>? OnHandoverRecommended;
    public event Action<SignalQuality>? OnQualityChanged;
    
    // Configuration
    public int PollingIntervalMs { get; set; } = 100; // 100ms = 10 Hz
    public bool AutoHandover { get; set; } = false;    // Auto-execute handover commands
    public bool CollectTrainingData { get; set; } = false;
    
    /// <summary>
    /// Creates a new Cellular Intelligence service.
    /// </summary>
    /// <param name="modemPort">Serial port for modem (e.g., COM3, /dev/ttyUSB0)</param>
    public CellularIntelligence(string modemPort = "COM3")
    {
        _modem = new ModemController(modemPort);
        _locator = new RFLocatorModel();
        _handover = new HandoverPredictor();
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Initializes and starts the cellular intelligence service.
    /// </summary>
    public async Task<bool> StartAsync()
    {
        Console.WriteLine("◈ Starting Cellular Intelligence Service...");
        
        // Connect to modem
        var connected = await _modem.ConnectAsync();
        if (!connected)
        {
            Console.WriteLine("∴ Failed to connect to modem");
            return false;
        }
        
        // Load models if available
        var modelDir = Path.Combine(AppContext.BaseDirectory, "models");
        
        var locatorModelPath = Path.Combine(modelDir, "rf_locator.zip");
        if (File.Exists(locatorModelPath))
        {
            _locator.LoadModel(locatorModelPath);
        }
        
        var handoverModelPath = Path.Combine(modelDir, "handover_lstm.onnx");
        if (File.Exists(handoverModelPath))
        {
            _handover.LoadModel(handoverModelPath);
        }
        
        // Start polling
        _cts = new CancellationTokenSource();
        _isRunning = true;
        
        _ = PollLoopAsync(_cts.Token);
        
        Console.WriteLine($"◈ Cellular Intelligence active (polling every {PollingIntervalMs}ms)");
        
        return true;
    }
    
    /// <summary>
    /// Stops the service.
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _pollingTimer?.Dispose();
        _modem.Disconnect();
        
        Console.WriteLine("◎ Cellular Intelligence stopped");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              POLLING LOOP
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
        // Get cell measurement
        var (serving, neighbors) = await _modem.GetEngineeringModeAsync();
        
        CurrentMeasurement = serving;
        CurrentNeighbors = neighbors;
        
        // Update signal quality
        CurrentQuality = ATCommandParser.ClassifySignal(serving);
        OnQualityChanged?.Invoke(CurrentQuality);
        
        // Record for handover prediction
        _handover.RecordMeasurement(serving, neighbors);
        
        // Trigger event
        OnMeasurement?.Invoke(serving);
        
        // ─────────────────────────────────────────────────────────────
        // Handover Analysis
        // ─────────────────────────────────────────────────────────────
        var handoverPrediction = _handover.IsModelLoaded 
            ? _handover.PredictWithModel()
            : _handover.PredictRuleBased();
        
        if (handoverPrediction.HandoverImminent && 
            (LastHandoverPrediction == null || !LastHandoverPrediction.HandoverImminent))
        {
            Console.WriteLine($"⚠ Handover recommended: {handoverPrediction.Reason}");
            Console.WriteLine($"   Target cell: {handoverPrediction.RecommendedCellId}");
            Console.WriteLine($"   Time to switch: {handoverPrediction.TimeToHandoverMs:F0}ms");
            
            OnHandoverRecommended?.Invoke(handoverPrediction);
            
            // Auto-execute handover if enabled
            if (AutoHandover)
            {
                await ExecuteHandoverAsync(handoverPrediction);
            }
        }
        
        LastHandoverPrediction = handoverPrediction;
        
        // ─────────────────────────────────────────────────────────────
        // Location Prediction
        // ─────────────────────────────────────────────────────────────
        if (_locator.IsModelLoaded)
        {
            var locationPrediction = _locator.Predict(serving);
            
            if (locationPrediction.Confidence > 0.3f)
            {
                LastLocation = locationPrediction;
                OnLocationUpdate?.Invoke(locationPrediction);
                OnLocationUpdated?.Invoke(locationPrediction); // Invoke alias event
            }
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              HANDOVER EXECUTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Executes a handover to the recommended cell.
    /// </summary>
    public async Task<bool> ExecuteHandoverAsync(HandoverPrediction prediction)
    {
        if (prediction.RecommendedCellId == 0)
            return false;
        
        Console.WriteLine($"◎ Executing handover to cell {prediction.RecommendedCellId}...");
        
        // This is modem-specific. For Quectel:
        // We can lock to the target EARFCN to encourage handover.
        // Real forced handover typically requires network-side cooperation.
        
        try
        {
            // Trigger network rescan - modem will typically select best cell
            var result = await _modem.RescanNetworkAsync();
            
            if (result)
            {
                Console.WriteLine($"◈ Handover triggered successfully");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Handover failed: {ex.Message}");
        }
        
        return false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              TRAINING DATA COLLECTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Records current measurement with GPS ground truth for training.
    /// </summary>
    public void RecordTrainingPoint(double latitude, double longitude)
    {
        if (CurrentMeasurement == null) return;
        
        _locator.AddTrainingPoint(CurrentMeasurement, latitude, longitude);
    }
    
    /// <summary>
    /// Starts a drive-test session for data collection.
    /// Pairs cell measurements with GPS coordinates.
    /// </summary>
    public async Task StartDriveTestAsync(
        Func<Task<(double lat, double lon)>> gpsProvider,
        CancellationToken ct)
    {
        Console.WriteLine("◈ Drive test started - collecting RF fingerprints...");
        
        CollectTrainingData = true;
        var pointCount = 0;
        
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var (lat, lon) = await gpsProvider();
                
                if (CurrentMeasurement != null)
                {
                    _locator.AddTrainingPoint(CurrentMeasurement, lat, lon);
                    pointCount++;
                    
                    if (pointCount % 100 == 0)
                    {
                        Console.WriteLine($"◎ Drive test: {pointCount} points collected");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"∴ GPS error: {ex.Message}");
            }
            
            await Task.Delay(1000, ct); // 1 Hz collection rate
        }
        
        Console.WriteLine($"◈ Drive test complete: {pointCount} points");
        CollectTrainingData = false;
    }
    
    /// <summary>
    /// Trains the RF locator model with collected data.
    /// </summary>
    public void TrainLocatorModel()
    {
        _locator.Train();
        
        // Save model
        var modelDir = Path.Combine(AppContext.BaseDirectory, "models");
        Directory.CreateDirectory(modelDir);
        _locator.SaveModel(Path.Combine(modelDir, "rf_locator.zip"));
    }
    
    /// <summary>
    /// Saves training data to disk.
    /// </summary>
    public async Task SaveTrainingDataAsync(string path)
    {
        await _locator.SaveTrainingDataAsync(path);
    }
    
    /// <summary>
    /// Loads training data from disk.
    /// </summary>
    public async Task LoadTrainingDataAsync(string path)
    {
        await _locator.LoadTrainingDataAsync(path);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              UTILITY
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets current cell info as a summary string.
    /// </summary>
    public string GetStatusSummary()
    {
        if (CurrentMeasurement == null)
            return "No signal data";
        
        var m = CurrentMeasurement;
        var q = CurrentQuality ?? ATCommandParser.ClassifySignal(m);
        
        return $"""
            Cell: {m.CellId} ({m.RadioType})
            RSRP: {m.RSRP:F1} dBm ({q.Strength})
            RSRQ: {m.RSRQ:F1} dB
            SINR: {m.SINR:F1} dB
            Neighbors: {CurrentNeighbors.Count}
            Quality Score: {q.Score:F0}/100
            Throughput Est: {q.EstimatedThroughputMbps:F0} Mbps
            """;
    }
    
    /// <summary>
    /// Lists available modem ports.
    /// </summary>
    public static string[] GetAvailableModems()
    {
        return ModemController.GetAvailablePorts();
    }
    
    public void Dispose()
    {
        Stop();
        _modem.Dispose();
        _handover.Dispose();
    }
}
