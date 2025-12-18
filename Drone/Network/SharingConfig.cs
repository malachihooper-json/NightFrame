/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SHARING CONFIGURATION                                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Manages data cap awareness, consent tracking, and sharing limits.         ║
 * ║  Persists to LiteDB for survival across restarts.                          ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using LiteDB;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Configuration for internet sharing with data cap awareness.
/// </summary>
public class SharingConfig
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              SHARING LIMITS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Whether sharing is enabled. Master switch.
    /// </summary>
    public bool IsEnabled { get; set; } = false;
    
    /// <summary>
    /// Monthly bandwidth limit for guest sharing in GB. 0 = unlimited.
    /// </summary>
    public double MonthlyBandwidthLimitGB { get; set; } = 50.0;
    
    /// <summary>
    /// Current month's usage in bytes.
    /// </summary>
    public long CurrentMonthUsageBytes { get; set; } = 0;
    
    /// <summary>
    /// The month this usage tracking applies to (1-12).
    /// </summary>
    public int CurrentMonth { get; set; } = DateTime.UtcNow.Month;
    
    /// <summary>
    /// The year this usage tracking applies to.
    /// </summary>
    public int CurrentYear { get; set; } = DateTime.UtcNow.Year;
    
    /// <summary>
    /// Percentage of limit at which to warn user (0-100).
    /// </summary>
    public int WarningThresholdPercent { get; set; } = 80;
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              CONSENT TRACKING
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Whether user has completed the consent flow.
    /// </summary>
    public bool ConsentGranted { get; set; } = false;
    
    /// <summary>
    /// Timestamp when consent was granted.
    /// </summary>
    public DateTime? ConsentTimestamp { get; set; }
    
    /// <summary>
    /// Individual consent points acknowledged.
    /// </summary>
    public ConsentAcknowledgments Acknowledgments { get; set; } = new();
    
    /// <summary>
    /// Hash of consent record for tamper evidence.
    /// </summary>
    public string? ConsentHash { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              ISP INFORMATION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// User's ISP (self-reported, for guidance).
    /// </summary>
    public string? IspName { get; set; }
    
    /// <summary>
    /// Whether user confirmed ISP allows sharing.
    /// </summary>
    public bool IspAllowsSharing { get; set; } = false;
    
    /// <summary>
    /// User's internet plan has a data cap.
    /// </summary>
    public bool HasDataCap { get; set; } = true;
    
    /// <summary>
    /// User's total monthly data cap in GB (if applicable).
    /// </summary>
    public double? UserDataCapGB { get; set; }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              COMPUTED PROPERTIES
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Current usage in GB.
    /// </summary>
    public double CurrentMonthUsageGB => CurrentMonthUsageBytes / (1024.0 * 1024.0 * 1024.0);
    
    /// <summary>
    /// Whether the monthly limit has been reached.
    /// </summary>
    public bool LimitReached => MonthlyBandwidthLimitGB > 0 && CurrentMonthUsageGB >= MonthlyBandwidthLimitGB;
    
    /// <summary>
    /// Whether warning threshold has been reached.
    /// </summary>
    public bool WarningThresholdReached => MonthlyBandwidthLimitGB > 0 && 
        (CurrentMonthUsageGB / MonthlyBandwidthLimitGB * 100) >= WarningThresholdPercent;
    
    /// <summary>
    /// Remaining bandwidth in GB.
    /// </summary>
    public double RemainingGB => MonthlyBandwidthLimitGB > 0 
        ? Math.Max(0, MonthlyBandwidthLimitGB - CurrentMonthUsageGB) 
        : double.MaxValue;
    
    /// <summary>
    /// Whether sharing can proceed (enabled, consented, not at limit).
    /// </summary>
    public bool CanShare => IsEnabled && ConsentGranted && !LimitReached;
}

/// <summary>
/// Individual consent acknowledgments for the 5-point consent flow.
/// </summary>
public class ConsentAcknowledgments
{
    /// <summary>
    /// User understands guest traffic consumes their bandwidth.
    /// </summary>
    public bool BandwidthConsumption { get; set; }
    
    /// <summary>
    /// User understands this may affect their bills.
    /// </summary>
    public bool BillingImpact { get; set; }
    
    /// <summary>
    /// User confirms ISP agreement permits sharing.
    /// </summary>
    public bool IspTermsCompliance { get; set; }
    
