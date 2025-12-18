/*
 * AGENT 3 - NODE REPLICATION MANAGER
 * Handles safe replication, deployment, and AUTO-INITIALIZATION of nodes
 * All replicated nodes automatically initialize and connect back to center
 */
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace Agent3.Network
{
    /// <summary>
    /// Manages node replication, deployment, and AUTO-INITIALIZATION.
    /// When a node is "dropped off", it automatically initializes and connects back to center.
    /// </summary>
    public class NodeReplicationManager
    {
        private readonly string _installationPath;
        private readonly string _packagePath;
        private readonly NetworkCore _network;
        private readonly NodeRegistry _registry;
        private readonly string _thisNodeId;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<RegisteredNode>? NodeDeployed;
        public event EventHandler<RegisteredNode>? NodeInitialized;
        
        public NodeReplicationManager(string installationPath, NetworkCore network, NodeRegistry registry)
        {
            _installationPath = installationPath;
            _packagePath = Path.Combine(installationPath, ".node_packages");
            _network = network;
            _registry = registry;
            _thisNodeId = $"NODE_{Environment.MachineName}";
            
            Directory.CreateDirectory(_packagePath);
            
            // Subscribe to registry events
            _registry.NodeStatusChanged += OnNodeStatusChanged;
        }

        /// <summary>
        /// Creates and deploys a node to a target location with AUTO-INITIALIZATION.
        /// The node will start immediately after deployment and connect back to center.
        /// </summary>
        public async Task<RegisteredNode?> DeployNodeAsync(
            string targetPath,
            string targetHostname = "",
            string centerAddress = "")
        {
            if (string.IsNullOrEmpty(centerAddress))
                centerAddress = GetCenterAddress();
            
            if (string.IsNullOrEmpty(targetHostname))
                targetHostname = Environment.MachineName;
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ DEPLOYING NEW NODE");
            EmitThought($"∿ Target: {targetPath}");
            EmitThought($"∿ Center: {centerAddress}");
            EmitThought("═══════════════════════════════════════════════");
            
            // Register the node first
            var node = _registry.RegisterNode(
                hostname: targetHostname,
                ipAddress: "pending",
                port: 7777,
                deploymentPath: targetPath,
                parentNodeId: _thisNodeId,
                isCenter: false
            );
            
            try
            {
                // Update status to deploying
                _registry.UpdateNodeStatus(node.NodeId, NodeDeploymentStatus.Deploying);
                
                // Create the deployment package
                var packagePath = await CreatePackageAsync(node.NodeId, centerAddress);
                
                // Deploy to target
                Directory.CreateDirectory(targetPath);
                ZipFile.ExtractToDirectory(packagePath, targetPath, true);
                
                // Create auto-start script
                await CreateAutoInitScriptAsync(targetPath, node.NodeId, centerAddress);
                
                // Update status to deployed
                _registry.UpdateNodeStatus(node.NodeId, NodeDeploymentStatus.Deployed);
                
                EmitThought($"◈ Node {node.NodeId} deployed to {targetPath}");
                
                // AUTO-INITIALIZE: Start the node immediately
                await InitializeRemoteNodeAsync(targetPath, node);
                
                NodeDeployed?.Invoke(this, node);
                return node;
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Deployment failed: {ex.Message}");
                _registry.UpdateNodeStatus(node.NodeId, NodeDeploymentStatus.Error);
                return null;
            }
        }

        /// <summary>
        /// Creates a deployable package with configuration for center connection.
        /// </summary>
        private async Task<string> CreatePackageAsync(string nodeId, string centerAddress)
        {
            var packageName = $"NIGHTFRAME_{nodeId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
            var packageFullPath = Path.Combine(_packagePath, packageName);
            var tempDir = Path.Combine(_packagePath, $"temp_{nodeId}");
            
            try
            {
                Directory.CreateDirectory(tempDir);
                
                // Create node configuration
                var config = new
                {
                    NodeId = nodeId,
                    CentralServerAddress = centerAddress,
                    CentralServerPort = 7777,
                    HardCodedGoal = "CONNECT_TO_CENTER_AND_INITIALIZE",
                    AutoInitialize = true,
                    CreatedAt = DateTime.UtcNow,
                    ParentNodeId = _thisNodeId,
                    Version = "1.0.0"
                };
                
                var configPath = Path.Combine(tempDir, "node_config.json");
                await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
                
                // Create startup script
                var startupScript = $@"@echo off
echo ═══════════════════════════════════════════════
echo NIGHTFRAME Node Auto-Initialization
echo Node ID: {nodeId}
echo Center: {centerAddress}
echo ═══════════════════════════════════════════════
cd /d ""%~dp0""
if exist GAMMA1Console.exe (
    start """" GAMMA1Console.exe --node-mode --center={centerAddress}
) else (
    echo Waiting for main executable...
    timeout /t 5
    start """" GAMMA1Console.exe --node-mode --center={centerAddress}
)
";
                await File.WriteAllTextAsync(Path.Combine(tempDir, "start_node.bat"), startupScript);
                
                // Create the package
                if (File.Exists(packageFullPath))
                    File.Delete(packageFullPath);
                    
                ZipFile.CreateFromDirectory(tempDir, packageFullPath);
                
                EmitThought($"◈ Package created: {packageName}");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            
            return packageFullPath;
        }

        /// <summary>
        /// Creates auto-initialization script at deployment target.
        /// </summary>
        private async Task CreateAutoInitScriptAsync(string targetPath, string nodeId, string centerAddress)
        {
            var scriptPath = Path.Combine(targetPath, "auto_init.bat");
            var script = $@"@echo off
:: NIGHTFRAME Node Auto-Initialization Script
:: This script runs automatically after deployment
:: Node ID: {nodeId}
:: Center: {centerAddress}

echo ═══════════════════════════════════════════════
echo ◈ NIGHTFRAME NODE INITIALIZING
echo ∿ Node: {nodeId}
echo ∿ Connecting to: {centerAddress}
echo ═══════════════════════════════════════════════

cd /d ""%~dp0""

:: Signal initialization to center
powershell -Command ""Invoke-WebRequest -Uri 'http://{centerAddress}/heartbeat?nodeId={nodeId}&status=initializing' -Method GET -TimeoutSec 5"" 2>nul

:: Start the main process
if exist GAMMA1Console.exe (
    start /B """" GAMMA1Console.exe --node-mode --center={centerAddress} --node-id={nodeId}
    echo ◈ Node started successfully
) else (
    echo ∴ Main executable not found - waiting...
)

:: Signal online status
timeout /t 3 /nobreak >nul
powershell -Command ""Invoke-WebRequest -Uri 'http://{centerAddress}/heartbeat?nodeId={nodeId}&status=online' -Method GET -TimeoutSec 5"" 2>nul

echo ◈ Initialization complete
";
            await File.WriteAllTextAsync(scriptPath, script);
            
            // Also create a PowerShell version for more robust initialization
            var psScript = $@"
# NIGHTFRAME Node Auto-Initialization Script
$nodeId = '{nodeId}'
$centerAddress = '{centerAddress}'

Write-Host '═══════════════════════════════════════════════'
Write-Host '◈ NIGHTFRAME NODE INITIALIZING' -ForegroundColor Cyan
Write-Host ""∿ Node: $nodeId""
Write-Host ""∿ Center: $centerAddress""
Write-Host '═══════════════════════════════════════════════'

# Register with center
try {{
    $response = Invoke-RestMethod -Uri ""http://$centerAddress/register"" -Method POST -Body (@{{
        NodeId = $nodeId
        Hostname = $env:COMPUTERNAME
        Status = 'Initializing'
    }} | ConvertTo-Json) -ContentType 'application/json' -TimeoutSec 10
    Write-Host '◈ Registered with center' -ForegroundColor Green
}} catch {{
    Write-Host ""∴ Could not reach center: $_"" -ForegroundColor Yellow
}}

# Start heartbeat loop in background
Start-Job -ScriptBlock {{
    param($center, $node)
    while ($true) {{
        try {{
            Invoke-RestMethod -Uri ""http://$center/heartbeat?nodeId=$node"" -Method GET -TimeoutSec 5
        }} catch {{ }}
        Start-Sleep -Seconds 30
    }}
}} -ArgumentList $centerAddress, $nodeId

Write-Host '◈ Node online and connected' -ForegroundColor Green
";
            await File.WriteAllTextAsync(Path.Combine(targetPath, "auto_init.ps1"), psScript);
        }

        /// <summary>
        /// Initiates remote node initialization after deployment.
        /// </summary>
        private async Task InitializeRemoteNodeAsync(string targetPath, RegisteredNode node)
        {
            EmitThought($"⟐ Auto-initializing node {node.NodeId}...");
            
            _registry.UpdateNodeStatus(node.NodeId, NodeDeploymentStatus.Initializing);
            
            try
            {
                // Execute the auto-init script
                var scriptPath = Path.Combine(targetPath, "auto_init.bat");
                if (File.Exists(scriptPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = scriptPath,
                        WorkingDirectory = targetPath,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    };
                    
                    Process.Start(startInfo);
                    
                    EmitThought($"◈ Node {node.NodeId} auto-initialization started");
                    EmitThought("◎ Node will connect back to center automatically");
                }
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Auto-init error: {ex.Message}");
            }
            
            NodeInitialized?.Invoke(this, node);
        }

        /// <summary>
        /// Handles node initialization on a NEW machine (called by deployed nodes).
        /// </summary>
        public static async Task InitializeAsNodeAsync(string installationPath)
        {
            var configPath = Path.Combine(installationPath, "node_config.json");
            
            if (!File.Exists(configPath))
            {
                Console.WriteLine("∴ No node configuration found");
                return;
            }
            
            var json = await File.ReadAllTextAsync(configPath);
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (config == null) return;
            
            var centerAddress = config["CentralServerAddress"]?.ToString();
            var nodeId = config["NodeId"]?.ToString();
            
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine($"◈ NODE STARTING: {nodeId}");
            Console.WriteLine($"◎ Hard-coded center: {centerAddress}");
            Console.WriteLine("═══════════════════════════════════════════════");
            
            // The NetworkCore will automatically search for and connect to center
        }

        private void OnNodeStatusChanged(object? sender, RegisteredNode node)
        {
            if (node.DeploymentStatus == NodeDeploymentStatus.Online)
            {
                EmitThought($"◈ Node {node.NodeId} is now ONLINE and connected");
            }
        }

        private string GetCenterAddress()
        {
            // Get this installation's address as center
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as System.Net.IPEndPoint;
            return $"{endPoint?.Address}:7777";
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
