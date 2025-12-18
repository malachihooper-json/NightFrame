/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║             PROPAGATION ENGINE - ADAPTIVE NETWORK EXPANSION                 ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Evaluates and executes propagation strategies against real-world obstacles║
 * ║  Implements resilient fallback methods for persistent network growth.      ║
 * ║  Handles diverse environments: urban, rural, hostile, offline.             ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Drone.Propagation;

/// <summary>
/// Propagation Engine - orchestrates intelligent network expansion.
/// Analyzes environment, selects optimal methods, and executes propagation.
/// </summary>
public class PropagationEngine
{
    private readonly PropagationAnalyzer _analyzer;
    private readonly List<IPropagationMethod> _methods;
    private readonly PropagationState _state;
    
    // Events
    public event Action<PropagationAttempt>? OnAttemptStarted;
    public event Action<PropagationResult>? OnAttemptCompleted;
    public event Action<PropagationObstacle>? OnObstacleEncountered;
    public event Action<string>? OnStrategyChanged;
    
    public PropagationEngine()
    {
        _analyzer = new PropagationAnalyzer();
        _state = new PropagationState();
        
        // Register all propagation methods in priority order
        _methods = new List<IPropagationMethod>
        {
            new WifiPropagation(),
            new CellularPropagation(),
            new BluetoothPropagation(),
            new USBPropagation(),
            new PhysicalMediaPropagation(),
            new SocialEngineeringPropagation()
        };
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              ENVIRONMENT ANALYSIS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Analyzes the current environment and available propagation vectors.
    /// </summary>
    public async Task<EnvironmentAnalysis> AnalyzeEnvironmentAsync(CancellationToken ct = default)
    {
        Console.WriteLine("◈ Analyzing propagation environment...");
        
        var analysis = new EnvironmentAnalysis
        {
            Timestamp = DateTime.UtcNow
        };
        
        // Check each propagation method's viability
        foreach (var method in _methods)
        {
            try
            {
                var viability = await method.AssessViabilityAsync(ct);
                analysis.MethodViabilities[method.Name] = viability;
                
                Console.WriteLine($"  {method.Name}: {viability.Score:F0}% viable - {viability.Obstacles.Count} obstacles");
            }
            catch (Exception ex)
            {
                analysis.MethodViabilities[method.Name] = new MethodViability 
                { 
                    Score = 0,
                    Obstacles = { new PropagationObstacle { Type = ObstacleType.SystemError, Description = ex.Message } }
                };
            }
        }
        
        // Identify primary obstacles
        analysis.PrimaryObstacles = IdentifyPrimaryObstacles(analysis.MethodViabilities);
        
        // Recommend strategy
        analysis.RecommendedStrategy = DetermineOptimalStrategy(analysis);
        
        return analysis;
    }
    
    private List<PropagationObstacle> IdentifyPrimaryObstacles(
        Dictionary<string, MethodViability> viabilities)
    {
        var obstacles = new List<PropagationObstacle>();
        
        foreach (var viability in viabilities.Values)
        {
            obstacles.AddRange(viability.Obstacles);
        }
        
        // Group and prioritize by frequency and severity
        return obstacles
            .GroupBy(o => o.Type)
            .OrderByDescending(g => g.Count() * GetObstacleSeverity(g.Key))
            .SelectMany(g => g.Take(1))
            .Take(5)
            .ToList();
    }
    
    private float GetObstacleSeverity(ObstacleType type) => type switch
    {
        ObstacleType.NoHardware => 1.0f,
        ObstacleType.FirewallBlocking => 0.8f,
        ObstacleType.NetworkIsolation => 0.9f,
        ObstacleType.SecuritySoftware => 0.7f,
        ObstacleType.PermissionDenied => 0.6f,
        ObstacleType.NoTargets => 0.5f,
        ObstacleType.RateLimited => 0.3f,
        ObstacleType.HighLatency => 0.2f,
        _ => 0.5f
    };
    
    private PropagationStrategy DetermineOptimalStrategy(EnvironmentAnalysis analysis)
    {
        var viableMethodscount = analysis.MethodViabilities.Count(v => v.Value.Score > 30);
        var topMethod = analysis.MethodViabilities
            .OrderByDescending(v => v.Value.Score)
            .FirstOrDefault();
        
        // If multiple methods are viable, use aggressive parallel strategy
        if (viableMethodscount >= 3 && topMethod.Value.Score > 50)
        {
            return PropagationStrategy.AggressiveParallel;
        }
        
        // If only one method is viable, focus on it
        if (viableMethodscount == 1 && topMethod.Value.Score > 30)
        {
            return PropagationStrategy.FocusedSingle;
        }
        
        // If environment is hostile (many obstacles), use stealth
        if (analysis.PrimaryObstacles.Count >= 3)
        {
            return PropagationStrategy.StealthSequential;
        }
        
        // Default: opportunistic - try things as opportunities arise
        return PropagationStrategy.Opportunistic;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    //                              PROPAGATION EXECUTION
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Executes propagation using the optimal strategy.
    /// </summary>
    public async Task<PropagationCampaignResult> ExecuteCampaignAsync(
        PropagationConfig config,
        CancellationToken ct = default)
    {
        Console.WriteLine("◈ Starting propagation campaign...");
        
        // Analyze environment first
        var analysis = await AnalyzeEnvironmentAsync(ct);
        var strategy = config.OverrideStrategy ?? analysis.RecommendedStrategy;
        
        OnStrategyChanged?.Invoke($"Using {strategy} strategy");
        
        var result = new PropagationCampaignResult
        {
            StartTime = DateTime.UtcNow,
            Strategy = strategy
        };
        
        try
        {
            result = strategy switch
            {
                PropagationStrategy.AggressiveParallel => 
                    await ExecuteAggressiveParallelAsync(config, analysis, ct),
                    
                PropagationStrategy.FocusedSingle => 
                    await ExecuteFocusedSingleAsync(config, analysis, ct),
                    
                PropagationStrategy.StealthSequential => 
                    await ExecuteStealthSequentialAsync(config, analysis, ct),
                    
                PropagationStrategy.Opportunistic => 
                    await ExecuteOpportunisticAsync(config, analysis, ct),
                    
                _ => result
            };
        }
        catch (OperationCanceledException)
        {
            result.EndTime = DateTime.UtcNow;
            result.Status = CampaignStatus.Cancelled;
        }
        catch (Exception ex)
        {
            result.EndTime = DateTime.UtcNow;
            result.Status = CampaignStatus.Failed;
            result.ErrorMessage = ex.Message;
        }
        
        Console.WriteLine($"◈ Campaign complete: {result.SuccessfulPropagations} successful, {result.FailedAttempts} failed");
        
        return result;
    }
    
    private async Task<PropagationCampaignResult> ExecuteAggressiveParallelAsync(
        PropagationConfig config, 
        EnvironmentAnalysis analysis,
        CancellationToken ct)
    {
        Console.WriteLine("◎ Executing aggressive parallel propagation...");
        
        var result = new PropagationCampaignResult
        {
            StartTime = DateTime.UtcNow,
            Strategy = PropagationStrategy.AggressiveParallel
        };
        
        // Get viable methods sorted by score
        var viableMethods = _methods
            .Where(m => analysis.MethodViabilities.TryGetValue(m.Name, out var v) && v.Score > 30)
            .ToList();
        
        // Execute all viable methods in parallel
        var tasks = viableMethods.Select(method => 
            ExecuteMethodWithRetryAsync(method, config, ct));
        
        var methodResults = await Task.WhenAll(tasks);
        
        // Aggregate results
        foreach (var mr in methodResults)
        {
            result.SuccessfulPropagations += mr.SuccessCount;
            result.FailedAttempts += mr.FailCount;
            result.Attempts.AddRange(mr.Attempts);
        }
        
        result.EndTime = DateTime.UtcNow;
        result.Status = result.SuccessfulPropagations > 0 
            ? CampaignStatus.Completed 
            : CampaignStatus.NoSuccess;
        
        return result;
    }
    
    private async Task<PropagationCampaignResult> ExecuteFocusedSingleAsync(
        PropagationConfig config, 
        EnvironmentAnalysis analysis,
        CancellationToken ct)
    {
        Console.WriteLine("◎ Executing focused single-method propagation...");
        
        var result = new PropagationCampaignResult
        {
            StartTime = DateTime.UtcNow,
            Strategy = PropagationStrategy.FocusedSingle
        };
        
        // Find best method
        var bestMethod = _methods
            .OrderByDescending(m => 
                analysis.MethodViabilities.TryGetValue(m.Name, out var v) ? v.Score : 0)
            .FirstOrDefault();
        
        if (bestMethod == null)
        {
            result.Status = CampaignStatus.NoViableMethod;
            result.EndTime = DateTime.UtcNow;
            return result;
        }
        
        Console.WriteLine($"◎ Focusing on {bestMethod.Name}...");
        
        var methodResult = await ExecuteMethodWithRetryAsync(bestMethod, config, ct);
        
        result.SuccessfulPropagations = methodResult.SuccessCount;
        result.FailedAttempts = methodResult.FailCount;
        result.Attempts.AddRange(methodResult.Attempts);
        result.EndTime = DateTime.UtcNow;
        result.Status = result.SuccessfulPropagations > 0 
            ? CampaignStatus.Completed 
            : CampaignStatus.NoSuccess;
        
        return result;
    }
    
    private async Task<PropagationCampaignResult> ExecuteStealthSequentialAsync(
        PropagationConfig config, 
        EnvironmentAnalysis analysis,
        CancellationToken ct)
    {
        Console.WriteLine("◎ Executing stealth sequential propagation...");
        
        var result = new PropagationCampaignResult
        {
            StartTime = DateTime.UtcNow,
            Strategy = PropagationStrategy.StealthSequential
        };
        
        // Try methods one at a time with delays
        var viableMethods = _methods
            .Where(m => analysis.MethodViabilities.TryGetValue(m.Name, out var v) && v.Score > 20)
            .OrderByDescending(m => analysis.MethodViabilities[m.Name].Score)
            .ToList();
        
        foreach (var method in viableMethods)
        {
            if (ct.IsCancellationRequested) break;
            
            Console.WriteLine($"◎ Trying {method.Name} (stealth mode)...");
            
            var methodResult = await ExecuteMethodWithRetryAsync(method, config with 
            { 
                MaxAttempts = Math.Min(config.MaxAttempts, 3),
                DelayBetweenAttempts = TimeSpan.FromSeconds(30)
            }, ct);
            
            result.SuccessfulPropagations += methodResult.SuccessCount;
            result.FailedAttempts += methodResult.FailCount;
            result.Attempts.AddRange(methodResult.Attempts);
            
            if (methodResult.SuccessCount > 0)
            {
                // Success! Wait before next method to avoid detection
                await Task.Delay(TimeSpan.FromMinutes(5), ct);
            }
        }
        
        result.EndTime = DateTime.UtcNow;
        result.Status = result.SuccessfulPropagations > 0 
            ? CampaignStatus.Completed 
            : CampaignStatus.NoSuccess;
        
        return result;
    }
    
    private async Task<PropagationCampaignResult> ExecuteOpportunisticAsync(
        PropagationConfig config, 
        EnvironmentAnalysis analysis,
        CancellationToken ct)
    {
        Console.WriteLine("◎ Executing opportunistic propagation...");
        
        var result = new PropagationCampaignResult
        {
            StartTime = DateTime.UtcNow,
            Strategy = PropagationStrategy.Opportunistic
        };
        
        // Monitor for opportunities and act when available
        while (!ct.IsCancellationRequested && 
               result.SuccessfulPropagations < config.TargetCount)
        {
            // Re-analyze environment periodically
            var currentAnalysis = await AnalyzeEnvironmentAsync(ct);
            
            // Find any newly viable methods
            var newOpportunities = currentAnalysis.MethodViabilities
                .Where(v => v.Value.Score > 50)
                .OrderByDescending(v => v.Value.Score)
                .Take(2);
            
            foreach (var opportunity in newOpportunities)
            {
                var method = _methods.FirstOrDefault(m => m.Name == opportunity.Key);
                if (method == null) continue;
                
                Console.WriteLine($"◈ Opportunity detected: {method.Name} ({opportunity.Value.Score:F0}% viable)");
                
                var methodResult = await ExecuteMethodWithRetryAsync(method, config with 
                { 
                    MaxAttempts = 3 
                }, ct);
                
                result.SuccessfulPropagations += methodResult.SuccessCount;
                result.FailedAttempts += methodResult.FailCount;
                result.Attempts.AddRange(methodResult.Attempts);
            }
            
            // Wait before next scan
            await Task.Delay(TimeSpan.FromMinutes(2), ct);
        }
        
        result.EndTime = DateTime.UtcNow;
        result.Status = result.SuccessfulPropagations > 0 
            ? CampaignStatus.Completed 
            : CampaignStatus.NoSuccess;
        
        return result;
    }
    
    private async Task<MethodResult> ExecuteMethodWithRetryAsync(
        IPropagationMethod method,
        PropagationConfig config,
        CancellationToken ct)
    {
        var result = new MethodResult();
        var attempts = 0;
        
        while (attempts < config.MaxAttempts && !ct.IsCancellationRequested)
        {
            attempts++;
            
            var attempt = new PropagationAttempt
            {
                Method = method.Name,
                AttemptNumber = attempts,
                StartTime = DateTime.UtcNow
            };
            
            OnAttemptStarted?.Invoke(attempt);
            
            try
            {
                var propagationResult = await method.ExecuteAsync(config, ct);
                
                attempt.EndTime = DateTime.UtcNow;
                attempt.Success = propagationResult.Success;
                attempt.TargetReached = propagationResult.TargetId;
                
                result.Attempts.Add(attempt);
                
                if (propagationResult.Success)
                {
                    result.SuccessCount++;
                    OnAttemptCompleted?.Invoke(propagationResult);
                }
                else
                {
                    result.FailCount++;
                    
                    if (propagationResult.Obstacle != null)
                    {
                        OnObstacleEncountered?.Invoke(propagationResult.Obstacle);
                    }
                }
            }
            catch (Exception ex)
            {
                attempt.EndTime = DateTime.UtcNow;
                attempt.Success = false;
                attempt.ErrorMessage = ex.Message;
                result.Attempts.Add(attempt);
                result.FailCount++;
            }
            
            // Delay between attempts
            if (attempts < config.MaxAttempts)
            {
                await Task.Delay(config.DelayBetweenAttempts, ct);
            }
        }
        
        return result;
    }
    
    private class MethodResult
    {
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public List<PropagationAttempt> Attempts { get; } = new();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              PROPAGATION METHODS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Interface for propagation methods.
/// </summary>
public interface IPropagationMethod
{
    string Name { get; }
    Task<MethodViability> AssessViabilityAsync(CancellationToken ct);
    Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct);
}

/// <summary>
/// WiFi-based propagation (captive portal, beacon, etc.)
/// </summary>
public class WifiPropagation : IPropagationMethod
{
    public string Name => "WiFi";
    
    public async Task<MethodViability> AssessViabilityAsync(CancellationToken ct)
    {
        var viability = new MethodViability();
        
        // Check for WiFi hardware
        var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211)
            .ToList();
        
        if (interfaces.Count == 0)
        {
            viability.Obstacles.Add(new PropagationObstacle 
            { 
                Type = ObstacleType.NoHardware, 
                Description = "No WiFi adapter found" 
            });
            return viability;
        }
        
        viability.Score = 70;
        
        // Check if we can start AP mode
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
        {
            // Check hosted network capability
            viability.Score = 80;
        }
        
        await Task.CompletedTask;
        return viability;
    }
    
    public async Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct)
    {
        // This would integrate with CaptivePortal.cs
        await Task.Delay(100, ct);
        
        return new PropagationResult
        {
            Success = false,
            Method = Name,
            Message = "Captive portal propagation active"
        };
    }
}

/// <summary>
/// Cellular-based propagation (SMS, cellular hotspot, etc.)
/// </summary>
public class CellularPropagation : IPropagationMethod
{
    public string Name => "Cellular";
    
