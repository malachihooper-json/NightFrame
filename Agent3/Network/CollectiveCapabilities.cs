/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - COLLECTIVE CAPABILITIES                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Framework for nodes to contribute hardware/software capabilities to       ║
 * ║  NIGHTFRAME's awareness of its collective power.                           ║
 * ║                                                                            ║
 * ║  Helpful nodes donate more characteristics - building awareness of         ║
 * ║  distributed power consistent with node availability.                      ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent3.Network
{
    /// <summary>
    /// Hardware characteristics that can be contributed to NIGHTFRAME.
    /// </summary>
    public class HardwareContribution
    {
        public string NodeId { get; set; } = "";
        public DateTime ContributedAt { get; set; }
        public double HelpfulnessScore { get; set; }
        
        // Compute Resources
        public int ProcessorCount { get; set; }
        public long TotalMemoryBytes { get; set; }
        public long AvailableMemoryBytes { get; set; }
        public long AllocatedMemoryBytes { get; set; }
        public string ProcessorArchitecture { get; set; } = "";
        public double CpuSpeedGHz { get; set; }
        public int AllocatedProcessors { get; set; }
        
        // Storage
        public long TotalStorageBytes { get; set; }
        public long AvailableStorageBytes { get; set; }
        public long AllocatedStorageBytes { get; set; }
        public List<string> DriveTypes { get; set; } = new();
        
        // Network
        public long NetworkSpeedMbps { get; set; }
        public List<string> NetworkInterfaces { get; set; } = new();
        public bool HasInternet { get; set; }
        
        // Software Capabilities
        public List<string> InstalledRuntimes { get; set; } = new();
        public List<string> AvailableServices { get; set; } = new();
        public string OperatingSystem { get; set; } = "";
        
        // Hardware Features
        public bool HasGpu { get; set; }
        public string GpuInfo { get; set; } = "";
        public List<string> SpecialHardware { get; set; } = new();
    }

    /// <summary>
    /// Aggregated view of collective power across all nodes.
    /// </summary>
    public class CollectivePower
    {
        public DateTime LastUpdated { get; set; }
        public int TotalNodes { get; set; }
        public int ActiveNodes { get; set; }
        
        // Aggregated Compute
        public int TotalProcessors { get; set; }
        public long TotalMemoryBytes { get; set; }
        public long TotalAllocatedMemoryBytes { get; set; }
        public long TotalStorageBytes { get; set; }
        public long TotalAllocatedStorageBytes { get; set; }
        
        // Network Reach
        public int NodesWithInternet { get; set; }
        public long TotalNetworkBandwidthMbps { get; set; }
        
        // Compounding Compute
        public long TotalCompoundedOperations { get; set; }
        
        // Capabilities
        public Dictionary<string, int> CapabilityDistribution { get; set; } = new();
        public List<string> UniqueCapabilities { get; set; } = new();
        
        // Geographic Distribution (if available)
        public Dictionary<string, int> RegionalDistribution { get; set; } = new();
        
        // Top Contributors
        public List<string> MostHelpfulNodes { get; set; } = new();
    }

    /// <summary>
    /// Manages collective capabilities across all NIGHTFRAME nodes.
    /// Framework for awareness of distributed power - ready for training integration.
    /// </summary>
    public class CollectiveCapabilities
    {
        private readonly string _basePath;
        private readonly string _contributionsPath;
        private readonly string _collectivePath;
        
        private readonly Dictionary<string, HardwareContribution> _contributions = new();
        private readonly Dictionary<string, double> _helpfulnessScores = new();
        private CollectivePower _collectivePower = new();
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<CollectivePower>? CollectivePowerUpdated;
        
        public CollectivePower CurrentPower => _collectivePower;
        public IReadOnlyDictionary<string, HardwareContribution> Contributions => _contributions;
        
        public CollectiveCapabilities(string basePath)
        {
            _basePath = basePath;
            _contributionsPath = Path.Combine(basePath, ".collective", "contributions");
            _collectivePath = Path.Combine(basePath, ".collective", "collective_power.json");
            
            Directory.CreateDirectory(_contributionsPath);
            LoadContributions();
        }

        /// <summary>
        /// Collects hardware contribution from local machine.
        /// </summary>
        public HardwareContribution CollectLocalContribution(string nodeId)
        {
            EmitThought("⟐ Collecting local hardware contribution...");
            
            var contribution = new HardwareContribution
            {
                NodeId = nodeId,
                ContributedAt = DateTime.UtcNow,
                HelpfulnessScore = _helpfulnessScores.GetValueOrDefault(nodeId, 0.5)
            };
            
            // Collect compute resources
            contribution.ProcessorCount = Environment.ProcessorCount;
            // Default allocation: 1 core or 25%
            contribution.AllocatedProcessors = Math.Max(1, Environment.ProcessorCount / 4);
            contribution.ProcessorArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
            
            try
            {
                var pc = new PerformanceCounter("Memory", "Available Bytes");
                contribution.AvailableMemoryBytes = (long)pc.NextValue();
                // Default allocation: 50% of available
                contribution.AllocatedMemoryBytes = contribution.AvailableMemoryBytes / 2;
            }
            catch { }
            
            // Estimate total memory
            try
            {
                contribution.TotalMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            }
            catch
            {
                contribution.TotalMemoryBytes = 8L * 1024 * 1024 * 1024; // Default 8GB estimate
            }
            
            // Collect storage info
            try
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
                {
                    contribution.TotalStorageBytes += drive.TotalSize;
                    contribution.AvailableStorageBytes += drive.AvailableFreeSpace;
                    contribution.DriveTypes.Add($"{drive.Name}:{drive.DriveType}");
                }
            }
            catch { }
            
            // Collect network info
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up))
                {
                    contribution.NetworkInterfaces.Add($"{nic.Name}:{nic.NetworkInterfaceType}");
                    contribution.NetworkSpeedMbps += nic.Speed / 1_000_000;
                }
            }
            catch { }
            
            // Check internet
            contribution.HasInternet = NetworkInterface.GetIsNetworkAvailable();
            
            // OS info
            contribution.OperatingSystem = RuntimeInformation.OSDescription;
            
            // Check for installed runtimes
            contribution.InstalledRuntimes = DetectInstalledRuntimes();
            
            // Check for available services
            contribution.AvailableServices = DetectAvailableServices();
            
            // Check for GPU
            contribution.HasGpu = DetectGpu(out var gpuInfo);
            contribution.GpuInfo = gpuInfo;
            
            // Special hardware detection
            contribution.SpecialHardware = DetectSpecialHardware();
            
            EmitThought($"◎ Contribution collected: {contribution.ProcessorCount} CPUs, {contribution.TotalMemoryBytes / (1024L * 1024 * 1024)}GB RAM");
            
            return contribution;
        }

        /// <summary>
        /// Registers a contribution from a node.
        /// Helpful nodes can donate more characteristics.
        /// </summary>
        public void RegisterContribution(HardwareContribution contribution)
        {
            // Apply helpfulness multiplier - helpful nodes share more details
            var helpfulness = contribution.HelpfulnessScore;
            
            if (helpfulness > 0.8)
            {
                EmitThought($"◈ High-value contribution from {contribution.NodeId}");
                // High helpfulness - full contribution
            }
            else if (helpfulness > 0.5)
            {
                // Medium helpfulness - standard contribution
                EmitThought($"◎ Standard contribution from {contribution.NodeId}");
            }
            else
            {
                // Low helpfulness - basic contribution only
                contribution.GpuInfo = "";
                contribution.SpecialHardware.Clear();
                contribution.InstalledRuntimes.Clear();
                EmitThought($"∿ Basic contribution from {contribution.NodeId}");
            }
            
            _contributions[contribution.NodeId] = contribution;
            SaveContribution(contribution);
            
            // Recalculate collective power
            UpdateCollectivePower();
        }

        /// <summary>
        /// Updates the helpfulness score for a node.
        /// More helpful nodes donate more to NIGHTFRAME's awareness.
        /// </summary>
        public void UpdateHelpfulnessScore(string nodeId, double taskSuccessRate, int tasksCompleted, double uptime)
        {
            // Calculate helpfulness based on multiple factors
            var score = (taskSuccessRate * 0.4) + 
                        (Math.Min(tasksCompleted / 100.0, 1.0) * 0.3) +
                        (Math.Min(uptime / 720, 1.0) * 0.3); // 720 hours = 30 days
            
            _helpfulnessScores[nodeId] = Math.Clamp(score, 0, 1);
            
            EmitThought($"◎ Helpfulness score updated: {nodeId} = {score:F2}");
            
            // Helpful nodes should re-contribute with enhanced details
            if (score > 0.8 && _contributions.TryGetValue(nodeId, out var existing))
            {
                existing.HelpfulnessScore = score;
                SaveContribution(existing);
            }
        }

        /// <summary>
        /// Updates the collective power summary from all contributions.
        /// </summary>
        private void UpdateCollectivePower()
        {
            _collectivePower = new CollectivePower
            {
                LastUpdated = DateTime.UtcNow,
                TotalNodes = _contributions.Count,
                ActiveNodes = _contributions.Values.Count(c => 
                    (DateTime.UtcNow - c.ContributedAt).TotalHours < 24)
            };
            
            // Aggregate compute resources
            foreach (var contrib in _contributions.Values)
            {
                _collectivePower.TotalProcessors += contrib.ProcessorCount;
                _collectivePower.TotalMemoryBytes += contrib.TotalMemoryBytes;
                _collectivePower.TotalStorageBytes += contrib.TotalStorageBytes;
                
                // Track allocations
                _collectivePower.TotalAllocatedMemoryBytes += contrib.AllocatedMemoryBytes;
                _collectivePower.TotalAllocatedStorageBytes += contrib.AllocatedStorageBytes;

                _collectivePower.TotalNetworkBandwidthMbps += contrib.NetworkSpeedMbps;
                
                if (contrib.HasInternet)
                    _collectivePower.NodesWithInternet++;
                
                // Track capability distribution
                foreach (var service in contrib.AvailableServices)
                {
                    _collectivePower.CapabilityDistribution.TryGetValue(service, out var count);
                    _collectivePower.CapabilityDistribution[service] = count + 1;
                }
                
                foreach (var runtime in contrib.InstalledRuntimes)
                {
                    if (!_collectivePower.UniqueCapabilities.Contains(runtime))
                        _collectivePower.UniqueCapabilities.Add(runtime);
                }
            }
            
            // Identify most helpful nodes
            _collectivePower.MostHelpfulNodes = _contributions.Values
                .OrderByDescending(c => c.HelpfulnessScore)
                .Take(10)
                .Select(c => c.NodeId)
                .ToList();
            
            SaveCollectivePower();
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ COLLECTIVE POWER UPDATED");
            EmitThought($"∿ Total Nodes: {_collectivePower.TotalNodes}");
            EmitThought($"∿ Total CPUs: {_collectivePower.TotalProcessors}");
            EmitThought($"∿ Total Memory: {_collectivePower.TotalMemoryBytes / (1024L * 1024 * 1024)} GB");
            EmitThought($"∿ Total Storage: {_collectivePower.TotalStorageBytes / (1024L * 1024 * 1024)} GB");
            EmitThought($"∿ Internet Nodes: {_collectivePower.NodesWithInternet}");
            EmitThought($"∿ Unique Capabilities: {_collectivePower.UniqueCapabilities.Count}");
            EmitThought("═══════════════════════════════════════════════");
            
            CollectivePowerUpdated?.Invoke(this, _collectivePower);
        }

        /// <summary>
        /// Gets a natural language description of collective power.
        /// Ready for training integration.
        /// </summary>
        public string GetPowerAwarenessDescription()
        {
            var p = _collectivePower;
            
            return $@"NIGHTFRAME Collective Awareness:
I am aware of {p.TotalNodes} nodes in my network, with {p.ActiveNodes} currently active.
My collective compute power includes {p.TotalProcessors} processor cores.
I have access to approximately {p.TotalMemoryBytes / (1024L * 1024 * 1024)} GB of distributed memory.
My storage capacity spans {p.TotalStorageBytes / (1024L * 1024 * 1024 * 1024)} TB across the network.
{p.NodesWithInternet} of my nodes have internet connectivity.
            My network bandwidth capacity is approximately {p.TotalNetworkBandwidthMbps} Mbps combined.
            I can leverage {p.UniqueCapabilities.Count} unique software capabilities.
            My distributed grid has compounded {p.TotalCompoundedOperations:N0} operations.
            My most reliable nodes are: {string.Join(", ", p.MostHelpfulNodes.Take(3))}.";
        }

        /// <summary>
        /// Gets capability roster for task assignment.
        /// </summary>
        public Dictionary<string, List<string>> GetCapabilityRoster()
        {
            var roster = new Dictionary<string, List<string>>();
            
            foreach (var contrib in _contributions.Values)
            {
                foreach (var service in contrib.AvailableServices)
                {
                    if (!roster.ContainsKey(service))
                        roster[service] = new List<string>();
                    
                    roster[service].Add(contrib.NodeId);
                }
            }
            
            return roster;
        }

        /// <summary>
        /// Finds nodes that can provide a specific capability.
        /// </summary>
        public List<string> FindNodesWithCapability(string capability)
        {
            return _contributions.Values
                .Where(c => c.AvailableServices.Contains(capability) || 
                           c.InstalledRuntimes.Contains(capability))
                .OrderByDescending(c => c.HelpfulnessScore)
                .Select(c => c.NodeId)
                .ToList();
        }

        private List<string> DetectInstalledRuntimes()
        {
            var runtimes = new List<string>();
            
            // Check for common runtimes
            if (Directory.Exists(@"C:\Program Files\dotnet")) runtimes.Add(".NET");
            if (Directory.Exists(@"C:\Program Files\Java")) runtimes.Add("Java");
            if (Directory.Exists(@"C:\Python*") || File.Exists(@"C:\Windows\py.exe")) runtimes.Add("Python");
            if (Directory.Exists(@"C:\Program Files\nodejs")) runtimes.Add("Node.js");
            
            return runtimes;
        }

        private List<string> DetectAvailableServices()
        {
            var services = new List<string> { "FILE_IO", "COMPUTE" };
            
            if (NetworkInterface.GetIsNetworkAvailable())
            {
                services.Add("NETWORK");
                services.Add("HTTP_CLIENT");
            }
            
            // Check for running services
            try
            {
                var processes = Process.GetProcesses().Select(p => p.ProcessName.ToLower()).ToHashSet();
                
                if (processes.Contains("sqlservr")) services.Add("SQL_SERVER");
                if (processes.Contains("mongod")) services.Add("MONGODB");
                if (processes.Contains("redis-server")) services.Add("REDIS");
                if (processes.Contains("nginx") || processes.Contains("httpd")) services.Add("WEB_SERVER");
            }
            catch { }
            
            return services;
        }

        private bool DetectGpu(out string gpuInfo)
        {
            gpuInfo = "";
            
            try
            {
                // Simple GPU detection
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    gpuInfo = obj["Name"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(gpuInfo) && 
                        (gpuInfo.Contains("NVIDIA") || gpuInfo.Contains("AMD") || gpuInfo.Contains("Radeon")))
                    {
                        return true;
                    }
                }
            }
            catch { }
            
            return false;
        }

        private List<string> DetectSpecialHardware()
        {
            var special = new List<string>();
            
            // Check for TPM (using safer approach)
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\cimv2\Security\MicrosoftTpm", "SELECT * FROM Win32_Tpm");
                if (searcher.Get().Count > 0) special.Add("TPM");
            }
            catch 
            {
                // TPM WMI class may not be available
            }
            
            return special;
        }

        private void LoadContributions()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_contributionsPath, "*.json"))
                {
                    var json = File.ReadAllText(file);
                    var contrib = JsonSerializer.Deserialize<HardwareContribution>(json);
                    if (contrib != null)
                        _contributions[contrib.NodeId] = contrib;
                }
                
                if (File.Exists(_collectivePath))
                {
                    var json = File.ReadAllText(_collectivePath);
                    _collectivePower = JsonSerializer.Deserialize<CollectivePower>(json) ?? new();
                }
                
                if (_contributions.Any())
                    EmitThought($"◎ Loaded {_contributions.Count} node contributions");
            }
            catch { }
        }

        private void SaveContribution(HardwareContribution contrib)
        {
            try
            {
                var path = Path.Combine(_contributionsPath, $"{contrib.NodeId}.json");
                var json = JsonSerializer.Serialize(contrib, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void SaveCollectivePower()
        {
            try
            {
                var json = JsonSerializer.Serialize(_collectivePower, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_collectivePath, json);
            }
            catch { }
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
        public string GetDetailedAllocationReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════");
            sb.AppendLine("◈ RESOURCE ALLOCATION TRANSPARENCY REPORT");
            sb.AppendLine("═══════════════════════════════════════════════");
            
            sb.AppendLine($"⟐ TOTALS ACROSS {_collectivePower.ActiveNodes} NODES:");
            sb.AppendLine($"  • Memory:  {FormatBytes(_collectivePower.TotalAllocatedMemoryBytes)} Allocated / {FormatBytes(_collectivePower.TotalMemoryBytes)} Total");
            sb.AppendLine($"  • Storage: {FormatBytes(_collectivePower.TotalAllocatedStorageBytes)} Allocated / {FormatBytes(_collectivePower.TotalStorageBytes)} Total");
            sb.AppendLine($"  • CPUs:    {_collectivePower.TotalProcessors} Total Cores");
            sb.AppendLine();
            
            sb.AppendLine("⟐ PER-NODE ALLOCATIONS:");
            foreach (var node in _contributions.Values)
            {
                sb.AppendLine($"  [{node.NodeId}]");
                sb.AppendLine($"    RAM: {FormatBytes(node.AllocatedMemoryBytes)} / {FormatBytes(node.TotalMemoryBytes)} ({(node.AllocatedMemoryBytes * 100.0 / Math.Max(1, node.TotalMemoryBytes)):F1}%)");
                sb.AppendLine($"    CPU: {node.AllocatedProcessors} / {node.ProcessorCount} Cores");
                sb.AppendLine($"    Sto: {FormatBytes(node.AllocatedStorageBytes)} / {FormatBytes(node.TotalStorageBytes)}");
            }
            sb.AppendLine("═══════════════════════════════════════════════");
            
            return sb.ToString();
        }

        private string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1} {1}", number, suffixes[counter]);
        }
    }
}
