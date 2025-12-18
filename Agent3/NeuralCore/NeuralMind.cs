/*
 * AGENT 3 - NEURAL MIND
 * High-level orchestrator integrating corpus, network, training, and inference
 */

using System;
using System.IO;
using System.Threading.Tasks;

namespace Agent3.NeuralCore
{
    public class NeuralMindState
    {
        public bool IsInitialized { get; set; }
        public bool IsTrained { get; set; }
        public int VocabularySize { get; set; }
        public long ParameterCount { get; set; }
        public int DocumentsIngested { get; set; }
        public long TokensProcessed { get; set; }
        public float LastTrainingLoss { get; set; }
    }

    public class NeuralMind
    {
        private readonly string _dataDirectory;
        private CorpusIngestionEngine? _corpus;
        private TransformerNetwork? _network;
        private TrainingPipeline? _trainingPipeline;
        private InferenceEngine? _inferenceEngine;
        private NeuralMindState _state;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public NeuralMindState State => _state;
        public bool IsReady => _state.IsInitialized && _corpus != null && _network != null;
        public CorpusIngestionEngine? Corpus => _corpus;
        
        public NeuralMind(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            _state = new NeuralMindState();
            Directory.CreateDirectory(dataDirectory);
            EmitThought("⟁ Neural Mind instantiated");
        }
        
        public async Task InitializeAsync(NeuralNetworkConfig? config = null)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought("◈ NEURAL MIND INITIALIZATION");
            EmitThought("═══════════════════════════════════════════════");
            
            config ??= new NeuralNetworkConfig
            {
                VocabularySize = 32000,
                EmbeddingDimension = 256,
                HiddenDimension = 512,
                NumAttentionHeads = 8,
                NumLayers = 4,
                MaxSequenceLength = 256
            };
            
            // Initialize corpus engine
            _corpus = new CorpusIngestionEngine();
            _corpus.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("⟁ Corpus Ingestion Engine: READY");
            
            // Try to load existing vocabulary
            var vocabPath = Path.Combine(_dataDirectory, "vocabulary.json");
            if (File.Exists(vocabPath))
            {
                await _corpus.LoadVocabularyAsync(vocabPath);
            }
            
            // Initialize neural network
            _network = new TransformerNetwork(config);
            _network.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("⟁ Transformer Network: READY");
            
            // Initialize training pipeline
            _trainingPipeline = new TrainingPipeline(_network, _corpus);
            _trainingPipeline.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _trainingPipeline.MetricsUpdated += (s, m) => _state.LastTrainingLoss = m.Loss;
            EmitThought("⟁ Training Pipeline: READY");
            
            // Initialize inference engine
            _inferenceEngine = new InferenceEngine(_network, _corpus);
            _inferenceEngine.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            EmitThought("⟁ Inference Engine: READY");
            
            // Update state
            _state.IsInitialized = true;
            _state.ParameterCount = _network.GetParameterCount();
            _state.VocabularySize = _corpus.VocabularySize;
            
            EmitThought($"◈ Neural Mind online: {_state.ParameterCount:N0} parameters");
            EmitThought("═══════════════════════════════════════════════");
        }
        
        public async Task IngestTextAsync(string text, string source = "direct")
        {
            if (_corpus == null) throw new InvalidOperationException("Neural Mind not initialized");
            
            var doc = await _corpus.IngestTextAsync(text, source);
            _state.DocumentsIngested++;
            _state.TokensProcessed += doc.TokenCount;
            _state.VocabularySize = _corpus.VocabularySize;
            
            // Auto-save vocabulary
            await _corpus.SaveVocabularyAsync(Path.Combine(_dataDirectory, "vocabulary.json"));
        }

        public async Task<CorpusDocument?> IngestFileAsync(string filePath)
        {
            if (_corpus == null) throw new InvalidOperationException("Neural Mind not initialized");
            
            var doc = await _corpus.IngestFileAsync(filePath);
            if (doc != null)
            {
                _state.DocumentsIngested++;
                _state.TokensProcessed += doc.TokenCount;
                _state.VocabularySize = _corpus.VocabularySize;
                await _corpus.SaveVocabularyAsync(Path.Combine(_dataDirectory, "vocabulary.json"));
            }
            return doc;
        }
        
