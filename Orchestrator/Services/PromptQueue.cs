/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    PROMPT QUEUE - NATURAL LANGUAGE INPUT                   ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Queues user prompts for distributed processing.                           ║
 * ║  Handles the "Pending State" pattern when no nodes are available.          ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Collections.Concurrent;
using System.Text;

namespace NIGHTFRAME.Orchestrator.Services;

public enum PromptStatus { Queued, Processing, Completed, Failed }

public class PromptEntry
{
    public required string PromptId { get; init; }
    public required string Prompt { get; init; }
    public required int Priority { get; init; }
    public required DateTime CreatedAt { get; init; }
    public PromptStatus Status { get; set; } = PromptStatus.Queued;
    public string? JobId { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PromptQueue
{
    private readonly ConcurrentDictionary<string, PromptEntry> _prompts = new();
    private readonly PriorityQueue<string, int> _queue = new();
    private readonly ShardCoordinator _shardCoordinator;
    private readonly ILogger<PromptQueue> _logger;
    private readonly object _queueLock = new();
    
    public int PendingCount => _prompts.Values.Count(p => p.Status == PromptStatus.Queued || p.Status == PromptStatus.Processing);
    
    public PromptQueue(ShardCoordinator shardCoordinator, ILogger<PromptQueue> logger)
    {
        _shardCoordinator = shardCoordinator;
        _logger = logger;
    }

    
    /// <summary>
    /// Enqueues a prompt for processing.
    /// </summary>
    public async Task<string> EnqueueAsync(string prompt, int priority = 5)
    {
        var promptId = $"PROMPT_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}"[..24];
        
        var entry = new PromptEntry
        {
            PromptId = promptId,
            Prompt = prompt,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        };
        
        _prompts[promptId] = entry;
        
        lock (_queueLock)
        {
            // Lower number = higher priority
            _queue.Enqueue(promptId, 10 - Math.Clamp(priority, 1, 10));
        }
        
        _logger.LogInformation("◈ Prompt {PromptId} queued (priority {Priority})", promptId, priority);
        
        // Try to process immediately
        await TryProcessNextAsync();
        
        return promptId;
    }
    
    /// <summary>
    /// Attempts to process the next prompt in queue.
    /// </summary>
    public async Task TryProcessNextAsync()
    {
        string? promptId;
        
        lock (_queueLock)
        {
            if (!_queue.TryDequeue(out promptId, out _))
                return;
        }
        
        if (!_prompts.TryGetValue(promptId, out var entry))
            return;
        
        entry.Status = PromptStatus.Processing;
        
        try
        {
            // Tokenize prompt into input data
            var inputData = TokenizePrompt(entry.Prompt);
            
            // Create inference job
            var jobId = await _shardCoordinator.CreateInferenceJobAsync(promptId, inputData);
            entry.JobId = jobId;
            
            _logger.LogInformation("◈ Prompt {PromptId} now processing as job {JobId}", promptId, jobId);
        }
        catch (Exception ex)
        {
            entry.Status = PromptStatus.Failed;
            entry.Error = ex.Message;
            _logger.LogError(ex, "∴ Failed to process prompt {PromptId}", promptId);
        }
    }
    
    /// <summary>
    /// Gets the status of a prompt.
    /// </summary>
    public object? GetStatus(string promptId)
    {
        if (!_prompts.TryGetValue(promptId, out var entry))
            return null;
        
        object? jobStatus = null;
        if (entry.JobId != null)
        {
            jobStatus = _shardCoordinator.GetJobStatus(entry.JobId);
        }
        
        return new
        {
            entry.PromptId,
            entry.Prompt,
            entry.Status,
            entry.Priority,
            entry.CreatedAt,
            entry.JobId,
            entry.Result,
            entry.Error,
            entry.CompletedAt,
            job = jobStatus
        };
    }
    
    /// <summary>
    /// Updates prompt with result when job completes.
    /// </summary>
    public void SetResult(string promptId, string result)
    {
        if (_prompts.TryGetValue(promptId, out var entry))
        {
            entry.Status = PromptStatus.Completed;
            entry.Result = result;
            entry.CompletedAt = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Gets all prompts for a time range.
    /// </summary>
    public IEnumerable<object> GetRecent(int count = 50)
    {
        return _prompts.Values
            .OrderByDescending(p => p.CreatedAt)
            .Take(count)
            .Select(p => new
            {
                p.PromptId,
                prompt = p.Prompt.Length > 100 ? p.Prompt[..100] + "..." : p.Prompt,
                p.Status,
                p.Priority,
                p.CreatedAt,
                p.CompletedAt
            });
    }
    
    /// <summary>
    /// Simple tokenization for neural input.
    /// In production, use proper BPE tokenizer.
    /// </summary>
    private byte[] TokenizePrompt(string prompt)
    {
        // Simple UTF8 encoding for now
        // TODO: Replace with proper BPE tokenizer
        var tokens = Encoding.UTF8.GetBytes(prompt);
        
        // Pad or truncate to fixed size
        var maxLength = 512;
        if (tokens.Length > maxLength)
        {
            return tokens[..maxLength];
        }
        
        var padded = new byte[maxLength];
        Array.Copy(tokens, padded, tokens.Length);
        return padded;
    }
}