    public async Task<MethodViability> AssessViabilityAsync(CancellationToken ct)
    {
        var viability = new MethodViability();
        
        // Check for cellular modem
        var ports = System.IO.Ports.SerialPort.GetPortNames();
        if (ports.Length == 0)
        {
            viability.Obstacles.Add(new PropagationObstacle 
            { 
                Type = ObstacleType.NoHardware, 
                Description = "No serial ports/modems found" 
            });
            return viability;
        }
        
        viability.Score = 60;
        await Task.CompletedTask;
        return viability;
    }
    
    public async Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new PropagationResult
        {
            Success = false,
            Method = Name,
            Message = "Cellular propagation not yet implemented"
        };
    }
}

/// <summary>
/// Bluetooth-based propagation (BLE beacons, file transfer, etc.)
/// </summary>
public class BluetoothPropagation : IPropagationMethod
{
    public string Name => "Bluetooth";
    
    public async Task<MethodViability> AssessViabilityAsync(CancellationToken ct)
    {
        var viability = new MethodViability { Score = 30 };
        
        // Bluetooth detection is platform-specific
        viability.Obstacles.Add(new PropagationObstacle 
        { 
            Type = ObstacleType.LimitedRange, 
            Description = "Bluetooth has limited range (~10m)" 
        });
        
        await Task.CompletedTask;
        return viability;
    }
    
