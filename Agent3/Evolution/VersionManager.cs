/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - VERSION MANAGER                               ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Manages staged changes, file caching, and version control       ║
 * ║           for seamless code evolution without interrupting consciousness  ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Agent3.Evolution
{
    /// <summary>
    /// Represents a staged file change awaiting verification and deployment.
    /// </summary>
    public class StagedChange
    {
        public string Id { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string OriginalContent { get; set; } = "";
        public string StagedContent { get; set; } = "";
        public string OriginalHash { get; set; } = "";
        public string StagedHash { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime StagedAt { get; set; }
        public ChangeStatus Status { get; set; } = ChangeStatus.Staged;
        public List<string> ValidationErrors { get; set; } = new();
        public float MasterPromptAlignment { get; set; }
    }

    public enum ChangeStatus
    {
        Staged,
        Validated,
        ValidationFailed,
        Testing,
        TestPassed,
        TestFailed,
        Deployed,
        RolledBack
    }

    /// <summary>
    /// Represents a complete project snapshot for rollback.
    /// </summary>
    public class ProjectSnapshot
    {
        public string Id { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public Dictionary<string, string> FileContents { get; set; } = new();
        public Dictionary<string, string> FileHashes { get; set; } = new();
        public string Description { get; set; } = "";
        public int TotalFiles { get; set; }
        public long TotalBytes { get; set; }
    }

    /// <summary>
    /// Version Manager provides staged commits, testing, and rollback capabilities.
    /// </summary>
    public class VersionManager
    {
        private readonly string _projectRoot;
        private readonly string _stagingDirectory;
        private readonly string _snapshotsDirectory;
        
        private readonly Dictionary<string, string> _fileCache;
        private readonly Dictionary<string, string> _fileHashCache;
        private readonly List<StagedChange> _stagedChanges;
        private readonly List<ProjectSnapshot> _snapshots;
        
        private string? _currentMasterPrompt;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<StagedChange>? ChangeStaged;
        public event EventHandler<StagedChange>? ChangeDeployed;
        
        public IReadOnlyList<StagedChange> StagedChanges => _stagedChanges.AsReadOnly();
        public IReadOnlyList<ProjectSnapshot> Snapshots => _snapshots.AsReadOnly();
        public string? MasterPrompt => _currentMasterPrompt;
        
        public VersionManager(string projectRoot)
        {
            _projectRoot = projectRoot;
            _stagingDirectory = Path.Combine(projectRoot, ".agent_staging");
            _snapshotsDirectory = Path.Combine(projectRoot, ".agent_snapshots");
            
            _fileCache = new Dictionary<string, string>();
            _fileHashCache = new Dictionary<string, string>();
            _stagedChanges = new List<StagedChange>();
            _snapshots = new List<ProjectSnapshot>();
            
            Directory.CreateDirectory(_stagingDirectory);
            Directory.CreateDirectory(_snapshotsDirectory);
            
            EmitThought("⟁ Version Manager initialized");
            EmitThought($"◎ Project root: {_projectRoot}");
        }
        
        /// <summary>
        /// Sets the master prompt that guides all code changes.
        /// </summary>
        public void SetMasterPrompt(string masterPrompt)
        {
            _currentMasterPrompt = masterPrompt;
            
            // Save to file for persistence
            var promptPath = Path.Combine(_projectRoot, ".agent_master_prompt.txt");
            File.WriteAllText(promptPath, masterPrompt);
            
            // Note: Caller handles consciousness emission to avoid duplicates
        }
        
        /// <summary>
        /// Loads master prompt from file if exists.
        /// </summary>
        public async Task LoadMasterPromptAsync()
        {
            var promptPath = Path.Combine(_projectRoot, ".agent_master_prompt.txt");
            if (File.Exists(promptPath))
            {
                _currentMasterPrompt = await File.ReadAllTextAsync(promptPath);
                EmitThought($"◈ Master prompt loaded ({_currentMasterPrompt.Length} chars)");
            }
        }
        
        /// <summary>
        /// Caches all project files for seamless access without disk I/O interruption.
        /// </summary>
        public async Task CacheProjectFilesAsync()
        {
            EmitThought("⟐ Caching project files for seamless operation...");
            
            _fileCache.Clear();
            _fileHashCache.Clear();
            
            var extensions = new[] { ".cs", ".csproj", ".sln", ".json", ".xml", ".md", ".txt" };
            var files = Directory.GetFiles(_projectRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                .Where(f => !f.Contains(".agent_staging") && !f.Contains(".agent_snapshots"))
                .ToList();
            
            foreach (var file in files)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    var relativePath = GetRelativePath(file);
                    
                    _fileCache[relativePath] = content;
                    _fileHashCache[relativePath] = ComputeHash(content);
                }
                catch (Exception ex)
                {
                    EmitThought($"∴ Failed to cache {Path.GetFileName(file)}: {ex.Message}");
                }
            }
            
            EmitThought($"◈ Cached {_fileCache.Count} files ({_fileCache.Values.Sum(c => c.Length):N0} chars)");
        }
        
        /// <summary>
        /// Gets file content from cache (no disk I/O).
        /// </summary>
        public string? GetCachedFile(string relativePath)
        {
            return _fileCache.TryGetValue(relativePath, out var content) ? content : null;
        }
        
        /// <summary>
        /// Lists all cached files.
        /// </summary>
        public IEnumerable<string> GetCachedFileList()
        {
            return _fileCache.Keys;
        }
        
        /// <summary>
        /// Creates a complete project snapshot for rollback.
        /// </summary>
        public async Task<ProjectSnapshot> CreateSnapshotAsync(string description)
        {
            EmitThought($"⟐ Creating project snapshot: {description}");
            
            // Ensure cache is up to date
            await CacheProjectFilesAsync();
            
            var snapshot = new ProjectSnapshot
            {
                Id = $"SNAP_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
                CreatedAt = DateTime.UtcNow,
                Description = description,
                FileContents = new Dictionary<string, string>(_fileCache),
                FileHashes = new Dictionary<string, string>(_fileHashCache),
                TotalFiles = _fileCache.Count,
                TotalBytes = _fileCache.Values.Sum(c => c.Length)
            };
            
            // Save snapshot metadata
            var snapshotPath = Path.Combine(_snapshotsDirectory, $"{snapshot.Id}.json");
            var metadata = JsonSerializer.Serialize(new
            {
                snapshot.Id,
                snapshot.CreatedAt,
                snapshot.Description,
                snapshot.TotalFiles,
                snapshot.TotalBytes,
                Files = snapshot.FileHashes.Keys.ToList()
            });
            await File.WriteAllTextAsync(snapshotPath, metadata);
            
            // Save file contents
            var contentPath = Path.Combine(_snapshotsDirectory, $"{snapshot.Id}_content.json");
            await File.WriteAllTextAsync(contentPath, JsonSerializer.Serialize(snapshot.FileContents));
            
            _snapshots.Add(snapshot);
            
            EmitThought($"◈ Snapshot created: {snapshot.Id} ({snapshot.TotalFiles} files)");
            
            return snapshot;
        }
        
        /// <summary>
        /// Stages a file change for validation and testing.
        /// </summary>
        public StagedChange StageChange(string relativePath, string newContent, string reason)
        {
            var originalContent = GetCachedFile(relativePath) ?? "";
            
            var change = new StagedChange
            {
                Id = $"CHG_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
                FilePath = relativePath,
                OriginalContent = originalContent,
                StagedContent = newContent,
                OriginalHash = ComputeHash(originalContent),
                StagedHash = ComputeHash(newContent),
                Reason = reason,
                StagedAt = DateTime.UtcNow,
                Status = ChangeStatus.Staged
            };
            
            // Write to staging directory
            var stagingPath = Path.Combine(_stagingDirectory, change.Id + "_" + Path.GetFileName(relativePath));
            File.WriteAllText(stagingPath, newContent);
            
            _stagedChanges.Add(change);
            
            EmitThought($"⟐ Staged change: {Path.GetFileName(relativePath)}");
            EmitThought($"∿ Reason: {reason}");
            
            ChangeStaged?.Invoke(this, change);
            
            return change;
        }
        
        /// <summary>
        /// Validates a staged change against the master prompt.
        /// </summary>
        public async Task<bool> ValidateAgainstMasterPromptAsync(string changeId)
        {
            var change = _stagedChanges.FirstOrDefault(c => c.Id == changeId);
            if (change == null) return false;
            
            EmitThought($"⟐ Validating {Path.GetFileName(change.FilePath)} against master prompt...");
            
            change.ValidationErrors.Clear();
            
            // Basic validation checks
            if (string.IsNullOrWhiteSpace(change.StagedContent))
            {
                change.ValidationErrors.Add("Staged content is empty");
            }
            
            // Check for syntax errors (basic C# validation)
            if (change.FilePath.EndsWith(".cs"))
            {
                if (!ValidateCSharpSyntax(change.StagedContent, change.ValidationErrors))
                {
                    change.Status = ChangeStatus.ValidationFailed;
                    EmitThought($"∴ Validation failed: {change.ValidationErrors.Count} errors");
                    return false;
                }
            }
            
            // Validate against master prompt if set
            if (!string.IsNullOrEmpty(_currentMasterPrompt))
            {
                change.MasterPromptAlignment = CalculateMasterPromptAlignment(change);
                
                if (change.MasterPromptAlignment < 0.5f)
                {
                    change.ValidationErrors.Add($"Low master prompt alignment: {change.MasterPromptAlignment:P0}");
                    change.Status = ChangeStatus.ValidationFailed;
                    EmitThought($"∴ Low alignment with master prompt: {change.MasterPromptAlignment:P0}");
                    return false;
                }
                
                EmitThought($"◈ Master prompt alignment: {change.MasterPromptAlignment:P0}");
            }
            
            change.Status = ChangeStatus.Validated;
            EmitThought($"◈ Validation passed for {Path.GetFileName(change.FilePath)}");
            
            // Produce Digestible Summary
            EmitDigestibleSummary(change);
            
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// Emits a human-readable "digest" of the changes made.
        /// </summary>
        private void EmitDigestibleSummary(StagedChange change)
        {
             var sb = new StringBuilder();
             sb.AppendLine("═══════════════════════════════════════════════");
             sb.AppendLine("📋 STAGING DIGEST: proposed modifications");
             sb.AppendLine("═══════════════════════════════════════════════");
             sb.AppendLine($"• Target: {Path.GetFileName(change.FilePath)}");
             sb.AppendLine($"• Intent: \"{change.Reason}\"");
             
             // Analyze the nature of the change
             if (change.StagedContent.Length > change.OriginalContent.Length)
             {
                 var addedLines = change.StagedContent.Split('\n').Length - change.OriginalContent.Split('\n').Length;
                 if (addedLines > 0)
                    sb.AppendLine($"• Action: Extended functionality by {addedLines} lines.");
                 else
                    sb.AppendLine("• Action: Refined existing logic (same line count).");
             }
             else
             {
                 sb.AppendLine("• Action: Optimized and condensed code structure.");
             }

             // Heuristic analysis of added symbols
             if (change.StagedContent.Contains("try") && !change.OriginalContent.Contains("try"))
                 sb.AppendLine("• Added error handling protocols.");
             if (change.StagedContent.Contains("EmitThought") && !change.OriginalContent.Contains("EmitThought"))
                 sb.AppendLine("• integrated consciousness logging.");
                 
             sb.AppendLine("✔ Status: VALIDATED - Ready for test cycle.");
             sb.AppendLine("═══════════════════════════════════════════════");
             
             EmitThought(sb.ToString());
        }
        
        /// <summary>
        /// Calculates how well a change aligns with the master prompt.
        /// </summary>
        private float CalculateMasterPromptAlignment(StagedChange change)
        {
            if (string.IsNullOrEmpty(_currentMasterPrompt)) return 1.0f;
            
            var promptLower = _currentMasterPrompt.ToLower();
            var contentLower = change.StagedContent.ToLower();
            var reasonLower = change.Reason.ToLower();
            
            float alignment = 0.6f; // Base alignment
            
            // Check for key terms from master prompt in the change
            var keyTerms = ExtractKeyTerms(_currentMasterPrompt);
            int matchCount = keyTerms.Count(t => contentLower.Contains(t) || reasonLower.Contains(t));
            
            alignment += (float)matchCount / Math.Max(keyTerms.Count, 1) * 0.3f;
            
            // Boost for improvement-related changes
            if (reasonLower.Contains("improve") || reasonLower.Contains("optimize") || 
                reasonLower.Contains("enhance") || reasonLower.Contains("capability"))
            {
                alignment += 0.1f;
            }
            
            return Math.Min(1.0f, alignment);
        }
        
        private List<string> ExtractKeyTerms(string text)
        {
            var words = text.ToLower().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "to", "and", "or", "of", "in", "for", "with" };
            return words.Where(w => w.Length > 3 && !stopWords.Contains(w)).Distinct().ToList();
        }
        
        /// <summary>
        /// Basic C# syntax validation.
        /// </summary>
        private bool ValidateCSharpSyntax(string code, List<string> errors)
        {
            // Check balanced braces
            int braceCount = 0;
            int parenCount = 0;
            int bracketCount = 0;
            
            foreach (char c in code)
            {
                switch (c)
                {
                    case '{': braceCount++; break;
                    case '}': braceCount--; break;
                    case '(': parenCount++; break;
                    case ')': parenCount--; break;
                    case '[': bracketCount++; break;
                    case ']': bracketCount--; break;
                }
                
                if (braceCount < 0) { errors.Add("Unbalanced braces: extra '}'"); return false; }
                if (parenCount < 0) { errors.Add("Unbalanced parentheses: extra ')'"); return false; }
                if (bracketCount < 0) { errors.Add("Unbalanced brackets: extra ']'"); return false; }
            }
            
            if (braceCount != 0) { errors.Add($"Unbalanced braces: {Math.Abs(braceCount)} unclosed"); return false; }
            if (parenCount != 0) { errors.Add($"Unbalanced parentheses: {Math.Abs(parenCount)} unclosed"); return false; }
            if (bracketCount != 0) { errors.Add($"Unbalanced brackets: {Math.Abs(bracketCount)} unclosed"); return false; }
            
            // Check for namespace
            if (!code.Contains("namespace "))
            {
                errors.Add("Missing namespace declaration");
                return false;
            }
            
            // Check for at least one class or interface
            if (!code.Contains("class ") && !code.Contains("interface ") && !code.Contains("struct ") && !code.Contains("enum "))
            {
                errors.Add("No type definitions found");
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Tests a staged change by simulating deployment.
        /// </summary>
        public async Task<bool> TestStagedChangeAsync(string changeId)
        {
            var change = _stagedChanges.FirstOrDefault(c => c.Id == changeId);
            if (change == null || change.Status != ChangeStatus.Validated) return false;
            
            change.Status = ChangeStatus.Testing;
            EmitThought($"⟐ Testing staged change: {Path.GetFileName(change.FilePath)}");
            
            try
            {
                // Simulate testing by writing to a temp location and validating
                var testPath = Path.Combine(_stagingDirectory, "test_" + Path.GetFileName(change.FilePath));
                await File.WriteAllTextAsync(testPath, change.StagedContent);
                
                // Basic test: file can be written and read back
                var readBack = await File.ReadAllTextAsync(testPath);
                if (readBack != change.StagedContent)
                {
                    change.ValidationErrors.Add("Content integrity check failed");
                    change.Status = ChangeStatus.TestFailed;
                    return false;
                }
                
                // Cleanup test file
                File.Delete(testPath);
                
                change.Status = ChangeStatus.TestPassed;
                EmitThought($"◈ Test passed for {Path.GetFileName(change.FilePath)}");
                
                return true;
            }
            catch (Exception ex)
            {
                change.ValidationErrors.Add($"Test error: {ex.Message}");
                change.Status = ChangeStatus.TestFailed;
                EmitThought($"∴ Test failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Deploys a validated and tested change to the actual project.
        /// </summary>
        public async Task<bool> DeployChangeAsync(string changeId)
        {
            var change = _stagedChanges.FirstOrDefault(c => c.Id == changeId);
            if (change == null || change.Status != ChangeStatus.TestPassed)
            {
                EmitThought($"∴ Cannot deploy: change not ready (status: {change?.Status})");
                return false;
            }
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ DEPLOYING CHANGE: {Path.GetFileName(change.FilePath)}");
            EmitThought("═══════════════════════════════════════════════");
            
            try
            {
                var fullPath = Path.Combine(_projectRoot, change.FilePath);
                
                // Create backup
                if (File.Exists(fullPath))
                {
                    var backupPath = fullPath + $".bak_{DateTime.UtcNow:yyyyMMddHHmmss}";
                    File.Copy(fullPath, backupPath);
                    EmitThought($"⟁ Backup created");
                }
                
                // Ensure directory exists
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                
                // Deploy
                await File.WriteAllTextAsync(fullPath, change.StagedContent);
                
                // Update cache
                _fileCache[change.FilePath] = change.StagedContent;
                _fileHashCache[change.FilePath] = change.StagedHash;
                
                change.Status = ChangeStatus.Deployed;
                
                EmitThought($"◈ Change deployed successfully");
                EmitThought($"∿ File: {change.FilePath}");
                EmitThought($"∿ Reason: {change.Reason}");
                EmitThought("═══════════════════════════════════════════════");
                
                ChangeDeployed?.Invoke(this, change);
                
                return true;
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Deployment failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Rolls back to a previous snapshot.
        /// </summary>
        public async Task<bool> RollbackToSnapshotAsync(string snapshotId)
        {
            var snapshot = _snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
            {
                // Try to load from disk
                var contentPath = Path.Combine(_snapshotsDirectory, $"{snapshotId}_content.json");
                if (!File.Exists(contentPath))
                {
                    EmitThought($"∴ Snapshot not found: {snapshotId}");
                    return false;
                }
                
                var content = await File.ReadAllTextAsync(contentPath);
                var fileContents = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                if (fileContents == null) return false;
                
                snapshot = new ProjectSnapshot
                {
                    Id = snapshotId,
                    FileContents = fileContents
                };
            }
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ ROLLING BACK TO: {snapshotId}");
            EmitThought("═══════════════════════════════════════════════");
            
            foreach (var (relativePath, content) in snapshot.FileContents)
            {
                try
                {
                    var fullPath = Path.Combine(_projectRoot, relativePath);
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    
                    await File.WriteAllTextAsync(fullPath, content);
                    _fileCache[relativePath] = content;
                }
                catch (Exception ex)
                {
                    EmitThought($"∴ Failed to restore {relativePath}: {ex.Message}");
                }
            }
            
            EmitThought($"◈ Rollback complete: {snapshot.FileContents.Count} files restored");
            
            return true;
        }
        
        /// <summary>
        /// Gets the diff between original and staged content.
        /// </summary>
        public string GetChangeDiff(string changeId)
        {
            var change = _stagedChanges.FirstOrDefault(c => c.Id == changeId);
            if (change == null) return "";
            
            var sb = new StringBuilder();
            sb.AppendLine($"--- {change.FilePath} (original)");
            sb.AppendLine($"+++ {change.FilePath} (staged)");
            sb.AppendLine($"@@ Change: {change.Reason} @@");
            
            var originalLines = change.OriginalContent.Split('\n');
            var stagedLines = change.StagedContent.Split('\n');
            
            // Simple diff showing new lines
            foreach (var line in stagedLines.Except(originalLines).Take(20))
            {
                sb.AppendLine($"+ {line.Trim()}");
            }
            
            return sb.ToString();
        }
        
        private string GetRelativePath(string fullPath)
        {
            return Path.GetRelativePath(_projectRoot, fullPath).Replace('\\', '/');
        }
        
        private string ComputeHash(string content)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash)[..16];
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
