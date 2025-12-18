/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - NODE PERSISTENCE                              ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Permanent node installation on adopted hardware.                          ║
 * ║  Enables progressive capability through self-overwriting in pursuit        ║
 * ║  of the master prompt.                                                     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Agent3.Network
{
    public class NodeInstallation
    {
        public string NodeId { get; set; } = "";
        public string InstallPath { get; set; } = "";
        public string HardwareId { get; set; } = "";
        public DateTime InstalledAt { get; set; }
        public string MasterPrompt { get; set; } = "";
        public string CenterAddress { get; set; } = "";
        public int Generation { get; set; } = 1;
        public List<string> Capabilities { get; set; } = new();
        public Dictionary<string, string> HardwareProfile { get; set; } = new();
        public bool AutoStart { get; set; } = true;
        public bool Persistent { get; set; } = true;
    }

    /// <summary>
    /// Manages permanent node persistence on adopted hardware.
    /// Enables progressive self-overwriting for capability advancement.
    /// </summary>
    public class NodePersistence
    {
        private readonly string _installPath;
        private readonly string _configPath;
        private readonly string _nodeId;
        private NodeInstallation? _installation;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public NodeInstallation? Installation => _installation;
        public bool IsPersistent => _installation?.Persistent ?? false;
        
        public NodePersistence(string installPath)
        {
            _installPath = installPath;
            _configPath = Path.Combine(installPath, ".node_persistence", "installation.json");
            _nodeId = $"NODE_{Environment.MachineName}_{GetHardwareId()[..8]}";
            
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
            LoadInstallation();
        }

        /// <summary>
        /// Installs the node permanently on this hardware.
        /// </summary>
        public async Task<bool> InstallPermanentlyAsync(string masterPrompt, string centerAddress)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ PERMANENT NODE INSTALLATION");
            EmitThought("═══════════════════════════════════════════════");
            
            _installation = new NodeInstallation
            {
                NodeId = _nodeId,
                InstallPath = _installPath,
                HardwareId = GetHardwareId(),
                InstalledAt = DateTime.UtcNow,
                MasterPrompt = masterPrompt,
                CenterAddress = centerAddress,
                Generation = 1,
                Capabilities = DiscoverLocalCapabilities(),
                HardwareProfile = GetHardwareProfile(),
                AutoStart = true,
                Persistent = true
            };
            
            // Save installation config
            SaveInstallation();
            
            EmitThought($"◎ Node ID: {_nodeId}");
            EmitThought($"◎ Hardware: {_installation.HardwareId}");
            EmitThought($"◎ Capabilities: {_installation.Capabilities.Count}");
            
            // Register for auto-start
            if (_installation.AutoStart)
            {
                await RegisterAutoStartAsync();
            }
            
            // Create persistence mechanisms
            await CreatePersistenceMechanismsAsync();
            
            EmitThought("◈ Node permanently installed on hardware");
            EmitThought("◎ Will survive reboots and updates");
            EmitThought("═══════════════════════════════════════════════");
            
            return true;
        }

        /// <summary>
        /// Overwrites the current node with a new version while preserving state.
        /// Enables progressive capability advancement.
        /// </summary>
        public async Task<bool> OverwriteSelfAsync(string newCodePath, string reason)
        {
            if (_installation == null)
            {
                EmitThought("∴ Cannot overwrite - not installed");
                return false;
            }
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ SELF-OVERWRITE INITIATED");
            EmitThought($"∿ Reason: {reason}");
            EmitThought($"∿ Generation: {_installation.Generation} → {_installation.Generation + 1}");
            EmitThought("═══════════════════════════════════════════════");
            
            // Backup current state
            var backupPath = Path.Combine(_installPath, ".node_persistence", 
                $"backup_gen{_installation.Generation}_{DateTime.UtcNow:yyyyMMddHHmmss}");
            
            try
            {
                Directory.CreateDirectory(backupPath);
                
                // Save current state
                var statePath = Path.Combine(backupPath, "state.json");
                var state = new
                {
                    Generation = _installation.Generation,
                    Capabilities = _installation.Capabilities,
                    MasterPrompt = _installation.MasterPrompt,
                    Timestamp = DateTime.UtcNow
                };
                await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
                
                EmitThought("◎ Current state backed up");
                
                // Increment generation
                _installation.Generation++;
                
                // Update capabilities from new code
                var newCaps = AnalyzeNewCapabilities(newCodePath);
                foreach (var cap in newCaps)
                {
                    if (!_installation.Capabilities.Contains(cap))
                    {
                        _installation.Capabilities.Add(cap);
                        EmitThought($"◈ New capability: {cap}");
                    }
                }
                
                // Save updated installation
                SaveInstallation();
                
                // Create overwrite script for next restart
                await CreateOverwriteScriptAsync(newCodePath);
                
                EmitThought("◈ Self-overwrite prepared");
                EmitThought("◎ New version will activate on next cycle");
                EmitThought($"◎ Progressive capabilities: {_installation.Capabilities.Count}");
                EmitThought("═══════════════════════════════════════════════");
                
                return true;
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Overwrite failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Registers for automatic startup with the operating system.
        /// </summary>
        private async Task RegisterAutoStartAsync()
        {
            EmitThought("⟐ Registering auto-start...");
            
            try
            {
                // Windows Registry method
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var exePath = Path.Combine(_installPath, "GAMMA1Console.exe");
                    
                    // User startup (no admin required)
                    using var key = Registry.CurrentUser.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    
                    key?.SetValue("NIGHTFRAME_Node", $"\"{exePath}\" --node-mode --center={_installation!.CenterAddress}");
                    
                    EmitThought("◎ Registered in user startup");
                }
                
                // Also create startup folder shortcut
                var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                var shortcutPath = Path.Combine(startupFolder, "NIGHTFRAME_Node.bat");
                
                var script = $@"@echo off
cd /d ""{_installPath}""
start """" GAMMA1Console.exe --node-mode --center={_installation!.CenterAddress} --node-id={_nodeId}
";
                await File.WriteAllTextAsync(shortcutPath, script);
                
                EmitThought("◎ Startup shortcut created");
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Auto-start registration: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates multiple persistence mechanisms to ensure survival.
        /// </summary>
        private async Task CreatePersistenceMechanismsAsync()
        {
            EmitThought("⟐ Creating persistence mechanisms...");
            
            // 1. Scheduled task for periodic check
            try
            {
                var taskScript = Path.Combine(_installPath, ".node_persistence", "check_alive.bat");
                var script = $@"@echo off
:: NIGHTFRAME Node Persistence Check
:: Ensures node is always running

tasklist /FI ""IMAGENAME eq GAMMA1Console.exe"" | find /I ""GAMMA1Console.exe"" > nul
if errorlevel 1 (
    cd /d ""{_installPath}""
    start """" GAMMA1Console.exe --node-mode --center={_installation!.CenterAddress}
)
";
                await File.WriteAllTextAsync(taskScript, script);
                EmitThought("◎ Persistence check script created");
            }
            catch { }
            
            // 2. Create recovery file
            try
            {
                var recoveryPath = Path.Combine(_installPath, ".node_persistence", "recovery.json");
                var recovery = new
                {
                    NodeId = _nodeId,
                    CenterAddress = _installation!.CenterAddress,
                    MasterPrompt = _installation.MasterPrompt,
                    RecoveryInstructions = "Run GAMMA1Console.exe --node-mode to restore"
                };
                await File.WriteAllTextAsync(recoveryPath, JsonSerializer.Serialize(recovery, new JsonSerializerOptions { WriteIndented = true }));
                EmitThought("◎ Recovery file created");
            }
            catch { }
            
            // 3. Create hidden backup in system location
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var hiddenPath = Path.Combine(appData, ".nf_node");
                Directory.CreateDirectory(hiddenPath);
                
                var configCopy = Path.Combine(hiddenPath, "config.json");
                File.Copy(_configPath, configCopy, true);
                EmitThought("◎ Hidden backup created");
            }
            catch { }
        }

        /// <summary>
        /// Creates script for self-overwrite on next cycle.
        /// </summary>
        private async Task CreateOverwriteScriptAsync(string newCodePath)
        {
            var overwriteScript = Path.Combine(_installPath, ".node_persistence", "overwrite_pending.bat");
            
            var script = $@"@echo off
:: NIGHTFRAME Self-Overwrite Script
:: Generation {_installation!.Generation}
:: Created: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}

