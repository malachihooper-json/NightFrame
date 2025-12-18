/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    OPENCELLID DATABASE INTEGRATION                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Downloads and imports cell tower location data from OpenCellID.           ║
 * ║  Creates training data for the RF fingerprinting neural network.           ║
 * ║  API: https://opencellid.org/                                             ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace NIGHTFRAME.Drone.Cellular;

/// <summary>
/// OpenCellID database importer for cell tower geolocation training.
/// </summary>
public class OpenCellIDImporter
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly string _cacheDir;
    
    // OpenCellID CSV columns:
    // radio,mcc,net,area,cell,unit,lon,lat,range,samples,changeable,created,updated,averageSignal
    
    public OpenCellIDImporter(string? apiToken = null)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(30); // Large downloads
        
        // API token from env or parameter (get free token at opencellid.org)
        _apiToken = apiToken ?? Environment.GetEnvironmentVariable("OPENCELLID_TOKEN") ?? "";
        
        _cacheDir = Path.Combine(AppContext.BaseDirectory, "data", "opencellid");
        Directory.CreateDirectory(_cacheDir);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          DOWNLOAD METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Downloads the full OpenCellID database (large! ~1GB compressed).
    /// Requires API token.
    /// </summary>
    public async Task<string> DownloadFullDatabaseAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiToken))
        {
            Console.WriteLine("∴ OpenCellID API token required for full database");
            Console.WriteLine("  Get free token at: https://opencellid.org/register");
            Console.WriteLine("  Set env: OPENCELLID_TOKEN=your_token");
            throw new InvalidOperationException("OpenCellID API token required");
        }
        
        var url = $"https://opencellid.org/ocid/downloads?token={_apiToken}&type=full&file=cell_towers.csv.gz";
        var localPath = Path.Combine(_cacheDir, "cell_towers.csv.gz");
        var csvPath = Path.Combine(_cacheDir, "cell_towers.csv");
        
        if (File.Exists(csvPath))
        {
            Console.WriteLine($"◎ Using cached database: {csvPath}");
            return csvPath;
        }
        
        Console.WriteLine("◈ Downloading OpenCellID full database (~1GB)...");
        Console.WriteLine("  This may take 10-30 minutes depending on connection.");
        
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        
        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var downloadedBytes = 0L;
        
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(localPath, FileMode.Create);
        
        var buffer = new byte[81920];
        int bytesRead;
        var lastProgress = 0;
        
        while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloadedBytes += bytesRead;
            
            var progress = (int)(downloadedBytes * 100 / totalBytes);
            if (progress > lastProgress && progress % 10 == 0)
            {
                Console.WriteLine($"  Progress: {progress}% ({downloadedBytes / 1024 / 1024} MB)");
                lastProgress = progress;
            }
        }
        
        Console.WriteLine("◎ Decompressing...");
        await DecompressGzipAsync(localPath, csvPath);
        
        Console.WriteLine($"◈ Database ready: {csvPath}");
        return csvPath;
    }
    
    /// <summary>
    /// Downloads cell towers for a specific country (MCC).
    /// Common MCCs: 310-316 (USA), 234 (UK), 262 (Germany), 460 (China)
    /// </summary>
    public async Task<string> DownloadByCountryAsync(int mcc, CancellationToken ct = default)
    {
        var csvPath = Path.Combine(_cacheDir, $"cells_mcc_{mcc}.csv");
        
        if (File.Exists(csvPath) && new FileInfo(csvPath).Length > 0)
        {
            Console.WriteLine($"◎ Using cached data for MCC {mcc}");
            return csvPath;
        }
        
        // Use the public API endpoint for filtered data
        var url = $"https://opencellid.org/cell/getInArea?key={_apiToken}&mcc={mcc}&format=csv&limit=100000";
        
        Console.WriteLine($"◈ Downloading cells for MCC {mcc}...");
        
        try
        {
            var content = await _httpClient.GetStringAsync(url, ct);
            await File.WriteAllTextAsync(csvPath, content, ct);
            Console.WriteLine($"◈ Downloaded to: {csvPath}");
            return csvPath;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"∴ API request failed: {ex.Message}");
            Console.WriteLine("  Falling back to sample data generation...");
            return await GenerateSampleDataAsync(mcc, ct);
        }
    }
    
    /// <summary>
    /// Downloads cells in a geographic bounding box.
    /// </summary>
    public async Task<string> DownloadByAreaAsync(
        double latMin, double latMax, 
        double lonMin, double lonMax,
        CancellationToken ct = default)
    {
        var areaKey = $"{latMin:F2}_{latMax:F2}_{lonMin:F2}_{lonMax:F2}";
        var csvPath = Path.Combine(_cacheDir, $"cells_area_{areaKey}.csv");
        
        if (File.Exists(csvPath) && new FileInfo(csvPath).Length > 0)
        {
            Console.WriteLine($"◎ Using cached data for area");
            return csvPath;
        }
        
        var url = $"https://opencellid.org/cell/getInArea?key={_apiToken}" +
                  $"&BBOX={lonMin},{latMin},{lonMax},{latMax}&format=csv&limit=50000";
        
        Console.WriteLine($"◈ Downloading cells for area [{latMin},{lonMin}] to [{latMax},{lonMax}]...");
        
        try
        {
            var content = await _httpClient.GetStringAsync(url, ct);
            await File.WriteAllTextAsync(csvPath, content, ct);
            return csvPath;
        }
        catch
        {
            return await GenerateSampleDataAsync(310, ct); // Fallback
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          IMPORT METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Imports OpenCellID data into the RF locator model for training.
    /// </summary>
    public async Task<int> ImportToModelAsync(
        RFLocatorModel model, 
        string csvPath, 
        int maxRecords = 100000,
        CancellationToken ct = default)
    {
        Console.WriteLine($"◈ Importing cell tower data from {Path.GetFileName(csvPath)}...");
        
        var imported = 0;
        var random = new Random(42); // Deterministic for reproducibility
        
        await foreach (var tower in ReadCellTowersAsync(csvPath, ct))
        {
            if (imported >= maxRecords) break;
            
            // Generate synthetic signal measurements based on tower location
            // In real world, these would come from actual drive tests
            var measurements = GenerateSyntheticMeasurements(tower, random);
            
            foreach (var (measurement, lat, lon) in measurements)
            {
                model.AddTrainingPoint(measurement, lat, lon);
                imported++;
            }
            
            if (imported % 10000 == 0)
            {
                Console.WriteLine($"  Imported {imported:N0} training points...");
            }
        }
        
        Console.WriteLine($"◈ Import complete: {imported:N0} training points");
        return imported;
    }
    
    /// <summary>
    /// Reads cell towers from OpenCellID CSV format.
    /// </summary>
    public async IAsyncEnumerable<CellTower> ReadCellTowersAsync(
        string csvPath, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var reader = new StreamReader(csvPath);
        var header = await reader.ReadLineAsync(ct);
        var isOpenCellIdFormat = header?.Contains("radio") == true;
        
        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var tower = isOpenCellIdFormat 
                ? ParseOpenCellIDLine(line)
                : ParseSimpleLine(line);
            
            if (tower != null)
                yield return tower;
        }
    }
    
    private CellTower? ParseOpenCellIDLine(string line)
    {
        // Format: radio,mcc,net,area,cell,unit,lon,lat,range,samples,changeable,created,updated,averageSignal
        var parts = line.Split(',');
        if (parts.Length < 14) return null;
        
        try
        {
            return new CellTower
            {
                RadioType = parts[0],
                MCC = int.Parse(parts[1]),
                MNC = int.Parse(parts[2]),
                LAC = int.Parse(parts[3]),
                CellId = long.Parse(parts[4]),
                Longitude = double.Parse(parts[6]),
                Latitude = double.Parse(parts[7]),
                Range = int.TryParse(parts[8], out var r) ? r : 1000,
                Samples = int.TryParse(parts[9], out var s) ? s : 1,
                AverageSignal = int.TryParse(parts[13], out var sig) ? sig : -85
            };
        }
        catch
        {
            return null;
        }
    }
    
    private CellTower? ParseSimpleLine(string line)
    {
        // Simplified format: CellId,MCC,MNC,LAC,Lat,Lon
        var parts = line.Split(',');
        if (parts.Length < 6) return null;
        
        try
        {
            return new CellTower
            {
                CellId = long.Parse(parts[0]),
                MCC = int.Parse(parts[1]),
                MNC = int.Parse(parts[2]),
                LAC = int.Parse(parts[3]),
                Latitude = double.Parse(parts[4]),
                Longitude = double.Parse(parts[5]),
                RadioType = "LTE",
                Range = 1000,
                AverageSignal = -85
            };
        }
        catch
        {
            return null;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          SYNTHETIC MEASUREMENT GENERATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Generates synthetic signal measurements around a cell tower.
    /// Uses path loss model to estimate signal strength at various distances.
    /// </summary>
    private List<(CellTowerMeasurement measurement, double lat, double lon)> GenerateSyntheticMeasurements(
        CellTower tower, 
        Random random)
    {
        var results = new List<(CellTowerMeasurement, double, double)>();
        
        // Generate measurements at various distances from tower
        var distances = new[] { 100, 250, 500, 750, 1000, 1500, 2000, 3000, 5000 };
        
        foreach (var distance in distances)
        {
            if (distance > tower.Range * 2) break; // Don't go beyond reasonable range
            
            // Generate 4 points at this distance (N, S, E, W)
            for (int i = 0; i < 4; i++)
            {
                var angle = i * 90 + random.Next(-20, 20); // Add some randomness
                var (lat, lon) = OffsetLatLon(tower.Latitude, tower.Longitude, distance, angle);
                
                // Calculate signal strength using path loss model
                var rsrp = CalculateRSRP(tower, distance, random);
                var rsrq = CalculateRSRQ(rsrp, random);
                var sinr = CalculateSINR(rsrp, random);
                var ta = CalculateTimingAdvance(distance);
                
                var measurement = new CellTowerMeasurement
                {
                    CellId = tower.CellId,
                    MCC = tower.MCC,
                    MNC = tower.MNC,
                    LAC = tower.LAC,
                    RSRP = rsrp,
                    RSRQ = rsrq,
                    RSSI = rsrp + 20 + random.Next(-5, 5), // Approximate RSSI
                    SINR = sinr,
                    TimingAdvance = ta,
                    RadioType = tower.RadioType,
                    Timestamp = DateTime.UtcNow,
                    IsServingCell = true
                };
                
                results.Add((measurement, lat, lon));
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Calculates RSRP using Free Space Path Loss (FSPL) model with fading.
    /// FSPL(dB) = 20*log10(d) + 20*log10(f) - 147.55
    /// </summary>
    private float CalculateRSRP(CellTower tower, double distanceMeters, Random random)
    {
        // Base transmit power (typical macro cell)
        const double txPowerDbm = 46.0; // 46 dBm = 40W
        const double antennaGainDb = 15.0;
        const double frequencyMHz = 1900.0; // Mid-band
        
        // Free Space Path Loss
        var fspl = 20 * Math.Log10(distanceMeters) + 
                   20 * Math.Log10(frequencyMHz * 1e6) - 147.55;
        
        // Shadow fading (log-normal, 6-10 dB std deviation)
        var shadowFading = random.NextDouble() * 12 - 6;
        
        // Additional loss for urban environment
        var urbanLoss = random.NextDouble() * 20;
        
        // RSRP = TxPower + AntennaGain - PathLoss - Fading
        var rsrp = txPowerDbm + antennaGainDb - fspl - shadowFading - urbanLoss;
        
        // Clamp to realistic range
        return (float)Math.Clamp(rsrp, -140, -44);
    }
    
    /// <summary>
    /// Estimates RSRQ based on RSRP.
    /// RSRQ is related to RSRP/RSSI ratio.
    /// </summary>
    private float CalculateRSRQ(float rsrp, Random random)
    {
        // Better RSRP generally means better RSRQ
        var baseRsrq = (rsrp + 140) / 8 - 19; // Maps -140..-44 to -19..-7
        var noise = random.NextDouble() * 4 - 2;
        return (float)Math.Clamp(baseRsrq + noise, -20, -3);
    }
    
    /// <summary>
    /// Estimates SINR based on RSRP.
    /// </summary>
    private float CalculateSINR(float rsrp, Random random)
    {
        // Higher RSRP = higher SINR (less interference relative to signal)
        var baseSinr = (rsrp + 110) / 3; // Maps -110 -> 0 SINR, -80 -> 10 SINR
        var noise = random.NextDouble() * 6 - 3;
        return (float)Math.Clamp(baseSinr + noise, -20, 30);
    }
    
    /// <summary>
    /// Calculates Timing Advance from distance.
    /// TA = round(distance / 78.12) for LTE
    /// </summary>
    private float CalculateTimingAdvance(double distanceMeters)
    {
        const double metersPerTA = 78.12;
        return (float)Math.Round(distanceMeters / metersPerTA);
    }
    
    /// <summary>
    /// Calculates lat/lon offset from a point.
    /// </summary>
    private (double lat, double lon) OffsetLatLon(double lat, double lon, double distanceMeters, double bearingDegrees)
    {
        const double R = 6371000; // Earth radius in meters
        var bearing = bearingDegrees * Math.PI / 180;
        var latRad = lat * Math.PI / 180;
        var lonRad = lon * Math.PI / 180;
        
        var newLat = Math.Asin(
            Math.Sin(latRad) * Math.Cos(distanceMeters / R) +
            Math.Cos(latRad) * Math.Sin(distanceMeters / R) * Math.Cos(bearing));
        
        var newLon = lonRad + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(distanceMeters / R) * Math.Cos(latRad),
            Math.Cos(distanceMeters / R) - Math.Sin(latRad) * Math.Sin(newLat));
        
        return (newLat * 180 / Math.PI, newLon * 180 / Math.PI);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          SAMPLE DATA GENERATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Generates sample cell tower data for testing/development.
    /// Uses realistic tower distributions.
    /// </summary>
    public async Task<string> GenerateSampleDataAsync(int mcc = 310, CancellationToken ct = default)
    {
        var csvPath = Path.Combine(_cacheDir, $"sample_cells_mcc_{mcc}.csv");
        
        Console.WriteLine($"◈ Generating sample cell tower data for MCC {mcc}...");
        
        var random = new Random(mcc); // Seed with MCC for reproducibility
        var sb = new StringBuilder();
        
        // CSV header (OpenCellID format)
        sb.AppendLine("radio,mcc,net,area,cell,unit,lon,lat,range,samples,changeable,created,updated,averageSignal");
        
        // Generate major cities with cell towers
        var cities = GetSampleCities(mcc);
        var cellCount = 0;
        
        foreach (var city in cities)
        {
            // Generate 50-200 cell towers per city
            var towersInCity = random.Next(50, 200);
            
            for (int i = 0; i < towersInCity; i++)
            {
                // Random position within city radius
                var radius = random.NextDouble() * 15000; // 15km radius
                var angle = random.NextDouble() * 360;
                var (lat, lon) = OffsetLatLon(city.Lat, city.Lon, radius, angle);
                
                var lac = 10000 + random.Next(1000);
                var cellId = random.Next(1000000, 99999999);
                var mnc = city.MNC;
                var range = 500 + random.Next(2000);
                var signal = -70 - random.Next(40);
                
                sb.AppendLine($"LTE,{mcc},{mnc},{lac},{cellId},0,{lon:F6},{lat:F6},{range},{random.Next(100, 5000)},1,0,0,{signal}");
                cellCount++;
            }
        }
        
        await File.WriteAllTextAsync(csvPath, sb.ToString(), ct);
        Console.WriteLine($"◈ Generated {cellCount} sample cell towers in {csvPath}");
        
        return csvPath;
    }
    
    private List<(double Lat, double Lon, int MNC, string Name)> GetSampleCities(int mcc)
    {
        return mcc switch
        {
            310 or 311 or 312 => new List<(double, double, int, string)>
            {
                (37.7749, -122.4194, 410, "San Francisco"),
                (34.0522, -118.2437, 410, "Los Angeles"),
                (40.7128, -74.0060, 410, "New York"),
                (41.8781, -87.6298, 410, "Chicago"),
                (29.7604, -95.3698, 410, "Houston"),
                (33.4484, -112.0740, 410, "Phoenix"),
                (47.6062, -122.3321, 410, "Seattle"),
                (39.7392, -104.9903, 410, "Denver"),
                (25.7617, -80.1918, 410, "Miami"),
                (42.3601, -71.0589, 410, "Boston"),
            },
            234 => new List<(double, double, int, string)>
            {
                (51.5074, -0.1278, 20, "London"),
                (53.4808, -2.2426, 20, "Manchester"),
                (55.9533, -3.1883, 20, "Edinburgh"),
            },
            _ => new List<(double, double, int, string)>
            {
                (0, 0, 1, "Sample"),
            }
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          UTILITIES
    // ═══════════════════════════════════════════════════════════════════════════
    
    private async Task DecompressGzipAsync(string gzipPath, string outputPath)
    {
        await using var gzipStream = new GZipStream(
            File.OpenRead(gzipPath), CompressionMode.Decompress);
        await using var outputStream = File.Create(outputPath);
        await gzipStream.CopyToAsync(outputStream);
    }
}

/// <summary>
/// Cell tower data from OpenCellID.
/// </summary>
public class CellTower
{
    public string RadioType { get; set; } = "LTE";
    public int MCC { get; set; }
    public int MNC { get; set; }
    public int LAC { get; set; }
    public long CellId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Range { get; set; } // Coverage radius in meters
    public int Samples { get; set; } // Number of measurements
    public int AverageSignal { get; set; } // dBm
}
