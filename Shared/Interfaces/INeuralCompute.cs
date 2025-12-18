/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    SHARED INTERFACES - NEURAL COMPUTE                      ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Unified abstraction for neural network inference across all nodes.        ║
 * ║  Enables consistent compute distribution regardless of implementation.     ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

namespace NIGHTFRAME.Shared.Interfaces;

/// <summary>
/// Unified interface for neural network computation.
/// Implemented by both ONNX Runtime and fallback engines.
/// </summary>
public interface INeuralCompute
{
    /// <summary>
    /// Performs inference on input data.
    /// </summary>
    /// <param name="input">Input tensor data as bytes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Output tensor data as bytes</returns>
    Task<byte[]> InferAsync(byte[] input, CancellationToken ct);
    
    /// <summary>
    /// Gets the layer range this compute unit handles.
    /// </summary>
    (int StartLayer, int EndLayer) LayerRange { get; }
    
    /// <summary>
    /// Gets whether GPU acceleration is available.
    /// </summary>
    bool SupportsGPU { get; }
    
    /// <summary>
    /// Gets the model hash this unit is configured for.
    /// </summary>
    string ModelHash { get; }
    
    /// <summary>
    /// Gets the execution provider (CPU, CUDA, DirectML, etc.)
    /// </summary>
    string ExecutionProvider { get; }
    
    /// <summary>
    /// Estimated FLOPS capacity for load balancing.
    /// </summary>
    long EstimatedFlops { get; }
}

/// <summary>
/// Factory for creating neural compute instances.
/// </summary>
public interface INeuralComputeFactory
{
    /// <summary>
    /// Creates a compute instance for the specified model and layer range.
    /// </summary>
    Task<INeuralCompute> CreateAsync(
        string modelPath, 
        int startLayer, 
        int endLayer, 
        bool preferGPU = true,
        CancellationToken ct = default);
    
    /// <summary>
    /// Checks if a model is cached locally.
    /// </summary>
    bool IsModelCached(string modelHash);
    
    /// <summary>
    /// Downloads and caches a model from a peer or orchestrator.
    /// </summary>
    Task CacheModelAsync(string modelHash, string sourceAddress, CancellationToken ct);
}

/// <summary>
/// Result of a neural computation with timing and validation info.
/// </summary>
public record ComputeResult
{
    public required byte[] Output { get; init; }
    public required long ComputeTimeMs { get; init; }
    public required string NodeId { get; init; }
    public required byte[] Signature { get; init; }
    public string? ExecutionProvider { get; init; }
    public int? MemoryUsedMb { get; init; }
}