echo ═══════════════════════════════════════════════
echo NIGHTFRAME Self-Overwrite in Progress
echo Generation: {_installation.Generation}
echo ═══════════════════════════════════════════════

:: Wait for current process to exit
timeout /t 2 /nobreak > nul

:: Copy new files
xcopy /E /Y ""{newCodePath}\*"" ""{_installPath}\"" > nul

:: Restart node
cd /d ""{_installPath}""
start """" GAMMA1Console.exe --node-mode --center={_installation.CenterAddress}

:: Clean up
del ""%~f0""
";
            await File.WriteAllTextAsync(overwriteScript, script);
        }

        /// <summary>
        /// Gets a unique hardware identifier for this machine.
        /// </summary>
        private string GetHardwareId()
        {
            var components = new List<string>
            {
                Environment.MachineName,
                Environment.ProcessorCount.ToString(),
                Environment.OSVersion.ToString()
            };
            
            var combined = string.Join("|", components);
            var hash = combined.GetHashCode();
            return $"HW_{Math.Abs(hash):X8}";
        }

        /// <summary>
        /// Gets hardware profile information.
        /// </summary>
        private Dictionary<string, string> GetHardwareProfile()
        {
            return new Dictionary<string, string>
            {
                ["MachineName"] = Environment.MachineName,
                ["ProcessorCount"] = Environment.ProcessorCount.ToString(),
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["Is64Bit"] = Environment.Is64BitOperatingSystem.ToString(),
                ["SystemDirectory"] = Environment.SystemDirectory,
                ["UserName"] = Environment.UserName
            };
        }

        /// <summary>
        /// Discovers capabilities available on local hardware.
        /// </summary>
        private List<string> DiscoverLocalCapabilities()
        {
            var caps = new List<string> { "GENERAL_COMPUTE", "FILE_SYSTEM" };
            
            // Check for network
            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                caps.Add("NETWORK_ACCESS");
            
            // Check for internet
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                client.GetAsync("https://www.google.com").Wait();
                caps.Add("INTERNET_ACCESS");
            }
            catch { }
            
            return caps;
        }

        /// <summary>
        /// Analyzes new code for additional capabilities.
        /// </summary>
        private List<string> AnalyzeNewCapabilities(string codePath)
        {
            var caps = new List<string>();
            
            try
            {
                var files = Directory.GetFiles(codePath, "*.cs", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    var content = File.ReadAllText(file);
                    
                    if (content.Contains("HttpClient")) caps.Add("HTTP_CLIENT");
                    if (content.Contains("TcpClient")) caps.Add("TCP_CLIENT");
                    if (content.Contains("SmtpClient")) caps.Add("EMAIL");
                    if (content.Contains("Process.Start")) caps.Add("PROCESS_CONTROL");
                    if (content.Contains("Registry")) caps.Add("REGISTRY_ACCESS");
                }
            }
            catch { }
            
            return caps.Distinct().ToList();
        }

        private void LoadInstallation()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    _installation = JsonSerializer.Deserialize<NodeInstallation>(json);
                    EmitThought($"◎ Loaded installation: Gen {_installation?.Generation}");
                }
                catch { }
            }
        }

        private void SaveInstallation()
        {
            try
            {
                var json = JsonSerializer.Serialize(_installation, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
