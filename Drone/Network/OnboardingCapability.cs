/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              ONBOARDING CAPABILITY - PLATFORM DETECTION                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Detects device capabilities for onboarding flow customization.            ║
 * ║  Determines if SSID broadcast is possible, suggests appropriate path.      ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Represents device capability for joining the mesh network.
/// </summary>
public enum OnboardingPath
{
    /// <summary>Full member - can download app and broadcast SSID.</summary>
    FullMember,
    
    /// <summary>Guest only - cannot broadcast SSID (mobile, restricted device).</summary>
    GuestOnly,
    
    /// <summary>Unknown platform - show generic guidance.</summary>
    Unknown
}

/// <summary>
/// Device capability result for onboarding flow customization.
/// </summary>
public class DeviceCapability
{
    public required string Platform { get; init; }
    public required string DeviceType { get; init; }
    public required OnboardingPath RecommendedPath { get; init; }
    public bool CanBroadcastSSID { get; init; }
    public bool CanRunBackgroundApp { get; init; }
    public bool HasWiFiAdapter { get; init; }
    public bool SupportsHostedNetwork { get; init; }
    public string GuestOnlyReason { get; init; } = "";
    public List<string> AvailableActions { get; init; } = new();
    public string OnboardingMessage { get; init; } = "";
}

/// <summary>
/// Detects device capabilities for appropriate onboarding flow.
/// </summary>
public static class OnboardingCapability
{
    // Known platforms and their capabilities
    private static readonly Dictionary<string, (bool canBroadcast, bool canBackground)> PlatformCapabilities = new()
    {
        // Desktop platforms - can broadcast and run background
        ["Windows"] = (true, true),
        ["macOS"] = (true, true),
        ["Linux"] = (true, true),
        
        // Mobile platforms - cannot broadcast (iOS limitation, Android varies)
        ["iOS"] = (false, false),
        ["iPadOS"] = (false, false),
        ["Android"] = (false, true),  // Android could theoretically, but most can't
        
        // Other platforms - guest only
        ["ChromeOS"] = (false, false),
        ["PlayStation"] = (false, false),
        ["Xbox"] = (false, false),
        ["Nintendo"] = (false, false),
        ["SmartTV"] = (false, false),
        ["Unknown"] = (false, false)
    };
    
    /// <summary>
    /// Detects device capabilities from User-Agent string.
    /// </summary>
    public static DeviceCapability DetectFromUserAgent(string userAgent)
    {
        var (platform, deviceType) = ParseUserAgent(userAgent);
        var capTuple = PlatformCapabilities.GetValueOrDefault(platform, (false, false));
        var canBroadcast = capTuple.Item1;
        var canBackground = capTuple.Item2;
        
        // Determine onboarding path
        var path = canBroadcast 
            ? OnboardingPath.FullMember 
            : (platform == "Unknown" ? OnboardingPath.Unknown : OnboardingPath.GuestOnly);
        
        // Generate appropriate message
        var (message, reason, actions) = GenerateOnboardingContent(platform, canBroadcast, canBackground);
        
        return new DeviceCapability
        {
            Platform = platform,
            DeviceType = deviceType,
            RecommendedPath = path,
            CanBroadcastSSID = canBroadcast,
            CanRunBackgroundApp = canBackground,
            HasWiFiAdapter = true, // Assume true since they connected via WiFi
            SupportsHostedNetwork = canBroadcast,
            GuestOnlyReason = reason,
            AvailableActions = actions,
            OnboardingMessage = message
        };
    }
    
    /// <summary>
    /// Parses User-Agent string to determine platform.
    /// </summary>
    private static (string platform, string deviceType) ParseUserAgent(string ua)
    {
        if (string.IsNullOrEmpty(ua))
            return ("Unknown", "Unknown");
        
        ua = ua.ToLowerInvariant();
        
        // iOS devices
        if (ua.Contains("iphone"))
            return ("iOS", "iPhone");
        if (ua.Contains("ipad"))
            return ("iPadOS", "iPad");
        
        // Android devices
        if (ua.Contains("android"))
        {
            var deviceType = ua.Contains("mobile") ? "Phone" : "Tablet";
            return ("Android", deviceType);
        }
        
        // Desktop platforms
        if (ua.Contains("windows"))
            return ("Windows", ua.Contains("arm") ? "Windows ARM" : "Windows PC");
        if (ua.Contains("macintosh") || ua.Contains("mac os"))
            return ("macOS", "Mac");
        if (ua.Contains("linux"))
        {
            if (ua.Contains("cros"))
                return ("ChromeOS", "Chromebook");
            return ("Linux", "Linux PC");
        }
        
        // Gaming consoles
        if (ua.Contains("playstation"))
            return ("PlayStation", "Gaming Console");
        if (ua.Contains("xbox"))
            return ("Xbox", "Gaming Console");
        if (ua.Contains("nintendo"))
            return ("Nintendo", "Gaming Console");
        
        // Smart TVs
        if (ua.Contains("smart-tv") || ua.Contains("webos") || ua.Contains("tizen") || ua.Contains("roku"))
            return ("SmartTV", "Smart TV");
        
        // Other browsers (might be bots or unknown)
        return ("Unknown", "Unknown Device");
    }
    
