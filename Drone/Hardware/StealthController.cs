/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    STEALTH CONTROLLER - COVERT OPERATION                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Monitors user activity and throttles CPU to avoid detection.              ║
 * ║  Hooks into OS interrupts to detect mouse/keyboard activity.               ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Runtime.InteropServices;

namespace NIGHTFRAME.Drone.Hardware;

public class StealthController
{
    private bool _isRunning = false;
    private DateTime _lastUserActivity = DateTime.UtcNow;
    private Task? _monitorTask;
    private CancellationTokenSource? _cts;
    
    // Throttle threshold in seconds of inactivity
    private const int IdleThresholdSeconds = 60;
    
    // CPU load tracking
    public double CurrentCpuLoad { get; private set; } = 0;
    
    public StealthController()
    {
    }
    
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        _cts = new CancellationTokenSource();
        _monitorTask = MonitorLoop(_cts.Token);
        
        Console.WriteLine("◎ Stealth controller active");
    }
    
    public void Stop()
    {
        _isRunning = false;
        _cts?.Cancel();
        _monitorTask?.Wait(TimeSpan.FromSeconds(2));
    }
    
    /// <summary>
    /// Waits until the system is idle before allowing heavy work.
    /// </summary>
    public async Task WaitForIdleAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var idleTime = GetIdleTime();
            
            if (idleTime.TotalSeconds >= IdleThresholdSeconds)
            {
                return; // System is idle, proceed
            }
            
            // System is active, wait and throttle
            await Task.Delay(1000, ct);
        }
    }
    
    /// <summary>
    /// Gets the time since last user input.
    /// </summary>
    public TimeSpan GetIdleTime()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsIdleTime();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxIdleTime();
        }
        
        // Fallback: assume idle
        return TimeSpan.FromMinutes(5);
    }
    
    /// <summary>
    /// Checks if user is currently active.
    /// </summary>
    public bool IsUserActive() => GetIdleTime().TotalSeconds < IdleThresholdSeconds;
    
    private async Task MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Update idle time
                var idleTime = GetIdleTime();
                
                if (idleTime.TotalSeconds < 5)
                {
                    _lastUserActivity = DateTime.UtcNow;
                }
                
                // Simulate CPU load tracking
                // In production, use PerformanceCounter
                CurrentCpuLoad = GetCpuLoad();
                
                await Task.Delay(1000, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(5000, ct);
            }
        }
    }
    
    private double GetCpuLoad()
    {
        // Simple estimation based on GC activity
        var memoryInfo = GC.GetGCMemoryInfo();
        var fragmentation = memoryInfo.FragmentedBytes / (double)Math.Max(1, memoryInfo.TotalAvailableMemoryBytes);
        
        return Math.Min(1.0, fragmentation * 2);
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          WINDOWS IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    private TimeSpan GetWindowsIdleTime()
    {
        var lastInput = new LASTINPUTINFO();
        lastInput.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        
        if (GetLastInputInfo(ref lastInput))
        {
            var idleMs = Environment.TickCount - lastInput.dwTime;
            return TimeSpan.FromMilliseconds(idleMs);
        }
        
        return TimeSpan.Zero;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
    
    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    
    // ═══════════════════════════════════════════════════════════════════════════
    //                          LINUX IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════════════════
    
    private TimeSpan GetLinuxIdleTime()
    {
        // Try to read from /proc/uptime and input devices
        try
        {
            // Check input device timestamps
            var inputDir = "/dev/input";
            if (Directory.Exists(inputDir))
            {
                var latestInput = Directory.GetFiles(inputDir, "event*")
                    .Select(f => new FileInfo(f).LastAccessTimeUtc)
                    .OrderByDescending(t => t)
                    .FirstOrDefault();
                
                if (latestInput != default)
                {
                    return DateTime.UtcNow - latestInput;
                }
            }
        }
        catch { }
        
        // Fallback: assume idle
        return TimeSpan.FromMinutes(5);
    }
}
