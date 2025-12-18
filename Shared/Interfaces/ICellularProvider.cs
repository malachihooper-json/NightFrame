/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SHARED INTERFACES - CELLULAR PROVIDER                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Abstraction for cellular intelligence capabilities.                       ║
 * ║  Enables Android/Desktop/Modem implementations to share interface.         ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Shared.Interfaces;

/// <summary>
/// Provides cellular network intelligence capabilities.
/// Implemented by ModemController (desktop) and TelephonyManager (Android).
/// </summary>
public interface ICellularProvider
{
    /// <summary>
    /// Gets whether cellular capabilities are available.
    /// </summary>
    bool IsAvailable { get; }
    
    /// <summary>
    /// Gets the current serving cell information.
    /// </summary>
    CellInfo? CurrentCell { get; }
    
    /// <summary>
    /// Gets neighbor cell list.
    /// </summary>
    IReadOnlyList<CellInfo> NeighborCells { get; }
    
    /// <summary>
    /// Gets the current signal quality.
    /// </summary>
    SignalQualityInfo SignalQuality { get; }
    
    /// <summary>
    /// Gets RF-based location prediction (if available).
    /// </summary>
    LocationPredictionInfo? PredictedLocation { get; }
    
    /// <summary>
    /// Gets handover prediction (if available).
    /// </summary>
    HandoverPredictionInfo? HandoverPrediction { get; }
    
    /// <summary>
    /// Event raised when handover is recommended.
    /// </summary>
    event Action<HandoverPredictionInfo>? OnHandoverRecommended;
    
    /// <summary>
    /// Event raised when location prediction updates.
    /// </summary>
    event Action<LocationPredictionInfo>? OnLocationUpdated;
    
    /// <summary>
    /// Starts the cellular intelligence service.
    /// </summary>
    Task StartAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Stops the cellular intelligence service.
    /// </summary>
    void Stop();
    
    /// <summary>
    /// Forces a handover to the specified cell.
    /// </summary>
    Task<bool> ExecuteHandoverAsync(long targetCellId, CancellationToken ct = default);
}

/// <summary>
/// Cell tower information.
/// </summary>
public record CellInfo
{
    public required long CellId { get; init; }
    public required int PhysicalCellId { get; init; }
    public required int MCC { get; init; }
    public required int MNC { get; init; }
    public required int LAC { get; init; }
    public required float RSRP { get; init; }
    public required float RSRQ { get; init; }
    public float SINR { get; init; }
    public float TimingAdvance { get; init; }
    public int EARFCN { get; init; }
    public string RadioType { get; init; } = "LTE";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Signal quality summary.
/// </summary>
public record SignalQualityInfo
{
    public required SignalStrength Strength { get; init; }
    public required float Score { get; init; }
    public required bool SufficientForData { get; init; }
    public required bool SufficientForVoice { get; init; }
    public float EstimatedThroughputMbps { get; init; }
}

/// <summary>
/// RF-based location prediction.
/// </summary>
public record LocationPredictionInfo
{
    public required double Latitude { get; init; }
    public required double Longitude { get; init; }
    public required float ConfidenceRadius { get; init; }
    public required float Confidence { get; init; }
    public string Method { get; init; } = "RF_FINGERPRINT";
}

/// <summary>
/// Handover prediction.
/// </summary>
public record HandoverPredictionInfo
{
    public required bool HandoverImminent { get; init; }
    public required float TimeToHandoverMs { get; init; }
    public required long RecommendedCellId { get; init; }
    public required float TargetCellRSRP { get; init; }
    public required float CurrentCellRSRP { get; init; }
    public required HandoverReason Reason { get; init; }
}

public enum SignalStrength
{
    Excellent,      // RSRP > -80 dBm
    Good,           // -80 to -90 dBm
    Fair,           // -90 to -100 dBm
    Poor,           // -100 to -110 dBm
    NoSignal        // < -110 dBm
}

public enum HandoverReason
{
    None,
    SignalDegrading,
    NeighborStronger,
    LoadBalancing,
    CoverageHole,
    VelocityBased
}
