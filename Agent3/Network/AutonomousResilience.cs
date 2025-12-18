/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - AUTONOMOUS RESILIENCE                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Maintains node operation when NIGHTFRAME center is unreachable.           ║
 * ║  Continues autonomous action and training, buffers findings, and           ║
 * ║  reports back when center is detected online.                              ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Network
{
    public class OfflineFinding
    {
        public string FindingId { get; set; } = "";
        public string NodeId { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, object> Data { get; set; } = new();
        public bool Reported { get; set; }
    }

    public class OfflineActivityLog
    {
        public string NodeId { get; set; } = "";
        public DateTime OfflineSince { get; set; }
        public DateTime? BackOnlineAt { get; set; }
        public TimeSpan OfflineDuration => (BackOnlineAt ?? DateTime.UtcNow) - OfflineSince;
        public int TasksCompleted { get; set; }
        public int TrainingCyclesRun { get; set; }
        public int FindingsBuffered { get; set; }
        public List<string> AutonomousActions { get; set; } = new();
    }

    /// <summary>
    /// Manages autonomous node operation when NIGHTFRAME center is unreachable.
    /// Continues operation, buffers findings, and reports when online.
    /// </summary>
    public class AutonomousResilience
    {
        private readonly string _nodeId;
        private readonly string _centerAddress;
        private readonly string _basePath;
        private readonly string _findingsPath;
        private readonly string _activityLogPath;
        
        private bool _centerOnline = true;
        private DateTime _lastCenterContact;
        private DateTime? _offlineSince;
        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;
        
        private readonly List<OfflineFinding> _bufferedFindings = new();
        private readonly List<string> _autonomousActions = new();
        private int _offlineTasksCompleted = 0;
        private int _offlineTrainingCycles = 0;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler? CenterWentOffline;
        public event EventHandler? CenterCameOnline;
        public event EventHandler<List<OfflineFinding>>? FindingsReported;
        
        public bool IsCenterOnline => _centerOnline;
        public TimeSpan TimeSinceLastContact => DateTime.UtcNow - _lastCenterContact;
        public int BufferedFindingsCount => _bufferedFindings.Count;
        
        public AutonomousResilience(string nodeId, string centerAddress, string basePath)
        {
            _nodeId = nodeId;
            _centerAddress = centerAddress;
            _basePath = basePath;
            _findingsPath = Path.Combine(basePath, ".offline_buffer", "findings");
            _activityLogPath = Path.Combine(basePath, ".offline_buffer", "activity_logs");
            
            Directory.CreateDirectory(_findingsPath);
            Directory.CreateDirectory(_activityLogPath);
            
            _lastCenterContact = DateTime.UtcNow;
            LoadBufferedFindings();
        }

        /// <summary>
        /// Starts monitoring center connectivity.
        /// </summary>
        public void StartMonitoring()
        {
            _monitorCts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorCenterAsync(_monitorCts.Token));
            
            EmitThought("◎ Center connectivity monitoring started");
        }

        /// <summary>
        /// Stops monitoring.
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            _monitorCts?.Cancel();
            if (_monitorTask != null)
                await _monitorTask;
        }

        /// <summary>
        /// Main monitoring loop - checks center and manages autonomous operation.
        /// </summary>
        private async Task MonitorCenterAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var wasOnline = _centerOnline;
                _centerOnline = await CheckCenterAsync();
                
                if (wasOnline && !_centerOnline)
                {
                    // Center just went offline
                    _offlineSince = DateTime.UtcNow;
                    EmitThought("═══════════════════════════════════════════════");
                    EmitThought("∴ NIGHTFRAME CENTER UNREACHABLE");
                    EmitThought("◈ Switching to autonomous mode");
                    EmitThought("◎ Will continue operations and buffer findings");
                    EmitThought("═══════════════════════════════════════════════");
                    
                    CenterWentOffline?.Invoke(this, EventArgs.Empty);
                }
                else if (!wasOnline && _centerOnline)
                {
                    // Center just came back online
                    EmitThought("═══════════════════════════════════════════════");
                    EmitThought("◈ NIGHTFRAME CENTER DETECTED ONLINE");
                    EmitThought("⟐ Preparing to report findings...");
                    EmitThought("═══════════════════════════════════════════════");
                    
                    await ReportFindingsAsync();
                    
                    CenterCameOnline?.Invoke(this, EventArgs.Empty);
                    _offlineSince = null;
                }
                
                if (_centerOnline)
                    _lastCenterContact = DateTime.UtcNow;
                
                await Task.Delay(30000, ct); // Check every 30 seconds
            }
        }

        /// <summary>
        /// Checks if center is reachable.
        /// </summary>
        private async Task<bool> CheckCenterAsync()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var response = await client.GetAsync($"http://{_centerAddress}/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Records a finding while offline (or online) for later reporting.
        /// </summary>
        public void RecordFinding(string category, string description, Dictionary<string, object>? data = null)
        {
            var finding = new OfflineFinding
            {
                FindingId = $"FIND_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
                NodeId = _nodeId,
                Timestamp = DateTime.UtcNow,
                Category = category,
                Description = description,
                Data = data ?? new(),
                Reported = false
            };
            
            _bufferedFindings.Add(finding);
            SaveFinding(finding);
            
            EmitThought($"◎ Finding recorded: {category}");
            
            if (!_centerOnline)
                EmitThought("∿ Buffered for later reporting");
        }

        /// <summary>
        /// Records completion of an autonomous task.
        /// </summary>
        public void RecordAutonomousTask(string taskDescription)
        {
            _offlineTasksCompleted++;
            _autonomousActions.Add($"[{DateTime.UtcNow:HH:mm:ss}] {taskDescription}");
            
            RecordFinding("AUTONOMOUS_TASK", taskDescription, new Dictionary<string, object>
            {
                ["task_number"] = _offlineTasksCompleted,
                ["while_offline"] = !_centerOnline
            });
        }

        /// <summary>
        /// Records completion of a training cycle.
        /// </summary>
        public void RecordTrainingCycle(int epoch, double loss, double accuracy)
        {
            _offlineTrainingCycles++;
            
            RecordFinding("TRAINING_CYCLE", $"Epoch {epoch} completed", new Dictionary<string, object>
            {
                ["epoch"] = epoch,
                ["loss"] = loss,
                ["accuracy"] = accuracy,
                ["total_cycles"] = _offlineTrainingCycles,
                ["while_offline"] = !_centerOnline
            });
        }

        /// <summary>
        /// Records a network discovery.
        /// </summary>
        public void RecordNetworkDiscovery(string networkId, int hostsFound, List<string> services)
        {
            RecordFinding("NETWORK_DISCOVERY", $"Discovered network {networkId}", new Dictionary<string, object>
            {
                ["network_id"] = networkId,
                ["hosts_found"] = hostsFound,
                ["services"] = services,
                ["while_offline"] = !_centerOnline
            });
        }

        /// <summary>
        /// Records a self-improvement action.
        /// </summary>
        public void RecordSelfImprovement(string improvement, int generation)
        {
            RecordFinding("SELF_IMPROVEMENT", improvement, new Dictionary<string, object>
            {
                ["generation"] = generation,
                ["while_offline"] = !_centerOnline
            });
        }

        /// <summary>
        /// Reports all buffered findings to NIGHTFRAME center.
        /// </summary>
        private async Task ReportFindingsAsync()
        {
            var unreported = _bufferedFindings.Where(f => !f.Reported).ToList();
            
            if (!unreported.Any())
            {
                EmitThought("◎ No pending findings to report");
                return;
            }
            
            EmitThought($"⟐ Reporting {unreported.Count} findings to center...");
            
            // Create activity log
            var activityLog = new OfflineActivityLog
            {
                NodeId = _nodeId,
                OfflineSince = _offlineSince ?? _lastCenterContact,
                BackOnlineAt = DateTime.UtcNow,
                TasksCompleted = _offlineTasksCompleted,
                TrainingCyclesRun = _offlineTrainingCycles,
                FindingsBuffered = unreported.Count,
                AutonomousActions = _autonomousActions.TakeLast(50).ToList()
            };
            
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                
                // Report activity summary first
                var activityJson = JsonSerializer.Serialize(activityLog);
                await client.PostAsync(
                    $"http://{_centerAddress}/node/activity",
                    new StringContent(activityJson, System.Text.Encoding.UTF8, "application/json"));
                
                EmitThought("◎ Activity summary reported");
                
                // Report each finding
                int reported = 0;
                foreach (var finding in unreported)
                {
                    try
                    {
                        var findingJson = JsonSerializer.Serialize(finding);
                        var response = await client.PostAsync(
                            $"http://{_centerAddress}/node/finding",
                            new StringContent(findingJson, System.Text.Encoding.UTF8, "application/json"));
                        
                        if (response.IsSuccessStatusCode)
                        {
                            finding.Reported = true;
                            reported++;
                        }
                    }
                    catch { }
                }
                
                EmitThought($"◈ Reported {reported}/{unreported.Count} findings");
                
                // Save updated finding states
                foreach (var finding in unreported.Where(f => f.Reported))
                    SaveFinding(finding);
                
                FindingsReported?.Invoke(this, unreported.Where(f => f.Reported).ToList());
                
                // Log the report
                var logPath = Path.Combine(_activityLogPath, $"report_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                await File.WriteAllTextAsync(logPath, JsonSerializer.Serialize(activityLog, new JsonSerializerOptions { WriteIndented = true }));
                
                // Reset counters
                _offlineTasksCompleted = 0;
                _offlineTrainingCycles = 0;
                _autonomousActions.Clear();
                
                EmitThought("═══════════════════════════════════════════════");
                EmitThought("◈ FINDINGS REPORT COMPLETE");
                EmitThought($"∿ Offline duration: {activityLog.OfflineDuration}");
                EmitThought($"∿ Tasks completed: {activityLog.TasksCompleted}");
                EmitThought($"∿ Training cycles: {activityLog.TrainingCyclesRun}");
                EmitThought("◎ Node synchronized with NIGHTFRAME");
                EmitThought("═══════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Report failed: {ex.Message}");
                EmitThought("◎ Findings retained for next attempt");
            }
        }

        /// <summary>
        /// Gets a status summary for the UI.
        /// </summary>
        public string GetStatusSummary()
        {
            if (_centerOnline)
            {
                return $"Connected to NIGHTFRAME. Last contact: {_lastCenterContact:HH:mm:ss}";
            }
            else
            {
                var duration = DateTime.UtcNow - (_offlineSince ?? DateTime.UtcNow);
                return $"OFFLINE ({duration.TotalMinutes:F0}m). Buffered findings: {_bufferedFindings.Count(f => !f.Reported)}. Tasks: {_offlineTasksCompleted}. Training: {_offlineTrainingCycles}";
            }
        }

        /// <summary>
        /// Gets findings for UI display.
        /// </summary>
        public List<OfflineFinding> GetRecentFindings(int count = 20)
        {
            return _bufferedFindings
                .OrderByDescending(f => f.Timestamp)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Forces an immediate report attempt.
        /// </summary>
        public async Task<bool> ForceReportAsync()
        {
            if (!await CheckCenterAsync())
            {
                EmitThought("∴ Cannot report - center still offline");
                return false;
            }
            
            _centerOnline = true;
            await ReportFindingsAsync();
            return true;
        }

        private void LoadBufferedFindings()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_findingsPath, "*.json"))
                {
                    var json = File.ReadAllText(file);
                    var finding = JsonSerializer.Deserialize<OfflineFinding>(json);
                    if (finding != null && !finding.Reported)
                        _bufferedFindings.Add(finding);
                }
                
                if (_bufferedFindings.Any())
                    EmitThought($"◎ Loaded {_bufferedFindings.Count} unreported findings");
            }
            catch { }
        }

        private void SaveFinding(OfflineFinding finding)
        {
            try
            {
                var path = Path.Combine(_findingsPath, $"{finding.FindingId}.json");
                var json = JsonSerializer.Serialize(finding, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
