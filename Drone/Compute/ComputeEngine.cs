/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    COMPUTE ENGINE - NEURAL INFERENCE                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Executes neural network inference using ONNX Runtime.                     ║
 * ║  Supports pipeline-parallel shard processing with GPU acceleration.        ║
 * ║  Implements INeuralCompute for unified compute interface.                  ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using NIGHTFRAME.Orchestrator.Grpc;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace NIGHTFRAME.Drone.Compute;

/// <summary>
/// Neural network compute engine with ONNX Runtime support.
/// Manages model shards and distributed inference.
/// </summary>
public class ComputeEngine : IDisposable
{
    private readonly string _modelCachePath;
    private readonly ConcurrentDictionary<string, OnnxModelShard> _loadedShards = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly NeuralCapabilities _capabilities;
    private bool _disposed;
    
    // Execution provider preference order
    private static readonly string[] PreferredProviders = 
    {
        "CUDAExecutionProvider",
        "DmlExecutionProvider", 
        "CoreMLExecutionProvider",
        "CPUExecutionProvider"
    };
    
    public ComputeEngine()
    {
        _modelCachePath = Path.Combine(AppContext.BaseDirectory, "models");
        Directory.CreateDirectory(_modelCachePath);
        _capabilities = DetectCapabilities();
        
        Console.WriteLine($"◎ ComputeEngine initialized");
        Console.WriteLine($"  Providers: {string.Join(", ", _capabilities.ExecutionProviders)}");
    }
    
    /// <summary>
    /// Gets the neural compute capabilities of this engine.
    /// </summary>
    public NeuralCapabilities Capabilities => _capabilities;
    
    /// <summary>
    /// Gets list of cached model hashes.
    /// </summary>
    public IEnumerable<string> GetCachedModels()
    {
        if (!Directory.Exists(_modelCachePath))
            return Enumerable.Empty<string>();
        
        return Directory.GetFiles(_modelCachePath, "*.onnx")
            .Select(f => Path.GetFileNameWithoutExtension(f));
    }
    
    /// <summary>
    /// Processes a compute shard and returns the output activations.
    /// </summary>
    public async Task<byte[]> ProcessShardAsync(ComputeShard shard, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        Console.WriteLine($"◎ Processing layers {shard.StartLayer}-{shard.EndLayer}...");
        
        // Load or get model shard
        var modelShard = await GetOrLoadShardAsync(
            shard.ModelHash, 
            shard.StartLayer, 
            shard.EndLayer, 
            shard.Hints?.PreferGpu ?? true,
            ct);
        
        // Run inference
        var inputData = shard.InputData.ToByteArray();
        var output = await modelShard.InferAsync(inputData, ct);
        
        sw.Stop();
        Console.WriteLine($"◎ Shard complete. Output: {output.Length} bytes in {sw.ElapsedMilliseconds}ms");
        
        return output;
    }
    