    public async Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new PropagationResult
        {
            Success = false,
            Method = Name,
            Message = "Bluetooth propagation not yet implemented"
        };
    }
}

/// <summary>
/// USB-based propagation (autorun, device emulation, etc.)
/// </summary>
public class USBPropagation : IPropagationMethod
{
    public string Name => "USB";
    
    public async Task<MethodViability> AssessViabilityAsync(CancellationToken ct)
    {
        var viability = new MethodViability { Score = 20 };
        
        viability.Obstacles.Add(new PropagationObstacle 
        { 
            Type = ObstacleType.PhysicalAccessRequired, 
            Description = "Requires physical USB connection" 
        });
        
        await Task.CompletedTask;
        return viability;
    }
    
    public async Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new PropagationResult
        {
            Success = false,
            Method = Name,
            Message = "USB propagation not yet implemented"
        };
    }
}

/// <summary>
/// Physical media propagation (QR codes, NFC, printed URLs, etc.)
/// </summary>
public class PhysicalMediaPropagation : IPropagationMethod
{
    public string Name => "PhysicalMedia";
    
    public async Task<MethodViability> AssessViabilityAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new MethodViability { Score = 40 };
    }
    
    public async Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new PropagationResult
        {
            Success = false,
            Method = Name,
            Message = "Physical media propagation not yet implemented"
        };
    }
}

