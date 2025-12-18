/*
 * AGENT 3 - NEURAL NETWORK MESH
 * Manages the spatial interconnection of traveling nodes
 * Tracks all nodes, their connections, and collective intelligence
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace Agent3.Network
{
    public class MeshStatistics
    {
        public int TotalNodes { get; set; }
        public int ActiveNodes { get; set; }
        public int TotalConnections { get; set; }
        public int NeuralConnections { get; set; }
        public double AverageEffectiveness { get; set; }
        public int TotalTasksCompleted { get; set; }
        public int TotalPathsEliminated { get; set; }
    }

    /// <summary>
    /// Manages the spatial interconnection mesh of all traveling nodes.
    /// Acts as the collective consciousness and coordination layer.
    /// </summary>
    public class NeuralNetworkMesh
    {
        private readonly string _basePath;
        private readonly string _meshFile;
        private readonly Dictionary<string, TravelingNode> _nodes = new();
        private readonly Dictionary<string, NeuralConnection> _globalConnections = new();
        private readonly List<TravelOutcome> _allOutcomes = new();
        private readonly NodeRegistry _registry;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<TravelingNode>? NodeSpawned;
        public event EventHandler<string>? NodeTerminated;
        
        public IReadOnlyDictionary<string, TravelingNode> Nodes => _nodes;
        public IReadOnlyDictionary<string, NeuralConnection> GlobalConnections => _globalConnections;
        
        public NeuralNetworkMesh(string basePath, NodeRegistry registry)
        {
            _basePath = basePath;
            _meshFile = Path.Combine(basePath, ".neural_mesh", "mesh.json");
            _registry = registry;
            
            Directory.CreateDirectory(Path.GetDirectoryName(_meshFile)!);
            LoadMesh();
            
            EmitThought("⟁ Neural Network Mesh initialized");
        }

        /// <summary>
        /// Spawns a new traveling node for task execution.
        /// </summary>
        public TravelingNode SpawnNode(string centerAddress)
        {
            var nodeId = $"TRAVELER_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            
            var node = new TravelingNode(nodeId, centerAddress);
            
            // Wire events
            node.ConsciousnessEvent += (s, t) => EmitThought($"[{nodeId}] {t}");
            node.TravelCompleted += OnNodeTravelCompleted;
            node.ConnectionEstablished += OnConnectionEstablished;
            
            _nodes[nodeId] = node;
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ NODE SPAWNED: {nodeId}");
            EmitThought("◎ Ready for autonomous travel");
            EmitThought("═══════════════════════════════════════════════");
            
            NodeSpawned?.Invoke(this, node);
            SaveMesh();
            
            return node;
        }

        /// <summary>
        /// Dispatches a task to the best available node or spawns a new one.
        /// </summary>
        public async Task<bool> DispatchTaskAsync(string task, string centerAddress)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ DISPATCHING TASK: {task}");
            EmitThought("═══════════════════════════════════════════════");
            
            // Find idle node or spawn new one
            var node = _nodes.Values.FirstOrDefault(n => n.State == NodeState.Idle);
            
            if (node == null)
            {
                EmitThought("⟐ No idle nodes - spawning new traveler...");
                node = SpawnNode(centerAddress);
            }
            else
            {
                EmitThought($"⟐ Using existing node: {node.NodeId}");
            }
            
            // Execute task
            var success = await node.ExecuteTaskAsync(task);
            
            // Self-improvement after task
            node.AnalyzeAndImprove();
            
            SaveMesh();
            return success;
        }

        /// <summary>
        /// Terminates a node (eliminate).
        /// </summary>
        public void TerminateNode(string nodeId, string reason)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                EmitThought($"∴ Terminating node {nodeId}: {reason}");
                _nodes.Remove(nodeId);
                NodeTerminated?.Invoke(this, nodeId);
                SaveMesh();
            }
        }

        /// <summary>
        /// Gets mesh statistics.
        /// </summary>
        public MeshStatistics GetStatistics()
        {
            return new MeshStatistics
            {
                TotalNodes = _nodes.Count,
                ActiveNodes = _nodes.Values.Count(n => n.State != NodeState.Idle),
                TotalConnections = _globalConnections.Count,
                NeuralConnections = _globalConnections.Values.Count(c => c.Type == ConnectionType.Neural),
                AverageEffectiveness = _globalConnections.Values.Any() 
                    ? _globalConnections.Values.Average(c => c.EffectivenessRatio) * 100 
                    : 0,
                TotalTasksCompleted = _allOutcomes.Count(o => !o.Eliminated),
                TotalPathsEliminated = _allOutcomes.Count(o => o.Eliminated)
            };
        }

        /// <summary>
        /// Finds the best path to a capability using collective knowledge.
        /// </summary>
        public List<string> FindPathToCapability(string capability)
        {
            var connections = _globalConnections.Values
                .Where(c => c.CapabilitiesAvailable.Contains(capability))
                .OrderByDescending(c => c.EffectivenessRatio)
                .ToList();
            
            if (connections.Any())
            {
                var best = connections.First();
                return new List<string> { best.SourceNode, best.TargetAddress };
            }
            
            return new List<string>();
        }

        /// <summary>
        /// Prunes ineffective connections from the mesh.
        /// </summary>
        public void PruneIneffectiveConnections(double threshold = 0.3)
        {
            var toPrune = _globalConnections.Values
                .Where(c => c.SuccessCount + c.FailureCount > 10)
                .Where(c => c.EffectivenessRatio < threshold)
                .Select(c => c.ConnectionId)
                .ToList();
            
            foreach (var id in toPrune)
            {
                EmitThought($"∴ Pruning connection: {id}");
                _globalConnections.Remove(id);
            }
            
            if (toPrune.Any())
            {
                EmitThought($"◎ Pruned {toPrune.Count} ineffective connections");
                SaveMesh();
            }
        }

        /// <summary>
        /// Performs collective self-improvement across all nodes.
        /// </summary>
        public void CollectiveSelfImprovement()
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ COLLECTIVE SELF-IMPROVEMENT");
            EmitThought("═══════════════════════════════════════════════");
            
            // Analyze all nodes
            foreach (var node in _nodes.Values)
            {
                node.AnalyzeAndImprove();
            }
            
            // Prune mesh
            PruneIneffectiveConnections();
            
            // Statistics
            var stats = GetStatistics();
            EmitThought($"◎ Mesh nodes: {stats.TotalNodes}");
            EmitThought($"◎ Neural connections: {stats.NeuralConnections}");
            EmitThought($"◎ Average effectiveness: {stats.AverageEffectiveness:F1}%");
            EmitThought($"◎ Tasks completed: {stats.TotalTasksCompleted}");
            EmitThought($"◎ Paths eliminated: {stats.TotalPathsEliminated}");
            EmitThought("═══════════════════════════════════════════════");
        }

        private void OnNodeTravelCompleted(object? sender, TravelOutcome outcome)
        {
            _allOutcomes.Add(outcome);
            SaveMesh();
        }

        private void OnConnectionEstablished(object? sender, NeuralConnection conn)
        {
            if (_globalConnections.TryGetValue(conn.ConnectionId, out var existing))
            {
                existing.SuccessCount += conn.SuccessCount;
                existing.LastUsed = conn.LastUsed;
                existing.CapabilitiesAvailable = conn.CapabilitiesAvailable;
            }
            else
            {
                _globalConnections[conn.ConnectionId] = conn;
            }
            SaveMesh();
        }

        private void LoadMesh()
        {
            if (File.Exists(_meshFile))
            {
                try
                {
                    var json = File.ReadAllText(_meshFile);
                    var data = JsonSerializer.Deserialize<MeshData>(json);
                    if (data != null)
                    {
                        foreach (var kv in data.Connections)
                            _globalConnections[kv.Key] = kv.Value;
                        
                        // Restore nodes? 
                        // Note: TravelingNode serialization is complex due to events/state. 
                        // For this prototype, we will assume nodes are re-spawned or basic state is recovered.
                        // Ideally, we would deserialize TravelingNode objects.
                        
                        EmitThought($"◎ Loaded {data.Connections.Count} connections from mesh");
                    }
                }
                catch { }
            }
        }

        private void SaveMesh()
        {
            try
            {
                var data = new MeshData
                {
                    Connections = _globalConnections,
                    // Nodes = ... (If we were fully serializing nodes)
                };
                
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_meshFile, json);
            }
            catch { }
        }

        private class MeshData
        {
            public Dictionary<string, NeuralConnection> Connections { get; set; } = new();
            // public Dictionary<string, TravelingNodeState> Nodes { get; set; } = new(); 
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
