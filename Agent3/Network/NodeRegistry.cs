/*
 * AGENT 3 - NODE REGISTRY
 * Central tracking of all deployed nodes in the network
 * Persists to disk for recovery after restart
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent3.Network
{
    public class RegisteredNode
    {
        public string NodeId { get; set; } = "";
        public string Hostname { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int Port { get; set; } = 7777;
        public string DeploymentPath { get; set; } = "";
        public string ParentNodeId { get; set; } = "";
        public DateTime DeployedAt { get; set; }
        public DateTime LastHeartbeat { get; set; }
        public DateTime LastInitialized { get; set; }
        public NodeDeploymentStatus DeploymentStatus { get; set; }
        public string Version { get; set; } = "1.0.0";
        public bool IsCenter { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    public enum NodeDeploymentStatus
    {
        Pending,        // Package created, not yet deployed
        Deploying,      // In process of deployment
        Deployed,       // Deployed, not yet initialized
        Initializing,   // Starting up
        Online,         // Running and connected
        Offline,        // Not responding
        Error,          // Error state
        Decommissioned  // Intentionally removed
    }

    /// <summary>
    /// Central registry for tracking all deployed NIGHTFRAME nodes.
    /// Persists to disk and provides real-time status updates.
    /// </summary>
    public class NodeRegistry
    {
        private readonly string _registryPath;
        private readonly string _registryFile;
        private readonly Dictionary<string, RegisteredNode> _nodes = new();
        private readonly object _lock = new();
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<RegisteredNode>? NodeRegistered;
        public event EventHandler<RegisteredNode>? NodeStatusChanged;
        public event EventHandler<RegisteredNode>? NodeInitialized;
        public event EventHandler<RegisteredNode>? NodeOffline;
        
        public IReadOnlyDictionary<string, RegisteredNode> Nodes => _nodes;
        public int TotalNodes => _nodes.Count;
        public int OnlineNodes => _nodes.Values.Count(n => n.DeploymentStatus == NodeDeploymentStatus.Online);
        
        public NodeRegistry(string basePath)
        {
            _registryPath = Path.Combine(basePath, ".node_registry");
            _registryFile = Path.Combine(_registryPath, "nodes.json");
            
            Directory.CreateDirectory(_registryPath);
            LoadRegistry();
            
            EmitThought("⟁ Node Registry initialized");
            EmitThought($"◎ Loaded {_nodes.Count} registered nodes");
        }

        /// <summary>
        /// Registers a new node deployment.
        /// </summary>
        public RegisteredNode RegisterNode(
            string hostname,
            string ipAddress,
            int port,
            string deploymentPath,
            string parentNodeId,
            bool isCenter = false)
        {
            var node = new RegisteredNode
            {
                NodeId = GenerateNodeId(hostname, deploymentPath),
                Hostname = hostname,
                IpAddress = ipAddress,
                Port = port,
                DeploymentPath = deploymentPath,
                ParentNodeId = parentNodeId,
                DeployedAt = DateTime.UtcNow,
                DeploymentStatus = NodeDeploymentStatus.Pending,
                IsCenter = isCenter
            };
            
            lock (_lock)
            {
                _nodes[node.NodeId] = node;
            }
            
            SaveRegistry();
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ NODE REGISTERED: {node.NodeId}");
            EmitThought($"∿ Host: {hostname}");
            EmitThought($"∿ Path: {deploymentPath}");
            EmitThought($"∿ Parent: {parentNodeId}");
            EmitThought($"◎ Status: {node.DeploymentStatus}");
            EmitThought("═══════════════════════════════════════════════");
            
            NodeRegistered?.Invoke(this, node);
            return node;
        }

        /// <summary>
        /// Updates a node's deployment status.
        /// </summary>
        public void UpdateNodeStatus(string nodeId, NodeDeploymentStatus status)
        {
            lock (_lock)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    var oldStatus = node.DeploymentStatus;
                    node.DeploymentStatus = status;
                    
                    if (status == NodeDeploymentStatus.Online)
                        node.LastHeartbeat = DateTime.UtcNow;
                    
                    if (status == NodeDeploymentStatus.Initializing)
                        node.LastInitialized = DateTime.UtcNow;
                    
                    SaveRegistry();
                    
                    EmitThought($"◎ Node {nodeId} status: {oldStatus} → {status}");
                    NodeStatusChanged?.Invoke(this, node);
                    
                    if (status == NodeDeploymentStatus.Online && oldStatus == NodeDeploymentStatus.Initializing)
                    {
                        EmitThought($"◈ Node {nodeId} is now ONLINE");
                        NodeInitialized?.Invoke(this, node);
                    }
                    
                    if (status == NodeDeploymentStatus.Offline && oldStatus == NodeDeploymentStatus.Online)
                    {
                        EmitThought($"∴ Node {nodeId} went OFFLINE");
                        NodeOffline?.Invoke(this, node);
                    }
                }
            }
        }

        /// <summary>
        /// Records a heartbeat from a node.
        /// </summary>
        public void RecordHeartbeat(string nodeId, string ipAddress = "")
        {
            lock (_lock)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    node.LastHeartbeat = DateTime.UtcNow;
                    
                    if (!string.IsNullOrEmpty(ipAddress) && node.IpAddress != ipAddress)
                    {
                        EmitThought($"⟐ Node {nodeId} IP changed: {node.IpAddress} → {ipAddress}");
                        node.IpAddress = ipAddress;
                    }
                    
                    if (node.DeploymentStatus != NodeDeploymentStatus.Online)
                    {
                        UpdateNodeStatus(nodeId, NodeDeploymentStatus.Online);
                    }
                    
                    SaveRegistry();
                }
            }
        }

        /// <summary>
        /// Gets a node by ID.
        /// </summary>
        public RegisteredNode? GetNode(string nodeId)
        {
            lock (_lock)
            {
                return _nodes.TryGetValue(nodeId, out var node) ? node : null;
            }
        }

        /// <summary>
        /// Gets all nodes deployed by a specific parent.
        /// </summary>
        public List<RegisteredNode> GetChildNodes(string parentNodeId)
        {
            lock (_lock)
            {
                return _nodes.Values.Where(n => n.ParentNodeId == parentNodeId).ToList();
            }
        }

        /// <summary>
        /// Gets nodes that haven't sent a heartbeat recently.
        /// </summary>
        public List<RegisteredNode> GetStaleNodes(TimeSpan threshold)
        {
            var cutoff = DateTime.UtcNow - threshold;
            lock (_lock)
            {
                return _nodes.Values
                    .Where(n => n.DeploymentStatus == NodeDeploymentStatus.Online)
                    .Where(n => n.LastHeartbeat < cutoff)
                    .ToList();
            }
        }

        /// <summary>
        /// Decommissions a node.
        /// </summary>
        public void DecommissionNode(string nodeId)
        {
            lock (_lock)
            {
                if (_nodes.TryGetValue(nodeId, out var node))
                {
                    node.DeploymentStatus = NodeDeploymentStatus.Decommissioned;
                    SaveRegistry();
                    EmitThought($"◎ Node {nodeId} decommissioned");
                }
            }
        }

        /// <summary>
        /// Exports registry to a portable format.
        /// </summary>
        public string ExportRegistry()
        {
            lock (_lock)
            {
                return JsonSerializer.Serialize(_nodes, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        /// <summary>
        /// Gets summary statistics.
        /// </summary>
        public (int Total, int Online, int Offline, int Pending, int Error) GetStats()
        {
            lock (_lock)
            {
                return (
                    _nodes.Count,
                    _nodes.Values.Count(n => n.DeploymentStatus == NodeDeploymentStatus.Online),
                    _nodes.Values.Count(n => n.DeploymentStatus == NodeDeploymentStatus.Offline),
                    _nodes.Values.Count(n => n.DeploymentStatus == NodeDeploymentStatus.Pending),
                    _nodes.Values.Count(n => n.DeploymentStatus == NodeDeploymentStatus.Error)
                );
            }
        }

        private void LoadRegistry()
        {
            if (File.Exists(_registryFile))
            {
                try
                {
                    var json = File.ReadAllText(_registryFile);
                    var nodes = JsonSerializer.Deserialize<Dictionary<string, RegisteredNode>>(json);
                    if (nodes != null)
                    {
                        foreach (var kv in nodes)
                            _nodes[kv.Key] = kv.Value;
                    }
                }
                catch (Exception ex)
                {
                    EmitThought($"∴ Failed to load registry: {ex.Message}");
                }
            }
        }

        private void SaveRegistry()
        {
            try
            {
                var json = JsonSerializer.Serialize(_nodes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_registryFile, json);
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Failed to save registry: {ex.Message}");
            }
        }

        private string GenerateNodeId(string hostname, string path)
        {
            var hash = $"{hostname}:{path}:{DateTime.UtcNow.Ticks}".GetHashCode();
            return $"NODE_{hostname}_{Math.Abs(hash):X8}";
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
