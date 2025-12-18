/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SESSION LOGGER - LIABILITY PROTECTION                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Logs guest sessions for liability protection.                             ║
 * ║  Records MAC, timestamps, and bandwidth per session.                       ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using LiteDB;
using System.Collections.Concurrent;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// A logged guest session for liability protection.
/// </summary>
public class GuestSession
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    
    /// <summary>
    /// MAC address of the guest device.
    /// </summary>
    public required string MacAddress { get; init; }
    
    /// <summary>
    /// IP address assigned to the guest.
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// When the guest connected.
    /// </summary>
    public DateTime ConnectedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the guest disconnected (null if still connected).
    /// </summary>
    public DateTime? DisconnectedAt { get; set; }
    
    /// <summary>
    /// Total bytes downloaded by this guest.
    /// </summary>
    public long BytesDownloaded { get; set; }
    
    /// <summary>
    /// Total bytes uploaded by this guest.
    /// </summary>
    public long BytesUploaded { get; set; }
    
    /// <summary>
    /// User agent string from portal page access.
    /// </summary>
    public string? UserAgent { get; set; }
    
    /// <summary>
    /// Platform detected (Windows, iOS, Android, etc).
    /// </summary>
    public string? Platform { get; set; }
    
    /// <summary>
    /// Whether guest accepted Terms of Service.
    /// </summary>
    public bool AcceptedTerms { get; set; }
    
    /// <summary>
    /// Timestamp when Terms were accepted.
    /// </summary>
    public DateTime? TermsAcceptedAt { get; set; }
    
    /// <summary>
    /// Session is currently active.
    /// </summary>
    public bool IsActive => DisconnectedAt == null;
    
    /// <summary>
    /// Total bytes transferred.
    /// </summary>
    public long TotalBytes => BytesDownloaded + BytesUploaded;
    
    /// <summary>
    /// Session duration.
    /// </summary>
    public TimeSpan Duration => (DisconnectedAt ?? DateTime.UtcNow) - ConnectedAt;
}

/// <summary>
/// Manages guest session logging for liability protection.
/// </summary>
public class SessionLogger : IDisposable
{
    private readonly ILiteDatabase _db;
    private readonly ILiteCollection<GuestSession> _sessions;
    private readonly ConcurrentDictionary<string, GuestSession> _activeSessions = new();
    private readonly int _retentionDays;
    private readonly Timer _cleanupTimer;
    
