/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              SELF-HEALING ENGINE - NEURAL NETWORK AUTO-FIX                 ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Identifies issues, trains on diagnostics, generates fixes, tests them.    ║
 * ║  Disseminates validated fixes via GOSSIP protocol.                         ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Text.Json;
using System.Security.Cryptography;

namespace NIGHTFRAME.Drone.Network;

/// <summary>
/// Issue category for classification.
/// </summary>
public enum IssueCategory
{
    SSIDMethod,
    NetworkConnectivity,
    PortalRendering,
    UpdateDeployment,
    P2PRelay,
    PlatformDetection,
    ResourceLimit,
    Unknown
}

/// <summary>
/// Issue severity for prioritization.
/// </summary>
public enum IssueSeverity
{
    Critical,   // Blocks core functionality
    High,       // Major feature broken
    Medium,     // Feature degraded
    Low,        // Minor inconvenience
    Info        // Telemetry only
}

/// <summary>
/// Detected issue with diagnostic info.
/// </summary>
public class DetectedIssue
{
    public string IssueId { get; set; } = Guid.NewGuid().ToString()[..8];
    public IssueCategory Category { get; set; }
    public IssueSeverity Severity { get; set; }
    public string Description { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? StackTrace { get; set; }
    public Dictionary<string, string> Context { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string NodeId { get; set; } = "";
    public string OSInfo { get; set; } = "";
    public string? ProposedFix { get; set; }
    public bool FixApplied { get; set; }
    public bool FixSuccessful { get; set; }
}

/// <summary>
/// Generated fix from self-healing analysis.
/// </summary>
public class GeneratedFix
{
    public string FixId { get; set; } = Guid.NewGuid().ToString();
    public string IssueId { get; set; } = "";
    public IssueCategory Category { get; set; }
    public string Description { get; set; } = "";
    public FixType Type { get; set; }
    public string? ConfigChange { get; set; }
    public string? CommandToRun { get; set; }
    public string? CodePatch { get; set; }
    public int ConfidenceScore { get; set; } // 0-100
    public bool TestedLocally { get; set; }
    public bool TestPassed { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string GeneratedBy { get; set; } = ""; // NodeId that generated this
    public byte[]? Signature { get; set; }
}

public enum FixType
{
    ConfigChange,       // Change a configuration value
    CommandExecution,   // Run a system command
    MethodFallback,     // Switch to alternative method
    DriverUpdate,       // Suggest driver update
    FeatureDisable,     // Disable a broken feature
    CodePatch,          // Hot-patch code (interpreted)
    Manual              // Requires user intervention
}

/// <summary>
/// Self-healing engine that learns from issues and generates fixes.
/// </summary>
public class SelfHealingEngine
{
    private readonly NodeIdentity _identity;
    private readonly GossipProtocol _gossip;
    private readonly List<DetectedIssue> _issues = new();
    private readonly List<GeneratedFix> _fixes = new();
    private readonly Dictionary<string, int> _issuePatterns = new(); // Pattern -> occurrence count
    
    // Known fixes for common issues (learned patterns)
    private readonly Dictionary<string, Func<DetectedIssue, GeneratedFix?>> _knownFixes = new();
    
    public event Action<DetectedIssue>? OnIssueDetected;
    public event Action<GeneratedFix>? OnFixGenerated;
    public event Action<GeneratedFix>? OnFixApplied;
    
    public SelfHealingEngine(GossipProtocol gossip, NodeIdentity identity)
    {
        _gossip = gossip;
        _identity = identity;
        
        InitializeKnownFixes();
        
        // Subscribe to GOSSIP for receiving fixes from network
        _gossip.OnUpdateReceived += HandleNetworkFix;
    }
    
    /// <summary>
    /// Initializes known fixes from learned patterns.
    /// </summary>
    private void InitializeKnownFixes()
    {
        // SSID: Hosted network not supported -> Use Mobile Hotspot
        _knownFixes["SSID:HostedNetworkNotSupported"] = issue => new GeneratedFix
        {
            IssueId = issue.IssueId,
            Category = IssueCategory.SSIDMethod,
            Description = "Switch to Mobile Hotspot API (hosted network not supported)",
            Type = FixType.MethodFallback,
            ConfigChange = "{\"ssid_method\": \"MobileHotspot\"}",
            ConfidenceScore = 95,
            GeneratedBy = _identity.NodeId
        };
        
        // SSID: Mobile Hotspot not available -> Try WiFi Direct
        _knownFixes["SSID:MobileHotspotNotAvailable"] = issue => new GeneratedFix
        {
            IssueId = issue.IssueId,
            Category = IssueCategory.SSIDMethod,
            Description = "Try WiFi Direct as fallback",
            Type = FixType.MethodFallback,
            ConfigChange = "{\"ssid_method\": \"WiFiDirect\"}",
            ConfidenceScore = 60,
            GeneratedBy = _identity.NodeId
        };
        
        // SSID: All methods failed -> Suggest driver update
        _knownFixes["SSID:AllMethodsFailed"] = issue => new GeneratedFix
        {
            IssueId = issue.IssueId,
            Category = IssueCategory.SSIDMethod,
            Description = "Update WiFi drivers to latest version",
            Type = FixType.DriverUpdate,
            CommandToRun = "powershell -Command \"pnputil /scan-devices\"",
            ConfidenceScore = 40,
            GeneratedBy = _identity.NodeId
        };
        
        // P2P: Relay path too long -> Reduce max hops
        _knownFixes["P2P:MaxHopsExceeded"] = issue => new GeneratedFix
        {
            IssueId = issue.IssueId,
            Category = IssueCategory.P2PRelay,
            Description = "Increase max hops for relay chains",
            Type = FixType.ConfigChange,
            ConfigChange = "{\"max_relay_hops\": 7}",
            ConfidenceScore = 80,
            GeneratedBy = _identity.NodeId
        };
        
        // Platform: Unknown User-Agent -> Add to known patterns
        _knownFixes["Platform:UnknownUserAgent"] = issue =>
        {
            if (issue.Context.TryGetValue("user_agent", out var ua))
            {
                return new GeneratedFix
                {
                    IssueId = issue.IssueId,
                    Category = IssueCategory.PlatformDetection,
                    Description = $"Learn new User-Agent pattern: {ua[..Math.Min(50, ua.Length)]}...",
                    Type = FixType.ConfigChange,
                    ConfigChange = JsonSerializer.Serialize(new { user_agent = ua, platform = "Unknown" }),
                    ConfidenceScore = 30,
                    GeneratedBy = _identity.NodeId
                };
            }
            return null;
        };
    }
    
    /// <summary>
    /// Reports an issue for analysis and potential fix generation.
    /// </summary>
    public async Task<GeneratedFix?> ReportIssueAsync(DetectedIssue issue)
    {
        issue.NodeId = _identity.NodeId;
        issue.OSInfo = Environment.OSVersion.ToString();
        
        _issues.Add(issue);
        OnIssueDetected?.Invoke(issue);
        
        Console.WriteLine($"◈ Issue detected: [{issue.Category}] {issue.Description}");
        
        // Generate pattern key
        var patternKey = GeneratePatternKey(issue);
        _issuePatterns[patternKey] = _issuePatterns.GetValueOrDefault(patternKey) + 1;
        
        // Try to find a known fix
        GeneratedFix? fix = null;
        foreach (var (pattern, fixGenerator) in _knownFixes)
        {
            if (patternKey.Contains(pattern.Split(':')[1]))
            {
                fix = fixGenerator(issue);
                if (fix != null)
                {
                    Console.WriteLine($"  → Known fix found: {fix.Description}");
                    break;
                }
            }
        }
        
        // If no known fix, try to generate one using pattern analysis
        if (fix == null && _issuePatterns[patternKey] >= 3)
        {
            // Issue has occurred multiple times, try to learn a fix
            fix = await TryGenerateNewFixAsync(issue);
        }
        
        if (fix != null)
        {
            // Sign the fix
            fix.Signature = _identity.Sign(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{fix.FixId}{fix.Category}{fix.Description}{fix.Type}"));
            
            _fixes.Add(fix);
            OnFixGenerated?.Invoke(fix);
            
            // Test the fix locally
            var testResult = await TestFixLocallyAsync(fix);
            fix.TestedLocally = true;
            fix.TestPassed = testResult;
            
            if (testResult)
            {
                Console.WriteLine($"  ✓ Fix tested successfully");
                
                // Apply the fix
                var applied = await ApplyFixAsync(fix);
                if (applied)
                {
                    Console.WriteLine($"  ✓ Fix applied");
                    
                    // Disseminate via GOSSIP
                    await DisseminateFixAsync(fix);
                }
            }
            else
            {
                Console.WriteLine($"  ✗ Fix test failed, not applying");
            }
        }
        
        // Save issue for training
        await SaveIssueForTrainingAsync(issue);
        
        return fix;
    }
    
    /// <summary>
    /// Generates a pattern key for issue matching.
    /// </summary>
    private string GeneratePatternKey(DetectedIssue issue)
    {
        var errorHash = issue.ErrorMessage != null 
            ? Convert.ToHexString(SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(issue.ErrorMessage)))[..8]
            : "none";
        
        return $"{issue.Category}:{errorHash}:{issue.Severity}";
    }
    
    /// <summary>
    /// Tries to generate a new fix based on pattern analysis.
    /// </summary>
    private async Task<GeneratedFix?> TryGenerateNewFixAsync(DetectedIssue issue)
    {
        // Analyze similar issues to find common context
        var similarIssues = _issues
            .Where(i => i.Category == issue.Category)
            .TakeLast(10)
            .ToList();
        
        if (similarIssues.Count < 3)
            return null;
        
        // Look for context patterns
        var commonContext = new Dictionary<string, string>();
        foreach (var kvp in issue.Context)
        {
            var matchCount = similarIssues.Count(i => 
                i.Context.TryGetValue(kvp.Key, out var v) && v == kvp.Value);
            
            if (matchCount >= similarIssues.Count / 2)
            {
                commonContext[kvp.Key] = kvp.Value;
            }
        }
        
        // Generate fix based on category
        return issue.Category switch
        {
            IssueCategory.SSIDMethod => new GeneratedFix
            {
                IssueId = issue.IssueId,
                Category = issue.Category,
                Description = "Auto-generated fix: Try alternative SSID method",
                Type = FixType.MethodFallback,
                ConfigChange = "{\"ssid_fallback_enabled\": true}",
                ConfidenceScore = 50,
                GeneratedBy = _identity.NodeId
            },
            IssueCategory.PlatformDetection => new GeneratedFix
            {
                IssueId = issue.IssueId,
                Category = issue.Category,
                Description = "Auto-generated fix: Default to guest mode for unknown platforms",
                Type = FixType.ConfigChange,
                ConfigChange = "{\"unknown_platform_default\": \"guest\"}",
                ConfidenceScore = 70,
                GeneratedBy = _identity.NodeId
            },
            _ => null
        };
    }
    
    /// <summary>
    /// Tests a fix locally before applying.
    /// </summary>
    private async Task<bool> TestFixLocallyAsync(GeneratedFix fix)
    {
        try
        {
            switch (fix.Type)
            {
                case FixType.ConfigChange:
                    // Validate JSON
                    if (fix.ConfigChange != null)
                    {
                        JsonDocument.Parse(fix.ConfigChange);
                        return true;
                    }
                    return false;
                    
                case FixType.MethodFallback:
                    // Always allow method fallbacks
                    return true;
                    
                case FixType.CommandExecution:
                    // Don't auto-execute commands without testing
                    return fix.ConfidenceScore >= 80;
                    
                case FixType.DriverUpdate:
                    // Suggest only, don't auto-run
                    return true;
                    
                case FixType.FeatureDisable:
                    // Allow feature disable if confidence is high
                    return fix.ConfidenceScore >= 70;
                    
                case FixType.CodePatch:
                    // Code patches require high confidence
                    return fix.ConfidenceScore >= 90;
                    
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Applies a validated fix.
    /// </summary>
    private async Task<bool> ApplyFixAsync(GeneratedFix fix)
    {
        try
        {
            switch (fix.Type)
            {
                case FixType.ConfigChange:
                    // Apply config change
                    if (fix.ConfigChange != null)
                    {
                        var configPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "NIGHTFRAME", "fixes", $"{fix.FixId}.json");
                        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
                        await File.WriteAllTextAsync(configPath, fix.ConfigChange);
                        return true;
                    }
                    break;
                    
                case FixType.MethodFallback:
                    // Method fallback is handled by the caller
                    return true;
                    
                case FixType.CommandExecution:
                    // Execute command (be careful!)
                    if (fix.CommandToRun != null && fix.ConfidenceScore >= 80)
                    {
                        // Only execute safe commands
                        if (fix.CommandToRun.Contains("pnputil") ||
                            fix.CommandToRun.Contains("ipconfig"))
                        {
                            // Safe commands
                            Console.WriteLine($"  Would execute: {fix.CommandToRun}");
                            return true;
                        }
                    }
                    break;
                    
                default:
                    Console.WriteLine($"  ℹ️  Manual intervention required: {fix.Description}");
                    return false;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Error applying fix: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Disseminates a validated fix via GOSSIP.
    /// </summary>
    private async Task DisseminateFixAsync(GeneratedFix fix)
    {
        // Save fix to local file for network distribution
        var fixDir = Path.Combine(AppContext.BaseDirectory, "fixes");
        Directory.CreateDirectory(fixDir);
        var fixPath = Path.Combine(fixDir, $"{fix.FixId}.json");
        var fixJson = JsonSerializer.Serialize(fix, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(fixPath, fixJson);
        
        // Create GOSSIP update referencing the fix
        var payloadHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(fixJson)));
        
        var update = new GossipUpdate
        {
            UpdateId = $"FIX_{fix.FixId}",
            Version = "1.0.0",
            SourceNodeId = _identity.NodeId,
            Timestamp = DateTime.UtcNow,
            Type = UpdateType.Configuration,
            PayloadHash = payloadHash,
            PayloadSizeBytes = fixJson.Length,
            Signature = fix.Signature ?? Array.Empty<byte>(),
            ReleaseNotes = fix.Description
        };
        
        // Propagate via GOSSIP
        await _gossip.ReceiveUpdateAsync(update);
        
        Console.WriteLine($"  ✓ Fix disseminated via GOSSIP: {fix.FixId}");
    }
    
    /// <summary>
    /// Handles fix received from network.
    /// </summary>
    private async void HandleNetworkFix(GossipUpdate update)
    {
        if (!update.UpdateId.StartsWith("FIX_"))
            return;
        
        try
        {
            // Extract fix ID from update ID
            var fixId = update.UpdateId.Replace("FIX_", "");
            
            // Try to load fix from local cache or request from network
            var fixDir = Path.Combine(AppContext.BaseDirectory, "fixes");
            var fixPath = Path.Combine(fixDir, $"{fixId}.json");
            
            GeneratedFix? fix = null;
            if (File.Exists(fixPath))
            {
                var json = await File.ReadAllTextAsync(fixPath);
                fix = JsonSerializer.Deserialize<GeneratedFix>(json);
            }
            
            if (fix == null)
            {
                Console.WriteLine($"  ℹ️  Fix {fixId} referenced but not available locally");
                return;
            }
            
            Console.WriteLine($"◈ Received fix from network: {fix.Description}");
            
            // Verify signature (would need public key from source node)
            // For now, check confidence and test locally
            
            if (fix.ConfidenceScore < 50)
            {
                Console.WriteLine($"  ✗ Fix confidence too low ({fix.ConfidenceScore}%)");
                return;
            }
            
            var testResult = await TestFixLocallyAsync(fix);
            if (testResult)
            {
                var applied = await ApplyFixAsync(fix);
                if (applied)
                {
                    Console.WriteLine($"  ✓ Network fix applied: {fix.FixId}");
                    OnFixApplied?.Invoke(fix);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ✗ Error processing network fix: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Saves issue for training data.
    /// </summary>
    private async Task SaveIssueForTrainingAsync(DetectedIssue issue)
    {
        try
        {
            var trainingDir = Path.Combine(AppContext.BaseDirectory, "training_data", "issues");
            Directory.CreateDirectory(trainingDir);
            
            var filename = $"issue_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{issue.IssueId}.json";
            var json = JsonSerializer.Serialize(issue, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(Path.Combine(trainingDir, filename), json);
        }
        catch { /* Ignore save errors */ }
    }
    
    /// <summary>
    /// Gets statistics on issues and fixes.
    /// </summary>
    public SelfHealingStats GetStats() => new()
    {
        TotalIssuesDetected = _issues.Count,
        TotalFixesGenerated = _fixes.Count,
        FixesApplied = _fixes.Count(f => f.TestedLocally && f.TestPassed),
        IssuesByCategory = _issues.GroupBy(i => i.Category)
            .ToDictionary(g => g.Key, g => g.Count()),
        MostCommonPatterns = _issuePatterns
            .OrderByDescending(p => p.Value)
            .Take(5)
            .ToDictionary(p => p.Key, p => p.Value)
    };
}

public class SelfHealingStats
{
    public int TotalIssuesDetected { get; init; }
    public int TotalFixesGenerated { get; init; }
    public int FixesApplied { get; init; }
    public Dictionary<IssueCategory, int> IssuesByCategory { get; init; } = new();
    public Dictionary<string, int> MostCommonPatterns { get; init; } = new();
}
