/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║              AGENT 3 - CONTINUOUS LEARNING ENGINE                          ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Integrates web research, training, user prompts, and code       ║
 * ║           writing into a continuous autonomous improvement loop           ║
 * ║           in pursuit of the master prompt - never waits for user input    ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Agent3.Interfaces;
using Agent3.NeuralCore;
using Agent3.Learning;

namespace Agent3.Evolution
{
    /// <summary>
    /// Learning directive from prompts or autonomous decisions.
    /// </summary>
    public class LearningDirective
    {
        public string Id { get; set; } = "";
        public DirectiveType Type { get; set; }
        public string Content { get; set; } = "";
        public string Source { get; set; } = ""; // "user", "web", "code", "autonomous"
        public DateTime CreatedAt { get; set; }
        public bool Processed { get; set; }
        public float Priority { get; set; }
    }

    public enum DirectiveType
    {
        TrainingData,
        WebResearch,
        CodeImprovement,
        CapabilityAddition,
        Optimization,
        Analysis,
        MasterPromptUpdate
    }

    /// <summary>
    /// The Continuous Learning Engine operates autonomously, integrating
    /// all learning sources into a seamless improvement loop.
    /// </summary>
    public class ContinuousLearningEngine
    {
        // Core dependencies
        private readonly VersionManager _versionManager;
        private readonly CodeWriter _codeWriter;
        private readonly WebInterface? _webInterface;
        private readonly WebLearningIntegration? _webLearning;
        private readonly CorpusIngestionEngine _corpus;
        private readonly NeuralMind? _neuralMind;
        private readonly SelfModificationEngine _selfMod;
        private readonly Agent3.Network.NetworkCore? _networkCore;
        
        // State
        private readonly Queue<LearningDirective> _directiveQueue;
        private readonly List<LearningDirective> _processedDirectives;
        private readonly Dictionary<string, float> _topicKnowledge; // Topic -> Depth score (0-1)
        private readonly List<string> _strategicRoadmap; // Steps identified by reasoning
        
        private string _masterPrompt = "";
        private CancellationTokenSource? _runCts;
        private Task? _runTask;
        private bool _isRunning;
        private List<string> _consciousnessBuffer = new List<string>();
        
        // Metrics
        private int _cycleCount;
        private int _researchCount;
        private int _codeImprovements;
        private long _tokensLearned;
        private DateTime _startTime;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<LearningDirective>? DirectiveProcessed;
        
        public bool IsRunning => _isRunning;
        public string MasterPrompt => _masterPrompt;
        public int CycleCount => _cycleCount;
        public int QueuedDirectives => _directiveQueue.Count;
        public int ResearchCount => _researchCount;
        public int CodeImprovements => _codeImprovements;
        public long TokensLearned => _tokensLearned;
        
        public ContinuousLearningEngine(
            VersionManager versionManager,
            CodeWriter codeWriter,
            SelfModificationEngine selfMod,
            CorpusIngestionEngine corpus,
            WebInterface? webInterface = null,
            WebLearningIntegration? webLearning = null,
            NeuralMind? neuralMind = null,
            Agent3.Network.NetworkCore? networkCore = null)
        {
            _versionManager = versionManager;
            _codeWriter = codeWriter;
            _selfMod = selfMod;
            _corpus = corpus;
            _webInterface = webInterface;
            _webLearning = webLearning;
            _neuralMind = neuralMind;
            _networkCore = networkCore;
            
            _directiveQueue = new Queue<LearningDirective>();
            _processedDirectives = new List<LearningDirective>();
            _topicKnowledge = new Dictionary<string, float>();
            _strategicRoadmap = new List<string>();
            _consciousnessBuffer = new List<string>();
            
            // Wire consciousness streams
            _versionManager.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _codeWriter.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _selfMod.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            
            EmitThought("⟁ Continuous Learning Engine initialized");
            
            if (_networkCore != null)
            {
               EmitThought("◈ Network Integration: ACTIVE - Consciousness synchronization enabled");
            }
        }
        
