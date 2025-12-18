/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    RESOURCE LIMITS - HARD-CODED CAPS                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Centralized constants for all resource limits in NIGHTFRAME.              ║
 * ║  These caps protect both sharing users and the network.                    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 * 
 * IMPORTANT: These are the DEFAULT caps. Users can lower these limits but
 * cannot raise them above these maximums. This protects sharers from
 * accidentally contributing more than intended.
 */

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Centralized resource limits for NIGHTFRAME Drone nodes.
/// All caps are designed to protect sharing users while enabling the network.
/// </summary>
public static class ResourceLimits
{
    // ═══════════════════════════════════════════════════════════════════════════
    //                          BANDWIDTH LIMITS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Maximum bandwidth shared per month (in GB) before auto-stopping.
    /// Default: 50 GB/month. Users can set lower limits.
    /// </summary>
    public const double DefaultMonthlyBandwidthGB = 50.0;
    
    /// <summary>
    /// Maximum bandwidth a single guest can use (Kbps).
    /// Guests without the app are throttled to this speed.
    /// </summary>
    public const int GuestBandwidthKbps = 512;
    
    /// <summary>
    /// Bandwidth for mesh members (Kbps). 0 = unlimited.
    /// Members who contribute get full speed.
    /// </summary>
    public const int MemberBandwidthKbps = 0; // Unlimited
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          CONNECTION LIMITS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Maximum simultaneous guest connections per Drone.
    /// Prevents any single node from being overwhelmed.
    /// </summary>
    public const int MaxConcurrentGuests = 50;
    
    /// <summary>
    /// Maximum session duration for guests (hours).
    /// Guests must reconnect after this time.
    /// </summary>
    public const int MaxGuestSessionHours = 24;
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          COMPUTE LIMITS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Maximum CPU percentage for compute tasks.
    /// Ensures user's own work isn't affected.
    /// </summary>
    public const int MaxCpuPercentage = 20;
    
    /// <summary>
    /// Maximum RAM for compute tasks (MB).
    /// </summary>
    public const int MaxRamMB = 512;
    
    /// <summary>
    /// Maximum GPU utilization percentage.
    /// </summary>
    public const int MaxGpuPercentage = 30;
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          STORAGE LIMITS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Maximum disk space for caching and models (MB).
    /// </summary>
    public const int MaxDiskCacheMB = 1024; // 1 GB
    
    /// <summary>
    /// Maximum size for session logs before rotation (MB).
    /// </summary>
    public const int MaxSessionLogMB = 100;
    
    /// <summary>
    /// Days to retain session logs for liability protection.
    /// </summary>
    public const int SessionLogRetentionDays = 90;
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Returns a formatted summary of all resource limits for display.
    /// </summary>
    public static string GetFormattedSummary()
    {
        return $"""
        ┌─────────────────────────────────────────────────────────────────┐
        │                    RESOURCE LIMITS (HARD CAPS)                  │
        ├─────────────────────────────────────────────────────────────────┤
        │  BANDWIDTH                                                      │
        │    • Monthly sharing limit: {DefaultMonthlyBandwidthGB,5} GB (configurable lower)  │
        │    • Guest speed:           {GuestBandwidthKbps,5} Kbps (non-members)       │
        │    • Member speed:          Unlimited                           │
        │    • Max concurrent guests: {MaxConcurrentGuests,5}                           │
        ├─────────────────────────────────────────────────────────────────┤
        │  COMPUTE                                                        │
        │    • Max CPU usage:         {MaxCpuPercentage,5}%                            │
        │    • Max RAM usage:         {MaxRamMB,5} MB                          │
        │    • Max GPU usage:         {MaxGpuPercentage,5}%                            │
        ├─────────────────────────────────────────────────────────────────┤
        │  STORAGE                                                        │
        │    • Disk cache:            {MaxDiskCacheMB,5} MB                          │
        │    • Session log retention: {SessionLogRetentionDays,5} days                        │
        └─────────────────────────────────────────────────────────────────┘
        """;
    }
    
    /// <summary>
    /// Returns a simple one-line summary of key limits.
    /// </summary>
    public static string GetSimpleSummary()
    {
        return $"Bandwidth: {DefaultMonthlyBandwidthGB}GB/mo | CPU: {MaxCpuPercentage}% | RAM: {MaxRamMB}MB | GPU: {MaxGpuPercentage}%";
    }
    
    /// <summary>
    /// Returns limits as key-value pairs for UI display.
    /// </summary>
    public static Dictionary<string, string> GetLimitsForUI()
    {
        return new Dictionary<string, string>
        {
            ["Monthly Bandwidth"] = $"{DefaultMonthlyBandwidthGB} GB",
            ["Guest Speed"] = $"{GuestBandwidthKbps} Kbps",
            ["Member Speed"] = "Unlimited",
            ["Max Guests"] = $"{MaxConcurrentGuests} concurrent",
            ["CPU Limit"] = $"{MaxCpuPercentage}%",
            ["RAM Limit"] = $"{MaxRamMB} MB",
            ["GPU Limit"] = $"{MaxGpuPercentage}%",
            ["Disk Cache"] = $"{MaxDiskCacheMB} MB",
            ["Log Retention"] = $"{SessionLogRetentionDays} days"
        };
    }
}
