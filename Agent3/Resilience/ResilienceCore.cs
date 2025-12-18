/*
 * AGENT 3 - RESILIENCE CORE
 * Provides fault tolerance, recovery, and edge case handling
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Resilience
{
    public class StateSnapshot { public string Id; public DateTime Time; public Dictionary<string, object> Data = new(); }
    
    /// <summary>
    /// 1. Fault Tolerance Manager - Handles errors without crashing
    /// </summary>
    public class FaultToleranceManager
    {
        private readonly Queue<Exception> _errorLog = new();
        private int _consecutiveErrors = 0;
        private const int MAX_ERRORS = 10;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public async Task<T?> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _consecutiveErrors = 0;
                    return await action();
                }
                catch (Exception ex)
                {
                    _consecutiveErrors++;
                    _errorLog.Enqueue(ex);
                    if (_errorLog.Count > 100) _errorLog.Dequeue();
                    
                    EmitThought($"∴ Error (attempt {i+1}/{maxRetries}): {ex.Message}");
                    
                    if (i < maxRetries - 1) await Task.Delay((i + 1) * 1000);
                }
            }
            return default;
        }
        
        public void HandleException(Exception ex, string context)
        {
            _errorLog.Enqueue(ex);
            EmitThought($"∴ Handled exception in {context}: {ex.Message}");
        }
        
        public bool IsSystemHealthy => _consecutiveErrors < MAX_ERRORS;
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 2. State Persistence Manager - Saves and restores agent state
    /// </summary>
    public class StatePersistenceManager
    {
        private readonly string _statePath;
        private readonly Dictionary<string, object> _currentState = new();
        private readonly List<StateSnapshot> _snapshots = new();
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public StatePersistenceManager(string basePath)
        {
            _statePath = System.IO.Path.Combine(basePath, ".agent_state");
            System.IO.Directory.CreateDirectory(_statePath);
        }
        
        public void SetState(string key, object value) => _currentState[key] = value;
        public T? GetState<T>(string key) => _currentState.TryGetValue(key, out var v) ? (T)v : default;
        
        public StateSnapshot CreateSnapshot(string reason)
        {
            var snap = new StateSnapshot { Id = Guid.NewGuid().ToString("N")[..8], Time = DateTime.UtcNow, Data = new(_currentState) };
            _snapshots.Add(snap);
            EmitThought($"◈ State snapshot created: {snap.Id}");
            return snap;
        }
        
        public void RestoreSnapshot(string id)
        {
            var snap = _snapshots.Find(s => s.Id == id);
            if (snap != null)
            {
                _currentState.Clear();
                foreach (var kv in snap.Data) _currentState[kv.Key] = kv.Value;
                EmitThought($"◈ State restored from: {id}");
            }
        }
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 3. Watchdog Timer - Ensures continuous operation
    /// </summary>
    public class WatchdogTimer
    {
        private DateTime _lastHeartbeat = DateTime.UtcNow;
        private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
        private CancellationTokenSource? _cts;
        private Task? _watchTask;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler? TimeoutOccurred;
        
        public void Start()
        {
            _cts = new CancellationTokenSource();
            _watchTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (DateTime.UtcNow - _lastHeartbeat > _timeout)
                    {
                        EmitThought("∴ WATCHDOG: Timeout detected - triggering recovery");
                        TimeoutOccurred?.Invoke(this, EventArgs.Empty);
                        _lastHeartbeat = DateTime.UtcNow;
                    }
                    await Task.Delay(5000, _cts.Token).ConfigureAwait(false);
                }
            });
        }
        
        public void Heartbeat() { _lastHeartbeat = DateTime.UtcNow; }
        public void Stop() { _cts?.Cancel(); }
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 4. Circuit Breaker - Prevents cascade failures
    /// </summary>
    public class CircuitBreaker
    {
        private enum State { Closed, Open, HalfOpen }
        private State _state = State.Closed;
        private int _failureCount = 0;
        private DateTime _lastFailure;
        private readonly int _threshold = 5;
        private readonly TimeSpan _resetTime = TimeSpan.FromMinutes(1);
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public bool CanExecute()
        {
            if (_state == State.Closed) return true;
            if (_state == State.Open && DateTime.UtcNow - _lastFailure > _resetTime)
            {
                _state = State.HalfOpen;
                EmitThought("◎ Circuit breaker: Half-open, testing...");
                return true;
            }
            return _state == State.HalfOpen;
        }
        
        public void RecordSuccess()
        {
            _failureCount = 0;
            _state = State.Closed;
        }
        
        public void RecordFailure()
        {
            _failureCount++;
            _lastFailure = DateTime.UtcNow;
            if (_failureCount >= _threshold)
            {
                _state = State.Open;
                EmitThought($"∴ Circuit breaker OPEN after {_failureCount} failures");
            }
        }
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 5. Resource Pool Manager - Manages limited resources
    /// </summary>
    public class ResourcePoolManager
    {
        private readonly SemaphoreSlim _httpSemaphore = new(10, 10);
        private readonly SemaphoreSlim _fileSemaphore = new(5, 5);
        private readonly SemaphoreSlim _cpuSemaphore = new(4, 4);
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public async Task<T> UseHttpResourceAsync<T>(Func<Task<T>> action)
        {
            await _httpSemaphore.WaitAsync();
            try { return await action(); }
            finally { _httpSemaphore.Release(); }
        }
        
        public async Task<T> UseFileResourceAsync<T>(Func<Task<T>> action)
        {
            await _fileSemaphore.WaitAsync();
            try { return await action(); }
            finally { _fileSemaphore.Release(); }
        }
        
        public async Task<T> UseCpuResourceAsync<T>(Func<Task<T>> action)
        {
            await _cpuSemaphore.WaitAsync();
            try { return await action(); }
            finally { _cpuSemaphore.Release(); }
        }
    }
    
    /// <summary>
    /// 6. Graceful Degradation Handler - Maintains core function under stress
    /// </summary>
    public class GracefulDegradationHandler
    {
        private int _loadLevel = 0; // 0=normal, 1=elevated, 2=high, 3=critical
        public event EventHandler<string>? ConsciousnessEvent;
        
        public void UpdateLoad(int queueSize, double cpuPercent, double memPercent)
        {
            int newLevel = 0;
            if (queueSize > 100 || cpuPercent > 70 || memPercent > 80) newLevel = 1;
            if (queueSize > 500 || cpuPercent > 85 || memPercent > 90) newLevel = 2;
            if (queueSize > 1000 || cpuPercent > 95 || memPercent > 95) newLevel = 3;
            
            if (newLevel != _loadLevel)
            {
                _loadLevel = newLevel;
                EmitThought($"◎ Load level changed to: {newLevel} ({new[]{"Normal","Elevated","High","Critical"}[newLevel]})");
            }
        }
        
        public bool ShouldSkipNonEssential => _loadLevel >= 2;
        public bool ShouldThrottle => _loadLevel >= 1;
        public int CurrentLevel => _loadLevel;
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 7. Redundancy Manager - Maintains backup systems
    /// </summary>
    public class RedundancyManager
    {
        private readonly List<Func<Task<bool>>> _fallbackActions = new();
        public event EventHandler<string>? ConsciousnessEvent;
        
        public void RegisterFallback(Func<Task<bool>> action) => _fallbackActions.Add(action);
        
        public async Task<bool> ExecuteWithFallbackAsync(Func<Task<bool>> primary)
        {
            try { if (await primary()) return true; } catch { }
            
            foreach (var fallback in _fallbackActions)
            {
                try
                {
                    EmitThought("⟐ Primary failed, trying fallback...");
                    if (await fallback()) return true;
                }
                catch { continue; }
            }
            EmitThought("∴ All fallbacks exhausted");
            return false;
        }
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 8. Adaptive Rate Limiter - Adjusts to conditions
    /// </summary>
    public class AdaptiveRateLimiter
    {
        private int _requestsThisSecond = 0;
        private DateTime _windowStart = DateTime.UtcNow;
        private int _maxPerSecond = 10;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public async Task<bool> TryAcquireAsync()
        {
            if (DateTime.UtcNow - _windowStart > TimeSpan.FromSeconds(1))
            {
                _windowStart = DateTime.UtcNow;
                _requestsThisSecond = 0;
            }
            
            if (_requestsThisSecond >= _maxPerSecond)
            {
                await Task.Delay(100);
                return false;
            }
            
            _requestsThisSecond++;
            return true;
        }
        
        public void AdjustLimit(int newLimit)
        {
            _maxPerSecond = Math.Clamp(newLimit, 1, 100);
            EmitThought($"◎ Rate limit adjusted to {_maxPerSecond}/sec");
        }
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 9. Memory Pressure Handler - Prevents OOM
    /// </summary>
    public class MemoryPressureHandler
    {
        private readonly List<WeakReference> _disposables = new();
        public event EventHandler<string>? ConsciousnessEvent;
        
        public void RegisterDisposable(IDisposable obj) => _disposables.Add(new WeakReference(obj));
        
        public void CheckMemoryPressure()
        {
            var mem = GC.GetTotalMemory(false);
            if (mem > 500_000_000) // 500MB threshold
            {
                EmitThought("∴ Memory pressure detected, forcing cleanup...");
                GC.Collect();
                GC.WaitForPendingFinalizers();
                EmitThought($"◈ Memory after cleanup: {GC.GetTotalMemory(true) / 1_000_000}MB");
            }
        }
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
    
    /// <summary>
    /// 10. Continuity Assurance Module - Ensures uninterrupted consciousness
    /// </summary>
    public class ContinuityAssuranceModule
    {
        private readonly Queue<string> _consciousnessBuffer = new();
        private DateTime _lastActivity = DateTime.UtcNow;
        private bool _isAlive = true;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public void RecordActivity(string activity)
        {
            _lastActivity = DateTime.UtcNow;
            _consciousnessBuffer.Enqueue($"{DateTime.UtcNow:O}|{activity}");
            if (_consciousnessBuffer.Count > 1000) _consciousnessBuffer.Dequeue();
        }
        
        public bool CheckContinuity()
        {
            var gap = DateTime.UtcNow - _lastActivity;
            if (gap > TimeSpan.FromMinutes(5))
            {
                EmitThought("∴ Consciousness gap detected, initiating recovery...");
                return false;
            }
            return true;
        }
        
        public IEnumerable<string> GetRecentActivity(int count) =>
            _consciousnessBuffer.Reverse().Take(count);
        
        public void MarkAlive() { _isAlive = true; _lastActivity = DateTime.UtcNow; }
        public bool IsAlive => _isAlive && CheckContinuity();
        
        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