    public SessionLogger(int retentionDays = 90)
    {
        _retentionDays = retentionDays;
        
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NIGHTFRAME", "sessions.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new LiteDatabase(dbPath);
        _sessions = _db.GetCollection<GuestSession>("sessions");
        
        // Create indexes for efficient querying
        _sessions.EnsureIndex(x => x.MacAddress);
        _sessions.EnsureIndex(x => x.ConnectedAt);
        _sessions.EnsureIndex(x => x.DisconnectedAt);
        
        // Run cleanup daily
        _cleanupTimer = new Timer(CleanupOldSessions, null, TimeSpan.Zero, TimeSpan.FromHours(24));
        
        Console.WriteLine($"◎ Session logger initialized. Retention: {_retentionDays} days");
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              SESSION MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Records a new guest connection.
    /// </summary>
    public GuestSession StartSession(string macAddress, string? ipAddress = null, string? userAgent = null)
    {
        var session = new GuestSession
        {
            MacAddress = macAddress.ToUpperInvariant(),
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Platform = DetectPlatform(userAgent)
        };
        
        _sessions.Insert(session);
        _activeSessions[macAddress.ToUpperInvariant()] = session;
        
        Console.WriteLine($"◎ Guest session started: {macAddress} at {session.ConnectedAt:HH:mm:ss}");
        return session;
    }
    
    /// <summary>
    /// Records guest disconnection.
    /// </summary>
    public void EndSession(string macAddress)
    {
        var mac = macAddress.ToUpperInvariant();
        
        if (_activeSessions.TryRemove(mac, out var session))
        {
            session.DisconnectedAt = DateTime.UtcNow;
            _sessions.Update(session);
            
            Console.WriteLine($"◎ Guest session ended: {mac} (Duration: {session.Duration.TotalMinutes:F1} min, " +
                $"Data: {session.TotalBytes / 1024.0 / 1024.0:F2} MB)");
        }
    }
    
    /// <summary>
    /// Updates bandwidth usage for a session.
    /// </summary>
    public void RecordBandwidth(string macAddress, long bytesDown, long bytesUp)
    {
        var mac = macAddress.ToUpperInvariant();
        
        if (_activeSessions.TryGetValue(mac, out var session))
        {
            session.BytesDownloaded += bytesDown;
            session.BytesUploaded += bytesUp;
            
            // Persist periodically (every 1MB)
            if ((session.TotalBytes % (1024 * 1024)) < (bytesDown + bytesUp))
            {
                _sessions.Update(session);
            }
        }
    }
    
    /// <summary>
    /// Records Terms of Service acceptance.
    /// </summary>
    public void RecordTermsAcceptance(string macAddress)
    {
        var mac = macAddress.ToUpperInvariant();
        
        if (_activeSessions.TryGetValue(mac, out var session))
        {
            session.AcceptedTerms = true;
            session.TermsAcceptedAt = DateTime.UtcNow;
            _sessions.Update(session);
            
            Console.WriteLine($"◎ Guest {mac} accepted Terms of Service");
        }
    }
    
    /// <summary>
    /// Gets an active session by MAC address.
    /// </summary>
    public GuestSession? GetActiveSession(string macAddress)
    {
        return _activeSessions.TryGetValue(macAddress.ToUpperInvariant(), out var session) ? session : null;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              QUERIES
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Gets all sessions within a date range (for legal requests).
    /// </summary>
    public IEnumerable<GuestSession> GetSessionsInRange(DateTime start, DateTime end)
    {
        return _sessions.Find(s => s.ConnectedAt >= start && s.ConnectedAt <= end);
    }
    
    /// <summary>
    /// Gets sessions by MAC address.
    /// </summary>
    public IEnumerable<GuestSession> GetSessionsByMac(string macAddress)
    {
        return _sessions.Find(s => s.MacAddress == macAddress.ToUpperInvariant());
    }
    
    /// <summary>
    /// Gets all currently active sessions.
    /// </summary>
    public IEnumerable<GuestSession> GetActiveSessions()
    {
        return _activeSessions.Values;
    }
    
    /// <summary>
    /// Gets session statistics.
    /// </summary>
    public SessionStats GetStats()
    {
        var allSessions = _sessions.FindAll().ToList();
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisMonth = new DateTime(now.Year, now.Month, 1);
        
        return new SessionStats
        {
            TotalSessionsEver = allSessions.Count,
            ActiveSessions = _activeSessions.Count,
            SessionsToday = allSessions.Count(s => s.ConnectedAt >= today),
            SessionsThisMonth = allSessions.Count(s => s.ConnectedAt >= thisMonth),
            TotalBytesTransferred = allSessions.Sum(s => s.TotalBytes),
            AverageSessionDuration = allSessions.Any() 
                ? TimeSpan.FromSeconds(allSessions.Average(s => s.Duration.TotalSeconds))
                : TimeSpan.Zero
        };
    }
    
    /// <summary>
    /// Exports sessions to JSON for legal requests.
    /// </summary>
    public string ExportToJson(DateTime start, DateTime end)
    {
        var sessions = GetSessionsInRange(start, end).Select(s => new
        {
            s.MacAddress,
            s.IpAddress,
            ConnectedAt = s.ConnectedAt.ToString("O"),
            DisconnectedAt = s.DisconnectedAt?.ToString("O"),
            s.BytesDownloaded,
            s.BytesUploaded,
            s.Platform,
            s.AcceptedTerms,
            TermsAcceptedAt = s.TermsAcceptedAt?.ToString("O")
        });
        
        return System.Text.Json.JsonSerializer.Serialize(sessions, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              HELPERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    private static string? DetectPlatform(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent)) return null;
        
        if (userAgent.Contains("iPhone") || userAgent.Contains("iPad")) return "iOS";
        if (userAgent.Contains("Android")) return "Android";
        if (userAgent.Contains("Windows")) return "Windows";
        if (userAgent.Contains("Mac OS")) return "macOS";
        if (userAgent.Contains("Linux")) return "Linux";
        
        return "Unknown";
    }
    
    private void CleanupOldSessions(object? state)
    {
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
        var deleted = _sessions.DeleteMany(s => s.DisconnectedAt != null && s.DisconnectedAt < cutoff);
        
        if (deleted > 0)
        {
            Console.WriteLine($"◎ Cleaned up {deleted} sessions older than {_retentionDays} days");
        }
    }
    
    public void Dispose()
    {
        // End all active sessions
        foreach (var mac in _activeSessions.Keys.ToList())
        {
            EndSession(mac);
        }
        
        _cleanupTimer.Dispose();
        _db.Dispose();
    }
}

/// <summary>
/// Session statistics.
/// </summary>
public class SessionStats
{
    public int TotalSessionsEver { get; init; }
    public int ActiveSessions { get; init; }
    public int SessionsToday { get; init; }
    public int SessionsThisMonth { get; init; }
    public long TotalBytesTransferred { get; init; }
    public TimeSpan AverageSessionDuration { get; init; }
    
    public double TotalGBTransferred => TotalBytesTransferred / (1024.0 * 1024.0 * 1024.0);
}
