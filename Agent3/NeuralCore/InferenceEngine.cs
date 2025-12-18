/*
 * AGENT 3 - INFERENCE ENGINE
 * Generates text responses based on the trained neural network
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent3.NeuralCore
{
    public class InferenceConfig
    {
        public float Temperature { get; set; } = 0.7f;
        public int MaxNewTokens { get; set; } = 256;
        public float TopP { get; set; } = 0.9f;
        public int TopK { get; set; } = 50;
        public float RepetitionPenalty { get; set; } = 1.1f;
    }

    public class InferenceEngine
    {
        private readonly TransformerNetwork _network;
        private readonly CorpusIngestionEngine _corpus;
        private readonly InferenceConfig _config;
        private readonly Random _random;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<string>? TokenGenerated;
        
        public InferenceEngine(TransformerNetwork network, CorpusIngestionEngine corpus, InferenceConfig? config = null)
        {
            _network = network;
            _corpus = corpus;
            _config = config ?? new InferenceConfig();
            _random = new Random();
            EmitThought("⟁ Inference Engine initialized");
        }
        
        public async Task<string> GenerateResponseAsync(string prompt)
        {
            EmitThought($"⟐ Processing prompt: \"{prompt.Substring(0, Math.Min(50, prompt.Length))}...\"");
            
            // Tokenize prompt
            var promptTokens = TokenizePrompt(prompt);
            var promptIds = _corpus.TokensToIds(promptTokens);
            
            // Add BOS token
            var inputIds = new List<int> { 2 }; // BOS_TOKEN
            inputIds.AddRange(promptIds);
            
            EmitThought($"∿ Prompt tokenized: {inputIds.Count} tokens");
            
            // Generate
            var generatedIds = await GenerateTokensAsync(inputIds.ToArray());
            
            // Decode
            var outputTokens = _corpus.IdsToTokens(generatedIds.Skip(inputIds.Count).ToList());
            var response = DetokenizeOutput(outputTokens);
            
            EmitThought($"◈ Generated {generatedIds.Length - inputIds.Count} tokens");
            
            return response;
        }
        
        private List<string> TokenizePrompt(string text)
        {
            text = text.ToLowerInvariant();
            var tokens = new List<string>();
            var pattern = @"(\w+|[^\w\s])";
            
            foreach (System.Text.RegularExpressions.Match match in 
                     System.Text.RegularExpressions.Regex.Matches(text, pattern))
            {
                var token = match.Value.Trim();
                if (!string.IsNullOrEmpty(token)) tokens.Add(token);
            }
            
            return tokens;
        }
        
        private string DetokenizeOutput(List<string> tokens)
        {
            var sb = new StringBuilder();
            bool lastWasWord = false;
            
            foreach (var token in tokens)
            {
                if (token == "<EOS>" || token == "<PAD>") break;
                if (token.StartsWith("<")) continue;
                
                bool isWord = char.IsLetterOrDigit(token[0]);
                if (lastWasWord && isWord) sb.Append(' ');
                
                sb.Append(token);
                lastWasWord = isWord;
            }
            
            return sb.ToString().Trim();
        }
        
        private async Task<int[]> GenerateTokensAsync(int[] inputIds)
        {
            var generated = new List<int>(inputIds);
            var recentTokens = new Dictionary<int, int>();
            
            await Task.Run(() =>
            {
                for (int i = 0; i < _config.MaxNewTokens; i++)
                {
                    var input = generated.TakeLast(_network.Config.MaxSequenceLength).ToArray();
                    var logits = _network.Forward(input);
                    
                    int lastPos = input.Length - 1;
                    var nextLogits = ExtractLastLogits(logits, lastPos);
                    
                    // Apply repetition penalty
                    ApplyRepetitionPenalty(nextLogits, recentTokens);
                    
                    // Apply temperature
                    for (int j = 0; j < nextLogits.Length; j++)
                        nextLogits[j] /= _config.Temperature;
                    
                    // Sample
                    int nextToken = SampleWithTopK(nextLogits, _config.TopK);
                    
                    generated.Add(nextToken);
                    TokenGenerated?.Invoke(this, _corpus.IdsToTokens(new List<int> { nextToken })[0]);
                    
                    // Track for repetition penalty
                    recentTokens[nextToken] = recentTokens.GetValueOrDefault(nextToken, 0) + 1;
                    
                    // Stop on EOS
                    if (nextToken == 3) break;
                }
            });
            
            return generated.ToArray();
        }
        
        private float[] ExtractLastLogits(Tensor logits, int position)
        {
            var result = new float[_network.Config.VocabularySize];
            for (int i = 0; i < result.Length; i++)
                result[i] = logits.Get(position, i);
            return result;
        }
        
        private void ApplyRepetitionPenalty(float[] logits, Dictionary<int, int> recentTokens)
        {
            foreach (var (tokenId, count) in recentTokens)
            {
                if (tokenId < logits.Length)
                {
                    float penalty = (float)Math.Pow(_config.RepetitionPenalty, count);
                    logits[tokenId] = logits[tokenId] > 0 
                        ? logits[tokenId] / penalty 
                        : logits[tokenId] * penalty;
                }
            }
        }
        
        private int SampleWithTopK(float[] logits, int k)
        {
            // Get top-k indices
            var indexed = logits.Select((v, i) => (v, i))
                                .OrderByDescending(x => x.v)
                                .Take(k)
                                .ToList();
            
            // Softmax over top-k
            float maxVal = indexed[0].v;
            float sumExp = indexed.Sum(x => (float)Math.Exp(x.v - maxVal));
            
            var probs = indexed.Select(x => (float)Math.Exp(x.v - maxVal) / sumExp).ToList();
            
            // Sample
            float r = (float)_random.NextDouble();
            float cumulative = 0;
            
            for (int i = 0; i < probs.Count; i++)
            {
                cumulative += probs[i];
                if (r < cumulative) return indexed[i].i;
            }
            
            return indexed.Last().i;
        }
        
        public void SetTemperature(float temp) => _config.Temperature = Math.Clamp(temp, 0.1f, 2.0f);
        public void SetMaxTokens(int max) => _config.MaxNewTokens = Math.Clamp(max, 1, 1024);
        
        private void EmitThought(string thought) => ConsciousnessEvent?.Invoke(this, thought);
    }
}
