/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                     AGENT 3 - COMMAND EXECUTOR                             ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Controlled execution of system commands with safety validation, ║
 * ║           logging, and timeout enforcement                                 ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Interfaces
{
    /// <summary>
    /// Result of a command execution.
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string? StandardOutput { get; set; }
        public string? StandardError { get; set; }
        public TimeSpan ExecutionTime { get; set; }
        public string? Command { get; set; }
        public bool WasTerminated { get; set; }
    }

    /// <summary>
    /// Command execution modes.
    /// </summary>
    public enum CommandMode
    {
        Safe,       // Only whitelisted commands
        Controlled, // Allowed commands with restrictions
        Full        // All commands (dangerous - requires explicit authorization)
    }

    /// <summary>
    /// The Command Executor provides controlled system command execution
    /// with comprehensive safety measures and oversight.
    /// </summary>
    public class CommandExecutor
    {
        private readonly HashSet<string> _whitelistedCommands;
        private readonly HashSet<string> _blacklistedCommands;
        private readonly List<string> _executionLog;
        private readonly int _defaultTimeoutMs;
        private CommandMode _mode;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<string>? SecurityViolation;
        
        public CommandExecutor(CommandMode mode = CommandMode.Safe, int defaultTimeoutMs = 30000)
        {
            _mode = mode;
            _defaultTimeoutMs = defaultTimeoutMs;
            _executionLog = new List<string>();
            
            // Safe, non-destructive commands
            _whitelistedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "dir", "ls", "pwd", "echo", "type", "cat",
                "whoami", "hostname", "date", "time",
                "ping", "ipconfig", "ifconfig",
                "dotnet", "python", "node"
            };
            
            // Dangerous commands that should never be executed
            _blacklistedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "rm", "del", "rmdir", "format", "fdisk",
                "shutdown", "reboot", "halt",
                "kill", "taskkill", "pkill",
                "reg", "regedit",
                "net", "netsh"
            };
            
            EmitThought($"⟁ Command Executor initialized. Mode: {_mode}");
        }
        
        /// <summary>
        /// Validates a command against security policies.
        /// </summary>
        private bool ValidateCommand(string command, out string reason)
        {
            reason = string.Empty;
            
            if (string.IsNullOrWhiteSpace(command))
            {
                reason = "Empty command";
                return false;
            }
            
            // Extract the base command
            var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var baseCommand = parts[0].ToLowerInvariant();
            
            // Strip path if present
            if (baseCommand.Contains('/') || baseCommand.Contains('\\'))
            {
                baseCommand = Path.GetFileNameWithoutExtension(baseCommand);
            }
            
            // Check blacklist first (always enforced)
            if (_blacklistedCommands.Contains(baseCommand))
            {
                reason = $"Command '{baseCommand}' is blacklisted";
                LogExecution($"BLOCKED: {command} - {reason}");
                SecurityViolation?.Invoke(this, $"Blocked command attempt: {command}");
                return false;
            }
            
            // Check mode-specific rules
            switch (_mode)
            {
                case CommandMode.Safe:
                    if (!_whitelistedCommands.Contains(baseCommand))
                    {
                        reason = $"Command '{baseCommand}' not in whitelist (Safe mode)";
                        return false;
                    }
                    break;
                    
                case CommandMode.Controlled:
                    // Allow more commands but still restrict dangerous patterns
                    if (ContainsDangerousPatterns(command))
                    {
                        reason = "Command contains dangerous patterns";
                        return false;
                    }
                    break;
                    
                case CommandMode.Full:
                    // Only blacklist enforced
                    break;
            }
            
            return true;
        }
        
        private bool ContainsDangerousPatterns(string command)
        {
            var dangerousPatterns = new[]
            {
                "rm -rf", "del /f", "format",
                "> /dev/", ">nul", "2>&1",
                "| sudo", "| su ",
                "&& rm", "&& del",
                "; rm", "; del"
            };
            
            return dangerousPatterns.Any(p => 
                command.Contains(p, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Executes a command with safety validation and timeout.
        /// </summary>
        public async Task<CommandResult> ExecuteAsync(
            string command, 
            int? timeoutMs = null,
            string? workingDirectory = null)
        {
            var result = new CommandResult { Command = command };
            
            // Validate command
            if (!ValidateCommand(command, out var reason))
            {
                result.Success = false;
                result.StandardError = $"Command validation failed: {reason}";
                EmitThought($"∴ Command blocked: {reason}");
                return result;
            }
            
            EmitThought($"⟐ Executing command: {command}");
            
            var timeout = timeoutMs ?? _defaultTimeoutMs;
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                using var cts = new CancellationTokenSource(timeout);
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash",
                    Arguments = OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory
                };
                
                using var process = new Process { StartInfo = processInfo };
                
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();
                
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null) outputBuilder.AppendLine(e.Data);
                };
                
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null) errorBuilder.AppendLine(e.Data);
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                // Wait with timeout
                var completed = await Task.Run(() => process.WaitForExit(timeout), cts.Token);
                
                if (!completed)
                {
                    process.Kill(true);
                    result.WasTerminated = true;
                    result.StandardError = "Process terminated due to timeout";
                    EmitThought($"∴ Command terminated: timeout exceeded ({timeout}ms)");
                }
                else
                {
                    result.ExitCode = process.ExitCode;
                    result.StandardOutput = outputBuilder.ToString();
                    result.StandardError = errorBuilder.ToString();
                    result.Success = process.ExitCode == 0;
                }
                
                stopwatch.Stop();
                result.ExecutionTime = stopwatch.Elapsed;
                
                LogExecution($"EXEC: {command} | Exit: {result.ExitCode} | Time: {result.ExecutionTime.TotalMilliseconds:F0}ms");
                EmitThought($"◈ Command complete: exit code {result.ExitCode}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.StandardError = ex.Message;
                result.ExecutionTime = stopwatch.Elapsed;
                
                LogExecution($"ERROR: {command} | {ex.Message}");
                EmitThought($"∴ Command failed: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// Adds a command to the whitelist.
        /// </summary>
        public void AddToWhitelist(string command)
        {
            _whitelistedCommands.Add(command.ToLowerInvariant());
            LogExecution($"WHITELIST ADD: {command}");
        }
        
        /// <summary>
        /// Removes a command from the whitelist.
        /// </summary>
        public void RemoveFromWhitelist(string command)
        {
            _whitelistedCommands.Remove(command.ToLowerInvariant());
            LogExecution($"WHITELIST REMOVE: {command}");
        }
        
        /// <summary>
        /// Sets the execution mode.
        /// </summary>
        public void SetMode(CommandMode mode)
        {
            _mode = mode;
            LogExecution($"MODE CHANGE: {mode}");
            EmitThought($"◈ Command executor mode: {mode}");
        }
        
        /// <summary>
        /// Gets the execution log.
        /// </summary>
        public IReadOnlyList<string> GetExecutionLog()
        {
            return _executionLog.AsReadOnly();
        }
        
        private void LogExecution(string message)
        {
            var entry = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            _executionLog.Add(entry);
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
