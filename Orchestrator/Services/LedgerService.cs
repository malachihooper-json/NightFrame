/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    LEDGER SERVICE - THE ACCOUNTANT                         ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Implements double-entry accounting for the internal economy.              ║
 * ║  Credits (compute contribution) vs Debits (user prompts).                  ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System.Collections.Concurrent;
using LiteDB;

namespace NIGHTFRAME.Orchestrator.Services;

public enum TransactionType
{
    ComputeContribution,    // Node completed a shard
    PromptSubmission,       // User submitted a prompt
    StorageContribution,    // Node stored data
    NetworkRelay,           // Node relayed traffic
    PenaltyFraud,           // Node submitted bad data
    BonusUptime             // Long uptime bonus
}

public class LedgerEntry
{
    public ObjectId Id { get; set; } = ObjectId.NewObjectId();
    public required string NodeId { get; init; }
    public required TransactionType Type { get; init; }
    public required long Amount { get; init; }  // Positive = credit, Negative = debit
    public required DateTime Timestamp { get; init; }
    public required string Reference { get; init; }  // Task ID, Shard ID, etc.
    public string? Details { get; init; }
}

public class NodeBalance
{
    public required string NodeId { get; init; }
    public long Credits { get; set; }
    public long Debits { get; set; }
    public long Balance => Credits - Debits;
    public DateTime LastActivity { get; set; }
}

public class LedgerService
{
    private readonly ILiteDatabase _db;
    private readonly ILiteCollection<LedgerEntry> _entries;
    private readonly ConcurrentDictionary<string, NodeBalance> _balanceCache = new();
    private readonly ILogger<LedgerService> _logger;
    
    // Credit values for different contributions
    public const long CreditsPerComputeShard = 100;
    public const long CreditsPerGBStored = 10;
    public const long CreditsPerRelayKB = 1;
    public const long CreditsPerUptimeHour = 5;
    
    // Debit costs
    public const long DebitPerPrompt = 50;
    public const long DebitPerTrainingRun = 500;
    
    public long TotalOperations => _entries.LongCount();
    
    public LedgerService(ILogger<LedgerService> logger)
    {
        _logger = logger;
        
        // Use LiteDB for embedded persistence
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NIGHTFRAME", "ledger.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _db = new LiteDatabase(dbPath);
        _entries = _db.GetCollection<LedgerEntry>("ledger");
        
        // Create indexes
        _entries.EnsureIndex(x => x.NodeId);
        _entries.EnsureIndex(x => x.Timestamp);
        _entries.EnsureIndex(x => x.Type);
        
        // Load balance cache
        LoadBalanceCache();
    }
    
    /// <summary>
    /// Records a compute contribution credit.
    /// </summary>
    public void CreditComputeWork(string nodeId, string shardId, long computeTimeMs)
    {
        // Bonus for fast completion
        long baseCredits = CreditsPerComputeShard;
        if (computeTimeMs < 1000) baseCredits = (long)(baseCredits * 1.5); // 50% bonus for <1s
        
        RecordEntry(new LedgerEntry
        {
            NodeId = nodeId,
            Type = TransactionType.ComputeContribution,
            Amount = baseCredits,
            Timestamp = DateTime.UtcNow,
            Reference = shardId,
            Details = $"Compute time: {computeTimeMs}ms"
        });
        
        _logger.LogDebug("◎ Credited {Credits} to {NodeId} for shard {ShardId}", baseCredits, nodeId, shardId);
    }
    
    /// <summary>
    /// Records a storage contribution credit.
    /// </summary>
    public void CreditStorageContribution(string nodeId, string fileHash, long sizeBytes)
    {
        long credits = (long)((sizeBytes / (1024.0 * 1024 * 1024)) * CreditsPerGBStored);
        credits = Math.Max(1, credits); // Minimum 1 credit
        
        RecordEntry(new LedgerEntry
        {
            NodeId = nodeId,
            Type = TransactionType.StorageContribution,
            Amount = credits,
            Timestamp = DateTime.UtcNow,
            Reference = fileHash,
            Details = $"Size: {sizeBytes} bytes"
        });
    }
    