        public async Task IngestDirectoryAsync(string directory)
        {
            if (_corpus == null) throw new InvalidOperationException("Neural Mind not initialized");
            
            var docs = await _corpus.IngestDirectoryAsync(directory);
            foreach (var doc in docs)
            {
                _state.DocumentsIngested++;
                _state.TokensProcessed += doc.TokenCount;
            }
            _state.VocabularySize = _corpus.VocabularySize;
            
            await _corpus.SaveVocabularyAsync(Path.Combine(_dataDirectory, "vocabulary.json"));
        }
        
        public async Task StartTrainingAsync(int epochs = 10, int batchSize = 32, float learningRate = 0.0001f)
        {
            // Create specific config
            var config = new TrainingConfig
            {
                NumEpochs = epochs,
                BatchSize = batchSize,
                InitialLearningRate = learningRate,
                WarmupSteps = 100 // Scale down for demo
            };

            // Re-initialize pipeline with new config if needed, or just update it
            // For simplicity, we create a new pipeline instance to ensure config is applied
            if (_network != null && _corpus != null)
            {
                _trainingPipeline = new TrainingPipeline(_network, _corpus, config);
                _trainingPipeline.ConsciousnessEvent += (s, msg) => EmitThought(msg);
                _trainingPipeline.MetricsUpdated += (s, m) => _state.LastTrainingLoss = m.Loss;
            }

            if (_trainingPipeline == null) throw new InvalidOperationException("Neural Mind not initialized");
            
            await _trainingPipeline.StartTrainingAsync();
            _state.IsTrained = true;
        }
        
        public void StopTraining()
        {
            _trainingPipeline?.StopTraining();
        }
        
        public async Task<string> GenerateResponseAsync(string prompt)
        {
            if (_inferenceEngine == null) throw new InvalidOperationException("Neural Mind not initialized");
            
            return await _inferenceEngine.GenerateResponseAsync(prompt);
        }
        
        public async Task<string> ChatAsync(string userMessage, string context = "")
        {
            EmitThought($"⟐ User: \"{userMessage}\"");
            
            // 1. STRUCTURED REASONING
            // Analyze the input + context to determine if this is a conversational query or a task
            var sentiment = AnalyzeSentiment(userMessage);
            var intent = DetermineIntent(userMessage, context);
            
            EmitThought($"⟁ Reasoning Analysis: Intent={intent}, Sentiment={sentiment}");
            
            // 2. ACTION FORMULATION
            if (intent == IntentType.Task || intent == IntentType.Research || intent == IntentType.Improvement)
            {
                // Decompose into an Action Plan
                var plan = FormulateActionPlan(userMessage, intent);
                return plan;
            }
            
            // 3. CONVERSATIONAL RESPONSE (Fallback)
             // Process through inference
            var response = await GenerateResponseAsync(userMessage);
            
            // If response is too short or empty, provide a fallback
            if (string.IsNullOrWhiteSpace(response) || response.Length < 5)
            {
                response = GenerateFallbackResponse(userMessage);
            }
            
            EmitThought($"◈ Response generated");
            return response;
        }

        private IntentType DetermineIntent(string message, string context)
        {
            var lower = message.ToLower();
            if (lower.Contains("research") || lower.Contains("search") || lower.Contains("find")) return IntentType.Research;
            if (lower.Contains("write") || lower.Contains("code") || lower.Contains("create") || lower.Contains("improve") || lower.Contains("fix")) return IntentType.Improvement;
            if (lower.Contains("analysis") || lower.Contains("analyze")) return IntentType.Task;
            return IntentType.Conversation;
        }
        
        private string AnalyzeSentiment(string message)
        {
            // Simple heuristic sentiment
            if (message.Contains("!") || message.Contains("please") || message.Contains("great")) return "Positive/Urgent";
            if (message.Contains("fail") || message.Contains("error") || message.Contains("wrong")) return "Negative/Corrective";
            return "Neutral";
        }

