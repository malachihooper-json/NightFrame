/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    CELLULAR COORDINATOR SERVICE                            ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Aggregates cellular data from all drones for:                             ║
 * ║  - Crowd-sourced cell tower database                                       ║
 * ║  - RF fingerprinting training data                                         ║
 * ║  - Network coverage mapping                                                ║
 * ║  - Location intelligence                                                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Microsoft.AspNetCore.SignalR;
using NIGHTFRAME.Orchestrator.Hubs;
using System.Collections.Concurrent;

namespace NIGHTFRAME.Orchestrator.Services;

/// <summary>
/// Coordinates cellular intelligence data across all drones.
/// </summary>
public class CellularCoordinator
{
    private readonly IHubContext<ConsoleHub, IConsoleClient> _hubContext;
    private readonly ILogger<CellularCoordinator> _logger;
    private readonly DroneRegistry _registry;
    
    // Cell tower database (crowd-sourced)
    private readonly ConcurrentDictionary<long, CellTowerRecord> _cellDatabase = new();
    
    // Recent measurements per node
    private readonly ConcurrentDictionary<string, NodeCellularState> _nodeStates = new();
    
    // Training data collection
    private readonly ConcurrentQueue<CellMeasurementSample> _trainingData = new();
    private const int MaxTrainingDataSize = 100000;
    
