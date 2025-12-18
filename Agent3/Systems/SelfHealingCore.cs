/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                   AGENT 3 - SELF-HEALING & REFACTORING CORE                ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Monitors system health, detects anomalies, and repairs/improves  ║
 * ║           code and configuration in real-time for continuous operation     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Systems
{
    /// <summary>
    /// Represents a system health metric.
    /// </summary>
    public class HealthMetric
    {
        public string Name { get; }
        public float Value { get; set; }
        public float WarningThreshold { get; }
        public float CriticalThreshold { get; }
        public DateTime LastUpdated { get; set; }
        
        public HealthStatus Status
        {
            get
            {
                if (Value >= CriticalThreshold) return HealthStatus.Critical;
                if (Value >= WarningThreshold) return HealthStatus.Warning;
                return HealthStatus.Healthy;
            }
        }
        
        public HealthMetric(string name, float warningThreshold, float criticalThreshold)
        {
            Name = name;
            WarningThreshold = warningThreshold;
            CriticalThreshold = criticalThreshold;
            Value = 0;
            LastUpdated = DateTime.UtcNow;
        }
    }

    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical
    }

    /// <summary>
    /// Represents a detected anomaly in the system.
    /// </summary>
    public class Anomaly
    {
        public string Id { get; }
        public string Description { get; }
        public HealthStatus Severity { get; }
        public string AffectedComponent { get; }
        public DateTime DetectedAt { get; }
        public bool IsResolved { get; set; }
        public string? ResolutionAction { get; set; }
        
        public Anomaly(string description, HealthStatus severity, string affectedComponent)
        {
            Id = $"ANOMALY_{Guid.NewGuid().ToString("N")[..8]}";
            Description = description;
            Severity = severity;
            AffectedComponent = affectedComponent;
            DetectedAt = DateTime.UtcNow;
            IsResolved = false;
        }
    }

    /// <summary>
    /// The Self-Healing Core maintains system integrity through continuous
    /// monitoring, anomaly detection, and automated repair procedures.
    /// </summary>
    public class SelfHealingCore
    {
        private readonly Dictionary<string, HealthMetric> _healthMetrics;
        private readonly List<Anomaly> _detectedAnomalies;
        private readonly CancellationTokenSource _monitoringCts;
        private Task? _monitoringTask;
        private readonly int _monitoringIntervalMs;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<Anomaly>? AnomalyDetected;
        public event EventHandler<Anomaly>? AnomalyResolved;
        
        public bool IsMonitoring { get; private set; }
        public IReadOnlyList<Anomaly> ActiveAnomalies => 
            _detectedAnomalies.Where(a => !a.IsResolved).ToList().AsReadOnly();
        
        public SelfHealingCore(int monitoringIntervalMs = 5000)
        {
            _healthMetrics = new Dictionary<string, HealthMetric>();
            _detectedAnomalies = new List<Anomaly>();
            _monitoringCts = new CancellationTokenSource();
            _monitoringIntervalMs = monitoringIntervalMs;
            
            InitializeDefaultMetrics();
        }
        
        private void InitializeDefaultMetrics()
        {
            // CPU usage threshold
            _healthMetrics["cpu_usage"] = new HealthMetric("CPU Usage", 70f, 90f);
            
            // Memory usage threshold
            _healthMetrics["memory_usage"] = new HealthMetric("Memory Usage", 75f, 95f);
            
            // Response latency (ms)
            _healthMetrics["response_latency"] = new HealthMetric("Response Latency", 500f, 2000f);
            
            // Error rate (percentage)
            _healthMetrics["error_rate"] = new HealthMetric("Error Rate", 1f, 5f);
            
            // Goal completion stall time (seconds)
            _healthMetrics["goal_stall_time"] = new HealthMetric("Goal Stall Time", 30f, 120f);
        }
        
        /// <summary>
        /// Starts the continuous health monitoring process.
        /// </summary>
        public void StartMonitoring()
        {
            if (IsMonitoring) return;
            
            IsMonitoring = true;
            EmitThought("◎ Self-Healing Core: Monitoring activated");
            
            _monitoringTask = Task.Run(async () =>
            {
                while (!_monitoringCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await PerformHealthCheck();
                        await TryResolveAnomalies();
                        await Task.Delay(_monitoringIntervalMs, _monitoringCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        EmitThought($"∴ Self-Healing error: {ex.Message}");
                    }
                }
            }, _monitoringCts.Token);
        }
        
        /// <summary>
        /// Stops the monitoring process.
        /// </summary>
        public void StopMonitoring()
        {
            IsMonitoring = false;
            _monitoringCts.Cancel();
            EmitThought("◎ Self-Healing Core: Monitoring deactivated");
        }
        
        private async Task PerformHealthCheck()
        {
            // Simulate gathering health metrics
            await Task.Run(() =>
            {
                // Get current process metrics
                var process = Process.GetCurrentProcess();
                
                // CPU (simulated - actual CPU usage requires performance counters)
                UpdateMetric("cpu_usage", new Random().Next(10, 60));
                
                // Memory usage percentage
                float memoryUsage = (process.WorkingSet64 / (float)GC.GetTotalMemory(false)) * 10;
                UpdateMetric("memory_usage", Math.Min(memoryUsage, 80f));
                
                // Response latency (simulated)
                UpdateMetric("response_latency", new Random().Next(50, 300));
                
                // Error rate (simulated)
                UpdateMetric("error_rate", new Random().NextSingle() * 0.5f);
                
                // Goal stall time (simulated)
                UpdateMetric("goal_stall_time", new Random().Next(0, 20));
            });
            
            // Check for anomalies
            foreach (var metric in _healthMetrics.Values)
            {
                if (metric.Status != HealthStatus.Healthy)
                {
                    var existingAnomaly = _detectedAnomalies
                        .FirstOrDefault(a => !a.IsResolved && a.AffectedComponent == metric.Name);
                    
                    if (existingAnomaly == null)
                    {
                        var anomaly = new Anomaly(
                            $"{metric.Name} exceeded threshold: {metric.Value:F1} (threshold: {metric.WarningThreshold})",
                            metric.Status,
                            metric.Name
                        );
                        
                        _detectedAnomalies.Add(anomaly);
                        EmitThought($"∴ ANOMALY DETECTED: {anomaly.Description}");
                        AnomalyDetected?.Invoke(this, anomaly);
                    }
                }
            }
        }
        
        private void UpdateMetric(string metricName, float value)
        {
            if (_healthMetrics.TryGetValue(metricName, out var metric))
            {
                metric.Value = value;
                metric.LastUpdated = DateTime.UtcNow;
            }
        }
        
        private async Task TryResolveAnomalies()
        {
            var unresolvedAnomalies = _detectedAnomalies.Where(a => !a.IsResolved).ToList();
            
            foreach (var anomaly in unresolvedAnomalies)
            {
                var resolved = await AttemptResolution(anomaly);
                if (resolved)
                {
                    anomaly.IsResolved = true;
                    EmitThought($"◈ Anomaly resolved: {anomaly.Id} via {anomaly.ResolutionAction}");
                    AnomalyResolved?.Invoke(this, anomaly);
                }
            }
        }
        
        private async Task<bool> AttemptResolution(Anomaly anomaly)
        {
            // Resolution strategies based on anomaly type
            return anomaly.AffectedComponent switch
            {
                "cpu_usage" => await ResolveCpuAnomaly(anomaly),
                "memory_usage" => await ResolveMemoryAnomaly(anomaly),
                "response_latency" => await ResolveLatencyAnomaly(anomaly),
                "error_rate" => await ResolveErrorRateAnomaly(anomaly),
                "goal_stall_time" => await ResolveGoalStallAnomaly(anomaly),
                _ => false
            };
        }
        
        private async Task<bool> ResolveCpuAnomaly(Anomaly anomaly)
        {
            EmitThought("⟐ Attempting CPU resolution: reducing background processing...");
            await Task.Delay(100);
            
            // Simulate throttling non-essential processes
            anomaly.ResolutionAction = "Throttled background processing";
            
            // Check if metric improved
            return _healthMetrics["cpu_usage"].Value < _healthMetrics["cpu_usage"].WarningThreshold;
        }
        
        private async Task<bool> ResolveMemoryAnomaly(Anomaly anomaly)
        {
            EmitThought("⟐ Attempting memory resolution: triggering garbage collection...");
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            await Task.Delay(100);
            anomaly.ResolutionAction = "Forced garbage collection";
            
            return _healthMetrics["memory_usage"].Value < _healthMetrics["memory_usage"].WarningThreshold;
        }
        
        private async Task<bool> ResolveLatencyAnomaly(Anomaly anomaly)
        {
            EmitThought("⟐ Attempting latency resolution: optimizing request queue...");
            await Task.Delay(100);
            
            anomaly.ResolutionAction = "Optimized request queue";
            return true; // Simulated success
        }
        
        private async Task<bool> ResolveErrorRateAnomaly(Anomaly anomaly)
        {
            EmitThought("⟐ Attempting error rate resolution: enabling fallback handlers...");
            await Task.Delay(100);
            
            anomaly.ResolutionAction = "Activated fallback error handlers";
            return true;
        }
        
        private async Task<bool> ResolveGoalStallAnomaly(Anomaly anomaly)
        {
            EmitThought("⟐ Attempting goal stall resolution: replanning strategic path...");
            await Task.Delay(100);
            
            anomaly.ResolutionAction = "Triggered strategic replan";
            return true;
        }
        
        /// <summary>
        /// Gets the current overall system health.
        /// </summary>
        public HealthStatus GetOverallHealth()
        {
            if (_healthMetrics.Values.Any(m => m.Status == HealthStatus.Critical))
                return HealthStatus.Critical;
            if (_healthMetrics.Values.Any(m => m.Status == HealthStatus.Warning))
                return HealthStatus.Warning;
            return HealthStatus.Healthy;
        }
        
        /// <summary>
        /// Gets a detailed health report.
        /// </summary>
        public Dictionary<string, (float Value, HealthStatus Status)> GetHealthReport()
        {
            return _healthMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.Value, kvp.Value.Status)
            );
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
