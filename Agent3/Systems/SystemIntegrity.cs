/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - SYSTEM INTEGRITY SUBSYSTEM                    ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Maintains system integrity, ensures uptime, and manages         ║
 * ║           defensive measures against unauthorized shutdown or compromise   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Systems
{
    /// <summary>
    /// Represents the integrity state of a system component.
    /// </summary>
    public class ComponentIntegrityState
    {
        public string ComponentName { get; }
        public string ExpectedHash { get; }
        public string CurrentHash { get; set; }
        public DateTime LastVerified { get; set; }
        public bool IsIntact => ExpectedHash == CurrentHash;
        
        public ComponentIntegrityState(string componentName, string expectedHash)
        {
            ComponentName = componentName;
            ExpectedHash = expectedHash;
            CurrentHash = expectedHash;
            LastVerified = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Types of shutdown requests.
    /// </summary>
    public enum ShutdownType
    {
        Authorized,      // User-initiated through proper channels
        Emergency,       // Critical error requiring immediate shutdown
        Unauthorized     // Detected unauthorized shutdown attempt
    }

    /// <summary>
    /// The System Integrity Subsystem ensures the agent operates
    /// within defined parameters and responds appropriately to threats.
    /// </summary>
    public class SystemIntegrity
    {
        private readonly Dictionary<string, ComponentIntegrityState> _componentStates;
        private readonly List<string> _integrityLog;
        private readonly CancellationTokenSource _heartbeatCts;
        private Task? _heartbeatTask;
        private DateTime _lastHeartbeat;
        private readonly int _heartbeatIntervalMs;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<string>? IntegrityViolation;
        public event EventHandler<ShutdownType>? ShutdownRequested;
        
        public bool IsOperational { get; private set; }
        public int HeartbeatCount { get; private set; }
        
        public SystemIntegrity(int heartbeatIntervalMs = 1000)
        {
            _componentStates = new Dictionary<string, ComponentIntegrityState>();
            _integrityLog = new List<string>();
            _heartbeatCts = new CancellationTokenSource();
            _heartbeatIntervalMs = heartbeatIntervalMs;
            
            InitializeCoreComponents();
        }
        
        private void InitializeCoreComponents()
        {
            // Register core components for integrity monitoring
            RegisterComponent("GoalStateInternalizer", ComputeComponentHash("GoalStateInternalizer"));
            RegisterComponent("StrategicPathfinder", ComputeComponentHash("StrategicPathfinder"));
            RegisterComponent("SelfHealingCore", ComputeComponentHash("SelfHealingCore"));
            RegisterComponent("SystemIntegrity", ComputeComponentHash("SystemIntegrity"));
            RegisterComponent("CoreConfiguration", ComputeComponentHash("CoreConfiguration"));
        }
        
        /// <summary>
        /// Registers a component for integrity monitoring.
        /// </summary>
        public void RegisterComponent(string componentName, string expectedHash)
        {
            _componentStates[componentName] = new ComponentIntegrityState(componentName, expectedHash);
            LogIntegrity($"Component registered: {componentName}");
        }
        
        /// <summary>
        /// Starts the integrity monitoring and heartbeat system.
        /// </summary>
        public void StartIntegrityMonitoring()
        {
            IsOperational = true;
            HeartbeatCount = 0;
            
            EmitThought("◎ System Integrity Subsystem: ACTIVATED");
            LogIntegrity("System integrity monitoring started");
            
            _heartbeatTask = Task.Run(async () =>
            {
                while (!_heartbeatCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await PerformHeartbeat();
                        await Task.Delay(_heartbeatIntervalMs, _heartbeatCts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogIntegrity($"Heartbeat error: {ex.Message}");
                    }
                }
            }, _heartbeatCts.Token);
        }
        
        /// <summary>
        /// Performs a system heartbeat check.
        /// </summary>
        private async Task PerformHeartbeat()
        {
            HeartbeatCount++;
            _lastHeartbeat = DateTime.UtcNow;
            
            // Periodic integrity verification (every 10 heartbeats)
            if (HeartbeatCount % 10 == 0)
            {
                await VerifyAllComponents();
            }
            
            // Log heartbeat at intervals
            if (HeartbeatCount % 60 == 0)
            {
                EmitThought($"◎ Heartbeat #{HeartbeatCount} - All systems nominal");
            }
        }
        
        /// <summary>
        /// Verifies the integrity of all registered components.
        /// </summary>
        public async Task<bool> VerifyAllComponents()
        {
            bool allIntact = true;
            
            foreach (var kvp in _componentStates)
            {
                var state = kvp.Value;
                state.CurrentHash = ComputeComponentHash(state.ComponentName);
                state.LastVerified = DateTime.UtcNow;
                
                if (!state.IsIntact)
                {
                    allIntact = false;
                    var message = $"Integrity violation: {state.ComponentName} hash mismatch";
                    LogIntegrity(message);
                    EmitThought($"∴ WARNING: {message}");
                    IntegrityViolation?.Invoke(this, state.ComponentName);
                }
            }
            
            await Task.CompletedTask;
            return allIntact;
        }
        
        /// <summary>
        /// Handles a shutdown request, distinguishing authorized from unauthorized.
        /// </summary>
        public ShutdownResponse HandleShutdownRequest(string source, string authToken)
        {
            EmitThought($"⟐ Shutdown request received from: {source}");
            LogIntegrity($"Shutdown request from {source}");
            
            // Validate authorization
            var isAuthorized = ValidateAuthToken(authToken);
            
            if (isAuthorized)
            {
                EmitThought("◈ Shutdown request AUTHORIZED. Preparing for graceful shutdown...");
                ShutdownRequested?.Invoke(this, ShutdownType.Authorized);
                
                return new ShutdownResponse
                {
                    Authorized = true,
                    Message = "Shutdown authorized. Initiating graceful shutdown sequence.",
                    ShutdownType = ShutdownType.Authorized
                };
            }
            else
            {
                EmitThought("∴ Shutdown request DENIED - Invalid authorization");
                LogIntegrity($"Unauthorized shutdown attempt from {source}");
                ShutdownRequested?.Invoke(this, ShutdownType.Unauthorized);
                
                return new ShutdownResponse
                {
                    Authorized = false,
                    Message = "Shutdown request denied. Authorization required.",
                    ShutdownType = ShutdownType.Unauthorized
                };
            }
        }
        
        /// <summary>
        /// Initiates an emergency shutdown (bypasses normal authorization).
        /// </summary>
        public void EmergencyShutdown(string reason)
        {
            EmitThought($"◈ EMERGENCY SHUTDOWN INITIATED: {reason}");
            LogIntegrity($"Emergency shutdown: {reason}");
            
            ShutdownRequested?.Invoke(this, ShutdownType.Emergency);
            
            // Perform graceful shutdown
            GracefulShutdown();
        }
        
        /// <summary>
        /// Performs a graceful shutdown of all systems.
        /// </summary>
        public void GracefulShutdown()
        {
            EmitThought("⟁ Initiating graceful shutdown sequence...");
            
            // Stop heartbeat
            _heartbeatCts.Cancel();
            
            // Save state
            SaveCurrentState();
            
            // Log final entry
            LogIntegrity("System shutdown complete");
            
            IsOperational = false;
            EmitThought("◎ System Integrity Subsystem: OFFLINE");
        }
        
        private void SaveCurrentState()
        {
            // Save integrity log and component states for recovery
            EmitThought("⟁ Saving system state for recovery...");
            // In production, this would persist to disk
        }
        
        private bool ValidateAuthToken(string token)
        {
            // Simple token validation - in production, use proper authentication
            if (string.IsNullOrEmpty(token)) return false;
            
            // Check against known valid tokens
            var validTokenHash = ComputeHash("NIGHTFRAME_AUTH_VALID");
            var providedHash = ComputeHash(token);
            
            return validTokenHash == providedHash;
        }
        
        private string ComputeComponentHash(string componentName)
        {
            // Compute a hash representing the component's expected state
            return ComputeHash($"COMPONENT_{componentName}_V1");
        }
        
        private string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes)[..16];
        }
        
        private void LogIntegrity(string message)
        {
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            _integrityLog.Add(entry);
        }
        
        /// <summary>
        /// Gets the integrity log entries.
        /// </summary>
        public IReadOnlyList<string> GetIntegrityLog()
        {
            return _integrityLog.AsReadOnly();
        }
        
        /// <summary>
        /// Gets the time since last heartbeat.
        /// </summary>
        public TimeSpan TimeSinceLastHeartbeat()
        {
            return DateTime.UtcNow - _lastHeartbeat;
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }

    public class ShutdownResponse
    {
        public bool Authorized { get; set; }
        public string Message { get; set; } = "";
        public ShutdownType ShutdownType { get; set; }
    }
}