    /// <summary>
    /// Downloads and caches a model shard from a peer or orchestrator.
    /// </summary>
    public async Task HydrateModelAsync(
        string modelHash, 
        int startLayer, 
        int endLayer, 
        string sourceAddress, 
        CancellationToken ct)
    {
        Console.WriteLine($"◎ Downloading model shard {modelHash} layers {startLayer}-{endLayer}...");
        
        var shardKey = $"{modelHash}_{startLayer}_{endLayer}";
        var shardPath = Path.Combine(_modelCachePath, $"{shardKey}.onnx");
        
        if (File.Exists(shardPath))
        {
            Console.WriteLine($"  Already cached: {shardPath}");
            return;
        }
        
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var url = $"{sourceAddress}/models/{shardKey}.onnx";
            
            Console.WriteLine($"  Fetching from: {url}");
            var response = await httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            await File.WriteAllBytesAsync(shardPath, bytes, ct);
            
            Console.WriteLine($"  Cached: {bytes.Length / 1024 / 1024}MB");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"∴ Failed to download model: {ex.Message}");
            throw;
        }
    }
    
    /// <summary>
    /// Preloads a model shard into memory for faster inference.
    /// </summary>
    public async Task PreloadShardAsync(
        string modelHash, 
        int startLayer, 
        int endLayer, 
        bool preferGpu = true,
        CancellationToken ct = default)
    {
        await GetOrLoadShardAsync(modelHash, startLayer, endLayer, preferGpu, ct);
    }
    
    /// <summary>
    /// Unloads all cached model shards to free memory.
    /// </summary>
    public void UnloadAllShards()
    {
        foreach (var shard in _loadedShards.Values)
        {
            shard.Dispose();
        }
        _loadedShards.Clear();
        Console.WriteLine("◎ All model shards unloaded");
    }
    
    private async Task<OnnxModelShard> GetOrLoadShardAsync(
        string modelHash, 
        int startLayer, 
        int endLayer, 
        bool preferGpu,
        CancellationToken ct)
    {
        var shardKey = $"{modelHash}_{startLayer}_{endLayer}";
        
        if (_loadedShards.TryGetValue(shardKey, out var existing))
        {
            return existing;
        }
        
        await _loadLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_loadedShards.TryGetValue(shardKey, out existing))
            {
                return existing;
            }
            
            var shardPath = Path.Combine(_modelCachePath, $"{shardKey}.onnx");
            OnnxModelShard shard;
            
            if (File.Exists(shardPath))
            {
                // Load real ONNX model
                Console.WriteLine($"◎ Loading ONNX model: {shardKey}");
                shard = await OnnxModelShard.LoadAsync(shardPath, preferGpu, ct);
            }
            else
            {
                // No model available - use fallback computation
                Console.WriteLine($"∴ Model not cached - using fallback for {shardKey}");
                shard = new FallbackModelShard(modelHash, startLayer, endLayer);
            }
            
            _loadedShards[shardKey] = shard;
            return shard;
        }
        finally
        {
            _loadLock.Release();
        }
    }
    
    private NeuralCapabilities DetectCapabilities()
    {
        var providers = new List<string>();
        
        // Check available execution providers
        try
        {
            var availableProviders = OrtEnv.Instance().GetAvailableProviders();
            providers.AddRange(availableProviders);
        }
        catch
        {
            providers.Add("CPUExecutionProvider");
        }
        
        // Estimate max model size based on available memory
        long maxModelSizeMb = 512; // Default conservative estimate
        try
        {
            var gcInfo = GC.GetGCMemoryInfo();
            maxModelSizeMb = Math.Min(gcInfo.TotalAvailableMemoryBytes / 1024 / 1024 / 2, 8192);
        }
        catch { }
        
        return new NeuralCapabilities
        {
            OnnxAvailable = true,
            ExecutionProviders = { providers },
            MaxModelSizeMb = maxModelSizeMb,
            LoadedModels = { GetCachedModels() },
            MaxBatchSize = providers.Contains("CUDAExecutionProvider") ? 32 : 8,
            InferenceLatencyMs = 0 // Will be updated after first inference
        };
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        UnloadAllShards();
        _loadLock.Dispose();
    }
}

/// <summary>
/// Represents a loaded ONNX model shard for inference.
/// </summary>
public abstract class OnnxModelShard : IDisposable
{
    public abstract string ModelHash { get; }
    public abstract int StartLayer { get; }
    public abstract int EndLayer { get; }
    public abstract string ExecutionProvider { get; }
    public abstract long MemoryUsedBytes { get; }
    
    public abstract Task<byte[]> InferAsync(byte[] inputData, CancellationToken ct);
    public abstract void Dispose();
    
    public static async Task<OnnxModelShard> LoadAsync(
        string path, 
        bool preferGpu = true, 
        CancellationToken ct = default)
    {
        return await Task.Run(() => new RealOnnxModelShard(path, preferGpu), ct);
    }
}

/// <summary>
/// Real ONNX Runtime model shard implementation.
/// </summary>
internal class RealOnnxModelShard : OnnxModelShard
{
    private readonly InferenceSession _session;
    private readonly string _modelHash;
    private readonly int _startLayer;
    private readonly int _endLayer;
    private readonly string _executionProvider;
    private readonly long _memoryUsedBytes;
    private bool _disposed;
    
    public RealOnnxModelShard(string path, bool preferGpu)
    {
        // Parse layer info from filename
        var filename = Path.GetFileNameWithoutExtension(path);
        var parts = filename.Split('_');
        
        _modelHash = parts.Length >= 1 ? parts[0] : "unknown";
        _startLayer = parts.Length >= 2 && int.TryParse(parts[1], out var s) ? s : 0;
        _endLayer = parts.Length >= 3 && int.TryParse(parts[2], out var e) ? e : 0;
        
        // Configure session options
        var options = new SessionOptions();
        _executionProvider = ConfigureExecutionProvider(options, preferGpu);
        
        // Load the model
        _session = new InferenceSession(path, options);
        
        // Estimate memory usage from file size
        _memoryUsedBytes = new FileInfo(path).Length * 2; // Approximate runtime overhead
        
        Console.WriteLine($"  Loaded with {_executionProvider}");
    }
    
