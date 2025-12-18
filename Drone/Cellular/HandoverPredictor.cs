/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    HANDOVER PREDICTOR - LSTM MODEL                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Sequence model that predicts tower handovers before they're needed.       ║
 * ║  Analyzes temporal signal patterns to optimize cell transitions.           ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Concurrent;

namespace NIGHTFRAME.Drone.Cellular;

/// <summary>
/// LSTM-based handover predictor that analyzes signal time series
/// to predict when and where to switch cells.
/// </summary>
public class HandoverPredictor : IDisposable
{
    // Signal history buffer
    private readonly ConcurrentQueue<SignalSnapshot> _signalHistory = new();
    private readonly int _sequenceLength;
    private readonly int _numNeighbors;
    
    // ONNX Runtime session for inference
    private InferenceSession? _session;
    
    // Handover thresholds
    private const float RSRP_HANDOVER_THRESHOLD = -110f;    // dBm - below this, consider handover
    private const float RSRP_HYSTERESIS = 3f;               // dB - neighbor must be this much better
    private const float SIGNAL_FADE_RATE_THRESHOLD = -2f;   // dB/sec - fast degradation
    private const int PREDICTION_HORIZON_MS = 5000;         // Look ahead 5 seconds
    
    public bool IsModelLoaded => _session != null;
    