        /// <summary>
        /// Sets the master prompt that guides all learning and improvement.
        /// </summary>
        public void SetMasterPrompt(string masterPrompt)
        {
            _masterPrompt = masterPrompt;
            _versionManager.SetMasterPrompt(masterPrompt);
            
            // "Hash" and rehash master prompt knowledge requirements
            // This decomposes the high-level prompt into granular, actionable search vectors
            DecomposeAndQueueRequirements(masterPrompt);
        }
        
        /// <summary>
        /// Decomposes (hashes) the master prompt into extensive search requirements.
        /// </summary>
        /// <summary>
        /// Decomposes (hashes) the master prompt into extensive search requirements.
        /// </summary>
        private void DecomposeAndQueueRequirements(string prompt)
        {
            if (string.IsNullOrEmpty(prompt)) return;
            
            EmitThought($"⟐ Analyzing master directive: \"{prompt.Substring(0, Math.Min(50, prompt.Length))}...\"");
            
            var keywords = ExtractKeywords(prompt);
            
            // Generate semantic breakdown
            // 1. Core Concepts
            foreach(var k in keywords.Take(5)) 
            {
                 QueueDirective(new LearningDirective
                {
                    Type = DirectiveType.WebResearch,
                    Content = $"comprehensive guide to {k} technology",
                    Source = "master_prompt_analysis",
                    Priority = 0.85f
                });
            }

            // 2. Synthesized Questions for Deep Reasoning
            var questions = GenerateReasonedQuestions(prompt);
            foreach(var q in questions)
            {
                QueueDirective(new LearningDirective
                {
                    Type = DirectiveType.WebResearch,
                    Content = q,
                    Source = "strategic_reasoning",
                    Priority = 0.9f
                });
            }
            
            EmitThought($"◈ Strategic Planning: {keywords.Count} core concepts and {questions.Count} synthesized research vectors queued.");
        }

        private List<string> GenerateReasonedQuestions(string prompt)
        {
             // Simulate "Reasoning" by creating common research patterns based on the prompt type
             var questions = new List<string>();
             var lower = prompt.ToLower();

             if (lower.Contains("make") || lower.Contains("create") || lower.Contains("build"))
             {
                 questions.Add($"best practices for building {prompt.Replace("create", "").Replace("make", "").Trim()}");
                 questions.Add($"step by step tutorial {prompt.Replace("create", "").Replace("make", "").Trim()}");
                 questions.Add($"github open source {prompt.Replace("create", "").Replace("make", "").Trim()} implementations");
             }
             else if (lower.Contains("learn") || lower.Contains("study"))
             {
                 questions.Add($"advanced concepts in {prompt.Replace("learn", "").Replace("study", "").Trim()}");
                 questions.Add($"current state of the art {prompt.Replace("learn", "").Replace("study", "").Trim()}");
             }
             else
             {
                 // General complex inquiries
                 questions.Add($"recent breakthroughs in {prompt}");
                 questions.Add($"technical documentation for {prompt}");
                 questions.Add($"{prompt} expert analysis 2024");
             }
             return questions;
        }

        private List<string> ExtractKeywords(string prompt)
        {
            // Remove timestamps like [20:34:01]
            var cleanPrompt = Regex.Replace(prompt, @"\[\d{2}:\d{2}:\d{2}\]", " ");
            
            // Remove other brackets
            cleanPrompt = Regex.Replace(cleanPrompt, @"\[.*?\]|\(.*?\)", " ");
            
            // Allow only letters, numbers, and spaces
            cleanPrompt = Regex.Replace(cleanPrompt, @"[^a-zA-Z0-9\s]", " ");
            
            var ignored = new HashSet<string> { 
                "the", "and", "or", "a", "an", "to", "for", "of", "in", "with", "is", "are", 
                "what", "how", "why", "who", "when", "where", "can", "could", "should", "would",
                "master", "prompt", "goal", "objective", "agent" 
            };
            
            return cleanPrompt.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3 && !ignored.Contains(w) && !char.IsDigit(w[0]))
                .Distinct()
                .ToList();
        }
        