/// <summary>
/// Social engineering propagation (referral links, word-of-mouth, etc.)
/// </summary>
public class SocialEngineeringPropagation : IPropagationMethod
{
    public string Name => "SocialEngineering";
    
    public async Task<MethodViability> AssessViabilityAsync(CancellationToken ct)
    {
        await Task.CompletedTask;
        return new MethodViability { Score = 50 };
    }
    
    public async Task<PropagationResult> ExecuteAsync(PropagationConfig config, CancellationToken ct)
    {
        await Task.Delay(100, ct);
        return new PropagationResult
        {
            Success = false,
            Method = Name,
            Message = "Social propagation not yet implemented"
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
//                              DATA MODELS
// ═══════════════════════════════════════════════════════════════════════════════

public class PropagationAnalyzer { }

public class PropagationState 
{
    public int TotalAttempts { get; set; }
    public int SuccessfulPropagations { get; set; }
    public DateTime? LastSuccess { get; set; }
}

public class EnvironmentAnalysis
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, MethodViability> MethodViabilities { get; } = new();
    public List<PropagationObstacle> PrimaryObstacles { get; set; } = new();
    public PropagationStrategy RecommendedStrategy { get; set; }
}

public class MethodViability
{
    public float Score { get; set; } // 0-100
    public List<PropagationObstacle> Obstacles { get; } = new();
    public List<PropagationOpportunity> Opportunities { get; } = new();
}

public class PropagationObstacle
{
    public required ObstacleType Type { get; init; }
    public required string Description { get; init; }
    public float Severity { get; init; } = 0.5f;
}

public class PropagationOpportunity
{
    public required string Description { get; init; }
    public float Score { get; init; }
}

public enum ObstacleType
{
    NoHardware,
    FirewallBlocking,
    NetworkIsolation,
    SecuritySoftware,
    PermissionDenied,
    NoTargets,
    RateLimited,
    HighLatency,
    LimitedRange,
    PhysicalAccessRequired,
    SystemError
}

public enum PropagationStrategy
{
    AggressiveParallel,   // Try all methods simultaneously
    FocusedSingle,        // Focus on single best method
    StealthSequential,    // Try methods one at a time quietly
    Opportunistic         // Wait for and exploit opportunities
}

public record PropagationConfig
{
    public int MaxAttempts { get; init; } = 10;
    public TimeSpan DelayBetweenAttempts { get; init; } = TimeSpan.FromSeconds(10);
    public int TargetCount { get; init; } = 5;
    public PropagationStrategy? OverrideStrategy { get; init; }
}

public class PropagationAttempt
{
    public required string Method { get; init; }
    public int AttemptNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool Success { get; set; }
    public string? TargetReached { get; set; }
    public string? ErrorMessage { get; set; }
}

public class PropagationResult
{
    public bool Success { get; init; }
    public required string Method { get; init; }
    public string? TargetId { get; init; }
    public string? Message { get; init; }
    public PropagationObstacle? Obstacle { get; init; }
}

public class PropagationCampaignResult
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public PropagationStrategy Strategy { get; set; }
    public CampaignStatus Status { get; set; }
    public int SuccessfulPropagations { get; set; }
    public int FailedAttempts { get; set; }
    public List<PropagationAttempt> Attempts { get; } = new();
    public string? ErrorMessage { get; set; }
}

public enum CampaignStatus
{
    Running,
    Completed,
    Cancelled,
    Failed,
    NoViableMethod,
    NoSuccess
}