    /// <summary>
    /// Creates a handover predictor.
    /// </summary>
    /// <param name="sequenceLength">Number of time steps to analyze (default: 50)</param>
    /// <param name="numNeighbors">Max neighbor cells to track (default: 6)</param>
    public HandoverPredictor(int sequenceLength = 50, int numNeighbors = 6)
    {
        _sequenceLength = sequenceLength;
        _numNeighbors = numNeighbors;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              DATA STRUCTURES
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Single point in time signal snapshot.
    /// </summary>
    private class SignalSnapshot
    {
        public DateTime Timestamp { get; init; }
        public float ServingRSRP { get; init; }
        public float ServingRSRQ { get; init; }
        public float ServingSINR { get; init; }
        public long ServingCellId { get; init; }
        public float[] NeighborRSRPs { get; init; } = Array.Empty<float>();
        public long[] NeighborCellIds { get; init; } = Array.Empty<long>();
        public float VelocityKmh { get; init; }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              DATA INGESTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Adds a new signal measurement to the history buffer.
    /// </summary>
    public void RecordMeasurement(
        CellTowerMeasurement serving, 
        List<NeighborCell> neighbors,
        float velocityKmh = 0)
    {
        // Sort neighbors by signal strength
        var sortedNeighbors = neighbors
            .OrderByDescending(n => n.RSRP)
            .Take(_numNeighbors)
            .ToList();
        
        var snapshot = new SignalSnapshot
        {
            Timestamp = DateTime.UtcNow,
            ServingRSRP = serving.RSRP,
            ServingRSRQ = serving.RSRQ,
            ServingSINR = serving.SINR,
            ServingCellId = serving.CellId,
            NeighborRSRPs = sortedNeighbors.Select(n => n.RSRP).ToArray(),
            NeighborCellIds = sortedNeighbors.Select(n => n.CellId).ToArray(),
            VelocityKmh = velocityKmh
        };
        
        _signalHistory.Enqueue(snapshot);
        
        // Trim old entries
        while (_signalHistory.Count > _sequenceLength * 2)
        {
            _signalHistory.TryDequeue(out _);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              RULE-BASED PREDICTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Predicts handover using rule-based analysis.
    /// Falls back to this when no ML model is loaded.
    /// </summary>
    public HandoverPrediction PredictRuleBased()
    {
        var history = _signalHistory.ToArray();
        
        if (history.Length < 5)
        {
            return new HandoverPrediction { HandoverImminent = false };
        }
        
        var current = history[^1];
        var recent = history.TakeLast(10).ToArray();
        
        // ─────────────────────────────────────────────────────────────
        // Check 1: Signal already below threshold
        // ─────────────────────────────────────────────────────────────
        if (current.ServingRSRP < RSRP_HANDOVER_THRESHOLD)
        {
            var bestNeighbor = FindBestNeighbor(current);
            if (bestNeighbor != null)
            {
                return new HandoverPrediction
                {
                    HandoverImminent = true,
                    TimeToHandoverMs = 0,
                    RecommendedCellId = bestNeighbor.Value.cellId,
                    TargetCellRSRP = bestNeighbor.Value.rsrp,
                    CurrentCellRSRP = current.ServingRSRP,
                    Reason = HandoverReason.SignalDegrading
                };
            }
        }
        
        // ─────────────────────────────────────────────────────────────
        // Check 2: Signal fading rapidly
        // ─────────────────────────────────────────────────────────────
        var fadeRate = CalculateFadeRate(recent);
        
        if (fadeRate < SIGNAL_FADE_RATE_THRESHOLD)
        {
            // Predict when signal will cross threshold
            var currentRSRP = current.ServingRSRP;
            var timeToThreshold = (currentRSRP - RSRP_HANDOVER_THRESHOLD) / Math.Abs(fadeRate);
            var timeMs = timeToThreshold * 1000; // Convert to ms (assuming 1 sample/sec)
            
            if (timeMs < PREDICTION_HORIZON_MS)
            {
                var bestNeighbor = FindBestNeighbor(current);
                if (bestNeighbor != null)
                {
                    return new HandoverPrediction
                    {
                        HandoverImminent = true,
                        TimeToHandoverMs = (float)timeMs,
                        RecommendedCellId = bestNeighbor.Value.cellId,
                        TargetCellRSRP = bestNeighbor.Value.rsrp,
                        CurrentCellRSRP = current.ServingRSRP,
                        Reason = HandoverReason.SignalDegrading
                    };
                }
            }
        }
        
        // ─────────────────────────────────────────────────────────────
        // Check 3: Neighbor significantly stronger
        // ─────────────────────────────────────────────────────────────
        var strongerNeighbor = FindStrongerNeighbor(current);
        if (strongerNeighbor != null)
        {
            return new HandoverPrediction
            {
                HandoverImminent = true,
                TimeToHandoverMs = 1000, // Wait 1 second before recommending
                RecommendedCellId = strongerNeighbor.Value.cellId,
                TargetCellRSRP = strongerNeighbor.Value.rsrp,
                CurrentCellRSRP = current.ServingRSRP,
                Reason = HandoverReason.NeighborStronger
            };
        }
        
        // ─────────────────────────────────────────────────────────────
        // Check 4: High velocity prediction
        // ─────────────────────────────────────────────────────────────
        if (current.VelocityKmh > 50) // Moving fast
        {
            var predictedCoverage = PredictCoverageAtVelocity(current, recent);
            if (predictedCoverage != null)
            {
                return new HandoverPrediction
                {
                    HandoverImminent = true,
                    TimeToHandoverMs = predictedCoverage.Value.timeMs,
                    RecommendedCellId = predictedCoverage.Value.cellId,
                    TargetCellRSRP = predictedCoverage.Value.rsrp,
                    CurrentCellRSRP = current.ServingRSRP,
                    Reason = HandoverReason.VelocityBased
                };
            }
        }
        
        return new HandoverPrediction { HandoverImminent = false };
    }
    
    /// <summary>
    /// Calculates signal fade rate in dB/second.
    /// </summary>
    private float CalculateFadeRate(SignalSnapshot[] recent)
    {
        if (recent.Length < 2) return 0;
        
        // Linear regression on RSRP values
        var n = recent.Length;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;
        
        for (int i = 0; i < n; i++)
        {
            var x = (recent[i].Timestamp - recent[0].Timestamp).TotalSeconds;
            var y = recent[i].ServingRSRP;
            
            sumX += x;
            sumY += y;
            sumXY += x * y;
            sumX2 += x * x;
        }
        
        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        
        return (float)slope;
    }
    
    /// <summary>
    /// Finds the best neighbor cell to handover to.
    /// </summary>
    private (long cellId, float rsrp)? FindBestNeighbor(SignalSnapshot current)
    {
        if (current.NeighborRSRPs.Length == 0) return null;
        
        var bestIdx = 0;
        var bestRsrp = current.NeighborRSRPs[0];
        
        for (int i = 1; i < current.NeighborRSRPs.Length; i++)
        {
            if (current.NeighborRSRPs[i] > bestRsrp)
            {
                bestRsrp = current.NeighborRSRPs[i];
                bestIdx = i;
            }
        }
        
        // Only recommend if neighbor is usable
        if (bestRsrp > -115)
        {
            return (current.NeighborCellIds[bestIdx], bestRsrp);
        }
        
        return null;
    }
    
    /// <summary>
    /// Finds a neighbor that's significantly stronger (with hysteresis).
    /// </summary>
    private (long cellId, float rsrp)? FindStrongerNeighbor(SignalSnapshot current)
    {
        for (int i = 0; i < current.NeighborRSRPs.Length; i++)
        {
            if (current.NeighborRSRPs[i] > current.ServingRSRP + RSRP_HYSTERESIS)
            {
                return (current.NeighborCellIds[i], current.NeighborRSRPs[i]);
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Predicts coverage changes based on velocity.
    /// </summary>
    private (long cellId, float rsrp, float timeMs)? PredictCoverageAtVelocity(
        SignalSnapshot current, SignalSnapshot[] recent)
    {
        // Look at which neighbor signals are rising fastest
        if (recent.Length < 5 || current.NeighborRSRPs.Length == 0) return null;
        
        // This would normally use the ML model for better prediction
        // For now, use simple trend analysis
        
        // Find neighbor with strongest upward trend
        var neighborTrends = new List<(long cellId, float trend, float currentRsrp)>();
        
        for (int i = 0; i < current.NeighborRSRPs.Length; i++)
        {
            var cellId = current.NeighborCellIds[i];
            var rsrpValues = new List<float>();
            
            // Collect RSRP history for this neighbor
            foreach (var snapshot in recent)
            {
                var idx = Array.IndexOf(snapshot.NeighborCellIds, cellId);
                if (idx >= 0)
                {
                    rsrpValues.Add(snapshot.NeighborRSRPs[idx]);
                }
            }
            
            if (rsrpValues.Count >= 3)
            {
                // Simple trend: last - first
                var trend = rsrpValues[^1] - rsrpValues[0];
                neighborTrends.Add((cellId, trend, current.NeighborRSRPs[i]));
            }
        }
        
        // Find the neighbor with best rising trend
        var rising = neighborTrends
            .Where(t => t.trend > 1f) // At least 1 dB increase
            .OrderByDescending(t => t.trend)
            .FirstOrDefault();
        
        if (rising.cellId != 0 && rising.currentRsrp > current.ServingRSRP - 5)
        {
            // Estimate time until this neighbor becomes optimal
            var gapToClose = current.ServingRSRP - rising.currentRsrp + RSRP_HYSTERESIS;
            var timeToOptimal = (gapToClose / rising.trend) * 1000; // ms
            
            if (timeToOptimal > 0 && timeToOptimal < PREDICTION_HORIZON_MS)
            {
                return (rising.cellId, rising.currentRsrp, (float)timeToOptimal);
            }
        }
        
        return null;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              ONNX INFERENCE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Loads an ONNX model for ML-based prediction.
    /// </summary>
    public void LoadModel(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"∴ Model file not found: {modelPath}");
            return;
        }
        
        try
        {
            _session = new InferenceSession(modelPath);
            Console.WriteLine($"◈ Handover model loaded from {modelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Failed to load model: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Predicts handover using the loaded ONNX model.
    /// </summary>
    public HandoverPrediction PredictWithModel()
    {
        if (_session == null)
        {
            return PredictRuleBased();
        }
        
        var history = _signalHistory.TakeLast(_sequenceLength).ToArray();
        
        if (history.Length < _sequenceLength)
        {
            return PredictRuleBased(); // Not enough data
        }
        
        // Build input tensor: [batch=1, seq_length, features]
        // Features: serving RSRP, RSRQ, SINR + neighbor RSRPs + velocity
        var numFeatures = 3 + _numNeighbors + 1;
        var inputData = new float[1, _sequenceLength, numFeatures];
        
        for (int t = 0; t < _sequenceLength; t++)
        {
            var snapshot = history[t];
            inputData[0, t, 0] = NormalizeRSRP(snapshot.ServingRSRP);
            inputData[0, t, 1] = NormalizeRSRQ(snapshot.ServingRSRQ);
            inputData[0, t, 2] = NormalizeSINR(snapshot.ServingSINR);
            
            for (int n = 0; n < _numNeighbors; n++)
            {
                inputData[0, t, 3 + n] = n < snapshot.NeighborRSRPs.Length 
                    ? NormalizeRSRP(snapshot.NeighborRSRPs[n]) 
                    : 0;
            }
            
            inputData[0, t, numFeatures - 1] = snapshot.VelocityKmh / 200f; // Normalize velocity
        }
        
        // Create tensor
        var tensor = new DenseTensor<float>(
            inputData.Cast<float>().ToArray(), 
            new[] { 1, _sequenceLength, numFeatures });
        
        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", tensor)
        };
        
        using var results = _session.Run(inputs);
        var output = results.First().AsTensor<float>();
        
        // Parse output: [handover_prob, time_to_handover, best_neighbor_idx]
        var handoverProb = output[0];
        var timeToHandover = output[1] * PREDICTION_HORIZON_MS;
        var bestNeighborIdx = (int)output[2];
        
        var current = history[^1];
        
        return new HandoverPrediction
        {
            HandoverImminent = handoverProb > 0.7f,
            TimeToHandoverMs = timeToHandover,
            RecommendedCellId = bestNeighborIdx < current.NeighborCellIds.Length 
                ? current.NeighborCellIds[bestNeighborIdx] 
                : 0,
            TargetCellRSRP = bestNeighborIdx < current.NeighborRSRPs.Length 
                ? current.NeighborRSRPs[bestNeighborIdx] 
                : 0,
            CurrentCellRSRP = current.ServingRSRP,
            Reason = handoverProb > 0.7f ? HandoverReason.SignalDegrading : HandoverReason.None
        };
    }
    
    private static float NormalizeRSRP(float rsrp) => (rsrp + 140) / 100f;
    private static float NormalizeRSRQ(float rsrq) => (rsrq + 20) / 17f;
    private static float NormalizeSINR(float sinr) => (sinr + 20) / 50f;
    
    public void Dispose()
    {
        _session?.Dispose();
    }
}