        private List<string> GenerateKeywordCombinations(List<string> keywords)
        {
            var combos = new List<string>();
            for (int i = 0; i < keywords.Count; i++)
            {
                for (int j = i + 1; j < keywords.Count; j++)
                {
                    combos.Add($"{keywords[i]} {keywords[j]}");
                }
            }
            return combos;
        }
        
        private List<string> ExtractTopicsFromPrompt(string prompt)
        {
            var topics = new List<string>();
            
            var patterns = new[]
            {
                @"learn\s+about\s+([^,.!?]+)",
                @"understand\s+([^,.!?]+)",
                @"research\s+([^,.!?]+)",
                @"study\s+([^,.!?]+)",
                @"knowledge\s+of\s+([^,.!?]+)"
            };
            
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(prompt, pattern, RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    topics.Add(m.Groups[1].Value.Trim());
                }
            }
            
            return topics.Distinct().Take(10).ToList();
        }
        
        private List<string> ExtractCapabilitiesFromPrompt(string prompt)
        {
            var capabilities = new List<string>();
            
            var patterns = new[]
            {
                @"able\s+to\s+([^,.!?]+)",
                @"capability\s+to\s+([^,.!?]+)",
                @"can\s+([^,.!?]+)",
                @"should\s+([^,.!?]+)"
            };
            
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(prompt, pattern, RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    capabilities.Add(m.Groups[1].Value.Trim());
                }
            }
            
            return capabilities.Distinct().Take(10).ToList();
        }
        
        /// <summary>
        /// Queues a learning directive for processing.
        /// </summary>
        public void QueueDirective(LearningDirective directive)
        {
            directive.Id = $"DIR_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            directive.CreatedAt = DateTime.UtcNow;
            
            _directiveQueue.Enqueue(directive);
        }
        
        /// <summary>
        /// Processes a user prompt as training data and potential directive.
        /// </summary>
        public void ProcessUserPrompt(string prompt)
        {
            EmitThought($"⟐ Processing user prompt ({prompt.Length} chars)");
            
            // Always ingest as training data
            QueueDirective(new LearningDirective
            {
                Type = DirectiveType.TrainingData,
                Content = prompt,
                Source = "user",
                Priority = 1.0f // Highest priority
            });
            
            // Check for actionable directives
            var lower = prompt.ToLower();
            
            if (lower.Contains("search") || lower.Contains("research") || lower.Contains("find"))
            {
                QueueDirective(new LearningDirective
                {
                    Type = DirectiveType.WebResearch,
                    Content = prompt,
                    Source = "user",
                    Priority = 0.95f
                });
            }
            
            if (lower.Contains("add") || lower.Contains("create") || lower.Contains("implement") || lower.Contains("improve"))
            {
                QueueDirective(new LearningDirective
                {
                    Type = DirectiveType.CodeImprovement,
                    Content = prompt,
                    Source = "user",
                    Priority = 0.95f
                });
            }
            
            if (lower.Contains("master prompt") || lower.Contains("main goal") || lower.Contains("objective is"))
            {
                QueueDirective(new LearningDirective
                {
                    Type = DirectiveType.MasterPromptUpdate,
                    Content = prompt,
                    Source = "user",
                    Priority = 1.0f
                });
            }
        }
        
        /// <summary>
        /// Starts the continuous learning loop.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            _runCts = new CancellationTokenSource();
            _isRunning = true;
            _startTime = DateTime.UtcNow;
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ CONTINUOUS LEARNING ENGINE STARTED");
            EmitThought("◎ Mode: Autonomous improvement toward master prompt");
            EmitThought("◎ Integration: Web + Training + Code Writing");
            EmitThought("◎ No user input required - operating autonomously");
            EmitThought("═══════════════════════════════════════════════");
            
            // Start code writer
            _codeWriter.StartProcessing();
            
            // Start main loop
            _runTask = Task.Run(() => ContinuousLoopAsync(_runCts.Token));
        }
        
        /// <summary>
        /// Stops the continuous learning loop.
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isRunning) return;
            
            EmitThought("⟁ Stopping Continuous Learning Engine...");
            
            _runCts?.Cancel();
            await _codeWriter.StopProcessingAsync();
            
            if (_runTask != null)
            {
                try { await _runTask; } catch (OperationCanceledException) { }
            }
            
            _isRunning = false;
            
            EmitThought("◎ Continuous Learning Engine stopped");
        }
        
        /// <summary>
        /// The main continuous learning loop.
        /// </summary>
        private async Task ContinuousLoopAsync(CancellationToken ct)
        {
            EmitThought($"Learning loop started with {_directiveQueue.Count} directives queued.");
            
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    _cycleCount++;
                    
                    // Phase 1: Process any queued directives (user prompts, etc.)
                    if (_directiveQueue.Count > 0)
                    {
                        EmitThought($"Processing {_directiveQueue.Count} queued directives...");
                    }
                    await ProcessQueuedDirectivesAsync(ct);
                    
                    // Phase 2: Autonomous improvement activities
                    await PerformAutonomousActivityAsync(ct);
                    
                    // Phase 3: Consolidation and self-assessment
                    if (_cycleCount % 10 == 0)
                    {
                        await ConsolidateAndAssessAsync(ct);
                    }
                    
                    // Brief delay between cycles
                    await Task.Delay(2000, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    EmitThought($"Learning cycle error: {ex.Message}");
                    // Log to file for debugging
                    try
                    {
                        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gamma1_errors.log");
                        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ContinuousLoop: {ex}\n\n");
                    }
                    catch { }
                    await Task.Delay(3000, ct);
                }
            }
        }
        
        /// <summary>
        /// Processes all queued directives.
        /// </summary>
        private async Task ProcessQueuedDirectivesAsync(CancellationToken ct)
        {
            // Process directives in priority order
            var toProcess = new List<LearningDirective>();
            while (_directiveQueue.Count > 0 && toProcess.Count < 5)
            {
                toProcess.Add(_directiveQueue.Dequeue());
            }
            
            foreach (var directive in toProcess.OrderByDescending(d => d.Priority))
            {
                if (ct.IsCancellationRequested) break;
                
                await ProcessDirectiveAsync(directive, ct);
            }
        }
        
        /// <summary>
        /// Processes a single directive.
        /// </summary>
        private async Task ProcessDirectiveAsync(LearningDirective directive, CancellationToken ct)
        {
            EmitThought($"⟐ Processing directive: {directive.Type} from {directive.Source}");
            
            try
            {
                switch (directive.Type)
                {
                    case DirectiveType.TrainingData:
                        await ProcessTrainingDataAsync(directive, ct);
                        break;
                        
                    case DirectiveType.WebResearch:
                        await ProcessWebResearchAsync(directive, ct);
                        break;
                        
                    case DirectiveType.CodeImprovement:
                        await ProcessCodeImprovementAsync(directive, ct);
                        break;
                        
                    case DirectiveType.CapabilityAddition:
                        await ProcessCapabilityAdditionAsync(directive, ct);
                        break;
                        
                    case DirectiveType.MasterPromptUpdate:
                        ProcessMasterPromptUpdate(directive);
                        break;
                        
                    case DirectiveType.Analysis:
                        await ProcessAnalysisAsync(directive, ct);
                        break;
                }
                
                directive.Processed = true;
                _processedDirectives.Add(directive);
                DirectiveProcessed?.Invoke(this, directive);
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Directive processing error: {ex.Message}");
            }
        }
        
        private async Task ProcessTrainingDataAsync(LearningDirective directive, CancellationToken ct)
        {
            EmitThought("∿ Ingesting training data...");
            
            await _corpus.IngestTextAsync(directive.Content, directive.Source);
            
            var stats = _corpus.GetStatistics();
            _tokensLearned = stats.TotalTokens;
            
            EmitThought($"◈ Training data ingested ({stats.TotalTokens:N0} total tokens)");
        }
        
        private async Task ProcessWebResearchAsync(LearningDirective directive, CancellationToken ct)
        {
            if (_webLearning == null)
            {
                EmitThought("Web learning module not initialized - cannot research.");
                return;
            }
            
            EmitThought($"Starting research: {directive.Content}");
            
            try
            {
                var result = await _webLearning.SearchAndLearnAboutAsync(directive.Content, 3, ct);
                
                _researchCount++;
                _tokensLearned += result.TokensIngested;
                
                // Track topic knowledge
                var topic = directive.Content.ToLower().Split(' ').FirstOrDefault() ?? "general";
                _topicKnowledge[topic] = (_topicKnowledge.GetValueOrDefault(topic, 0) + result.TokensIngested) / 1000f;
                
                EmitThought($"Research complete: {result.PagesProcessed} pages, {result.TokensIngested:N0} tokens from '{directive.Content}'");
            }
            catch (Exception ex)
            {
                EmitThought($"Research failed for '{directive.Content}': {ex.Message}");
                // Log to error file
                try
                {
                    var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gamma1_errors.log");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] WebResearch: {ex}\n\n");
                }
                catch { }
            }
        }
        
        private async Task ProcessCodeImprovementAsync(LearningDirective directive, CancellationToken ct)
        {
            EmitThought("∿ Processing code improvement request...");
            
            // Analyze the request
            var lower = directive.Content.ToLower();
            
            // 1. Explicit capability addition
            if (lower.Contains("add") && (lower.Contains("method") || lower.Contains("function") || lower.Contains("capability")))
            {
                _codeWriter.AddCapabilityFromPrompt(directive.Content);
                _codeImprovements++;
            }
            // 2. "Improve" requests - Map to concrete module creation/update
            else if (lower.Contains("improve") || lower.Contains("enhance") || lower.Contains("upgrade"))
            {
                // Extract what to improve
                var feature = ExtractFeatureName(directive.Content);
                var className = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(feature) + "Module";
                className = Regex.Replace(className, @"\s+", ""); // Remove spaces
                
                EmitThought($"◈ Interpreting intent: Create/Update '{className}' to enhance {feature}");
                
                // Queue a comprehensive update
                _codeWriter.QueueRequest(new CodeGenerationRequest
                {
                    Type = CodeGenerationType.NewFile, // Create a new dedicated module for this improvement
                    TargetFile = $"Agent3/Systems/{className}.cs",
                    TargetClass = className,
                    Name = className,
                    Description = $"Implements enhanced {feature} capabilities as requested by user. Includes logic for {feature} processing and integration.",
                    Parameters = new Dictionary<string, string> 
                    { 
                        { "namespace", "Agent3.Systems" },
                        { "feature", feature }
                    }
                });
                
                _codeImprovements++;
            }
            // 3. Optimization
            else if (lower.Contains("optimize"))
            {
                _codeWriter.QueueRequest(new CodeGenerationRequest
                {
                    Type = CodeGenerationType.OptimizeCode,
                    TargetFile = "Agent3/Agent3Core.cs",
                    TargetClass = "Agent3Core",
                    Name = "Optimized" + DateTime.UtcNow.Ticks % 1000,
                    Description = directive.Content
                });
                _codeImprovements++;
            }
            
            await Task.CompletedTask;
        }

        private async Task ProcessCapabilityAdditionAsync(LearningDirective directive, CancellationToken ct)
        {
            EmitThought("∿ Processing capability addition request...");
            
            var capabilityName = ExtractCapabilityName(directive.Content);
            
            EmitThought($"◈ Adding new capability: {capabilityName}");
            
            _codeWriter.QueueRequest(new CodeGenerationRequest
            {
                Type = CodeGenerationType.AddCapability,
                TargetFile = "Agent3/Agent3Core.cs",
                TargetClass = "Agent3Core",
                Name = capabilityName,
                Description = directive.Content,
                Parameters = new Dictionary<string, string>
                {
                    { "capability_type", "new" },
                    { "source", directive.Source }
                }
            });
            
            _codeImprovements++;
            EmitThought($"◈ Capability '{capabilityName}' queued for implementation");
            
            await Task.CompletedTask;
        }

        private string ExtractFeatureName(string content)
        {
            // "Improve the reasoning capabilities" -> "reasoning capabilities"
            var match = Regex.Match(content, @"(?:improve|enhance|upgrade)\s+(?:the\s+)?(.*?)(?:of\s+.*)?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var feature = match.Groups[1].Value.Trim();
                // cleanup
                feature = Regex.Replace(feature, @"[!.]", "");
                if (feature.Length > 20) feature = feature.Substring(0, 20); // Cap length
                return feature;
            }
            return "CoreSystems";
        }
        
        private string ExtractCapabilityName(string content)
        {
            var words = content.Split(' ')
                .Where(w => w.Length > 3 && char.IsLetter(w[0]))
                .Take(3)
                .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower());
            
            return string.Join("", words);
        }
        
        private void ProcessMasterPromptUpdate(LearningDirective directive)
        {
            // Extract and set new master prompt
            var content = directive.Content;
            
            // Try to extract the actual prompt
            var match = Regex.Match(content, @"(?:master\s+prompt|objective|goal)\s*(?:is|:)\s*(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                content = match.Groups[1].Value.Trim();
            }
            
            SetMasterPrompt(content);
        }
        
        private async Task ProcessAnalysisAsync(LearningDirective directive, CancellationToken ct)
        {
            EmitThought("∿ Analyzing codebase...");
            
            var analyses = await _selfMod.AnalyzeCodebaseAsync();
            
            foreach (var analysis in analyses.Where(a => a.ImprovementOpportunities.Count > 0).Take(3))
            {
                EmitThought($"∿ Opportunity in {analysis.FileName}: {analysis.ImprovementOpportunities.First()}");
            }
        }
        
        /// <summary>
        /// Performs autonomous improvement activities when no directives are queued.
        /// </summary>
        private async Task PerformAutonomousActivityAsync(CancellationToken ct)
        {
            // Always try to keep the queue fed with work
            if (_directiveQueue.Count > 10) return; // Only pause if heavily backlogged
            
            // Rotate through different improvement strategies each cycle
            var strategy = _cycleCount % 5;
            
            switch (strategy)
            {
                case 0:
                    // Strategic gap analysis
                    await PerformStrategicResearchAsync();
                    break;
                    
                case 1:
                    // Deepen existing knowledge
                    PerformKnowledgeDeepeningResearch();
                    break;
                    
                case 2:
                    // Exploratory research
                    PerformExploratoryResearch();
                    break;
                    
                case 3:
                    // Generate self-training data and consolidate
                    await PerformSelfTrainingAsync();
                    break;
                    
                case 4:
                    // Look for code improvement opportunities
                    PerformCodeAnalysis();
                    break;
            }
            
            await Task.CompletedTask;
        }
        
        private async Task PerformStrategicResearchAsync()
        {
            string nextFocus = await DetermineStrategicFocusAsync();
            
            if (!string.IsNullOrEmpty(nextFocus))
            {
                EmitThought($"Identified knowledge gap: {nextFocus}");
                
                QueueDirective(new LearningDirective
                {
                    Type = DirectiveType.WebResearch,
                    Content = $"comprehensive technical documentation and implementation details for {nextFocus}",
                    Source = "strategic_reasoning_engine",
                    Priority = 0.9f
                });
            }
        }
        
        private void PerformKnowledgeDeepeningResearch()
        {
            if (_topicKnowledge.Count == 0)
            {
                PerformExploratoryResearch();
                return;
            }
            
            // Find the topic we know least about and deepen it
            var shallowTopic = _topicKnowledge.OrderBy(k => k.Value).First().Key;
            
            EmitThought($"Deepening knowledge on: {shallowTopic}");
            
            QueueDirective(new LearningDirective
            {
                Type = DirectiveType.WebResearch,
                Content = $"advanced techniques and best practices for {shallowTopic}",
                Source = "knowledge_deepening",
                Priority = 0.7f
            });
        }
        
        private async Task PerformSelfTrainingAsync()
        {
            var trainingData = GenerateSelfTrainingData();
            
            if (!string.IsNullOrEmpty(trainingData) && _corpus != null)
            {
                await _corpus.IngestTextAsync(trainingData, "self_generated_training");
                EmitThought($"Generated and ingested self-training data (cycle {_cycleCount})");
            }
        }
        
        private void PerformCodeAnalysis()
        {
            if (string.IsNullOrEmpty(_masterPrompt)) return;
            
            var keywords = ExtractKeywords(_masterPrompt).Take(3);
            var focus = keywords.FirstOrDefault() ?? "optimization";
            
            EmitThought($"Analyzing code improvement opportunities for: {focus}");
            
            QueueDirective(new LearningDirective
            {
                Type = DirectiveType.CodeImprovement,
                Content = $"Analyze and improve code related to {focus}",
                Source = "autonomous_code_analysis",
                Priority = 0.5f
            });
        }

        private async Task<string> DetermineStrategicFocusAsync()
        {
            if (string.IsNullOrEmpty(_masterPrompt)) return "autonomous agent fundamentals";

            // 1. Identify Key Concepts from Prompt
            var concepts = ExtractKeywords(_masterPrompt);
            
            // 2. Evaluate Knowledge Depth for each
            var unknownConcepts = new List<string>();
            foreach (var concept in concepts)
            {
                if (!_topicKnowledge.ContainsKey(concept) || _topicKnowledge[concept] < 0.3f)
                {
                    unknownConcepts.Add(concept);
                }
            }
            
            if (unknownConcepts.Count > 0)
            {
                // Return the first unknown concept as priority
                EmitThought($"⟐ Knowledge Gap Detected: Insufficient data on [{string.Join(", ", unknownConcepts)}]");
                return unknownConcepts[0];
            }
            
            // 3. If all base concepts known, ask Neural Mind for "Next Step" reasoning
            // (Simulating high-level reasoning capability)
            if (_neuralMind != null)
            {
                var context = $"Goal: {_masterPrompt}. I have learned about: {string.Join(", ", _topicKnowledge.Keys)}.";
                EmitThought("⟐ Neural Mind Reasoning: Analyzing prerequisite chains...");
                // In a real implementation, we'd await _neuralMind.ReasonAsync(context);
                // For now, we simulate the outcome of that reasoning:
                
                return "advanced integration patterns for " + concepts.FirstOrDefault();
            }
            
            return "";
        }
        
        private void PerformExploratoryResearch()
        {
             string topic = "emerging autonomous agent architectures";
             if (_topicKnowledge.Any())
             {
                 // Deepen existing knowledge
                 var shallowTopic = _topicKnowledge.OrderBy(k => k.Value).First().Key;
                 topic = $"advanced optimization techniques for {shallowTopic}";
             }
             
             QueueDirective(new LearningDirective
             {
                 Type = DirectiveType.WebResearch,
                 Content = topic,
                 Source = "exploratory_subroutine",
                 Priority = 0.5f 
             });
        }
        
        private string GenerateSelfTrainingData()
        {
            // Generate training data about what we've learned
            var sb = new StringBuilder();
            
            sb.AppendLine($"Autonomous learning report - Cycle {_cycleCount}");
            sb.AppendLine($"Total tokens learned: {_tokensLearned}");
            sb.AppendLine($"Code improvements made: {_codeImprovements}");
            sb.AppendLine($"Research sessions: {_researchCount}");
            
            if (_topicKnowledge.Count > 0)
            {
                sb.AppendLine("Topic knowledge:");
                foreach (var (topic, score) in _topicKnowledge.OrderByDescending(kv => kv.Value).Take(5))
                {
                    sb.AppendLine($"  - {topic}: {score:F1}");
                }
            }
            
            if (!string.IsNullOrEmpty(_masterPrompt))
            {
                sb.AppendLine($"Master prompt: {_masterPrompt}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Consolidates learning and performs self-assessment.
        /// </summary>
        private async Task ConsolidateAndAssessAsync(CancellationToken ct)
        {
            var elapsed = DateTime.UtcNow - _startTime;
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ SELF-ASSESSMENT REPORT");
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"∿ Runtime: {elapsed.TotalMinutes:F1} minutes");
            EmitThought($"∿ Autonomous cycles: {_cycleCount}");
            EmitThought($"∿ Directives processed: {_processedDirectives.Count}");
            EmitThought($"∿ Web research sessions: {_researchCount}");
            EmitThought($"∿ Code improvements: {_codeImprovements}");
            EmitThought($"∿ Tokens learned: {_tokensLearned:N0}");
            
            var corpusStats = _corpus.GetStatistics();
            EmitThought($"∿ Vocabulary size: {corpusStats.VocabularySize:N0}");
            EmitThought($"∿ Principles extracted: {corpusStats.ExtractedPrinciples}");
            
            // Sync with network if active
            if (_networkCore != null)
            {
                List<string> logsToSync;
                lock (_consciousnessBuffer)
                {
                    logsToSync = new List<string>(_consciousnessBuffer);
                    _consciousnessBuffer.Clear();
                }
                
                if (logsToSync.Count > 0)
                {
                    await _networkCore.BroadcastConsciousnessAsync(logsToSync);
                }
            }
            else
            {
                lock (_consciousnessBuffer)
                {
                    _consciousnessBuffer.Clear();
                }
            }
            
            // Report on master prompt progress
            if (!string.IsNullOrEmpty(_masterPrompt))
            {
                var progress = CalculateMasterPromptProgress();
                EmitThought($"∿ Master prompt progress: {progress:P0}");
            }
            
            EmitThought("═══════════════════════════════════════════════");
            
            // Create periodic snapshot
            if (_cycleCount % 50 == 0)
            {
                await _versionManager.CreateSnapshotAsync($"Auto-snapshot at cycle {_cycleCount}");
            }
        }
        
        private void EmitThought(string thought)
        {
            if (string.IsNullOrWhiteSpace(thought)) return;
            
            // Buffer for network sync
            lock(_consciousnessBuffer)
            {
                 _consciousnessBuffer.Add($"[{DateTime.UtcNow:O}] {thought}");
                 // Prevent buffer overflow if no sync
                 if (_consciousnessBuffer.Count > 1000) _consciousnessBuffer.RemoveAt(0); 
            }
            
            ConsciousnessEvent?.Invoke(this, thought);
        }
        
        private float CalculateMasterPromptProgress()
        {
            // Heuristic: based on activities completed
            float progress = 0f;
            
            if (_researchCount > 0) progress += Math.Min(0.3f, _researchCount * 0.05f);
            if (_codeImprovements > 0) progress += Math.Min(0.3f, _codeImprovements * 0.1f);
            if (_tokensLearned > 0) progress += Math.Min(0.2f, _tokensLearned / 50000f);
            if (_processedDirectives.Count > 0) progress += Math.Min(0.2f, _processedDirectives.Count * 0.02f);
            
            return Math.Min(1.0f, progress);
        }
        

    }
}