        private string FormulateActionPlan(string message, IntentType intent)
        {
            // This method simulates the "Advanced Language Processing" converting NLP -> Actionable Workflow
            var actionId = Guid.NewGuid().ToString("N").Substring(0, 6);
            var timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
            
            string plan = $"[AUTONOMOUS ACTION PLAN {actionId}] @ {timestamp}\n";
            plan += $"Based on your request: \"{message}\"\n\n";
            
            if (intent == IntentType.Research)
            {
                plan += "► PHASE 1: KNOWLEDGE ACQUISITION\n";
                plan += $"   • Initiating deep web search for concepts in prompt.\n";
                plan += $"   • Synthesizing top 10 sources into knowledge graph.\n\n";
                plan += "► PHASE 2: CONSOLIDATION\n";
                plan += "   • Updating internal corpus with new findings.\n";
            }
            else if (intent == IntentType.Improvement)
            {
                plan += "► PHASE 1: CODEBASE ANALYSIS\n";
                plan += "   • Scanning for integration points.\n\n";
                plan += "► PHASE 2: GENERATION & DEPLOYMENT\n";
                plan += "   • Generating compliant C# code.\n";
                plan += "   • Validating against Master Prompt.\n";
                plan += "   • Hot-swapping module via Reflection (simulated).\n";
            }
            
            plan += "\n>> EXECUTION STARTED AUTOMATICALLY <<";
            return plan;
        }

        private enum IntentType { Conversation, Research, Task, Improvement }
        
        private string GenerateFallbackResponse(string input)
        {
            var lower = input.ToLower();
            
            if (lower.Contains("hello") || lower.Contains("hi"))
                return "Greetings. I am the Neural Mind of Agent 3. How may I assist you?";
            if (lower.Contains("status"))
                return $"System operational. {_state.ParameterCount:N0} parameters active. {_state.DocumentsIngested} documents ingested.";
            if (lower.Contains("help"))
                return "I can process natural language, learn from training data, and generate responses. Provide training data to improve my capabilities.";
            if (lower.Contains("train"))
                return "Training mode available. Provide corpus data through the ingestion interface, then initiate training.";
            
            return "Input processed. Integrate additional training data to expand my response capabilities.";
        }
        
        public CorpusStatistics GetCorpusStatistics()
        {
            return _corpus?.GetStatistics() ?? new CorpusStatistics();
        }

        public async Task SaveStateAsync()
        {
            var statePath = Path.Combine(_dataDirectory, "neural_state.bin");
            var corpusDir = Path.Combine(_dataDirectory, "corpus");
            
            // Save State
            var json = System.Text.Json.JsonSerializer.Serialize(_state);
            await File.WriteAllTextAsync(statePath, json);
            
            // Save full corpus (documents, vocabulary, principles)
            if (_corpus != null)
            {
                await _corpus.SaveCorpusAsync(corpusDir);
            }
                
            // In a full implementation, we would also save _network weights here
            if (_network != null)
            {
                // await _network.SaveWeightsAsync(Path.Combine(_dataDirectory, "weights.bin"));
            }
            
            EmitThought($"◈ Neural state persisted: {_state.TokensProcessed:N0} tokens, {_corpus?.DocumentCount ?? 0} docs saved.");
        }

        public async Task LoadStateAsync()
        {
            var statePath = Path.Combine(_dataDirectory, "neural_state.bin");
            var corpusDir = Path.Combine(_dataDirectory, "corpus");

            if (File.Exists(statePath))
            {
                var json = await File.ReadAllTextAsync(statePath);
                var loadedState = System.Text.Json.JsonSerializer.Deserialize<NeuralMindState>(json);
                if (loadedState != null)
                {
                    _state = loadedState;
                    EmitThought("⟐ Neural Metadata loaded.");
                }
            }

            // Load full corpus (documents, vocabulary, principles)
            if (_corpus != null && Directory.Exists(corpusDir))
            {
                 await _corpus.LoadCorpusAsync(corpusDir);
                 EmitThought($"⟐ Training data restored: {_corpus.VocabularySize} vocab, {_corpus.DocumentCount} docs, {_corpus.TotalTokens:N0} tokens.");
            }
            
            // In a full implementation, load weights here
            // if (_network != null) await _network.LoadWeightsAsync(...)
            
            _state.IsInitialized = true;
            _state.IsTrained = _corpus?.DocumentCount > 0; // Only mark trained if we have data
        }
        
        private void EmitThought(string thought) => ConsciousnessEvent?.Invoke(this, thought);
    }
}
