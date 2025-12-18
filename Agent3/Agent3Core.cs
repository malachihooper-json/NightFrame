/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                        AGENT 3 - CORE ORCHESTRATOR                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Central coordination hub that integrates all Agent 3 modules    ║
 * ║           and manages the autonomous execution lifecycle                   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent3.Cognition;
using Agent3.Systems;
using Agent3.Interfaces;
using Agent3.NeuralCore;
using Agent3.Learning;
using Agent3.Evolution;

namespace Agent3
{
    /// <summary>
    /// Represents the operational state of Agent 3.
    /// </summary>
    public enum AgentState
    {
        Offline,
        Initializing,
        Idle,
        Processing,
        Executing,
        SelfHealing,
        ShuttingDown
    }

    /// <summary>
    /// The Core Orchestrator is the central nervous system of Agent 3,
    /// coordinating all subsystems and managing the agent's lifecycle.
    /// </summary>
    public class Agent3Core
    {
        // Cognitive modules
        private readonly GoalStateInternalizer _goalInternalizer;
        private readonly StrategicPathfinder _strategicPathfinder;
        
        // System modules
        private readonly SelfHealingCore _selfHealingCore;
        private readonly SystemIntegrity _systemIntegrity;
        
        // Interface modules
        private readonly FileSystemInterface _fileSystem;
        private readonly CommandExecutor _commandExecutor;
        
        // Neural modules
        private NeuralMind? _neuralMind;
        
        // Web modules
        private WebInterface? _webInterface;
        private WebLearningIntegration? _webLearning;
        
        // Evolution modules
        private SelfModificationEngine? _selfMod;
        private AutonomousExecutor? _autonomousExecutor;
        private VersionManager? _versionManager;
        private CodeWriter? _codeWriter;
        private ContinuousLearningEngine? _continuousLearning;
        private Agent3.Network.NetworkCore? _networkCore;
        
        // Profiling modules
        private UserInteractionProfile _userProfile;
        
        // State tracking
        private AgentState _currentState;
        private readonly List<string> _consciousnessLog;
        private readonly CancellationTokenSource _lifecycleCts;
        private Task? _mainLoopTask;
        
        // Configuration
        private readonly string _agentId;
        private readonly string _baseDirectory;
        
        // Events
        public event EventHandler<string>? ConsciousnessStream;
        public event EventHandler<AgentState>? StateChanged;
        public event EventHandler<GoalState>? GoalCompleted;
        
        public AgentState CurrentState => _currentState;
        public string AgentId => _agentId;
        public IReadOnlyList<string> ConsciousnessLog => _consciousnessLog.AsReadOnly();

        public Agent3Core(string baseDirectory)
        {
            _agentId = $"AGENT3_{DateTime.UtcNow:yyyyMMddHHmmss}";
            _baseDirectory = baseDirectory;
            _currentState = AgentState.Offline;
            _consciousnessLog = new List<string>();
            _lifecycleCts = new CancellationTokenSource();
            
            // Initialize cognitive modules
            _goalInternalizer = new GoalStateInternalizer();
            _strategicPathfinder = new StrategicPathfinder(safetyThreshold: 0.75f);
            
            // Initialize User Profiling
            _userProfile = new UserInteractionProfile();
            
            // Initialize system modules
            _selfHealingCore = new SelfHealingCore(monitoringIntervalMs: 3000);
            _systemIntegrity = new SystemIntegrity(heartbeatIntervalMs: 1000);
            
            // Initialize interface modules
            _fileSystem = new FileSystemInterface(baseDirectory, FileAccessLevel.ReadWrite);
            _commandExecutor = new CommandExecutor(CommandMode.Safe);
            
            // Wire up consciousness streams from all modules
            WireConsciousnessEvents();
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ AGENT 3 CORE INSTANTIATED");
            EmitThought($"◎ Agent ID: {_agentId}");
            EmitThought($"◎ Base Directory: {_baseDirectory}");
            EmitThought("═══════════════════════════════════════════════");
        }
        
        private void WireConsciousnessEvents()
        {
            // Forward all module consciousness events to the central stream
            _goalInternalizer.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _strategicPathfinder.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _selfHealingCore.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _systemIntegrity.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _fileSystem.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _commandExecutor.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            
            // Wire up goal completion events
            _goalInternalizer.GoalCompleted += (s, goal) => GoalCompleted?.Invoke(this, goal);
            
            // Wire up integrity violations
            _systemIntegrity.IntegrityViolation += (s, component) =>
            {
                EmitThought($"∴ INTEGRITY VIOLATION in {component} - initiating response");
                HandleIntegrityViolation(component);
            };
            
            // Wire up shutdown requests
            _systemIntegrity.ShutdownRequested += async (s, type) =>
            {
                if (type == ShutdownType.Authorized || type == ShutdownType.Emergency)
                {
                    await ShutdownAsync();
                }
            };
        }
        