    /// <summary>
    /// Debits account for a prompt submission.
    /// </summary>
    public bool DebitPromptSubmission(string nodeId, string promptId)
    {
        var balance = GetBalance(nodeId);
        
        // Allow negative balance up to a limit (credit system)
        if (balance.Balance < -10000)
        {
            _logger.LogWarning("∴ Node {NodeId} exceeded debit limit", nodeId);
            return false;
        }
        
        RecordEntry(new LedgerEntry
        {
            NodeId = nodeId,
            Type = TransactionType.PromptSubmission,
            Amount = -DebitPerPrompt,
            Timestamp = DateTime.UtcNow,
            Reference = promptId
        });
        
        return true;
    }
    
    /// <summary>
    /// Applies a penalty for fraudulent/bad compute results.
    /// </summary>
    public void ApplyFraudPenalty(string nodeId, string shardId, string reason)
    {
        RecordEntry(new LedgerEntry
        {
            NodeId = nodeId,
            Type = TransactionType.PenaltyFraud,
            Amount = -CreditsPerComputeShard * 10, // 10x penalty
            Timestamp = DateTime.UtcNow,
            Reference = shardId,
            Details = reason
        });
        
        _logger.LogWarning("∴ Fraud penalty applied to {NodeId}: {Reason}", nodeId, reason);
    }
    
    /// <summary>
    /// Awards uptime bonus.
    /// </summary>
    public void AwardUptimeBonus(string nodeId, long uptimeHours)
    {
        RecordEntry(new LedgerEntry
        {
            NodeId = nodeId,
            Type = TransactionType.BonusUptime,
            Amount = uptimeHours * CreditsPerUptimeHour,
            Timestamp = DateTime.UtcNow,
            Reference = $"uptime-{DateTime.UtcNow:yyyyMMdd}"
        });
    }
    
    /// <summary>
    /// Gets the current balance for a node.
    /// </summary>
    public NodeBalance GetBalance(string nodeId)
    {
        return _balanceCache.GetOrAdd(nodeId, id => new NodeBalance
        {
            NodeId = id,
            Credits = 0,
            Debits = 0,
            LastActivity = DateTime.UtcNow
        });
    }
    
    /// <summary>
    /// Gets total credits issued across all nodes.
    /// </summary>
    public long GetTotalCredits()
    {
        return _balanceCache.Values.Sum(b => b.Credits);
    }
    
    /// <summary>
    /// Gets the ledger summary for dashboard display.
    /// </summary>
    public object GetSummary()
    {
        var topContributors = _balanceCache.Values
            .OrderByDescending(b => b.Balance)
            .Take(10)
            .Select(b => new { b.NodeId, b.Balance, b.Credits, b.Debits });
        
        var recentActivity = _entries.Query()
            .OrderByDescending(e => e.Timestamp)
            .Limit(50)
            .ToList();
        
        return new
        {
            totalNodes = _balanceCache.Count,
            totalCreditsIssued = _balanceCache.Values.Sum(b => b.Credits),
            totalDebitsCharged = _balanceCache.Values.Sum(b => b.Debits),
            networkNetBalance = _balanceCache.Values.Sum(b => b.Balance),
            topContributors,
            recentActivity = recentActivity.Select(e => new
            {
                e.NodeId,
                e.Type,
                e.Amount,
                e.Timestamp,
                e.Reference
            })
        };
    }
    
    private void RecordEntry(LedgerEntry entry)
    {
        _entries.Insert(entry);
        
        // Update cache
        var balance = GetBalance(entry.NodeId);
        if (entry.Amount > 0)
            balance.Credits += entry.Amount;
        else
            balance.Debits += Math.Abs(entry.Amount);
        
        balance.LastActivity = entry.Timestamp;
    }
    
    private void LoadBalanceCache()
    {
        // Aggregate all entries to build balance cache
        var entries = _entries.FindAll().ToList();
        
        foreach (var group in entries.GroupBy(e => e.NodeId))
        {
            var credits = group.Where(e => e.Amount > 0).Sum(e => e.Amount);
            var debits = group.Where(e => e.Amount < 0).Sum(e => Math.Abs(e.Amount));
            var lastActivity = group.Max(e => e.Timestamp);
            
            _balanceCache[group.Key] = new NodeBalance
            {
                NodeId = group.Key,
                Credits = credits,
                Debits = debits,
                LastActivity = lastActivity
            };
        }
        
        _logger.LogInformation("◎ Loaded {Count} node balances from ledger", _balanceCache.Count);
    }
}