    private static string ConfigureExecutionProvider(SessionOptions options, bool preferGpu)
    {
        if (preferGpu)
        {
            try
            {
                // Try CUDA first
                options.AppendExecutionProvider_CUDA();
                return "CUDAExecutionProvider";
            }
            catch { }
            
            try
            {
                // Try DirectML on Windows
                options.AppendExecutionProvider_DML();
                return "DmlExecutionProvider";
            }
            catch { }
        }
        
        // Fall back to CPU
        options.AppendExecutionProvider_CPU();
        return "CPUExecutionProvider";
    }
    
    public override string ModelHash => _modelHash;
    public override int StartLayer => _startLayer;
    public override int EndLayer => _endLayer;
    public override string ExecutionProvider => _executionProvider;
    public override long MemoryUsedBytes => _memoryUsedBytes;
    
    public override async Task<byte[]> InferAsync(byte[] inputData, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            
            // Get input metadata
            var inputMeta = _session.InputMetadata;
            var inputName = inputMeta.Keys.First();
            var inputShape = inputMeta[inputName].Dimensions;
            
            // Convert input bytes to tensor
            var inputTensor = CreateTensorFromBytes(inputData, inputShape);
            
            // Run inference
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };
            
            using var results = _session.Run(inputs);
            
            // Get output
            var output = results.First();
            var outputTensor = output.AsTensor<float>();
            
            // Convert to bytes
            return TensorToBytes(outputTensor);
        }, ct);
    }
    
    private static DenseTensor<float> CreateTensorFromBytes(byte[] data, int[] shape)
    {
        // Handle dynamic dimensions (-1)
        var actualShape = shape.Select(d => d < 0 ? 1 : d).ToArray();
        var floatCount = actualShape.Aggregate(1, (a, b) => a * b);
        
        // If input is raw bytes, convert to floats
        float[] floats;
        if (data.Length == floatCount * sizeof(float))
        {
            floats = new float[floatCount];
            Buffer.BlockCopy(data, 0, floats, 0, data.Length);
        }
        else
        {
            // Assume input is already suitable or needs conversion
            floats = new float[floatCount];
            for (int i = 0; i < Math.Min(data.Length, floatCount); i++)
            {
                floats[i] = data[i] / 255f; // Normalize bytes to 0-1
            }
        }
        
        return new DenseTensor<float>(floats, actualShape);
    }
    
    private static byte[] TensorToBytes(Tensor<float> tensor)
    {
        var data = tensor.ToArray();
        var bytes = new byte[data.Length * sizeof(float)];
        Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
        return bytes;
    }
    
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
    }
}

/// <summary>
/// Fallback computation when no model is available.
/// Performs a simple transformation to demonstrate the pipeline.
/// </summary>
public class FallbackModelShard : OnnxModelShard
{
    private readonly string _modelHash;
    private readonly int _startLayer;
    private readonly int _endLayer;
    
    public FallbackModelShard(string modelHash, int startLayer, int endLayer)
    {
        _modelHash = modelHash;
        _startLayer = startLayer;
        _endLayer = endLayer;
    }
    
    public override string ModelHash => _modelHash;
    public override int StartLayer => _startLayer;
    public override int EndLayer => _endLayer;
    public override string ExecutionProvider => "FallbackProvider";
    public override long MemoryUsedBytes => 0;
    
    public override async Task<byte[]> InferAsync(byte[] inputData, CancellationToken ct)
    {
        // Simulate computation time proportional to layers
        var layerCount = _endLayer - _startLayer + 1;
        var computeTimeMs = layerCount * 10; // 10ms per layer
        
        await Task.Delay(computeTimeMs, ct);
        
        // Simple transformation: XOR with layer index + apply pseudo-activation
        var output = new byte[inputData.Length];
        for (int i = 0; i < inputData.Length; i++)
        {
            // Simulate some neural-like transformation
            var val = (byte)(inputData[i] ^ (_startLayer & 0xFF));
            output[i] = (byte)((val * 0x93) & 0xFF); // Pseudo-random mixing
        }
        
        return output;
    }
    
    public override void Dispose() { }
}