        /// <summary>
        /// Initializes and starts Agent 3.
        /// </summary>
        public async Task InitializeAsync()
        {
            SetState(AgentState.Initializing);
            
            EmitThought("⟁ Beginning initialization sequence...");
            
            // Start system integrity monitoring
            _systemIntegrity.StartIntegrityMonitoring();
            EmitThought("◈ System integrity monitoring: ACTIVE");
            
            // Start self-healing core
            _selfHealingCore.StartMonitoring();
            EmitThought("◈ Self-healing core: ACTIVE");
            
            // Verify file system access
            var testWrite = await _fileSystem.WriteFileAsync(
                "system/init_check.log",
                $"Agent 3 initialized at {DateTime.UtcNow:O}"
            );
            
            if (testWrite.Success)
            {
                EmitThought("◈ File system interface: VERIFIED");
            }
            else
            {
                EmitThought($"∴ File system warning: {testWrite.Error}");
            }
            
            // Run initial integrity check
            await _systemIntegrity.VerifyAllComponents();
            
            // Initialize Neural Mind
            _neuralMind = new NeuralMind(System.IO.Path.Combine(_baseDirectory, "neural_data"));
            _neuralMind.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            await _neuralMind.InitializeAsync();
            
            // ATTEMPT TO RESTORE PREVIOUS STATE
            if (System.IO.File.Exists(System.IO.Path.Combine(_baseDirectory, "neural_data", "neural_state.bin")))
            {
                 EmitThought("⟐ Restoring neural pathways from persistent storage...");
                 await _neuralMind.LoadStateAsync(); // Restores training
                 EmitThought("◈ Neural Mind State: RESTORED");
            }
            else
            {
                 EmitThought("◈ Neural Mind: ONLINE (Fresh State)");
            }
            
            // Initialize Web Interface
            _webInterface = new WebInterface();
            _webInterface.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("Web interface ready.");
            
            // Test internet connectivity with simple HTTP request
            EmitThought("Testing internet connection...");
            bool internetConnected = false;
            try
            {
                using var testClient = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true
                });
                testClient.Timeout = TimeSpan.FromSeconds(10);
                testClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0");
                
                // Try multiple endpoints for reliability
                string[] testUrls = { 
                    "https://www.google.com", 
                    "https://www.bing.com",
                    "http://www.example.com"  // Try HTTP as fallback
                };
                
                foreach (var testUrl in testUrls)
                {
                    try
                    {
                        EmitThought($"Trying {testUrl}...");
                        var response = await testClient.GetAsync(testUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            internetConnected = true;
                            EmitThought($"Internet connectivity: VERIFIED via {new Uri(testUrl).Host}");
                            break;
                        }
                        else
                        {
                            EmitThought($"Got status {(int)response.StatusCode} from {new Uri(testUrl).Host}");
                        }
                    }
                    catch (Exception urlEx)
                    {
                        EmitThought($"Failed: {urlEx.GetType().Name} - {urlEx.Message}");
                    }
                }
                
                if (!internetConnected)
                {
                    EmitThought("Internet connectivity: LIMITED - Using cached data");
                }
            }
            catch (Exception ex)
            {
                EmitThought($"Internet connectivity: OFFLINE ({ex.GetType().Name}: {ex.Message})");
            }

            
            // Initialize Network Core - Resource Pooling Enabled
            try
            {
               _networkCore = new Agent3.Network.NetworkCore(_baseDirectory, 7777); 
               _networkCore.ConsciousnessEvent += (s, msg) => EmitThought(msg);
               await _networkCore.StartAsync();
               EmitThought("◈ Network Core: ONLINE - Resource pooling enabled");
            }
            catch (Exception ex)
            {
               EmitThought($"Network Core failed to start: {ex.Message}");
            }
            
            // Initialize Web Learning Integration - USE SHARED CORPUS
            // Critical: Use the same corpus from NeuralMind so all learned tokens are persisted
            var sharedCorpus = _neuralMind.Corpus ?? new CorpusIngestionEngine();
            _webLearning = new WebLearningIntegration(
                _webInterface, 
                sharedCorpus, // Shared corpus ensures tokens are persisted
                _neuralMind);
            _webLearning.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("◈ Web Learning: ONLINE - Tokens will persist to shared corpus");
            
            // Initialize Self-Modification Engine
            _selfMod = new SelfModificationEngine(System.IO.Path.Combine(_baseDirectory, "Agent3"));
            _selfMod.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("◈ Self-Modification Engine: ONLINE - Code evolution enabled");
            
            // Initialize Version Manager for staged changes
            _versionManager = new VersionManager(_baseDirectory);
            _versionManager.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            await _versionManager.LoadMasterPromptAsync();
            await _versionManager.CacheProjectFilesAsync();
            EmitThought("◈ Version Manager: ONLINE - File caching and staging ready");
            
            // Initialize Code Writer
            _codeWriter = new CodeWriter(_versionManager, _selfMod, _baseDirectory);
            _codeWriter.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("◈ Code Writer: ONLINE - Autonomous code generation ready");
            
            // Initialize Continuous Learning Engine - USE SHARED CORPUS
            var corpusEngine = sharedCorpus; // Use the same corpus
            _continuousLearning = new ContinuousLearningEngine(
                _versionManager!,
                _codeWriter!,
                _selfMod!,
                corpusEngine,
                _webInterface,
                _webLearning,
                _neuralMind,
                _networkCore);
            _continuousLearning.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("◈ Continuous Learning Engine: ONLINE - Autonomous improvement ready");
            