    public CellularCoordinator(
        IHubContext<ConsoleHub, IConsoleClient> hubContext,
        DroneRegistry registry,
        ILogger<CellularCoordinator> logger)
    {
        _hubContext = hubContext;
        _registry = registry;
        _logger = logger;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              PROCESS INCOMING DATA
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Process a cellular status update from a drone.
    /// </summary>
    public async Task ProcessCellularUpdate(string nodeId, CellularStatusData data)
    {
        _logger.LogDebug("◎ Cellular update from {NodeId}: {Tech} RSRP={Rsrp}", 
            nodeId, data.Technology, data.Rsrp);
        
        // Update node state
        var state = _nodeStates.GetOrAdd(nodeId, _ => new NodeCellularState(nodeId));
        state.Update(data);
        
        // Update cell database
        if (data.CellId > 0)
        {
            UpdateCellDatabase(data);
        }
        
        // Store for training if we have location
        if (data.Latitude.HasValue && data.Longitude.HasValue)
        {
            AddTrainingSample(nodeId, data);
        }
        
        // Broadcast to web console
        await _hubContext.Clients.All.CellularUpdate(new CellularUpdateDto(
            NodeId: nodeId,
            Technology: data.Technology,
            Rsrp: data.Rsrp,
            Rsrq: data.Rsrq,
            Sinr: data.Sinr,
            CellId: data.CellId,
            NeighborCount: data.NeighborCount,
            Timestamp: DateTime.UtcNow.ToString("o")
        ));
        
        // Update registry with cellular info
        _registry.UpdateCellularInfo(nodeId, data.Technology, data.Rsrp);
    }
    
    /// <summary>
    /// Process a location update from a drone.
    /// </summary>
    public void ProcessLocationUpdate(string nodeId, double latitude, double longitude, double confidence)
    {
        if (_nodeStates.TryGetValue(nodeId, out var state))
        {
            state.LastLatitude = latitude;
            state.LastLongitude = longitude;
            state.LocationConfidence = confidence;
        }
        
        _registry.UpdateLocation(nodeId, latitude, longitude, confidence);
    }
    
    /// <summary>
    /// Update the crowd-sourced cell tower database.
    /// </summary>
    private void UpdateCellDatabase(CellularStatusData data)
    {
        if (!data.Latitude.HasValue || !data.Longitude.HasValue) return;
        
        var record = _cellDatabase.GetOrAdd(data.CellId, _ => new CellTowerRecord
        {
            CellId = data.CellId,
            Mcc = data.Mcc,
            Mnc = data.Mnc,
            Lac = data.Lac,
            Technology = data.Technology,
            FirstSeen = DateTime.UtcNow
        });
        
        // Update with new observation
        record.AddObservation(data.Latitude.Value, data.Longitude.Value, data.Rsrp);
    }
    
    /// <summary>
    /// Add a sample for RF fingerprinting training.
    /// </summary>
    private void AddTrainingSample(string nodeId, CellularStatusData data)
    {
        var sample = new CellMeasurementSample
        {
            Timestamp = DateTime.UtcNow,
            Latitude = data.Latitude!.Value,
            Longitude = data.Longitude!.Value,
            Technology = data.Technology,
            CellId = data.CellId,
            Rsrp = data.Rsrp,
            Rsrq = data.Rsrq,
            Sinr = data.Sinr,
            TimingAdvance = data.TimingAdvance,
            NeighborCells = data.NeighborCells?.Select(n => new NeighborSample
            {
                CellId = n.CellId,
                Rsrp = n.Rsrp,
                Rsrq = n.Rsrq
            }).ToList() ?? new()
        };
        
        _trainingData.Enqueue(sample);
        
        // Trim if too large
        while (_trainingData.Count > MaxTrainingDataSize)
        {
            _trainingData.TryDequeue(out _);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              QUERIES
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Get all known cell towers.
    /// </summary>
    public IEnumerable<CellTowerRecord> GetCellDatabase() => _cellDatabase.Values;
    
    /// <summary>
    /// Get cell tower by ID.
    /// </summary>
    public CellTowerRecord? GetCellTower(long cellId) => 
        _cellDatabase.TryGetValue(cellId, out var record) ? record : null;
    
    /// <summary>
    /// Get cellular state for a node.
    /// </summary>
    public NodeCellularState? GetNodeState(string nodeId) =>
        _nodeStates.TryGetValue(nodeId, out var state) ? state : null;
    
    /// <summary>
    /// Get all nodes with cellular capability.
    /// </summary>
    public IEnumerable<NodeCellularState> GetCellularNodes() => _nodeStates.Values;
    
    /// <summary>
    /// Get training data samples.
    /// </summary>
    public IEnumerable<CellMeasurementSample> GetTrainingData(int maxSamples = 10000)
    {
        return _trainingData.Take(maxSamples);
    }
    
    /// <summary>
    /// Get cell towers near a location.
    /// </summary>
    public IEnumerable<CellTowerRecord> GetNearbyTowers(double lat, double lon, double radiusKm = 10)
    {
        return _cellDatabase.Values
            .Where(t => t.EstimatedLatitude.HasValue && t.EstimatedLongitude.HasValue)
            .Where(t => CalculateDistance(lat, lon, t.EstimatedLatitude!.Value, t.EstimatedLongitude!.Value) <= radiusKm)
            .OrderBy(t => CalculateDistance(lat, lon, t.EstimatedLatitude!.Value, t.EstimatedLongitude!.Value));
    }
    
    /// <summary>
    /// Get coverage statistics.
    /// </summary>
    public CoverageStatistics GetCoverageStats()
    {
        var towers = _cellDatabase.Values.ToList();
        return new CoverageStatistics
        {
            TotalTowers = towers.Count,
            LteTowers = towers.Count(t => t.Technology == "LTE"),
            Nr5gTowers = towers.Count(t => t.Technology == "5G NR"),
            WcdmaTowers = towers.Count(t => t.Technology == "WCDMA"),
            GsmTowers = towers.Count(t => t.Technology == "GSM"),
            TotalObservations = towers.Sum(t => t.ObservationCount),
            TotalTrainingSamples = _trainingData.Count,
            ActiveNodes = _nodeStates.Values.Count(s => s.IsActive)
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              HELPERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        // Haversine formula
        const double R = 6371; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
    
    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Incoming cellular status data from a drone.
/// </summary>
public class CellularStatusData
{
    public string Technology { get; set; } = "";
    public long CellId { get; set; }
    public int Mcc { get; set; }
    public int Mnc { get; set; }
    public int Lac { get; set; }
    public int Rsrp { get; set; }
    public int Rsrq { get; set; }
    public int Sinr { get; set; }
    public int TimingAdvance { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int NeighborCount { get; set; }
    public List<NeighborCellData>? NeighborCells { get; set; }
}

public class NeighborCellData
{
    public long CellId { get; set; }
    public int Rsrp { get; set; }
    public int Rsrq { get; set; }
}

/// <summary>
/// Crowd-sourced cell tower record.
/// </summary>
public class CellTowerRecord
{
    public long CellId { get; set; }
    public int Mcc { get; set; }
    public int Mnc { get; set; }
    public int Lac { get; set; }
    public string Technology { get; set; } = "";
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public int ObservationCount { get; set; }
    
    // Estimated location (weighted average)
    public double? EstimatedLatitude { get; set; }
    public double? EstimatedLongitude { get; set; }
    public double LocationConfidence { get; set; }
    
    // Signal statistics
    public int MinRsrp { get; set; } = int.MaxValue;
    public int MaxRsrp { get; set; } = int.MinValue;
    public double AvgRsrp { get; set; }
    
    private readonly object _lock = new();
    
    public void AddObservation(double lat, double lon, int rsrp)
    {
        lock (_lock)
        {
            ObservationCount++;
            LastSeen = DateTime.UtcNow;
            
            // Update signal stats
            MinRsrp = Math.Min(MinRsrp, rsrp);
            MaxRsrp = Math.Max(MaxRsrp, rsrp);
            AvgRsrp = ((AvgRsrp * (ObservationCount - 1)) + rsrp) / ObservationCount;
            
            // Update location estimate (weighted by signal strength)
            // Stronger signal = closer to tower = higher weight
            var weight = Math.Max(1, rsrp + 140); // RSRP is negative, -140 to -40 dBm
            
            if (EstimatedLatitude.HasValue && EstimatedLongitude.HasValue)
            {
                var totalWeight = LocationConfidence + weight;
                EstimatedLatitude = (EstimatedLatitude.Value * LocationConfidence + lat * weight) / totalWeight;
                EstimatedLongitude = (EstimatedLongitude.Value * LocationConfidence + lon * weight) / totalWeight;
                LocationConfidence = totalWeight;
            }
            else
            {
                EstimatedLatitude = lat;
                EstimatedLongitude = lon;
                LocationConfidence = weight;
            }
        }
    }
}

/// <summary>
/// Current cellular state for a node.
/// </summary>
public class NodeCellularState
{
    public string NodeId { get; }
    public string Technology { get; private set; } = "";
    public long CurrentCellId { get; private set; }
    public int CurrentRsrp { get; private set; }
    public int CurrentRsrq { get; private set; }
    public int CurrentSinr { get; private set; }
    public DateTime LastUpdate { get; private set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public double LocationConfidence { get; set; }
    
    public bool IsActive => DateTime.UtcNow - LastUpdate < TimeSpan.FromMinutes(5);
    
    public NodeCellularState(string nodeId)
    {
        NodeId = nodeId;
    }
    
    public void Update(CellularStatusData data)
    {
        Technology = data.Technology;
        CurrentCellId = data.CellId;
        CurrentRsrp = data.Rsrp;
        CurrentRsrq = data.Rsrq;
        CurrentSinr = data.Sinr;
        LastUpdate = DateTime.UtcNow;
        
        if (data.Latitude.HasValue) LastLatitude = data.Latitude;
        if (data.Longitude.HasValue) LastLongitude = data.Longitude;
    }
}

/// <summary>
/// Sample for RF fingerprinting training.
/// </summary>
public class CellMeasurementSample
{
    public DateTime Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Technology { get; set; } = "";
    public long CellId { get; set; }
    public int Rsrp { get; set; }
    public int Rsrq { get; set; }
    public int Sinr { get; set; }
    public int TimingAdvance { get; set; }
    public List<NeighborSample> NeighborCells { get; set; } = new();
}

public class NeighborSample
{
    public long CellId { get; set; }
    public int Rsrp { get; set; }
    public int Rsrq { get; set; }
}

/// <summary>
/// Coverage statistics summary.
/// </summary>
public class CoverageStatistics
{
    public int TotalTowers { get; set; }
    public int LteTowers { get; set; }
    public int Nr5gTowers { get; set; }
    public int WcdmaTowers { get; set; }
    public int GsmTowers { get; set; }
    public int TotalObservations { get; set; }
    public int TotalTrainingSamples { get; set; }
    public int ActiveNodes { get; set; }
}
