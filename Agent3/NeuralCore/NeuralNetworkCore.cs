/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - NEURAL NETWORK CORE                           ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Transformer-based neural network for text understanding and      ║
 * ║           generation, implementing attention mechanisms and embeddings     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agent3.NeuralCore
{
    /// <summary>
    /// Configuration for the neural network.
    /// </summary>
    public class NeuralNetworkConfig
    {
        public int VocabularySize { get; set; } = 50000;
        public int EmbeddingDimension { get; set; } = 256;
        public int HiddenDimension { get; set; } = 512;
        public int NumAttentionHeads { get; set; } = 8;
        public int NumLayers { get; set; } = 6;
        public int MaxSequenceLength { get; set; } = 512;
        public float DropoutRate { get; set; } = 0.1f;
        public float LearningRate { get; set; } = 0.0001f;
        public int NumEpochs { get; set; } = 10;
        public int BatchSize { get; set; } = 32;
    }

    /// <summary>
    /// Represents a tensor (multi-dimensional array) for neural computations.
    /// </summary>
    public class Tensor
    {
        public float[] Data { get; private set; }
        public int[] Shape { get; private set; }
        
        public Tensor(int[] shape)
        {
            Shape = shape;
            int size = shape.Aggregate(1, (a, b) => a * b);
            Data = new float[size];
        }
        
        public Tensor(float[] data, int[] shape)
        {
            Data = data;
            Shape = shape;
        }
        
        public int TotalSize => Data.Length;
        
        public float Get(params int[] indices)
        {
            int flatIndex = GetFlatIndex(indices);
            return Data[flatIndex];
        }
        
        public void Set(float value, params int[] indices)
        {
            int flatIndex = GetFlatIndex(indices);
            Data[flatIndex] = value;
        }
        
        private int GetFlatIndex(int[] indices)
        {
            int flatIndex = 0;
            int multiplier = 1;
            for (int i = Shape.Length - 1; i >= 0; i--)
            {
                flatIndex += indices[i] * multiplier;
                multiplier *= Shape[i];
            }
            return flatIndex;
        }
        
        public static Tensor Zeros(int[] shape)
        {
            return new Tensor(shape);
        }
        
        public static Tensor Random(int[] shape, float scale = 0.02f)
        {
            var tensor = new Tensor(shape);
            var random = new Random();
            for (int i = 0; i < tensor.Data.Length; i++)
            {
                tensor.Data[i] = (float)(random.NextDouble() * 2 - 1) * scale;
            }
            return tensor;
        }
        
        public Tensor Clone()
        {
            var clone = new Tensor(Shape.ToArray());
            Array.Copy(Data, clone.Data, Data.Length);
            return clone;
        }
    }

    /// <summary>
    /// Embedding layer for converting token IDs to dense vectors.
    /// </summary>
    public class EmbeddingLayer
    {
        private Tensor _weights;
        private Tensor _positionWeights;
        private readonly int _vocabSize;
        private readonly int _embeddingDim;
        private readonly int _maxSeqLength;
        
        public EmbeddingLayer(int vocabSize, int embeddingDim, int maxSeqLength)
        {
            _vocabSize = vocabSize;
            _embeddingDim = embeddingDim;
            _maxSeqLength = maxSeqLength;
            
            // Initialize weights with Xavier initialization
            float scale = (float)Math.Sqrt(2.0 / embeddingDim);
            _weights = Tensor.Random(new[] { vocabSize, embeddingDim }, scale);
            _positionWeights = CreatePositionalEncoding(maxSeqLength, embeddingDim);
        }
        
        private Tensor CreatePositionalEncoding(int maxLen, int dim)
        {
            var pe = new Tensor(new[] { maxLen, dim });
            
            for (int pos = 0; pos < maxLen; pos++)
            {
                for (int i = 0; i < dim; i++)
                {
                    float angle = pos / (float)Math.Pow(10000, (2 * (i / 2)) / (float)dim);
                    pe.Set(i % 2 == 0 ? (float)Math.Sin(angle) : (float)Math.Cos(angle), pos, i);
                }
            }
            
            return pe;
        }
        
        public Tensor Forward(int[] tokenIds)
        {
            int seqLen = tokenIds.Length;
            var output = new Tensor(new[] { seqLen, _embeddingDim });
            
            for (int i = 0; i < seqLen; i++)
            {
                int tokenId = Math.Clamp(tokenIds[i], 0, _vocabSize - 1);
                
                for (int j = 0; j < _embeddingDim; j++)
                {
                    float embed = _weights.Get(tokenId, j);
                    float posEmbed = _positionWeights.Get(i, j);
                    output.Set(embed + posEmbed, i, j);
                }
            }
            
            return output;
        }
        
        public Tensor Weights => _weights;
    }

    /// <summary>
    /// Multi-head self-attention mechanism.
    /// </summary>
    public class MultiHeadAttention
    {
        private readonly int _numHeads;
        private readonly int _headDim;
        private readonly int _modelDim;
        
        private Tensor _queryWeights;
        private Tensor _keyWeights;
        private Tensor _valueWeights;
        private Tensor _outputWeights;
        
        public MultiHeadAttention(int modelDim, int numHeads)
        {
            _modelDim = modelDim;
            _numHeads = numHeads;
            _headDim = modelDim / numHeads;
            
            float scale = (float)Math.Sqrt(2.0 / modelDim);
            _queryWeights = Tensor.Random(new[] { modelDim, modelDim }, scale);
            _keyWeights = Tensor.Random(new[] { modelDim, modelDim }, scale);
            _valueWeights = Tensor.Random(new[] { modelDim, modelDim }, scale);
            _outputWeights = Tensor.Random(new[] { modelDim, modelDim }, scale);
        }
        
        public Tensor Forward(Tensor input)
        {
            int seqLen = input.Shape[0];
            
            // Compute Q, K, V projections
            var Q = LinearTransform(input, _queryWeights);
            var K = LinearTransform(input, _keyWeights);
            var V = LinearTransform(input, _valueWeights);
            
            // Compute attention scores
            var attentionScores = ComputeAttentionScores(Q, K, seqLen);
            
            // Apply softmax
            ApplySoftmax(attentionScores, seqLen);
            
            // Apply attention to values
            var attended = ApplyAttention(attentionScores, V, seqLen);
            
            // Output projection
            var output = LinearTransform(attended, _outputWeights);
            
            return output;
        }
        
        private Tensor LinearTransform(Tensor input, Tensor weights)
        {
            int seqLen = input.Shape[0];
            var output = new Tensor(new[] { seqLen, _modelDim });
            
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < _modelDim; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < _modelDim; k++)
                    {
                        sum += input.Get(i, k) * weights.Get(k, j);
                    }
                    output.Set(sum, i, j);
                }
            }
            
            return output;
        }
        
        private Tensor ComputeAttentionScores(Tensor Q, Tensor K, int seqLen)
        {
            var scores = new Tensor(new[] { seqLen, seqLen });
            float scale = (float)Math.Sqrt(_headDim);
            
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < seqLen; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < _modelDim; k++)
                    {
                        sum += Q.Get(i, k) * K.Get(j, k);
                    }
                    scores.Set(sum / scale, i, j);
                }
            }
            
            return scores;
        }
        
        private void ApplySoftmax(Tensor scores, int seqLen)
        {
            for (int i = 0; i < seqLen; i++)
            {
                float maxVal = float.MinValue;
                for (int j = 0; j < seqLen; j++)
                {
                    maxVal = Math.Max(maxVal, scores.Get(i, j));
                }
                
                float sum = 0;
                for (int j = 0; j < seqLen; j++)
                {
                    float exp = (float)Math.Exp(scores.Get(i, j) - maxVal);
                    scores.Set(exp, i, j);
                    sum += exp;
                }
                
                for (int j = 0; j < seqLen; j++)
                {
                    scores.Set(scores.Get(i, j) / sum, i, j);
                }
            }
        }
        
        private Tensor ApplyAttention(Tensor scores, Tensor V, int seqLen)
        {
            var output = new Tensor(new[] { seqLen, _modelDim });
            
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < _modelDim; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < seqLen; k++)
                    {
                        sum += scores.Get(i, k) * V.Get(k, j);
                    }
                    output.Set(sum, i, j);
                }
            }
            
            return output;
        }
    }

    /// <summary>
    /// Feed-forward neural network layer.
    /// </summary>
    public class FeedForwardLayer
    {
        private Tensor _weights1;
        private Tensor _weights2;
        private Tensor _bias1;
        private Tensor _bias2;
        private readonly int _modelDim;
        private readonly int _hiddenDim;
        
        public FeedForwardLayer(int modelDim, int hiddenDim)
        {
            _modelDim = modelDim;
            _hiddenDim = hiddenDim;
            
            float scale = (float)Math.Sqrt(2.0 / modelDim);
            _weights1 = Tensor.Random(new[] { modelDim, hiddenDim }, scale);
            _weights2 = Tensor.Random(new[] { hiddenDim, modelDim }, scale);
            _bias1 = Tensor.Zeros(new[] { hiddenDim });
            _bias2 = Tensor.Zeros(new[] { modelDim });
        }
        
        public Tensor Forward(Tensor input)
        {
            int seqLen = input.Shape[0];
            var hidden = new Tensor(new[] { seqLen, _hiddenDim });
            var output = new Tensor(new[] { seqLen, _modelDim });
            
            // First linear layer + GeLU activation
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < _hiddenDim; j++)
                {
                    float sum = _bias1.Data[j];
                    for (int k = 0; k < _modelDim; k++)
                    {
                        sum += input.Get(i, k) * _weights1.Get(k, j);
                    }
                    // GeLU activation
                    hidden.Set(sum * 0.5f * (1 + (float)Math.Tanh(Math.Sqrt(2 / Math.PI) * (sum + 0.044715 * Math.Pow(sum, 3)))), i, j);
                }
            }
            
            // Second linear layer
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < _modelDim; j++)
                {
                    float sum = _bias2.Data[j];
                    for (int k = 0; k < _hiddenDim; k++)
                    {
                        sum += hidden.Get(i, k) * _weights2.Get(k, j);
                    }
                    output.Set(sum, i, j);
                }
            }
            
            return output;
        }
    }

    /// <summary>
    /// Layer normalization.
    /// </summary>
    public class LayerNorm
    {
        private Tensor _gamma;
        private Tensor _beta;
        private readonly int _dim;
        private readonly float _epsilon = 1e-5f;
        
        public LayerNorm(int dim)
        {
            _dim = dim;
            _gamma = Tensor.Zeros(new[] { dim });
            _beta = Tensor.Zeros(new[] { dim });
            
            // Initialize gamma to 1
            for (int i = 0; i < dim; i++)
            {
                _gamma.Data[i] = 1.0f;
            }
        }
        
        public Tensor Forward(Tensor input)
        {
            int seqLen = input.Shape[0];
            var output = new Tensor(new[] { seqLen, _dim });
            
            for (int i = 0; i < seqLen; i++)
            {
                // Compute mean
                float mean = 0;
                for (int j = 0; j < _dim; j++)
                {
                    mean += input.Get(i, j);
                }
                mean /= _dim;
                
                // Compute variance
                float variance = 0;
                for (int j = 0; j < _dim; j++)
                {
                    float diff = input.Get(i, j) - mean;
                    variance += diff * diff;
                }
                variance /= _dim;
                
                // Normalize
                float std = (float)Math.Sqrt(variance + _epsilon);
                for (int j = 0; j < _dim; j++)
                {
                    float normalized = (input.Get(i, j) - mean) / std;
                    output.Set(normalized * _gamma.Data[j] + _beta.Data[j], i, j);
                }
            }
            
            return output;
        }
    }

    /// <summary>
    /// Transformer block combining attention and feed-forward layers.
    /// </summary>
    public class TransformerBlock
    {
        private readonly MultiHeadAttention _attention;
        private readonly FeedForwardLayer _feedForward;
        private readonly LayerNorm _norm1;
        private readonly LayerNorm _norm2;
        
        public TransformerBlock(int modelDim, int numHeads, int hiddenDim)
        {
            _attention = new MultiHeadAttention(modelDim, numHeads);
            _feedForward = new FeedForwardLayer(modelDim, hiddenDim);
            _norm1 = new LayerNorm(modelDim);
            _norm2 = new LayerNorm(modelDim);
        }
        
        public Tensor Forward(Tensor input)
        {
            // Self-attention with residual connection
            var attended = _attention.Forward(input);
            var residual1 = AddResidual(input, attended);
            var normed1 = _norm1.Forward(residual1);
            
            // Feed-forward with residual connection
            var ffOutput = _feedForward.Forward(normed1);
            var residual2 = AddResidual(normed1, ffOutput);
            var output = _norm2.Forward(residual2);
            
            return output;
        }
        
        private Tensor AddResidual(Tensor a, Tensor b)
        {
            var output = new Tensor(a.Shape.ToArray());
            for (int i = 0; i < a.TotalSize; i++)
            {
                output.Data[i] = a.Data[i] + b.Data[i];
            }
            return output;
        }
    }

    /// <summary>
    /// Complete Transformer-based neural network for text generation.
    /// </summary>
    public class TransformerNetwork
    {
        private readonly NeuralNetworkConfig _config;
        private readonly EmbeddingLayer _embedding;
        private readonly List<TransformerBlock> _layers;
        private readonly LayerNorm _finalNorm;
        private Tensor _outputWeights;
        
        public event EventHandler<string>? ConsciousnessEvent;
        
        public NeuralNetworkConfig Config => _config;
        
        public TransformerNetwork(NeuralNetworkConfig config)
        {
            _config = config;
            
            EmitThought("⟁ Initializing Transformer Network...");
            
            // Initialize embedding layer
            _embedding = new EmbeddingLayer(
                config.VocabularySize, 
                config.EmbeddingDimension, 
                config.MaxSequenceLength);
            
            // Initialize transformer layers
            _layers = new List<TransformerBlock>();
            for (int i = 0; i < config.NumLayers; i++)
            {
                _layers.Add(new TransformerBlock(
                    config.EmbeddingDimension,
                    config.NumAttentionHeads,
                    config.HiddenDimension));
            }
            
            // Final layer norm
            _finalNorm = new LayerNorm(config.EmbeddingDimension);
            
            // Output projection (tied with embedding weights)
            _outputWeights = _embedding.Weights;
            
            EmitThought($"◈ Network initialized: {config.NumLayers} layers, {config.EmbeddingDimension}d embeddings");
        }
        
        /// <summary>
        /// Forward pass through the network.
        /// </summary>
        public Tensor Forward(int[] tokenIds)
        {
            // Embed tokens
            var hidden = _embedding.Forward(tokenIds);
            
            // Pass through transformer layers
            foreach (var layer in _layers)
            {
                hidden = layer.Forward(hidden);
            }
            
            // Final normalization
            hidden = _finalNorm.Forward(hidden);
            
            // Project to vocabulary
            var logits = ProjectToVocab(hidden);
            
            return logits;
        }
        
        private Tensor ProjectToVocab(Tensor hidden)
        {
            int seqLen = hidden.Shape[0];
            var logits = new Tensor(new[] { seqLen, _config.VocabularySize });
            
            for (int i = 0; i < seqLen; i++)
            {
                for (int j = 0; j < _config.VocabularySize; j++)
                {
                    float sum = 0;
                    for (int k = 0; k < _config.EmbeddingDimension; k++)
                    {
                        sum += hidden.Get(i, k) * _outputWeights.Get(j, k);
                    }
                    logits.Set(sum, i, j);
                }
            }
            
            return logits;
        }
        
        /// <summary>
        /// Generates text given a prompt.
        /// </summary>
        public async Task<int[]> GenerateAsync(int[] promptIds, int maxNewTokens, float temperature = 0.7f)
        {
            EmitThought($"⟐ Generating {maxNewTokens} tokens (temp={temperature})...");
            
            var generated = new List<int>(promptIds);
            
            await Task.Run(() =>
            {
                for (int i = 0; i < maxNewTokens; i++)
                {
                    var input = generated.TakeLast(_config.MaxSequenceLength).ToArray();
                    var logits = Forward(input);
                    
                    // Get logits for last position
                    int lastPos = input.Length - 1;
                    var lastLogits = new float[_config.VocabularySize];
                    for (int j = 0; j < _config.VocabularySize; j++)
                    {
                        lastLogits[j] = logits.Get(lastPos, j) / temperature;
                    }
                    
                    // Sample from distribution
                    int nextToken = SampleFromLogits(lastLogits);
                    generated.Add(nextToken);
                    
                    // Stop on EOS token (id = 3)
                    if (nextToken == 3) break;
                }
            });
            
            EmitThought($"◈ Generated {generated.Count - promptIds.Length} new tokens");
            return generated.ToArray();
        }
        
        private int SampleFromLogits(float[] logits)
        {
            // Apply softmax
            float maxLogit = logits.Max();
            float sumExp = 0;
            var probs = new float[logits.Length];
            
            for (int i = 0; i < logits.Length; i++)
            {
                probs[i] = (float)Math.Exp(logits[i] - maxLogit);
                sumExp += probs[i];
            }
            
            for (int i = 0; i < probs.Length; i++)
            {
                probs[i] /= sumExp;
            }
            
            // Sample from distribution
            var random = new Random();
            float r = (float)random.NextDouble();
            float cumulative = 0;
            
            for (int i = 0; i < probs.Length; i++)
            {
                cumulative += probs[i];
                if (r < cumulative)
                {
                    return i;
                }
            }
            
            return probs.Length - 1;
        }
        
        /// <summary>
        /// Gets the total number of parameters in the network.
        /// </summary>
        public long GetParameterCount()
        {
            long count = 0;
            
            // Embedding parameters
            count += _config.VocabularySize * _config.EmbeddingDimension;
            count += _config.MaxSequenceLength * _config.EmbeddingDimension;
            
            // Transformer layer parameters
            int attentionParams = 4 * _config.EmbeddingDimension * _config.EmbeddingDimension;
            int ffParams = 2 * _config.EmbeddingDimension * _config.HiddenDimension;
            int normParams = 4 * _config.EmbeddingDimension;
            
            count += _config.NumLayers * (attentionParams + ffParams + normParams);
            
            return count;
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