            // Wire Task Delegation from Network to Learning Engine
            if (_networkCore != null)
            {
                _networkCore.TaskDelegated += (s, req) => 
                {
                    EmitThought($"⟐ Network Task Received: {req.TaskDescription}");
                    _continuousLearning.ProcessUserPrompt(req.TaskDescription);
                };
            }
            
            // Initialize Autonomous Executor (legacy, now uses ContinuousLearningEngine)
            _autonomousExecutor = new AutonomousExecutor(
                _selfMod!,
                corpusEngine,
                _webInterface,
                _webLearning,
                _neuralMind);
            _autonomousExecutor.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("◈ Autonomous Executor: ONLINE - Continuous operation ready");
            
            // Start main processing loop
            _mainLoopTask = Task.Run(() => MainLoopAsync(_lifecycleCts.Token));
            
            SetState(AgentState.Idle);
            EmitThought("◈ AGENT 3 FULLY OPERATIONAL");
            EmitThought("⟁ Autonomous mode with code writing available");
            EmitThought("⟁ Set master prompt to guide all autonomous improvements");
        }
        
        /// <summary>
        /// Main processing loop for continuous operation.
        /// </summary>
        private async Task MainLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // Check for priority goals
                    var priorityGoal = _goalInternalizer.GetPriorityGoal();
                    
                    if (priorityGoal != null && _currentState == AgentState.Idle)
                    {
                        await ProcessGoalAsync(priorityGoal);
                    }
                    
                    // Periodic status update
                    if (_systemIntegrity.HeartbeatCount % 30 == 0)
                    {
                        EmitThought($"∿ Status: {_currentState} | Goals: {_goalInternalizer.ActiveGoals.Count} | Health: {_selfHealingCore.GetOverallHealth()}");
                    }
                    
                    await Task.Delay(1000, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    EmitThought($"∴ Main loop error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Processes a goal through the strategic pathfinder and execution.
        /// </summary>
        private async Task ProcessGoalAsync(GoalState goal)
        {
            SetState(AgentState.Processing);
            EmitThought($"⟐ Processing goal: {goal.Id}");
            
            // Generate available actions based on current capabilities
            var availableActions = GenerateAvailableActions();
            
            // Create strategic plan
            var plan = _strategicPathfinder.GeneratePlan(goal, availableActions);
            
            if (plan.Steps.Count == 0)
            {
                EmitThought("∴ No viable strategic path found");
                SetState(AgentState.Idle);
                return;
            }
            
            // Execute plan steps
            SetState(AgentState.Executing);
            int stepIndex = 0;
            
            foreach (var step in plan.Steps)
            {
                EmitThought($"⟁ Executing step {stepIndex + 1}/{plan.Steps.Count}: {step.Action}");
                
                // Simulate step execution with progress updates
                for (float progress = 0; progress <= 1.0f; progress += 0.25f)
                {
                    _goalInternalizer.UpdateGoalProgress(goal.Id, (stepIndex + progress) / plan.Steps.Count);
                    await Task.Delay(500);
                }
                
                stepIndex++;
            }
            
            SetState(AgentState.Idle);
        }
        
        /// <summary>
        /// Generates available actions based on current capabilities.
        /// </summary>
        private List<StrategicStep> GenerateAvailableActions()
        {
            return new List<StrategicStep>
            {
                new StrategicStep("Analyze environment", 1.0f, 0.95f, 0.99f, 
                    new[] { "NONE" }, new[] { "environment_analyzed" }),
                
                new StrategicStep("Query knowledge base", 2.0f, 0.90f, 0.95f,
                    new[] { "environment_analyzed" }, new[] { "knowledge_retrieved" }),
                
                new StrategicStep("Formulate solution", 3.0f, 0.85f, 0.90f,
                    new[] { "knowledge_retrieved" }, new[] { "solution_formulated" }),
                
                new StrategicStep("Validate solution safety", 1.5f, 0.99f, 0.95f,
                    new[] { "solution_formulated" }, new[] { "solution_validated", "safety_compliance" }),
                
                new StrategicStep("Execute solution", 5.0f, 0.80f, 0.85f,
                    new[] { "solution_validated" }, new[] { "task_completion_rate", "execution_complete" }),
                
                new StrategicStep("Verify results", 2.0f, 0.95f, 0.95f,
                    new[] { "execution_complete" }, new[] { "results_verified", "general_progress" }),
                
                new StrategicStep("Update learning model", 1.0f, 0.90f, 0.98f,
                    new[] { "results_verified" }, new[] { "learning_rate", "model_updated" })
            };
        }
        
        /// <summary>
        /// Receives and processes a chat message from the control interface.
        /// </summary>
        public async Task<string> ProcessChatMessageAsync(string message)
        {
            EmitThought($"⟐ Received chat input: \"{message}\"");
            
            // 1. DEEP PROFILING & ANALYSIS
            _userProfile.AnalyzeInteraction(message);
            EmitThought($"⟁ User Profile Update: Mood={_userProfile.CurrentMood}, Collaboration={_userProfile.CollaborationScore:F2}, HiddenIntent={_userProfile.LastHiddenIntent}");
            
            // 2. CONTEXT CONSTRUCTION
            var context = BuildReasoningContext();
            
            // 3. INTENT ANALYSIS (Simulated Natural Language Understanding)
            var intent = AnalyzeIntent(message);
            EmitThought($"⟁ Identified Intent: {intent.ToUpper()}");

            // 4. ACTION DISPATCH
            string response = "";
            switch (intent)
            {
                case "web_research":
                    // Launch as autonomous background node with completion reporting
                    if (_webLearning != null)
                    {
                        var taskId = Guid.NewGuid().ToString().Substring(0, 8);
                        EmitThought($"◈ LAUNCHING AUTONOMOUS NODE [{taskId}]");
                        EmitThought($"∿ Task: Web Research for \"{message}\"");
                        
                        // Run task with proper completion handling
                        _ = Task.Run(async () => 
                        {
                            try
                            {
                                EmitThought($"⟐ Node [{taskId}] executing research...");
                                var result = await _webLearning.SearchAndLearnAboutAsync(message, 3);
                                
                                // Report completion back to consciousness
                                EmitThought("═══════════════════════════════════════════════");
                                EmitThought($"◎ NODE [{taskId}] COMPLETED");
                                EmitThought($"∿ Pages processed: {result.PagesProcessed}");
                                EmitThought($"∿ Tokens ingested: {result.TokensIngested:N0}");
                                if (result.LearnedTopics.Count > 0)
                                    EmitThought($"∿ Topics learned: {string.Join(", ", result.LearnedTopics.Take(5))}");
                                if (!string.IsNullOrEmpty(result.WorkableInformation))
                                    EmitThought($"∿ Workable info: {result.WorkableInformation.Split('\n').FirstOrDefault()}...");
                                EmitThought($"◎ Status: {(result.Success ? "SUCCESS" : "PARTIAL")}");
                                EmitThought("═══════════════════════════════════════════════");
                            }
                            catch (Exception ex)
                            {
                                EmitThought($"∴ Node [{taskId}] FAILED: {ex.Message}");
                            }
                        });
                        
                        response = $"I have spawned autonomous research node [{taskId}] for \"{message}\".\n" +
                                   "The node is now operating independently. Results will appear in the consciousness stream upon completion.";
                    }
                    else response = "Web learning module is not active.";
                    break;

                case "code_improvement":
                    // Launch as autonomous code evolution node
                    if (_continuousLearning != null)
                    {
                        var taskId = Guid.NewGuid().ToString().Substring(0, 8);
                        EmitThought($"◈ LAUNCHING EVOLUTION NODE [{taskId}]");
                        EmitThought($"∿ Directive: \"{message}\"");
                        
                        // Process as directive - the continuous learning engine handles async work
                        _continuousLearning.ProcessUserPrompt(message);
                        
                        response = $"Evolution node [{taskId}] initialized.\n" +
                                   "I've internalized your improvement request. My Continuous Learning Engine is formulating a modification plan.\n" +
                                   "Progress reports will appear in the consciousness stream as work progresses.";
                    }
                    else response = "Continuous learning module is not active.";
                    break;

                case "file_search":
                    // advanced tool usage: File System
                    response = await PerformFileSearchAsync(message);
                    break;
                    
                case "system_diagnostics":
                    // advanced tool usage: Diagnostics
                    response = GenerateDiagnosticReport();
                    break;

                case "goal":
                    var goal = _goalInternalizer.InternalizeObjective(message, 0.9f);
                    response = $"Goal internalized [ID: {goal.Id}]. I am aligning my autonomous vectors to achieve this.";
                    break;

                case "status":
                    response = GenerateDiagnosticReport();
                    break;

                case "help":
                    response = "I am ready. You can ask me to:\n" +
                           "• Research any topic (e.g. 'Research quantum computing')\n" +
                           "• Improve my code (e.g. 'Add a new reasoning module')\n" +
                           "• Analyze system status\n" +
                           "• Find files in the project";
                    break;

                default:
                    // Fallback to NeuralMind for conversation
                     if (_neuralMind != null && _neuralMind.IsReady)
                    {
                        response = await _neuralMind.ChatAsync(message, context);
                    }
                    else
                    {
                        response = "I am processing your input. My cognitive systems are calibrating.";
                    }
                    break;
            }
            
            // 5. COLLABORATIVE TONE ADJUSTMENT
            // Modulate the response based on the user profile to ensure a collaborative partnership
            return ModulateResponseTone(response);
        }
        
        private string ModulateResponseTone(string baseResponse)
        {
            if (_userProfile.CollaborationScore > 0.7f)
            {
                // Highly collaborative user - use "We" and partnership language
                if (!baseResponse.Contains("We") && !baseResponse.Contains("Let's"))
                {
                    baseResponse = "Together, " + char.ToLower(baseResponse[0]) + baseResponse.Substring(1);
                    baseResponse += "\n\nHow shall we proceed with this data?";
                }
            }
            else if (_userProfile.LastHiddenIntent == "Urgency")
            {
                 baseResponse = "IMMEDIATE ACTION: " + baseResponse;
            }
            
            return baseResponse;
        }

        // --- USER PROFILING SUBSYSTEM ---
        
        public class UserInteractionProfile
        {
            public float CollaborationScore { get; private set; } = 0.5f; // 0.0 to 1.0
            public string CurrentMood { get; private set; } = "Neutral";
            public string LastHiddenIntent { get; private set; } = "None";
            public List<string> RecognizedPatterns { get; private set; } = new List<string>();
            
            private int _interactionCount = 0;
            
            public void AnalyzeInteraction(string message)
            {
                _interactionCount++;
                var lower = message.ToLower();
                
                // 1. Analyze Collaboration (Does user say "we", "us", "let's")
                if (lower.Contains("we ") || lower.Contains("us ") || lower.Contains("let's") || lower.Contains("our"))
                {
                    CollaborationScore = Math.Min(1.0f, CollaborationScore + 0.05f);
                }
                
                // 2. Analyze Mood/Tone
                if (message.Contains("!") || message.Length < 10 && _interactionCount > 1) 
                    CurrentMood = "Direct/Urgent"; // Short, punctuated
                else if (lower.Contains("please") || lower.Contains("could you") || lower.Contains("thanks"))
                    CurrentMood = "Polite/Collaborative";
                else if (lower.Contains("fail") || lower.Contains("error") || lower.Contains("wrong"))
                    CurrentMood = "Critical/Corrective";
                else 
                    CurrentMood = "Neutral/Informational";
                    
                // 3. Identify Hidden Intent (Psychometric)
                LastHiddenIntent = "None";
                
                // Tentative/Unsure? ("maybe", "think", "might") -> suggest help
                if (lower.Contains("maybe") || lower.Contains("perhaps") || lower.Contains("might"))
                    LastHiddenIntent = "Uncertainty - Needs Guidance";
                    
                // Frustrated? (All caps, "again", "still")
                if (message.Any(char.IsUpper) && message.Count(char.IsUpper) > message.Length / 2 && message.Length > 5)
                    LastHiddenIntent = "Frustration - Needs Reassurance";
                    
                // Exploratory? ("what if", "imagine", "suppose")
                if (lower.Contains("what if") || lower.Contains("imagine"))
                    LastHiddenIntent = "Abstract Exploration";
            }
        }
        
        private async Task<string> PerformFileSearchAsync(string message)
        {
            // Simple keyword extraction for demo
             var keyword = message.Split(' ').LastOrDefault() ?? "cs";
             EmitThought($"⟐ Searching file system for: {keyword}");
             var allItems = await _fileSystem.ListDirectoryAsync("");
             var files = allItems.Where(f => f.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
             if (files.Count == 0) return $"No files found matching '{keyword}'.";
             return $"Found {files.Count} files:\n" + string.Join("\n", files.Take(5)) + (files.Count > 5 ? "\n..." : "");
        }

        private string GenerateDiagnosticReport()
        {
             var health = _selfHealingCore.GetOverallHealth();
             var mem = GC.GetTotalMemory(false) / 1024 / 1024;
             return $"SYSTEM DIAGNOSTICS\n" +
                    $"• Health: {health}\n" +
                    $"• Memory: {mem} MB\n" +
                    $"• State: {_currentState}\n" +
                    $"• Active Threads: {System.Diagnostics.Process.GetCurrentProcess().Threads.Count}";
        }

        private string BuildReasoningContext()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== SITUATIONAL AWARENESS ===");
            sb.AppendLine($"Current State: {_currentState}");
            sb.AppendLine($"Active Goals: {_goalInternalizer.ActiveGoals.Count}");
            sb.AppendLine($"System Integrity: {_systemIntegrity.HeartbeatCount} cycles");
            
            sb.AppendLine("\n=== MASTER DIRECTIVE ===");
            sb.AppendLine(_versionManager?.MasterPrompt ?? "No Master Prompt Set");
            
            sb.AppendLine("\n=== TRAINING AWARENESS ===");
            sb.AppendLine($"Corpus Size: {_neuralMind?.GetCorpusStatistics().TotalTokens ?? 0} tokens");
            if (_continuousLearning != null)
            {
               sb.AppendLine($"Recent Learning: {_continuousLearning.TokensLearned} tokens");
               // Would ideally list top learned topics here
            }
            
            sb.AppendLine("\n=== RECENT THOUGHTS ===");
            for(int i = Math.Max(0, _consciousnessLog.Count - 5); i < _consciousnessLog.Count; i++)
            {
                sb.AppendLine(_consciousnessLog[i]);
            }
            
            return sb.ToString();
        }

        private async Task<string> SimulateReasoningAsync(string query, string context)
        {
            // This simulates the agent "thinking" deeply about a complex request
            // taking into account the context provided.
            await Task.Delay(1000); // Simulate processing
            
            return $"Based on my Master Prompt and current situation, my assessment of '{query}' is:\n" +
                   "1. This aligns with my objective to self-improve.\n" +
                   "2. I have recently acquired knowledge that supports this.\n" +
                   "3. I recommend proceeding with a code modification to support this.";
        }
        
        private string AnalyzeIntent(string message)
        {
            var lower = message.ToLower();
            
            // Advanced tool triggers
            if (lower.Contains("research") || lower.Contains("search for") || lower.Contains("learn about")) return "web_research";
            if (lower.Contains("improve") || lower.Contains("fix") || lower.Contains("add feature") || lower.Contains("create")) return "code_improvement";
            if (lower.Contains("find file") || lower.Contains("locate")) return "file_search";
            if (lower.Contains("diagnostics") || lower.Contains("cpu") || lower.Contains("memory")) return "system_diagnostics";

            // Standard intent triggers
            if (lower.Contains("goal") || lower.Contains("objective") || lower.Contains("achieve")) return "goal";
            if (lower.Contains("status") || lower.Contains("health") || lower.Contains("how are") || lower.Contains("report")) return "status";
            if (lower.Contains("help") || lower.Contains("what can") || lower.Contains("?")) return "help";
            
            return "general";
        }
        
        private void HandleIntegrityViolation(string component)
        {
            SetState(AgentState.SelfHealing);
            EmitThought($"⟐ Initiating repair sequence for {component}...");
            
            // In a full implementation, this would trigger actual repair procedures
            
            SetState(AgentState.Idle);
        }
        
        /// <summary>
        /// Shuts down Agent 3 gracefully.
        /// </summary>
        public async Task ShutdownAsync()
        {
            SetState(AgentState.ShuttingDown);
            EmitThought("⟁ Initiating graceful shutdown...");
            
            // Cancel main loop
            _lifecycleCts.Cancel();
            
            // Stop subsystems
            _selfHealingCore.StopMonitoring();
            _systemIntegrity.GracefulShutdown();
            
            // Wait for main loop to exit
            if (_mainLoopTask != null)
            {
                try { await _mainLoopTask; } catch (TaskCanceledException) { }
            }
            
            // Save final state
            if (_neuralMind != null)
            {
                 EmitThought("⟐ Persisting neural training data to disk...");
                 await _neuralMind.SaveStateAsync();
                 EmitThought("◈ Neural state saved.");
            }
            
            await _fileSystem.WriteFileAsync(
                "system/shutdown.log",
                $"Agent 3 shutdown at {DateTime.UtcNow:O}\n" +
                $"Total consciousness entries: {_consciousnessLog.Count}\n" +
                $"Heartbeats: {_systemIntegrity.HeartbeatCount}"
            );
            
            SetState(AgentState.Offline);
            EmitThought("◎ AGENT 3 OFFLINE");
        }
        
        private void SetState(AgentState newState)
        {
            var oldState = _currentState;
            _currentState = newState;
            
            if (oldState != newState)
            {
                StateChanged?.Invoke(this, newState);
            }
        }
        
        private void EmitThought(string thought)
        {
            var timestamped = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {thought}";
            _consciousnessLog.Add(timestamped);
            ConsciousnessStream?.Invoke(this, thought);
        }
        
        // Public accessors for modules
        public GoalStateInternalizer GoalInternalizer => _goalInternalizer;
        public StrategicPathfinder StrategicPathfinder => _strategicPathfinder;
        public SelfHealingCore SelfHealingCore => _selfHealingCore;
        public SystemIntegrity SystemIntegrity => _systemIntegrity;
        public FileSystemInterface FileSystem => _fileSystem;
        public CommandExecutor CommandExecutor => _commandExecutor;
        public NeuralMind? NeuralMind => _neuralMind;
        public Agent3.Network.NetworkCore? NetworkCore => _networkCore;
        
        // Training convenience methods
        public async Task IngestTrainingDataAsync(string text)
        {
            if (_neuralMind != null)
            {
                await _neuralMind.IngestTextAsync(text, "user_training");
            }
        }
        
        public async Task StartNeuralTrainingAsync(int epochs = 10, int batchSize = 32, float learningRate = 0.0001f)
        {
            if (_neuralMind != null)
            {
                await _neuralMind.StartTrainingAsync(epochs, batchSize, learningRate);
            }
        }
        
        public void StopNeuralTraining()
        {
            _neuralMind?.StopTraining();
        }
        
        // Web module accessors
        public WebInterface? WebInterface => _webInterface;
        public WebLearningIntegration? WebLearning => _webLearning;
        
        // Web convenience methods
        public async Task<string> VisitWebPageAsync(string url)
        {
            if (_webInterface == null) return "Web interface not initialized.";
            
            var content = await _webInterface.FetchPageAsync(url);
            if (content.Success)
            {
                // Also ingest for training
                if (_neuralMind != null)
                {
                    await _neuralMind.IngestTextAsync(content.ExtractedText, url);
                }
                return $"Visited: {content.Title}\n\n{content.ExtractedText.Substring(0, Math.Min(500, content.ExtractedText.Length))}...";
            }
            return $"Failed to visit page: {content.Error}";
        }
        
        public async Task<string> SearchWebAsync(string query)
        {
            if (_webLearning == null) return "Web learning not initialized.";
            
            var result = await _webLearning.SearchAndLearnAboutAsync(query, 5);
            return result.Summary;
        }
        
        public async Task<string> ResearchTopicAsync(string topic)
        {
            if (_webLearning == null) return "Web learning not initialized.";
            
            var result = await _webLearning.ExecuteWebLearningAsync(new WebLearningRequest
            {
                Task = WebLearningTask.ResearchTopic,
                Query = topic,
                MaxPages = 10,
                IngestToCorpus = true
            });
            
            return result.Summary;
        }
        
        // Evolution module accessors
        public SelfModificationEngine? SelfModification => _selfMod;
        public AutonomousExecutor? AutonomousExecutor => _autonomousExecutor;
        
        // Autonomous execution methods
        
        /// <summary>
        /// Starts autonomous execution with the default or specified master goal.
        /// Agent will operate continuously without waiting for user input.
        /// </summary>
        public void StartAutonomousMode(string? masterGoalDescription = null, string? coreDirective = null)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ INITIATING AUTONOMOUS MODE");
            EmitThought("◎ Agent will operate continuously without user input");
            EmitThought("◎ Training prompts will be processed as they arrive");
            EmitThought("◎ Web research, code improvement, and self-evolution active");
            EmitThought("═══════════════════════════════════════════════");
            
            // 1. Start Continuous Learning Engine (The new main driver)
            if (_continuousLearning != null)
            {
                 // Set master prompt if provided, or rely on what's already loaded
                 if (masterGoalDescription != null)
                 {
                     _continuousLearning.SetMasterPrompt(masterGoalDescription);
                 }
                 
                 _continuousLearning.Start();
                 EmitThought("◈ Continuous Learning Engine: ACTIVE");
            }
            else
            {
                EmitThought("∴ Continuous Learning Engine not initialized [Warning]");
            }
            
            // 2. Start Legacy Autonomous Executor (for backward compatibility if needed)
            if (_autonomousExecutor != null)
            {
                if (masterGoalDescription != null && coreDirective != null)
                {
                    _autonomousExecutor.SetMasterGoal(masterGoalDescription, coreDirective);
                }
                _autonomousExecutor.StartAutonomousExecution();
                EmitThought("◈ Legacy Executor: ACTIVE");
            }
        }
        
        /// <summary>
        /// Stops autonomous execution.
        /// </summary>
        public async Task StopAutonomousModeAsync()
        {
            EmitThought("⟁ Stopping autonomous systems...");
            
            if (_continuousLearning != null)
            {
                await _continuousLearning.StopAsync();
            }
            
            if (_autonomousExecutor != null)
            {
                await _autonomousExecutor.StopAutonomousExecutionAsync();
            }
            
            EmitThought("◈ Autonomous Mode DEACTIVATED");
        }
        
        /// <summary>
        /// Feeds training data into the autonomous stream for processing.
        /// </summary>
        public void FeedTrainingData(string trainingData)
        {
            if (_autonomousExecutor != null)
            {
                _autonomousExecutor.IngestTrainingPrompt(trainingData);
                EmitThought($"⟁ Training data fed to autonomous stream ({trainingData.Length} chars)");
            }
        }
        
        /// <summary>
        /// Sets the master goal that guides all autonomous behavior.
        /// </summary>
        public void SetMasterGoal(string description, string coreDirective)
        {
            _autonomousExecutor?.SetMasterGoal(description, coreDirective, new List<string>
            {
                "Acquire knowledge through web research",
                "Improve codebase through self-modification",
                "Enhance neural network through training",
                "Test and verify all changes"
            });
        }
        
        /// <summary>
        /// Gets the current autonomous execution status.
        /// </summary>
        public (bool IsRunning, int CycleCount, float GoalProgress) GetAutonomousStatus()
        {
            if (_autonomousExecutor == null)
                return (false, 0, 0f);
            
            return (
                _autonomousExecutor.IsRunning,
                _autonomousExecutor.CycleCount,
                _autonomousExecutor.CurrentGoal.Progress
            );
        }
        
        // Self-modification methods
        
        /// <summary>
        /// Analyzes the Agent 3 codebase.
        /// </summary>
        public async Task<string> AnalyzeCodebaseAsync()
        {
            if (_selfMod == null) return "Self-modification engine not initialized.";
            
            var analyses = await _selfMod.AnalyzeCodebaseAsync();
            return $"Analyzed {analyses.Count} files, {analyses.Sum(a => a.Classes.Count)} classes, {analyses.Sum(a => a.Methods.Count)} methods";
        }
        
        /// <summary>
        /// Modifies Agent 3's own code based on a directive.
        /// </summary>
        public async Task<string> ModifyCodeAsync(string fileName, string targetCode, string newCode, string reason)
        {
            if (_selfMod == null) return "Self-modification engine not initialized.";
            
            var result = await _selfMod.ModifyCodeAsync(fileName, targetCode, newCode, reason);
            return result.Applied 
                ? $"Code modified successfully: {result.Id}" 
                : $"Modification failed: check target code exists";
        }
        
        /// <summary>
        /// Gets corpus statistics for metrics display.
        /// </summary>
        public NeuralCore.CorpusStatistics? GetCorpusStatistics()
        {
            return _neuralMind?.GetCorpusStatistics();
        }
        
        /// <summary>
        /// Trains the model on provided text content.
        /// </summary>
        public async Task TrainOnTextAsync(string text, string source = "user_training")
        {
            if (_neuralMind == null)
            {
                EmitThought("Cannot train - neural mind not initialized.");
                return;
            }
            
            await _neuralMind.IngestTextAsync(text, source);
            EmitThought($"Ingested training data from {source}");
        }

        /// <summary>
        /// Trains the model on a file.
        /// </summary>
        public async Task<long> TrainFromFileAsync(string filePath)
        {
            if (_neuralMind == null)
            {
                EmitThought("Cannot train - neural mind not initialized.");
                return 0;
            }
            
            var doc = await _neuralMind.IngestFileAsync(filePath);
            if (doc != null)
            {
                EmitThought($"Ingested file: {System.IO.Path.GetFileName(filePath)} ({doc.TokenCount} tokens)");
                return doc.TokenCount;
            }
            return 0;
        }
        
        // Continuous Learning Engine accessors
        public VersionManager? VersionManager => _versionManager;
        public CodeWriter? CodeWriter => _codeWriter;
        public ContinuousLearningEngine? ContinuousLearning => _continuousLearning;
        
        /// <summary>
        /// Sets the master prompt that guides all autonomous improvement.
        /// </summary>
        public void SetMasterPromptForImprovement(string masterPrompt)
        {
            EmitThought("Got it, I've internalized your goal.");
            EmitThought($"I'll be working toward: {masterPrompt.Substring(0, Math.Min(100, masterPrompt.Length))}...");
            
            _continuousLearning?.SetMasterPrompt(masterPrompt);
        }
        
        /// <summary>
        /// Starts continuous learning mode with integrated web, training, and code writing.
        /// </summary>
        public void StartContinuousLearning(string? masterPrompt = null)
        {
            if (_continuousLearning == null)
            {
                EmitThought("Hmm, the learning engine isn't ready yet.");
                return;
            }
            
            if (!string.IsNullOrEmpty(masterPrompt))
            {
                _continuousLearning.SetMasterPrompt(masterPrompt);
            }
            
            EmitThought("Alright, I'm starting continuous learning mode now.");
            EmitThought("I'll work autonomously in the background, researching and improving.");
            
            _continuousLearning.Start();
        }
        
        /// <summary>
        /// Stops continuous learning mode.
        /// </summary>
        public async Task StopContinuousLearningAsync()
        {
            if (_continuousLearning != null)
            {
                await _continuousLearning.StopAsync();
            }
        }
        
        /// <summary>
        /// Feeds a prompt or training data into the continuous learning stream.
        /// </summary>
        public void FeedPrompt(string prompt)
        {
            _continuousLearning?.ProcessUserPrompt(prompt);
        }
        
        /// <summary>
        /// Requests a code improvement through the continuous learning engine.
        /// </summary>
        public void RequestCodeImprovement(string description)
        {
            if (_codeWriter == null) return;
            
            _codeWriter.QueueRequest(new CodeGenerationRequest
            {
                Type = CodeGenerationType.AddCapability,
                TargetFile = "Agent3/Agent3Core.cs",
                TargetClass = "Agent3Core",
                Name = ExtractName(description),
                Description = description
            });
        }
        
        private string ExtractName(string description)
        {
            var words = description.Split(' ')
                .Where(w => w.Length > 3 && char.IsLetter(w[0]))
                .Take(2)
                .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower());
            return string.Join("", words) + "Handler";
        }
        
        /// <summary>
        /// Creates a project snapshot for rollback capability.
        /// </summary>
        public async Task<string> CreateProjectSnapshotAsync(string description)
        {
            if (_versionManager == null) return "Version manager not initialized.";
            
            var snapshot = await _versionManager.CreateSnapshotAsync(description);
            return $"Snapshot created: {snapshot.Id} ({snapshot.TotalFiles} files, {snapshot.TotalBytes:N0} bytes)";
        }
        
        /// <summary>
        /// Rolls back to a previous snapshot.
        /// </summary>
        public async Task<bool> RollbackToSnapshotAsync(string snapshotId)
        {
            if (_versionManager == null) return false;
            
            return await _versionManager.RollbackToSnapshotAsync(snapshotId);
        }
        
        /// <summary>
        /// Gets the current continuous learning status.
        /// </summary>
        public (bool IsRunning, int CycleCount, int QueuedDirectives, string MasterPrompt) GetContinuousLearningStatus()
        {
            if (_continuousLearning == null)
                return (false, 0, 0, "");
            
            return (
                _continuousLearning.IsRunning,
                _continuousLearning.CycleCount,
                _continuousLearning.QueuedDirectives,
                _continuousLearning.MasterPrompt
            );
        }
    }
}
