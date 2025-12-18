/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    RF FINGERPRINT LOCATOR - SIMPLE MODEL                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  K-Nearest Neighbors based geolocation from cell tower signals.            ║
 * ║  Lightweight, no ML framework dependencies for AOT compatibility.          ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Collections.Concurrent;

namespace NIGHTFRAME.Drone.Cellular;

/// <summary>
/// RF Fingerprinting model using K-Nearest Neighbors.
/// Lightweight, works with Native AOT, no ML framework required.
/// </summary>
public class RFLocatorModel
{
    // Training data
    private readonly List<FingerPrint> _fingerprints = new();
    private readonly ConcurrentBag<TrainingPoint> _pendingTrainingData = new();
    
    // Model parameters
    private const int K = 5;  // Number of neighbors to consider
    
    public bool IsModelLoaded => _fingerprints.Count > 0;
    public int TrainingDataCount => _pendingTrainingData.Count + _fingerprints.Count;
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              DATA STRUCTURES
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// RF fingerprint with location.
    /// </summary>
    public class FingerPrint
    {
        public float[] Features { get; set; } = Array.Empty<float>();
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public long CellId { get; set; }
    }
    
    /// <summary>
    /// Training point with ground truth location.
    /// </summary>
    public class TrainingPoint
    {
        public CellTowerMeasurement Measurement { get; set; } = new();
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              DATA COLLECTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Adds a training point with known location.
    /// </summary>
    public void AddTrainingPoint(CellTowerMeasurement measurement, double latitude, double longitude)
    {
        _pendingTrainingData.Add(new TrainingPoint
        {
            Measurement = measurement,
            Latitude = latitude,
            Longitude = longitude
        });
        
        if (_pendingTrainingData.Count % 1000 == 0)
        {
            Console.WriteLine($"◎ RF Fingerprint: {_pendingTrainingData.Count} training points collected");
        }
    }
    
    /// <summary>
    /// Saves collected training data to CSV.
    /// </summary>
    public async Task SaveTrainingDataAsync(string path)
    {
        var lines = new List<string>
        {
            "RSRP,RSRQ,RSSI,SINR,TA,CellIdLow,CellIdHigh,MCC,MNC,Latitude,Longitude"
        };
        
        foreach (var point in _pendingTrainingData)
        {
            var m = point.Measurement;
            var normalized = m.ToNormalizedVector();
            lines.Add($"{normalized[0]:F6},{normalized[1]:F6},{normalized[2]:F6},{normalized[3]:F6},{normalized[4]:F6},{normalized[5]:F6},{normalized[6]:F6},{normalized[7]:F6},{normalized[8]:F6},{point.Latitude:F8},{point.Longitude:F8}");
        }
        
        await File.WriteAllLinesAsync(path, lines);
        Console.WriteLine($"◈ Training data saved to {path} ({lines.Count - 1} points)");
    }
    
    /// <summary>
    /// Loads training data from CSV.
    /// </summary>
    public async Task LoadTrainingDataAsync(string path)
    {
        var lines = await File.ReadAllLinesAsync(path);
        
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            if (parts.Length < 11) continue;
            
            try
            {
                var measurement = new CellTowerMeasurement
                {
                    RSRP = -140 + (float.Parse(parts[0]) * 96),
                    RSRQ = -20 + (float.Parse(parts[1]) * 17),
                    RSSI = -113 + (float.Parse(parts[2]) * 62),
                    SINR = -20 + (float.Parse(parts[3]) * 50),
                    TimingAdvance = float.Parse(parts[4]) * 1282
                };
                
                _pendingTrainingData.Add(new TrainingPoint
                {
                    Measurement = measurement,
                    Latitude = double.Parse(parts[9]),
                    Longitude = double.Parse(parts[10])
                });
            }
            catch { }
        }
        
        Console.WriteLine($"◈ Loaded {_pendingTrainingData.Count} training points from {path}");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              TRAINING (KNN - just organize data)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// "Trains" the model by organizing fingerprints for fast lookup.
    /// KNN doesn't have a traditional training phase - it just stores data.
    /// </summary>
    public void Train()
    {
        if (_pendingTrainingData.Count < 10)
        {
            Console.WriteLine($"∴ Insufficient training data: {_pendingTrainingData.Count}/10 minimum");
            return;
        }
        
        Console.WriteLine($"◈ Building RF Fingerprint database with {_pendingTrainingData.Count} samples...");
        
        // Convert training points to fingerprints
        foreach (var point in _pendingTrainingData)
        {
            _fingerprints.Add(new FingerPrint
            {
                Features = point.Measurement.ToNormalizedVector(),
                Latitude = point.Latitude,
                Longitude = point.Longitude,
                CellId = point.Measurement.CellId
            });
        }
        
        // Group by cell ID for faster lookup
        var byCellId = _fingerprints.GroupBy(f => f.CellId).Count();
        
        Console.WriteLine($"◈ Training complete:");
        Console.WriteLine($"   Fingerprints: {_fingerprints.Count:N0}");
        Console.WriteLine($"   Unique cells: {byCellId:N0}");
        Console.WriteLine($"   Avg per cell: {_fingerprints.Count / Math.Max(byCellId, 1):F1}");
    }
    