    /// <summary>
    /// Generates onboarding content based on capabilities.
    /// </summary>
    private static (string message, string reason, List<string> actions) GenerateOnboardingContent(
        string platform, bool canBroadcast, bool canBackground)
    {
        if (canBroadcast)
        {
            return (
                "🎉 Great news! Your device can join the NFRAME network as a full member. " +
                "Download our app to get unlimited internet at any NFRAME hotspot worldwide!",
                "",
                new List<string> { "download_app", "become_member", "guest_continue" }
            );
        }
        
        // Guest-only with clear explanation
        var reason = platform switch
        {
            "iOS" or "iPadOS" => "iOS devices cannot create WiFi hotspots from third-party apps due to Apple's security restrictions.",
            "Android" => "Most Android devices require system-level access to create hotspots, which our app cannot obtain.",
            "ChromeOS" => "Chromebooks have limited network capabilities and cannot create WiFi hotspots.",
            "PlayStation" or "Xbox" or "Nintendo" => "Gaming consoles cannot run background apps or create hotspots.",
            "SmartTV" => "Smart TVs cannot run background apps or create network access points.",
            _ => "Your device cannot create WiFi hotspots, but you can still use the network as a guest."
        };
        
        var message = $"📱 Welcome! While your {platform} device cannot broadcast a hotspot, " +
                     "you can still enjoy free internet as a guest. " +
                     "Want faster speeds? Install our app on a Windows, Mac, or Linux computer!";
        
        return (message, reason, new List<string> { "guest_continue", "learn_more" });
    }
    
    /// <summary>
    /// Checks local machine's SSID broadcast capability (run on host).
    /// </summary>
    public static async Task<bool> CheckLocalBroadcastCapabilityAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await CheckWindowsHostedNetworkAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await CheckLinuxAPCapabilityAsync();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // macOS can do Internet Sharing
            return true;
        }
        
        return false;
    }
    
    private static async Task<bool> CheckWindowsHostedNetworkAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show drivers",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return false;
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output.Contains("Hosted network supported  : Yes");
        }
        catch
        {
            return false;
        }
    }
    
    private static async Task<bool> CheckLinuxAPCapabilityAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "iw",
                Arguments = "list",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process == null) return false;
            
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output.Contains("* AP");
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Platform usage tracking for adaptive presentation.
/// </summary>
public class PlatformUsageTracker
{
    private readonly Dictionary<string, PlatformStats> _stats = new();
    private readonly object _lock = new();
    
    public void RecordVisit(string platform, string deviceType, OnboardingPath path)
    {
        lock (_lock)
        {
            var key = $"{platform}|{deviceType}";
            if (!_stats.TryGetValue(key, out var stats))
            {
                stats = new PlatformStats { Platform = platform, DeviceType = deviceType };
                _stats[key] = stats;
            }
            
            stats.TotalVisits++;
            stats.LastSeen = DateTime.UtcNow;
            
            if (path == OnboardingPath.FullMember)
                stats.MemberConversions++;
            else if (path == OnboardingPath.GuestOnly)
                stats.GuestContinues++;
        }
    }
    
    public IReadOnlyDictionary<string, PlatformStats> GetStats() => _stats;
    
    public PlatformStats? GetPlatformStats(string platform)
    {
        return _stats.Values.FirstOrDefault(s => s.Platform == platform);
    }
}

public class PlatformStats
{
    public required string Platform { get; init; }
    public required string DeviceType { get; init; }
    public int TotalVisits { get; set; }
    public int MemberConversions { get; set; }
    public int GuestContinues { get; set; }
    public DateTime LastSeen { get; set; }
    
    public double ConversionRate => TotalVisits > 0 
        ? (double)MemberConversions / TotalVisits * 100 
        : 0;
}
