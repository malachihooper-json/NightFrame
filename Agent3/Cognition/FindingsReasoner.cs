/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - FINDINGS REASONER                             ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Center-side reasoning about node findings.                                ║
 * ║  Evaluates findings against master prompt, determines relevance,           ║
 * ║  and directs which updates to implement or delete.                         ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Agent3.Cognition
{
    public enum FindingRelevance { Critical, High, Medium, Low, Irrelevant }
    public enum FindingAction { Implement, Queue, Review, Archive, Delete }

    public class EvaluatedFinding
    {
        public string FindingId { get; set; } = "";
        public string NodeId { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime OriginalTimestamp { get; set; }
        public DateTime EvaluatedAt { get; set; }
        
        // Reasoning results
        public FindingRelevance Relevance { get; set; }
        public FindingAction Action { get; set; }
        public double AlignmentScore { get; set; }
        public string Reasoning { get; set; } = "";
        public List<string> MasterPromptAlignments { get; set; } = new();
        public List<string> Concerns { get; set; } = new();
        
        // Implementation details
        public bool Processed { get; set; }
        public string ProcessingNotes { get; set; } = "";
    }

    public class ReasoningSession
    {
        public string SessionId { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string MasterPrompt { get; set; } = "";
        public int FindingsEvaluated { get; set; }
        public int Implemented { get; set; }
        public int Queued { get; set; }
        public int Deleted { get; set; }
        public List<string> KeyDecisions { get; set; } = new();
    }

    /// <summary>
    /// Reasons about node findings to determine relevance and actions.
    /// Evaluates against the master prompt for alignment.
    /// Also detects stale findings rendered irrelevant by center changes.
    /// </summary>
    public class FindingsReasoner
    {
        private readonly string _basePath;
        private readonly string _evaluationsPath;
        private readonly string _sessionsPath;
        private readonly string _changeHistoryPath;
        private string _masterPrompt = "";
        private List<string> _masterPromptKeywords = new();
        private List<string> _masterPromptGoals = new();
        
        private readonly Dictionary<string, EvaluatedFinding> _evaluatedFindings = new();
        private readonly List<StateChange> _stateChanges = new();
        private ReasoningSession? _currentSession;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<EvaluatedFinding>? FindingEvaluated;
        public event EventHandler<EvaluatedFinding>? UpdateImplemented;
        public event EventHandler<EvaluatedFinding>? FindingDeleted;
        public event EventHandler<EvaluatedFinding>? FindingObsolete;
        
        public string MasterPrompt => _masterPrompt;
        public IReadOnlyDictionary<string, EvaluatedFinding> EvaluatedFindings => _evaluatedFindings;
        
        public FindingsReasoner(string basePath)
        {
            _basePath = basePath;
            _evaluationsPath = Path.Combine(basePath, ".reasoning", "evaluations");
            _sessionsPath = Path.Combine(basePath, ".reasoning", "sessions");
            _changeHistoryPath = Path.Combine(basePath, ".reasoning", "change_history.json");
            
            Directory.CreateDirectory(_evaluationsPath);
            Directory.CreateDirectory(_sessionsPath);
            
            LoadMasterPrompt();
            LoadEvaluations();
            LoadChangeHistory();
        }

        /// <summary>
        /// Represents a state change at the center that may obsolete node findings.
        /// </summary>
        public class StateChange
        {
            public string ChangeId { get; set; } = "";
            public DateTime Timestamp { get; set; }
            public string ChangeType { get; set; } = "";
            public string Description { get; set; } = "";
            public List<string> ObsoletesCategories { get; set; } = new();
            public List<string> ObsoletesKeywords { get; set; } = new();
        }

        /// <summary>
        /// Records a state change that may render node findings obsolete.
        /// </summary>
        public void RecordStateChange(string changeType, string description, 
            List<string>? obsoletesCategories = null, List<string>? obsoletesKeywords = null)
        {
            var change = new StateChange
            {
                ChangeId = $"CHG_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..4]}",
                Timestamp = DateTime.UtcNow,
                ChangeType = changeType,
                Description = description,
                ObsoletesCategories = obsoletesCategories ?? new(),
                ObsoletesKeywords = obsoletesKeywords ?? new()
            };
            
            _stateChanges.Add(change);
            SaveChangeHistory();
            
            EmitThought($"◎ State change recorded: {changeType}");
            EmitThought($"  {description}");
        }

        /// <summary>
        /// Sets the master prompt for alignment evaluation.
        /// </summary>
        public void SetMasterPrompt(string prompt)
        {
            _masterPrompt = prompt;
            AnalyzeMasterPrompt();
            SaveMasterPrompt();
            
            EmitThought("◈ Master prompt updated for reasoning");
            EmitThought($"◎ Extracted {_masterPromptKeywords.Count} keywords");
            EmitThought($"◎ Identified {_masterPromptGoals.Count} goals");
        }

        /// <summary>
        /// Analyzes the master prompt to extract keywords and goals.
        /// </summary>
        private void AnalyzeMasterPrompt()
        {
            // Extract keywords (significant words)
            var words = Regex.Split(_masterPrompt.ToLower(), @"\W+")
                .Where(w => w.Length > 3)
                .Where(w => !IsStopWord(w))
                .Distinct()
                .ToList();
            
            _masterPromptKeywords = words;
            
            // Extract goals (sentences with action verbs)
            var sentences = _masterPrompt.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var actionVerbs = new[] { "learn", "improve", "optimize", "discover", "expand", "connect", 
                                      "train", "analyze", "find", "create", "build", "develop" };
            
            _masterPromptGoals = sentences
                .Where(s => actionVerbs.Any(v => s.ToLower().Contains(v)))
                .Select(s => s.Trim())
                .ToList();
        }

        /// <summary>
        /// Processes incoming findings from nodes with full reasoning.
        /// </summary>
        public async Task<List<EvaluatedFinding>> ProcessNodeFindingsAsync(List<object> rawFindings)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ REASONING ABOUT NODE FINDINGS");
            EmitThought($"∿ Processing {rawFindings.Count} findings");
            EmitThought($"◎ Evaluating against master prompt");
            EmitThought("═══════════════════════════════════════════════");
            
            // Start reasoning session
            _currentSession = new ReasoningSession
            {
                SessionId = $"REASON_{DateTime.UtcNow:yyyyMMddHHmmss}",
                StartTime = DateTime.UtcNow,
                MasterPrompt = _masterPrompt
            };
            
            var results = new List<EvaluatedFinding>();
            
            foreach (var raw in rawFindings)
            {
                var evaluated = await EvaluateFindingAsync(raw);
                results.Add(evaluated);
                _evaluatedFindings[evaluated.FindingId] = evaluated;
                
                _currentSession.FindingsEvaluated++;
                
                // Take action based on evaluation
                await ProcessEvaluatedFindingAsync(evaluated);
            }
            
            // Complete session
            _currentSession.EndTime = DateTime.UtcNow;
            SaveSession(_currentSession);
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ REASONING COMPLETE");
            EmitThought($"∿ Evaluated: {_currentSession.FindingsEvaluated}");
            EmitThought($"∿ Implemented: {_currentSession.Implemented}");
            EmitThought($"∿ Queued: {_currentSession.Queued}");
            EmitThought($"∿ Deleted: {_currentSession.Deleted}");
            EmitThought("═══════════════════════════════════════════════");
            
            return results;
        }

        /// <summary>
        /// Evaluates a single finding against the master prompt.
        /// </summary>
        private async Task<EvaluatedFinding> EvaluateFindingAsync(object rawFinding)
        {
            // Parse raw finding
            var json = rawFinding is string s ? s : JsonSerializer.Serialize(rawFinding);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var evaluated = new EvaluatedFinding
            {
                FindingId = root.TryGetProperty("FindingId", out var id) ? id.GetString() ?? "" : Guid.NewGuid().ToString(),
                NodeId = root.TryGetProperty("NodeId", out var nid) ? nid.GetString() ?? "" : "",
                Category = root.TryGetProperty("Category", out var cat) ? cat.GetString() ?? "" : "",
                Description = root.TryGetProperty("Description", out var desc) ? desc.GetString() ?? "" : "",
                OriginalTimestamp = root.TryGetProperty("Timestamp", out var ts) ? ts.GetDateTime() : DateTime.UtcNow,
                EvaluatedAt = DateTime.UtcNow
            };
            
            // Calculate alignment with master prompt
            var alignmentScore = CalculateAlignment(evaluated);
            evaluated.AlignmentScore = alignmentScore;
            
            // Determine relevance based on alignment
            evaluated.Relevance = DetermineRelevance(alignmentScore, evaluated.Category);
            
            // Determine action
            evaluated.Action = DetermineAction(evaluated);
            
            // Generate reasoning explanation
            evaluated.Reasoning = GenerateReasoning(evaluated);
            
            // Find specific alignments
            evaluated.MasterPromptAlignments = FindAlignments(evaluated.Description);
            
            // Identify concerns
            evaluated.Concerns = IdentifyConcerns(evaluated);
            
            EmitThought($"⟐ Evaluated: {evaluated.FindingId}");
            EmitThought($"  Alignment: {evaluated.AlignmentScore:F2} → {evaluated.Relevance}");
            EmitThought($"  Action: {evaluated.Action}");
            
            FindingEvaluated?.Invoke(this, evaluated);
            SaveEvaluation(evaluated);
            
            return evaluated;
        }

        /// <summary>
        /// Calculates alignment score between finding and master prompt.
        /// </summary>
        private double CalculateAlignment(EvaluatedFinding finding)
        {
            if (string.IsNullOrEmpty(_masterPrompt))
                return 0.5; // Neutral if no master prompt
            
            var descLower = finding.Description.ToLower();
            var catLower = finding.Category.ToLower();
            
            double score = 0;
            int matches = 0;
            
            // Keyword matching
            foreach (var keyword in _masterPromptKeywords)
            {
                if (descLower.Contains(keyword) || catLower.Contains(keyword))
                {
                    score += 0.1;
                    matches++;
                }
            }
            
            // Category bonuses
            var categoryBonus = finding.Category switch
            {
                "TRAINING_CYCLE" => _masterPrompt.ToLower().Contains("train") ? 0.3 : 0.1,
                "NETWORK_DISCOVERY" => _masterPrompt.ToLower().Contains("network") || _masterPrompt.ToLower().Contains("connect") ? 0.3 : 0.1,
                "SELF_IMPROVEMENT" => _masterPrompt.ToLower().Contains("improve") ? 0.4 : 0.2,
                "AUTONOMOUS_TASK" => 0.2,
                _ => 0.1
            };
            
            score += categoryBonus;
            
            // Goal alignment
            foreach (var goal in _masterPromptGoals)
            {
                var goalWords = goal.ToLower().Split(' ').Where(w => w.Length > 3);
                if (goalWords.Any(w => descLower.Contains(w)))
                {
                    score += 0.15;
                }
            }
            
            return Math.Clamp(score, 0, 1);
        }

        /// <summary>
        /// Determines relevance category from alignment score.
        /// </summary>
        private FindingRelevance DetermineRelevance(double alignment, string category)
        {
            // Certain categories get boosted relevance
            if (category == "SELF_IMPROVEMENT" && alignment > 0.3)
                return FindingRelevance.High;
            
            return alignment switch
            {
                >= 0.8 => FindingRelevance.Critical,
                >= 0.6 => FindingRelevance.High,
                >= 0.4 => FindingRelevance.Medium,
                >= 0.2 => FindingRelevance.Low,
                _ => FindingRelevance.Irrelevant
            };
        }

        /// <summary>
        /// Determines what action to take on the finding.
        /// </summary>
        private FindingAction DetermineAction(EvaluatedFinding finding)
        {
            return (finding.Relevance, finding.Category) switch
            {
                (FindingRelevance.Critical, _) => FindingAction.Implement,
                (FindingRelevance.High, "SELF_IMPROVEMENT") => FindingAction.Implement,
                (FindingRelevance.High, _) => FindingAction.Queue,
                (FindingRelevance.Medium, _) => FindingAction.Review,
                (FindingRelevance.Low, _) => FindingAction.Archive,
                (FindingRelevance.Irrelevant, _) => FindingAction.Delete,
                _ => FindingAction.Review
            };
        }

        /// <summary>
        /// Generates human-readable reasoning for the decision.
        /// </summary>
        private string GenerateReasoning(EvaluatedFinding finding)
        {
            var reasons = new List<string>();
            
            if (finding.Relevance == FindingRelevance.Critical)
                reasons.Add("Finding directly supports master prompt objectives");
            
            if (finding.Relevance == FindingRelevance.Irrelevant)
                reasons.Add("Finding does not align with current master prompt direction");
            
            if (finding.Category == "TRAINING_CYCLE")
                reasons.Add($"Training progress: contributes to model improvement");
            
            if (finding.Category == "SELF_IMPROVEMENT")
                reasons.Add($"Self-improvement: potential capability enhancement");
            
            if (finding.AlignmentScore > 0.7)
                reasons.Add($"High alignment score ({finding.AlignmentScore:F2}) with master prompt keywords");
            else if (finding.AlignmentScore < 0.3)
                reasons.Add($"Low alignment ({finding.AlignmentScore:F2}) - may not serve current objectives");
            
            reasons.Add($"Action: {finding.Action} based on {finding.Relevance} relevance");
            
            return string.Join(". ", reasons);
        }

        /// <summary>
        /// Finds specific alignments with master prompt.
        /// </summary>
        private List<string> FindAlignments(string description)
        {
            var alignments = new List<string>();
            var descLower = description.ToLower();
            
            foreach (var goal in _masterPromptGoals)
            {
                var goalWords = goal.ToLower().Split(' ').Where(w => w.Length > 4).ToList();
                var matches = goalWords.Count(w => descLower.Contains(w));
                
                if (matches >= 2 || (goalWords.Count > 0 && matches >= goalWords.Count / 2))
                {
                    alignments.Add($"Aligns with: \"{goal.Trim()}\"");
                }
            }
            
            return alignments;
        }

        /// <summary>
        /// Identifies concerns about implementing the finding.
        /// </summary>
        private List<string> IdentifyConcerns(EvaluatedFinding finding)
        {
            var concerns = new List<string>();
            
            if (finding.AlignmentScore < 0.3)
                concerns.Add("Low alignment may indicate off-track activity");
            
            if (finding.Category == "NETWORK_DISCOVERY" && !_masterPrompt.ToLower().Contains("network"))
                concerns.Add("Network activity not explicitly requested in master prompt");
            
            if (finding.Relevance == FindingRelevance.Irrelevant)
                concerns.Add("Resources may be better allocated to aligned tasks");
            
            return concerns;
        }

        /// <summary>
        /// Processes the evaluated finding based on determined action.
        /// </summary>
        private async Task ProcessEvaluatedFindingAsync(EvaluatedFinding finding)
        {
            switch (finding.Action)
            {
                case FindingAction.Implement:
                    await ImplementUpdateAsync(finding);
                    _currentSession!.Implemented++;
                    _currentSession.KeyDecisions.Add($"IMPLEMENT: {finding.Description}");
                    break;
                    
                case FindingAction.Queue:
                    finding.ProcessingNotes = "Queued for batch implementation";
                    _currentSession!.Queued++;
                    break;
                    
                case FindingAction.Delete:
                    DeleteFinding(finding);
                    _currentSession!.Deleted++;
                    _currentSession.KeyDecisions.Add($"DELETE: {finding.Description} (irrelevant to master prompt)");
                    break;
                    
                case FindingAction.Archive:
                    finding.ProcessingNotes = "Archived for potential future reference";
                    break;
                    
                case FindingAction.Review:
                    finding.ProcessingNotes = "Flagged for manual review";
                    break;
            }
            
            finding.Processed = true;
            SaveEvaluation(finding);
        }

        /// <summary>
        /// Implements an update from a finding.
        /// </summary>
        private async Task ImplementUpdateAsync(EvaluatedFinding finding)
        {
            EmitThought($"◈ Implementing update: {finding.FindingId}");
            EmitThought($"  {finding.Description}");
            
            finding.ProcessingNotes = $"Implemented at {DateTime.UtcNow:HH:mm:ss}";
            
            UpdateImplemented?.Invoke(this, finding);
        }

        /// <summary>
        /// Deletes an irrelevant finding.
        /// </summary>
        private void DeleteFinding(EvaluatedFinding finding)
        {
            EmitThought($"∴ Deleting irrelevant finding: {finding.FindingId}");
            EmitThought($"  Reason: {finding.Reasoning}");
            
            finding.ProcessingNotes = $"Deleted at {DateTime.UtcNow:HH:mm:ss} - not aligned with master prompt";
            
            // Remove evaluation file
            var path = Path.Combine(_evaluationsPath, $"{finding.FindingId}.json");
            if (File.Exists(path))
                File.Delete(path);
            
            FindingDeleted?.Invoke(this, finding);
        }

        /// <summary>
        /// Gets pending findings that need review.
        /// </summary>
        public List<EvaluatedFinding> GetPendingReviews()
        {
            return _evaluatedFindings.Values
                .Where(f => f.Action == FindingAction.Review && !f.Processed)
                .OrderByDescending(f => f.AlignmentScore)
                .ToList();
        }

        /// <summary>
        /// Gets queued implementations.
        /// </summary>
        public List<EvaluatedFinding> GetQueuedImplementations()
        {
            return _evaluatedFindings.Values
                .Where(f => f.Action == FindingAction.Queue)
                .OrderByDescending(f => f.Relevance)
                .ThenByDescending(f => f.AlignmentScore)
                .ToList();
        }

        private bool IsStopWord(string word)
        {
            var stopWords = new HashSet<string> { "the", "and", "for", "that", "this", "with", "from", "have", "been" };
            return stopWords.Contains(word);
        }

        /// <summary>
        /// Checks if a finding is obsolete due to center state changes.
        /// </summary>
        public (bool isObsolete, string reason) CheckIfObsolete(EvaluatedFinding finding)
        {
            // Check for changes that occurred after the finding was created
            var relevantChanges = _stateChanges
                .Where(c => c.Timestamp > finding.OriginalTimestamp)
                .ToList();
            
            if (!relevantChanges.Any())
                return (false, "");
            
            foreach (var change in relevantChanges)
            {
                // Check if category is obsoleted
                if (change.ObsoletesCategories.Contains(finding.Category))
                {
                    return (true, $"Finding category '{finding.Category}' obsoleted by: {change.Description}");
                }
                
                // Check if keywords match obsoleted keywords
                var descLower = finding.Description.ToLower();
                foreach (var keyword in change.ObsoletesKeywords)
                {
                    if (descLower.Contains(keyword.ToLower()))
                    {
                        return (true, $"Finding contains obsoleted keyword '{keyword}': {change.Description}");
                    }
                }
                
                // Master prompt change obsoletes low-alignment findings
                if (change.ChangeType == "MASTER_PROMPT_CHANGE" && finding.AlignmentScore < 0.4)
                {
                    return (true, "Master prompt changed - finding no longer aligned");
                }
                
                // System upgrade obsoletes version-specific findings
                if (change.ChangeType == "SYSTEM_UPGRADE" && 
                    (finding.Category == "SELF_IMPROVEMENT" || finding.Category == "CODE_CHANGE"))
                {
                    return (true, $"System upgraded - finding may be superseded: {change.Description}");
                }
                
                // Capability already implemented
                if (change.ChangeType == "CAPABILITY_IMPLEMENTED")
                {
                    var changeKeywords = change.Description.ToLower().Split(' ');
                    if (changeKeywords.Any(k => k.Length > 4 && descLower.Contains(k)))
                    {
                        return (true, $"Capability already implemented: {change.Description}");
                    }
                }
            }
            
            return (false, "");
        }

        /// <summary>
        /// Processes findings with staleness detection.
        /// </summary>
        public async Task<List<EvaluatedFinding>> ProcessWithStalenessCheckAsync(List<object> rawFindings)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ REASONING WITH STALENESS DETECTION");
            EmitThought($"∿ {_stateChanges.Count} state changes to check against");
            EmitThought("═══════════════════════════════════════════════");
            
            var results = await ProcessNodeFindingsAsync(rawFindings);
            
            // Check each result for staleness
            int obsoleteCount = 0;
            foreach (var finding in results)
            {
                var (isObsolete, reason) = CheckIfObsolete(finding);
                
                if (isObsolete)
                {
                    obsoleteCount++;
                    finding.Relevance = FindingRelevance.Irrelevant;
                    finding.Action = FindingAction.Delete;
                    finding.Concerns.Add($"OBSOLETE: {reason}");
                    finding.ProcessingNotes = $"Marked obsolete: {reason}";
                    
                    EmitThought($"∴ Finding obsolete: {finding.FindingId}");
                    EmitThought($"  Reason: {reason}");
                    
                    FindingObsolete?.Invoke(this, finding);
                    DeleteFinding(finding);
                }
            }
            
            if (obsoleteCount > 0)
            {
                EmitThought($"◎ {obsoleteCount} findings marked obsolete due to center changes");
            }
            
            return results;
        }

        /// <summary>
        /// Records a master prompt change (automatically obsoletes low-alignment findings).
        /// </summary>
        public void RecordMasterPromptChange(string oldPrompt, string newPrompt)
        {
            RecordStateChange(
                "MASTER_PROMPT_CHANGE",
                $"Master prompt updated from '{oldPrompt[..Math.Min(50, oldPrompt.Length)]}...' to '{newPrompt[..Math.Min(50, newPrompt.Length)]}...'",
                obsoletesCategories: null,
                obsoletesKeywords: null
            );
        }

        /// <summary>
        /// Records a system upgrade (obsoletes version-specific findings).
        /// </summary>
        public void RecordSystemUpgrade(string version, string description)
        {
            RecordStateChange(
                "SYSTEM_UPGRADE",
                $"System upgraded to version {version}: {description}",
                obsoletesCategories: new List<string> { "SELF_IMPROVEMENT", "CODE_CHANGE" },
                obsoletesKeywords: null
            );
        }

        /// <summary>
        /// Records when a capability has been implemented (obsoletes related findings).
        /// </summary>
        public void RecordCapabilityImplemented(string capability, List<string> relatedKeywords)
        {
            RecordStateChange(
                "CAPABILITY_IMPLEMENTED",
                $"Capability implemented: {capability}",
                obsoletesCategories: null,
                obsoletesKeywords: relatedKeywords
            );
        }

        /// <summary>
        /// Gets findings that are now obsolete and should be purged.
        /// </summary>
        public List<EvaluatedFinding> GetObsoleteFindings()
        {
            var obsolete = new List<EvaluatedFinding>();
            
            foreach (var finding in _evaluatedFindings.Values)
            {
                var (isObsolete, reason) = CheckIfObsolete(finding);
                if (isObsolete)
                {
                    finding.ProcessingNotes = $"Obsolete: {reason}";
                    obsolete.Add(finding);
                }
            }
            
            return obsolete;
        }

        /// <summary>
        /// Purges all obsolete findings.
        /// </summary>
        public int PurgeObsoleteFindings()
        {
            var obsolete = GetObsoleteFindings();
            
            foreach (var finding in obsolete)
            {
                DeleteFinding(finding);
                _evaluatedFindings.Remove(finding.FindingId);
            }
            
            if (obsolete.Any())
            {
                EmitThought($"◎ Purged {obsolete.Count} obsolete findings");
            }
            
            return obsolete.Count;
        }

        private void LoadMasterPrompt()
        {
            var path = Path.Combine(_basePath, ".master_prompt.txt");
            if (File.Exists(path))
            {
                _masterPrompt = File.ReadAllText(path);
                AnalyzeMasterPrompt();
            }
        }

        private void SaveMasterPrompt()
        {
            var path = Path.Combine(_basePath, ".master_prompt.txt");
            File.WriteAllText(path, _masterPrompt);
        }

        private void LoadEvaluations()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_evaluationsPath, "*.json"))
                {
                    var json = File.ReadAllText(file);
                    var eval = JsonSerializer.Deserialize<EvaluatedFinding>(json);
                    if (eval != null)
                        _evaluatedFindings[eval.FindingId] = eval;
                }
            }
            catch { }
        }

        private void LoadChangeHistory()
        {
            try
            {
                if (File.Exists(_changeHistoryPath))
                {
                    var json = File.ReadAllText(_changeHistoryPath);
                    var changes = JsonSerializer.Deserialize<List<StateChange>>(json);
                    if (changes != null)
                    {
                        _stateChanges.AddRange(changes);
                        EmitThought($"◎ Loaded {_stateChanges.Count} state changes for staleness detection");
                    }
                }
            }
            catch { }
        }

        private void SaveChangeHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(_stateChanges, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_changeHistoryPath, json);
            }
            catch { }
        }

        private void SaveEvaluation(EvaluatedFinding finding)
        {
            var path = Path.Combine(_evaluationsPath, $"{finding.FindingId}.json");
            var json = JsonSerializer.Serialize(finding, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private void SaveSession(ReasoningSession session)
        {
            var path = Path.Combine(_sessionsPath, $"{session.SessionId}.json");
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private void EmitThought(string t) => ConsciousnessEvent?.Invoke(this, t);
    }
}