    /// <summary>
    /// Saves the model to disk using CSV format (AOT-compatible).
    /// </summary>
    public void SaveModel(string path)
    {
        if (_fingerprints.Count == 0)
        {
            Console.WriteLine("∴ No fingerprints to save");
            return;
        }
        
        using var writer = new StreamWriter(path);
        
        // Header
        writer.WriteLine("Features,Latitude,Longitude,CellId");
        
        // Data
        foreach (var fp in _fingerprints)
        {
            var featuresStr = string.Join(";", fp.Features.Select(f => f.ToString("G9")));
            writer.WriteLine($"{featuresStr},{fp.Latitude:G15},{fp.Longitude:G15},{fp.CellId}");
        }
        
        Console.WriteLine($"◈ Model saved to {path} ({_fingerprints.Count:N0} fingerprints)");
    }
    
    /// <summary>
    /// Loads a pre-trained model from CSV.
    /// </summary>
    public void LoadModel(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"∴ Model file not found: {path}");
            return;
        }
        
        _fingerprints.Clear();
        
        using var reader = new StreamReader(path);
        var header = reader.ReadLine(); // Skip header
        
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            try
            {
                var parts = line.Split(',');
                if (parts.Length < 4) continue;
                
                var features = parts[0].Split(';').Select(float.Parse).ToArray();
                
                _fingerprints.Add(new FingerPrint
                {
                    Features = features,
                    Latitude = double.Parse(parts[1]),
                    Longitude = double.Parse(parts[2]),
                    CellId = long.Parse(parts[3])
                });
            }
            catch { }
        }
        
        Console.WriteLine($"◈ Model loaded from {path} ({_fingerprints.Count:N0} fingerprints)");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              INFERENCE (KNN)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Predicts location using K-Nearest Neighbors.
    /// </summary>
    public LocationPrediction Predict(CellTowerMeasurement measurement)
    {
        if (_fingerprints.Count == 0)
        {
            return new LocationPrediction
            {
                Confidence = 0,
                Method = "NO_MODEL"
            };
        }
        
        var queryFeatures = measurement.ToNormalizedVector();
        
        // Find K nearest neighbors
        var neighbors = _fingerprints
            .Select(fp => (
                Fingerprint: fp,
                Distance: EuclideanDistance(queryFeatures, fp.Features)
            ))
            .OrderBy(x => x.Distance)
            .Take(K)
            .ToList();
        
        if (neighbors.Count == 0)
        {
            return new LocationPrediction { Confidence = 0 };
        }
        
        // Weighted average by inverse distance
        var totalWeight = 0.0;
        var weightedLat = 0.0;
        var weightedLon = 0.0;
        
        foreach (var (fp, distance) in neighbors)
        {
            var weight = 1.0 / (distance + 0.0001); // Avoid division by zero
            weightedLat += fp.Latitude * weight;
            weightedLon += fp.Longitude * weight;
            totalWeight += weight;
        }
        
        var avgDistance = neighbors.Average(n => n.Distance);
        
        // Estimate confidence based on neighbor distance and signal quality
        var signalQuality = ATCommandParser.ClassifySignal(measurement);
        var distanceConfidence = Math.Max(0, 1 - avgDistance); // Lower distance = higher confidence
        var confidence = (signalQuality.Score / 100f) * 0.5f + (float)distanceConfidence * 0.5f;
        
        // Estimate radius based on neighbor spread
        var latSpread = neighbors.Max(n => n.Fingerprint.Latitude) - neighbors.Min(n => n.Fingerprint.Latitude);
        var lonSpread = neighbors.Max(n => n.Fingerprint.Longitude) - neighbors.Min(n => n.Fingerprint.Longitude);
        var spreadMeters = Math.Max(LatDiffToMeters(latSpread), LonDiffToMeters(lonSpread, weightedLat / totalWeight));
        
        var radiusMeters = Math.Max(50, Math.Min(2000, spreadMeters));
        
        return new LocationPrediction
        {
            Latitude = weightedLat / totalWeight,
            Longitude = weightedLon / totalWeight,
            Confidence = confidence,
            ConfidenceRadius = (float)radiusMeters,
            Method = "RF_FINGERPRINT_KNN"
        };
    }
    
    /// <summary>
    /// Predicts location using multiple measurements.
    /// </summary>
    public LocationPrediction PredictFromMultiple(IEnumerable<CellTowerMeasurement> measurements)
    {
        var results = measurements.Select(m => (Prediction: Predict(m), Measurement: m)).ToList();
        
        if (!results.Any())
        {
            return new LocationPrediction { Confidence = 0 };
        }
        
        // Weight by signal strength
        var totalWeight = results.Sum(r => Math.Pow(10, r.Measurement.RSRP / 10));
        
        var weightedLat = results.Sum(r =>
            r.Prediction.Latitude * Math.Pow(10, r.Measurement.RSRP / 10)) / totalWeight;
        
        var weightedLon = results.Sum(r =>
            r.Prediction.Longitude * Math.Pow(10, r.Measurement.RSRP / 10)) / totalWeight;
        
        return new LocationPrediction
        {
            Latitude = weightedLat,
            Longitude = weightedLon,
            Confidence = (float)results.Average(r => r.Prediction.Confidence),
            ConfidenceRadius = (float)results.Average(r => r.Prediction.ConfidenceRadius),
            Method = "RF_FINGERPRINT_MULTI"
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              UTILITIES
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private static double EuclideanDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length) return double.MaxValue;
        
        var sum = 0.0;
        for (int i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }
        return Math.Sqrt(sum);
    }
    
    private static double LatDiffToMeters(double latDiff)
    {
        return latDiff * 111320; // Approx meters per degree latitude
    }
    
    private static double LonDiffToMeters(double lonDiff, double latitude)
    {
        return lonDiff * 111320 * Math.Cos(latitude * Math.PI / 180);
    }
}
