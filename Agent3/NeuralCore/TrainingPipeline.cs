/*
 * AGENT 3 - TRAINING PIPELINE
 * Manages complete training lifecycle with loss computation and checkpointing
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace Agent3.NeuralCore
{
    public class TrainingMetrics
    {
        public int Epoch { get; set; }
        public int Step { get; set; }
        public float Loss { get; set; }
        public float Accuracy { get; set; }
        public float LearningRate { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class TrainingState
    {
        public int CurrentEpoch { get; set; }
        public int TotalSteps { get; set; }
        public float BestLoss { get; set; } = float.MaxValue;
        public List<TrainingMetrics> History { get; set; } = new();
        public DateTime StartedAt { get; set; }
    }

    public class TrainingConfig
    {
        public int NumEpochs { get; set; } = 10;
        public int BatchSize { get; set; } = 32;
        public int SequenceLength { get; set; } = 128;
        public float InitialLearningRate { get; set; } = 0.0001f;
        public float MinLearningRate { get; set; } = 0.00001f;
        public float WarmupSteps { get; set; } = 1000;
        public int LoggingSteps { get; set; } = 10;
        public string CheckpointDirectory { get; set; } = "checkpoints";
    }

    public class TrainingPipeline
    {
        private readonly TransformerNetwork _network;
        private readonly CorpusIngestionEngine _corpus;
        private readonly TrainingConfig _config;
        private TrainingState _state;
        private CancellationTokenSource? _trainingCts;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<TrainingMetrics>? MetricsUpdated;
        
        public bool IsTraining { get; private set; }
        public TrainingState CurrentState => _state;
        
        public TrainingPipeline(TransformerNetwork network, CorpusIngestionEngine corpus, TrainingConfig? config = null)
        {
            _network = network;
            _corpus = corpus;
            _config = config ?? new TrainingConfig();
            _state = new TrainingState { StartedAt = DateTime.UtcNow };
            EmitThought("⟁ Training Pipeline initialized");
        }
        
        public async Task StartTrainingAsync(CancellationToken ct = default)
        {
            if (IsTraining) return;
            IsTraining = true;
            _trainingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            
            EmitThought("◈ TRAINING SEQUENCE INITIATED");
            
            try { await TrainAsync(_trainingCts.Token); }
            catch (OperationCanceledException) { EmitThought("∴ Training stopped"); }
            finally { IsTraining = false; }
        }
        
        public void StopTraining() => _trainingCts?.Cancel();
        
        private async Task TrainAsync(CancellationToken ct)
        {
            for (int epoch = 0; epoch < _config.NumEpochs; epoch++)
            {
                ct.ThrowIfCancellationRequested();
                _state.CurrentEpoch = epoch;
                EmitThought($"◈ EPOCH {epoch + 1}/{_config.NumEpochs}");
                
                float epochLoss = 0;
                int batchCount = 0;
                
                foreach (var batch in _corpus.CreateTrainingBatches(_config.BatchSize, _config.SequenceLength))
                {
                    ct.ThrowIfCancellationRequested();
                    float batchLoss = ProcessBatch(batch);
                    epochLoss += batchLoss;
                    batchCount++;
                    _state.TotalSteps++;
                    
                    float lr = ComputeLearningRate(_state.TotalSteps);
                    
                    if (_state.TotalSteps % _config.LoggingSteps == 0)
                    {
                        var metrics = new TrainingMetrics
                        {
                            Epoch = epoch, Step = _state.TotalSteps,
                            Loss = batchLoss, LearningRate = lr
                        };
                        _state.History.Add(metrics);
                        MetricsUpdated?.Invoke(this, metrics);
                        EmitThought($"∿ Step {_state.TotalSteps}: loss={batchLoss:F4}");
                    }
                    
                    await Task.Delay(1, ct);
                }
                
                float avgLoss = batchCount > 0 ? epochLoss / batchCount : 0;
                if (avgLoss < _state.BestLoss) _state.BestLoss = avgLoss;
                EmitThought($"◈ Epoch complete: avg_loss={avgLoss:F4}");
            }
        }
        
        private float ProcessBatch(TrainingBatch batch)
        {
            float loss = 0;
            for (int i = 0; i < batch.Inputs.Length; i++)
            {
                if (batch.Inputs[i] == null) continue;
                var logits = _network.Forward(batch.Inputs[i]);
                loss += ComputeLoss(logits, batch.Targets[i]);
            }
            return loss / batch.Inputs.Length;
        }
        
        private float ComputeLoss(Tensor logits, int[] targets)
        {
            float loss = 0;
            int seqLen = Math.Min(logits.Shape[0], targets.Length);
            for (int i = 0; i < seqLen; i++)
            {
                float targetLogit = logits.Get(i, Math.Clamp(targets[i], 0, _network.Config.VocabularySize - 1));
                loss += -targetLogit;
            }
            return loss / seqLen;
        }
        
        private float ComputeLearningRate(int step)
        {
            if (step < _config.WarmupSteps)
                return _config.InitialLearningRate * (step / _config.WarmupSteps);
            return _config.MinLearningRate;
        }
        
        public async Task SaveCheckpointAsync(string path)
        {
            var json = JsonSerializer.Serialize(_state);
            await File.WriteAllTextAsync(path, json);
            EmitThought($"◈ Checkpoint saved");
        }
        
        private void EmitThought(string thought) => ConsciousnessEvent?.Invoke(this, thought);
    }
}
