/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AT COMMAND PARSER - MODEM INTERFACE                     ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Parses AT command responses from cellular modems (Quectel, Telit, etc).   ║
 * ║  Extracts signal quality, cell info, and neighbor cell lists.              ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Text.RegularExpressions;

namespace NIGHTFRAME.Drone.Cellular;

public class ATCommandParser
{
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +CSQ - SIGNAL QUALITY
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +CSQ response for basic signal quality.
    /// Format: +CSQ: rssi,ber
    /// rssi: 0-31 (maps to -113 to -51 dBm) or 99 (unknown)
    /// ber: 0-7 or 99 (unknown)
    /// </summary>
    public static (float rssiDbm, int ber) ParseCSQ(string response)
    {
        // Example: +CSQ: 20,99
        var match = Regex.Match(response, @"\+CSQ:\s*(\d+),(\d+)");
        
        if (!match.Success)
            throw new FormatException($"Invalid +CSQ response: {response}");
        
        var rssiRaw = int.Parse(match.Groups[1].Value);
        var ber = int.Parse(match.Groups[2].Value);
        
        // Convert raw CSQ to dBm
        // rssi 0 = -113 dBm, rssi 31 = -51 dBm, each step = 2 dBm
        float rssiDbm = rssiRaw == 99 
            ? float.NaN 
            : -113 + (rssiRaw * 2);
        
        return (rssiDbm, ber);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +CESQ - EXTENDED SIGNAL QUALITY
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +CESQ response for extended signal quality (3GPP 27.007).
    /// Format: +CESQ: rxlev,ber,rscp,ecno,rsrq,rsrp
    /// </summary>
    public static CellTowerMeasurement ParseCESQ(string response)
    {
        // Example: +CESQ: 99,99,255,255,20,45
        var match = Regex.Match(response, @"\+CESQ:\s*(\d+),(\d+),(\d+),(\d+),(\d+),(\d+)");
        
        if (!match.Success)
            throw new FormatException($"Invalid +CESQ response: {response}");
        
        var rxlev = int.Parse(match.Groups[1].Value);   // GSM signal
        var ber = int.Parse(match.Groups[2].Value);     // Bit error rate
        var rscp = int.Parse(match.Groups[3].Value);    // UMTS signal
        var ecno = int.Parse(match.Groups[4].Value);    // UMTS quality
        var rsrq = int.Parse(match.Groups[5].Value);    // LTE quality
        var rsrp = int.Parse(match.Groups[6].Value);    // LTE signal
        
        return new CellTowerMeasurement
        {
            // RSRP: 0-97 maps to -140 to -44 dBm
            RSRP = rsrp == 255 ? float.NaN : -140 + rsrp,
            
            // RSRQ: 0-34 maps to -20 to -3 dB (in 0.5 dB steps)
            RSRQ = rsrq == 255 ? float.NaN : -20 + (rsrq * 0.5f),
            
            // RSSI from rxlev: 0-63 maps to -110 to -48 dBm
            RSSI = rxlev == 99 ? float.NaN : -110 + rxlev,
            
            Timestamp = DateTime.UtcNow,
            RadioType = rsrp != 255 ? "LTE" : rscp != 255 ? "UMTS" : "GSM"
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +CPSI - SYSTEM INFORMATION (Quectel)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +CPSI response for detailed cell information (Quectel modems).
    /// Format varies by radio type.
    /// LTE: +CPSI: LTE,Online,MCC-MNC,TAC,EARFCN,CellID,PCI,Band,DL_BW,UL_BW,RSRP,RSRQ,RSSI,SINR
    /// </summary>
    public static CellTowerMeasurement ParseCPSI(string response)
    {
        // Example: +CPSI: LTE,Online,310-410,1234,5230,0x12AB34CD,123,7,20,20,-85,-12,-60,15
        
        var measurement = new CellTowerMeasurement
        {
            Timestamp = DateTime.UtcNow,
            IsServingCell = true
        };
        
        // Parse LTE format
        if (response.Contains("LTE"))
        {
            var lteMatch = Regex.Match(response, 
                @"\+CPSI:\s*LTE,\w+,(\d+)-(\d+),(\d+),(\d+),(0x[0-9A-Fa-f]+|\d+),(\d+),(\d+),\d+,\d+,(-?\d+),(-?\d+),(-?\d+),(-?\d+)");
            
            if (lteMatch.Success)
            {
                measurement.MCC = int.Parse(lteMatch.Groups[1].Value);
                measurement.MNC = int.Parse(lteMatch.Groups[2].Value);
                measurement.LAC = int.Parse(lteMatch.Groups[3].Value); // TAC for LTE
                measurement.EARFCN = int.Parse(lteMatch.Groups[4].Value);
                
                // Cell ID can be hex or decimal
                var cellIdStr = lteMatch.Groups[5].Value;
                measurement.CellId = cellIdStr.StartsWith("0x") 
                    ? Convert.ToInt64(cellIdStr, 16)
                    : long.Parse(cellIdStr);
                
                measurement.PhysicalCellId = int.Parse(lteMatch.Groups[6].Value);
                // Group 7 is band
                measurement.RSRP = float.Parse(lteMatch.Groups[8].Value);
                measurement.RSRQ = float.Parse(lteMatch.Groups[9].Value);
                measurement.RSSI = float.Parse(lteMatch.Groups[10].Value);
                measurement.SINR = float.Parse(lteMatch.Groups[11].Value);
                measurement.RadioType = "LTE";
            }
        }
        // Parse GSM format
        else if (response.Contains("GSM"))
        {
            var gsmMatch = Regex.Match(response,
                @"\+CPSI:\s*GSM,\w+,(\d+)-(\d+),(\d+),(\d+),(-?\d+)");
            
            if (gsmMatch.Success)
            {
                measurement.MCC = int.Parse(gsmMatch.Groups[1].Value);
                measurement.MNC = int.Parse(gsmMatch.Groups[2].Value);
                measurement.LAC = int.Parse(gsmMatch.Groups[3].Value);
                measurement.CellId = long.Parse(gsmMatch.Groups[4].Value);
                measurement.RSSI = float.Parse(gsmMatch.Groups[5].Value);
                measurement.RadioType = "GSM";
            }
        }
        
        return measurement;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +QENG - ENGINEERING MODE (Quectel)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +QENG response for engineering mode data including neighbor cells.
    /// Returns serving cell and list of neighbors.
    /// </summary>
    public static (CellTowerMeasurement serving, List<NeighborCell> neighbors) ParseQENG(string response)
    {
        var serving = new CellTowerMeasurement { Timestamp = DateTime.UtcNow };
        var neighbors = new List<NeighborCell>();
        
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Serving cell: +QENG: "servingcell","NOCONN","LTE","FDD",310,410,12AB34,123,5230,7,4,4,-75,-8,-50,20,1
            if (trimmed.Contains("\"servingcell\""))
            {
                var match = Regex.Match(trimmed,
                    @"""servingcell"",""[^""]+"",""(\w+)"",""[^""]+"",(\d+),(\d+),([0-9A-Fa-f]+),(\d+),(\d+),\d+,\d+,\d+,(-?\d+),(-?\d+),(-?\d+),(-?\d+)");
                
                if (match.Success)
                {
                    serving.RadioType = match.Groups[1].Value;
                    serving.MCC = int.Parse(match.Groups[2].Value);
                    serving.MNC = int.Parse(match.Groups[3].Value);
                    serving.CellId = Convert.ToInt64(match.Groups[4].Value, 16);
                    serving.PhysicalCellId = int.Parse(match.Groups[5].Value);
                    serving.EARFCN = int.Parse(match.Groups[6].Value);
                    serving.RSRP = float.Parse(match.Groups[7].Value);
                    serving.RSRQ = float.Parse(match.Groups[8].Value);
                    serving.RSSI = float.Parse(match.Groups[9].Value);
                    serving.SINR = float.Parse(match.Groups[10].Value);
                    serving.IsServingCell = true;
                }
            }
            // Neighbor cell: +QENG: "neighbourcell intra","LTE",5230,123,-80,-10,0
            else if (trimmed.Contains("\"neighbourcell"))
            {
                var match = Regex.Match(trimmed,
                    @"""neighbourcell[^""]+"",""(\w+)"",(\d+),(\d+),(-?\d+),(-?\d+)");
                
                if (match.Success)
                {
                    neighbors.Add(new NeighborCell
                    {
                        EARFCN = int.Parse(match.Groups[2].Value),
                        PhysicalCellId = int.Parse(match.Groups[3].Value),
                        RSRP = float.Parse(match.Groups[4].Value),
                        RSRQ = float.Parse(match.Groups[5].Value)
                    });
                }
            }
        }
        
        return (serving, neighbors);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +CGREG/+CEREG - NETWORK REGISTRATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +CEREG (LTE) or +CGREG (GSM/UMTS) for registration and location.
    /// Format: +CEREG: n,stat[,lac,ci[,AcT]]
    /// </summary>
    public static (int status, int lac, long cellId) ParseCEREG(string response)
    {
        // Example: +CEREG: 2,1,"1234","12AB34CD",7
        var match = Regex.Match(response, 
            @"\+C[EG]REG:\s*\d*,?(\d+)(?:,""([0-9A-Fa-f]+)"",""([0-9A-Fa-f]+)"")?");
        
        if (!match.Success)
            return (0, 0, 0);
        
        var status = int.Parse(match.Groups[1].Value);
        var lac = match.Groups[2].Success 
            ? Convert.ToInt32(match.Groups[2].Value, 16) 
            : 0;
        var cellId = match.Groups[3].Success 
            ? Convert.ToInt64(match.Groups[3].Value, 16) 
            : 0;
        
        return (status, lac, cellId);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              UTILITY: SIGNAL STRENGTH CLASSIFICATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Classifies LTE signal strength based on RSRP.
    /// </summary>
    public static SignalQuality ClassifySignal(CellTowerMeasurement measurement)
    {
        var quality = new SignalQuality();
        
        // Classify based on RSRP
        if (measurement.RSRP >= -80)
        {
            quality.Strength = SignalStrength.Excellent;
            quality.Score = 100;
            quality.Description = "Excellent signal - maximum performance";
            quality.EstimatedThroughputMbps = 100;
        }
        else if (measurement.RSRP >= -90)
        {
            quality.Strength = SignalStrength.Good;
            quality.Score = 80;
            quality.Description = "Good signal - reliable connection";
            quality.EstimatedThroughputMbps = 50;
        }
        else if (measurement.RSRP >= -100)
        {
            quality.Strength = SignalStrength.Fair;
            quality.Score = 50;
            quality.Description = "Fair signal - may experience slowdowns";
            quality.EstimatedThroughputMbps = 20;
        }
        else if (measurement.RSRP >= -110)
        {
            quality.Strength = SignalStrength.Poor;
            quality.Score = 25;
            quality.Description = "Poor signal - connection may drop";
            quality.EstimatedThroughputMbps = 5;
        }
        else
        {
            quality.Strength = SignalStrength.NoSignal;
            quality.Score = 0;
            quality.Description = "No usable signal";
            quality.EstimatedThroughputMbps = 0;
        }
        
        // Adjust for SINR (signal quality vs noise)
        if (measurement.SINR < 0)
        {
            quality.Score *= 0.5f;
            quality.Description += " (high interference)";
        }
        
        quality.SufficientForData = measurement.RSRP >= -110 && measurement.SINR >= -5;
        quality.SufficientForVoice = measurement.RSRP >= -105;
        
        return quality;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +COPS - CARRIER INFORMATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +COPS response for carrier/operator name.
    /// Format: +COPS: mode,format,"operator",AcT
    /// </summary>
    public static string ParseCarrier(string response)
    {
        // Example: +COPS: 0,0,"AT&T",7
        var match = Regex.Match(response, @"\+COPS:\s*\d+,\d+,""([^""]+)""");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +CREG - REGISTRATION STATE
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +CREG response for network registration status.
    /// </summary>
    public static NetworkRegistration ParseRegistration(string response)
    {
        // Example: +CREG: 2,1 (registered, home network)
        // Example: +CREG: 2,5 (registered, roaming)
        var match = Regex.Match(response, @"\+C[EG]?REG:\s*\d+,(\d+)");
        
        if (!match.Success)
            return new NetworkRegistration { Registered = false };
        
        var status = int.Parse(match.Groups[1].Value);
        
        // Status: 0=not registered, 1=registered home, 2=searching, 3=denied, 5=registered roaming
        return new NetworkRegistration
        {
            Registered = status == 1 || status == 5,
            Roaming = status == 5,
            Searching = status == 2,
            Technology = DetermineTechnology(response)
        };
    }
    
    private static string DetermineTechnology(string response)
    {
        // Check for technology indicator
        if (response.Contains("+C5GREG")) return "5G NR";
        if (response.Contains("+CEREG")) return "LTE";
        if (response.Contains("+CGREG")) return "UMTS";
        return "GSM";
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +CGPADDR - IP ADDRESS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +CGPADDR response for IP address.
    /// Format: +CGPADDR: cid,"ip_addr"
    /// </summary>
    public static string? ParseIPAddress(string response)
    {
        // Example: +CGPADDR: 1,"10.123.45.67"
        // Example: +CGPADDR: 1,"10.123.45.67","2001:db8::1"
        var match = Regex.Match(response, @"\+CGPADDR:\s*\d+,""([^""]+)""");
        return match.Success ? match.Groups[1].Value : null;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              +QNWPREFCFG - BAND CONFIGURATION (Quectel)
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Parses +QNWPREFCFG response for available bands.
    /// </summary>
    public static List<BandInfo> ParseAvailableBands(string response)
    {
        var bands = new List<BandInfo>();
        
        // Example: +QNWPREFCFG: "lte_band",1:2:4:5:7:12:13:66:71
        var match = Regex.Match(response, @"""lte_band"",([0-9:]+)");
        
        if (match.Success)
        {
            var bandNumbers = match.Groups[1].Value.Split(':');
            foreach (var bandStr in bandNumbers)
            {
                if (int.TryParse(bandStr, out var bandNum))
                {
                    bands.Add(GetBandInfo(bandNum, "LTE"));
                }
            }
        }
        
        // Also check for 5G NR bands
        match = Regex.Match(response, @"""nr5g_band"",([0-9:n]+)");
        if (match.Success)
        {
            var bandNumbers = match.Groups[1].Value.Split(':');
            foreach (var bandStr in bandNumbers)
            {
                var cleanBand = bandStr.TrimStart('n');
                if (int.TryParse(cleanBand, out var bandNum))
                {
                    bands.Add(GetBandInfo(bandNum, "5G NR"));
                }
            }
        }
        
        return bands;
    }
    
    private static BandInfo GetBandInfo(int bandNumber, string technology)
    {
        // Common band frequencies (US focus)
        var bandInfo = technology == "LTE" ? bandNumber switch
        {
            1 => (2100, 20),
            2 => (1900, 20),
            3 => (1800, 20),
            4 => (1700, 20),
            5 => (850, 10),
            7 => (2600, 20),
            8 => (900, 10),
            12 => (700, 10),
            13 => (700, 10),
            14 => (700, 10),
            17 => (700, 10),
            25 => (1900, 20),
            26 => (850, 10),
            29 => (700, 10),
            30 => (2300, 10),
            38 => (2600, 20),
            41 => (2500, 20),
            66 => (1700, 20),
            71 => (600, 10),
            _ => (0, 10)
        } : bandNumber switch
        {
            // 5G NR bands
            1 => (2100, 100),
            2 => (1900, 100),
            5 => (850, 20),
            7 => (2600, 100),
            41 => (2500, 100),
            66 => (1700, 100),
            71 => (600, 20),
            77 => (3700, 100),
            78 => (3500, 100),
            79 => (4700, 100),
            260 => (39000, 100), // mmWave
            261 => (28000, 100), // mmWave
            _ => (0, 100)
        };
        
        return new BandInfo
        {
            BandNumber = bandNumber,
            Technology = technology,
            FrequencyMHz = bandInfo.Item1,
            BandwidthMHz = bandInfo.Item2
        };
    }
}

/// <summary>
/// Network registration status.
/// </summary>
public class NetworkRegistration
{
    public bool Registered { get; init; }
    public bool Roaming { get; init; }
    public bool Searching { get; init; }
    public string Technology { get; init; } = "Unknown";
}
