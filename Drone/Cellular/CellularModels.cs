/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    CELLULAR INTELLIGENCE - DATA MODELS                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Data structures for cell tower fingerprinting and signal analysis.        ║
 * ║  Supports LTE, 5G NR, and legacy GSM/UMTS measurements.                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Cellular;

// ═══════════════════════════════════════════════════════════════════════════════
//                              INPUT DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Raw cell tower measurement data for RF fingerprinting.
/// </summary>
public class CellTowerMeasurement
{
    // Identification
    public int MCC { get; set; }                    // Mobile Country Code (e.g., 310 = USA)
    public int MNC { get; set; }                    // Mobile Network Code (e.g., 410 = AT&T)
    public long CellId { get; set; }                // Cell ID (CID/ECI for LTE)
    public int LAC { get; set; }                    // Location Area Code (TAC for LTE)
    public int PhysicalCellId { get; set; }         // PCI for LTE/NR
    
    // Signal Quality (LTE)
    public float RSRP { get; set; }                 // Reference Signal Received Power (-140 to -44 dBm)
    public float RSRQ { get; set; }                 // Reference Signal Received Quality (-20 to -3 dB)
    public float RSSI { get; set; }                 // Received Signal Strength Indicator (legacy)
    public float SINR { get; set; }                 // Signal to Interference + Noise Ratio (dB)
    
    // Distance Metrics
    public float TimingAdvance { get; set; }        // TA value (approx distance indicator)
    public int EARFCN { get; set; }                 // E-UTRA Absolute Radio Frequency Channel Number
    
    // Metadata
    public DateTime Timestamp { get; set; }
    public string RadioType { get; set; } = "LTE"; // GSM, UMTS, LTE, NR
    public bool IsServingCell { get; set; }         // Is this the connected cell?
    
    /// <summary>
    /// Normalizes signal values to 0-1 scale for neural network input.
    /// </summary>
    public float[] ToNormalizedVector()
    {
        return new[]
        {
            // RSRP: -140 to -44 dBm -> 0 to 1
            Normalize(RSRP, -140, -44),
            
            // RSRQ: -20 to -3 dB -> 0 to 1
            Normalize(RSRQ, -20, -3),
            
            // RSSI: -113 to -51 dBm -> 0 to 1
            Normalize(RSSI, -113, -51),
            
            // SINR: -20 to 30 dB -> 0 to 1
            Normalize(SINR, -20, 30),
            
            // Timing Advance: 0 to 1282 -> 0 to 1
            Normalize(TimingAdvance, 0, 1282),
            
            // Cell ID encoded as multiple features (modulo encoding)
            (CellId % 1000) / 1000f,
            ((CellId / 1000) % 1000) / 1000f,
            
            // Network encoding
            MCC / 1000f,
            MNC / 1000f
        };
    }
    
    private static float Normalize(float value, float min, float max)
    {
        return Math.Clamp((value - min) / (max - min), 0f, 1f);
    }
    
    /// <summary>
    /// Estimates rough distance to tower using Timing Advance.
    /// Each TA unit is approximately 78.12 meters for LTE.
    /// </summary>
    public float EstimateDistanceMeters()
    {
        const float MetersPerTA = 78.12f; // LTE TA resolution
        return TimingAdvance * MetersPerTA;
    }
}

/// <summary>
/// Neighbor cell measurement for handover prediction.
/// </summary>
public class NeighborCell
{
    public long CellId { get; set; }
    public int PhysicalCellId { get; set; }
    public float RSRP { get; set; }
    public float RSRQ { get; set; }
    public int EARFCN { get; set; }
}

/// <summary>
/// Time-series signal sample for LSTM prediction.
/// </summary>
public class SignalTimeSeries
{
    public DateTime Timestamp { get; set; }
    public float ServingCellRSRP { get; set; }
    public float ServingCellRSRQ { get; set; }
    public List<NeighborCell> Neighbors { get; set; } = new();
    public float VelocityKmh { get; set; }          // Device velocity (if available)
    public float HeadingDegrees { get; set; }       // Movement direction
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              OUTPUT DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Geolocation prediction output.
/// </summary>
public class LocationPrediction
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float ConfidenceRadius { get; set; }     // Uncertainty in meters
    public float Confidence { get; set; }           // 0-1 confidence score
    public string Method { get; set; } = "RF_FINGERPRINT";
}

/// <summary>
/// Handover prediction output.
/// </summary>
public class HandoverPrediction
{
    public bool HandoverImminent { get; set; }      // Should we switch?
    public float TimeToHandoverMs { get; set; }     // Predicted time until handover needed
    public long RecommendedCellId { get; set; }     // Best target cell
    public float TargetCellRSRP { get; set; }       // Predicted signal at target
    public float CurrentCellRSRP { get; set; }      // Current signal (for comparison)
    public HandoverReason Reason { get; set; }
}

public enum HandoverReason
{
    None,
    SignalDegrading,                // Current cell signal dropping
    NeighborStronger,               // Better cell available
    LoadBalancing,                  // Current cell congested
    CoverageHole,                   // Entering known weak area
    VelocityBased                   // Moving fast, preemptive switch
}

/// <summary>
/// Signal quality assessment.
/// </summary>
public class SignalQuality
{
    public SignalStrength Strength { get; set; }
    public float Score { get; set; }                // 0-100 quality score
    public string Description { get; set; } = "";
    public bool SufficientForData { get; set; }
    public bool SufficientForVoice { get; set; }
    public float EstimatedThroughputMbps { get; set; }
}

public enum SignalStrength
{
    Excellent,      // RSRP > -80 dBm
    Good,           // -80 to -90 dBm
    Fair,           // -90 to -100 dBm
    Poor,           // -100 to -110 dBm
    NoSignal        // < -110 dBm
}
