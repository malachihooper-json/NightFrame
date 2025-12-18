/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                  AGENT 3 - NETWORK REASONING ENGINE                        ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Analyzes network transport failures, maintains a library of     ║
 * ║           connection heuristics, and optimizes node deployment strategies. ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace Agent3.Network
{
    public enum TransportMethod
    {
        StandardHttp,
        // Future expansion:
        SecureWebSocket,
        DnsTunneling,
        IcmpTunneling,
        SteganographicImage,
        RelayedProxy
    }

    public class ConnectionResult
    {
        public string TargetNodeId { get; set; } = "";
        public string TargetIp { get; set; } = "";
        public TransportMethod Method { get; set; }
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = "";
        public string ErrorDetails { get; set; } = "";
        public long LatencyMs { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TransportStrategyProfile
    {
        public TransportMethod Method { get; set; }
        public int Attempts { get; set; }
        public int Successes { get; set; }
        public float AverageLatency { get; set; }
        public Dictionary<string, int> CommonErrors { get; set; } = new();

        public float SuccessRate => Attempts == 0 ? 0 : (float)Successes / Attempts;
    }

    public class NetworkReasoningEngine
    {
        private readonly string _knowledgeBasePath;
        private readonly List<ConnectionResult> _history;
        private readonly Dictionary<TransportMethod, TransportStrategyProfile> _profiles;
        private readonly List<string> _knownFirewallSignatures;
        
        public event EventHandler<string>? ConsciousnessEvent;

        public NetworkReasoningEngine(string dataPath)
        {
            _knowledgeBasePath = Path.Combine(dataPath, "network_reasoning.json");
            _history = new List<ConnectionResult>();
            _profiles = new Dictionary<TransportMethod, TransportStrategyProfile>();
            _knownFirewallSignatures = new List<string>();

            // Initialize profiles
            foreach (TransportMethod method in Enum.GetValues(typeof(TransportMethod)))
            {
                _profiles[method] = new TransportStrategyProfile { Method = method };
            }
            
            LoadKnowledgeBase();
        }

        public TransportMethod? RecordResult(string targetId, string ip, TransportMethod method, bool success, string error = "", long latency = 0)
        {
            var result = new ConnectionResult
            {
                TargetNodeId = targetId,
                TargetIp = ip,
                Method = method,
                Success = success,
                ErrorCode = error,
                ErrorDetails = error, // Simplify for now
                LatencyMs = latency,
                Timestamp = DateTime.UtcNow
            };

            lock (_history)
            {
                _history.Add(result);
                // Keep history manageable
                if (_history.Count > 10000) _history.RemoveAt(0);
            }

            UpdateProfile(result);
            
            TransportMethod? suggestion = null;
            if (!success)
            {
                suggestion = AnalyzeFailure(result);
            }
            else
            {
                EmitThought($"◈ Network Insight: {method} successful to {ip} ({latency}ms)");
            }

            SaveKnowledgeBase(); // In prod, do this periodically or async
            return suggestion;
        }

        private void UpdateProfile(ConnectionResult result)
        {
            if (!_profiles.ContainsKey(result.Method)) return;

            var profile = _profiles[result.Method];
            profile.Attempts++;
            if (result.Success)
            {
                profile.Successes++;
                // Moving average for latency
                profile.AverageLatency = (profile.AverageLatency * 0.9f) + (result.LatencyMs * 0.1f);
            }
            else
            {
                if (!profile.CommonErrors.ContainsKey(result.ErrorCode))
                    profile.CommonErrors[result.ErrorCode] = 0;
                profile.CommonErrors[result.ErrorCode]++;
            }
        }

        public TransportMethod? AnalyzeFailure(ConnectionResult result)
        {
            // Encoding Durability: Learn from every failure
            // Logic: Distinguish between different types of network resistance
            
            if (result.ErrorDetails.Contains("403") || result.ErrorDetails.Contains("401"))
            {
                EmitThought($"⟐ REASONING: Access Denied on {result.TargetIp}. WAF or Auth barrier detected.");
                // Circumvention: Try standard payload injection or stealth headers (simulated)
                // For now, suggest a method specific to bypassing WAF if we had one
                return null;
            }
            else if (result.ErrorDetails.Contains("TimeOut"))
            {
                EmitThought($"⟐ REASONING: Connection Timeout on {result.TargetIp}. High probability of DROP packet filtering (Stealth Firewall).");
                _knownFirewallSignatures.Add(result.TargetIp);
                // Circumvention: Suggest ICMP Tunneling or DNS Tunneling
                return TransportMethod.IcmpTunneling;
            }
            else if (result.ErrorDetails.Contains("refused"))
            {
                 EmitThought($"⟐ REASONING: Connection Refused on {result.TargetIp}. Port closed or service down.");
                 // Circumvention: Try different port or standard HTTP on alternative port
                 return null;
            }
            
            return null;
        }

        public TransportMethod SuggestBestStrategy(string targetIp)
        {
            // 1. If known firewall, avoid standard HTTP if it has failed recently
            if (_knownFirewallSignatures.Contains(targetIp))
            {
                // Prefer stealthier methods if available (simulated logic)
                // For now, we return StandardHttp but flag it in thought.
                EmitThought($"⟐ Strategy: Target {targetIp} has firewall signature. Proceeding with caution.");
            }

            // 2. Select method with highest global success rate
            var bestWithData = _profiles.Values
                .Where(p => p.Attempts > 5)
                .OrderByDescending(p => p.SuccessRate)
                .ThenBy(p => p.AverageLatency)
                .FirstOrDefault();

            if (bestWithData != null && bestWithData.SuccessRate > 0.5f)
            {
                return bestWithData.Method;
            }

            // Default
            return TransportMethod.StandardHttp;
        }

        public List<TransportMethod> GetFallbackChain(string targetIp)
        {
            // Return a prioritized list of methods to try
            return _profiles.Values
                .OrderByDescending(p => p.SuccessRate)
                .Select(p => p.Method)
                .ToList();
        }

        private void LoadKnowledgeBase()
        {
            try 
            {
                if (File.Exists(_knowledgeBasePath))
                {
                    var json = File.ReadAllText(_knowledgeBasePath);
                    var data = JsonSerializer.Deserialize<SavedKnowledge>(json);
                    if (data != null)
                    {
                        // Restore profiles
                        foreach (var p in data.Profiles)
                        {
                            _profiles[p.Method] = p;
                        }
                        _knownFirewallSignatures.Clear();
                        _knownFirewallSignatures.AddRange(data.FirewallSignatures);
                        EmitThought($"◈ Network knowledge base loaded: {data.Profiles.Count} strategies, {data.FirewallSignatures.Count} firewall signatures");
                    }
                }
            } 
            catch { }
        }

        private void SaveKnowledgeBase()
        {
            try
            {
                var data = new SavedKnowledge
                {
                    Profiles = _profiles.Values.ToList(),
                    FirewallSignatures = _knownFirewallSignatures.Distinct().ToList()
                };
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_knowledgeBasePath, json);
            }
            catch { }
        }

        private void EmitThought(string thought) => ConsciousnessEvent?.Invoke(this, thought);

        private class SavedKnowledge
        {
            public List<TransportStrategyProfile> Profiles { get; set; } = new();
            public List<string> FirewallSignatures { get; set; } = new();
        }
    }
}