    /// <summary>
    /// User understands guest traffic appears from their IP.
    /// </summary>
    public bool IpLiabilityAwareness { get; set; }
    
    /// <summary>
    /// User accepts responsibility for configuring limits.
    /// </summary>
    public bool LimitConfiguration { get; set; }
    
    /// <summary>
    /// All consent points acknowledged.
    /// </summary>
    public bool AllAcknowledged => BandwidthConsumption && BillingImpact && 
        IspTermsCompliance && IpLiabilityAwareness && LimitConfiguration;
}

/// <summary>
/// Manages sharing configuration persistence.
/// </summary>
public class SharingConfigManager
{
    private readonly ILiteDatabase _db;
    private readonly ILiteCollection<SharingConfig> _configs;
    private SharingConfig? _cachedConfig;
    private readonly object _lock = new();
    
    public SharingConfigManager()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NIGHTFRAME", "sharing.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new LiteDatabase(dbPath);
        _configs = _db.GetCollection<SharingConfig>("config");
    }
    
    /// <summary>
    /// Gets the current sharing configuration.
    /// </summary>
    public SharingConfig GetConfig()
    {
        lock (_lock)
        {
            if (_cachedConfig != null)
            {
                // Check if month rolled over
                if (_cachedConfig.CurrentMonth != DateTime.UtcNow.Month || 
                    _cachedConfig.CurrentYear != DateTime.UtcNow.Year)
                {
                    ResetMonthlyUsage();
                }
                return _cachedConfig;
            }
            
            _cachedConfig = _configs.FindOne(Query.All()) ?? new SharingConfig();
            
            // Check if month rolled over
            if (_cachedConfig.CurrentMonth != DateTime.UtcNow.Month || 
                _cachedConfig.CurrentYear != DateTime.UtcNow.Year)
            {
                ResetMonthlyUsage();
            }
            
            return _cachedConfig;
        }
    }
    
    /// <summary>
    /// Saves the current configuration.
    /// </summary>
    public void SaveConfig()
    {
        lock (_lock)
        {
            if (_cachedConfig == null) return;
            _configs.Upsert(_cachedConfig);
        }
    }
    
    /// <summary>
    /// Records bandwidth usage.
    /// </summary>
    public void RecordUsage(long bytes)
    {
        lock (_lock)
        {
            var config = GetConfig();
            config.CurrentMonthUsageBytes += bytes;
            SaveConfig();
        }
    }
    
    /// <summary>
    /// Resets monthly usage for new billing period.
    /// </summary>
    private void ResetMonthlyUsage()
    {
        if (_cachedConfig == null) return;
        
        Console.WriteLine($"◎ New month detected. Resetting sharing usage from {_cachedConfig.CurrentMonthUsageGB:F2} GB");
        _cachedConfig.CurrentMonthUsageBytes = 0;
        _cachedConfig.CurrentMonth = DateTime.UtcNow.Month;
        _cachedConfig.CurrentYear = DateTime.UtcNow.Year;
        SaveConfig();
    }
    
    /// <summary>
    /// Records consent with hash for tamper evidence.
    /// </summary>
    public void RecordConsent(ConsentAcknowledgments acknowledgments)
    {
        lock (_lock)
        {
            var config = GetConfig();
            config.Acknowledgments = acknowledgments;
            config.ConsentGranted = acknowledgments.AllAcknowledged;
            config.ConsentTimestamp = DateTime.UtcNow;
            
            // Create tamper-evident hash
            var consentData = $"{config.ConsentTimestamp:O}|{acknowledgments.AllAcknowledged}";
            config.ConsentHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(consentData)));
            
            SaveConfig();
            Console.WriteLine($"◎ Consent recorded at {config.ConsentTimestamp}");
        }
    }
    
    /// <summary>
    /// Gets a summary of current sharing status.
    /// </summary>
    public string GetStatusSummary()
    {
        var config = GetConfig();
        return $"""
            Sharing: {(config.CanShare ? "ACTIVE" : "INACTIVE")}
            Consent: {(config.ConsentGranted ? "Granted" : "NOT GRANTED")}
            Usage: {config.CurrentMonthUsageGB:F2} / {config.MonthlyBandwidthLimitGB:F0} GB ({config.RemainingGB:F2} GB remaining)
            Limit Reached: {config.LimitReached}
            """;
    }
}
