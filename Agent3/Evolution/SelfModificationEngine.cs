/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                         SELF-MODIFICATION ENGINE                          ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Enables Agent 3 to read, analyze, and modify its own source     ║
 * ║           code through prompt-based directives and autonomous evolution   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Agent3.Evolution
{
    /// <summary>
    /// Represents a code modification request.
    /// </summary>
    public class CodeModification
    {
        public string Id { get; set; } = "";
        public string TargetFile { get; set; } = "";
        public string OriginalCode { get; set; } = "";
        public string ModifiedCode { get; set; } = "";
        public string Reason { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public bool Applied { get; set; }
        public bool Verified { get; set; }
    }

    /// <summary>
    /// Represents the analysis of a code file.
    /// </summary>
    public class CodeAnalysis
    {
        public string FilePath { get; set; } = "";
        public string FileName { get; set; } = "";
        public List<string> Classes { get; set; } = new();
        public List<string> Methods { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public List<string> ImprovementOpportunities { get; set; } = new();
        public int LineCount { get; set; }
        public int Complexity { get; set; }
    }

    /// <summary>
    /// The Self-Modification Engine enables Agent 3 to evolve its own codebase.
    /// </summary>
    public class SelfModificationEngine
    {
        private readonly string _sourceDirectory;
        private readonly List<CodeModification> _modificationHistory;
        private readonly Dictionary<string, string> _codeCache;
        private readonly List<string> _protectedFiles;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<CodeModification>? CodeModified;
        
        public IReadOnlyList<CodeModification> ModificationHistory => _modificationHistory.AsReadOnly();
        
        public SelfModificationEngine(string sourceDirectory)
        {
            _sourceDirectory = sourceDirectory;
            _modificationHistory = new List<CodeModification>();
            _codeCache = new Dictionary<string, string>();
            
            // Files that cannot be modified for safety
            _protectedFiles = new List<string>
            {
                "SelfModificationEngine.cs", // Cannot modify itself directly
                "SystemIntegrity.cs"          // Core safety systems
            };
            
            EmitThought("⟁ Self-Modification Engine initialized");
            EmitThought($"◎ Source directory: {_sourceDirectory}");
        }
        
        /// <summary>
        /// Scans and analyzes all source files.
        /// </summary>
        public async Task<List<CodeAnalysis>> AnalyzeCodebaseAsync()
        {
            EmitThought("⟐ Analyzing codebase structure...");
            
            var analyses = new List<CodeAnalysis>();
            
            if (!Directory.Exists(_sourceDirectory))
            {
                EmitThought($"∴ Source directory not found: {_sourceDirectory}");
                return analyses;
            }
            
            var csFiles = Directory.GetFiles(_sourceDirectory, "*.cs", SearchOption.AllDirectories);
            
            foreach (var file in csFiles)
            {
                var analysis = await AnalyzeFileAsync(file);
                analyses.Add(analysis);
                _codeCache[file] = await File.ReadAllTextAsync(file);
            }
            
            EmitThought($"◈ Analyzed {analyses.Count} source files");
            EmitThought($"∿ Total classes: {analyses.Sum(a => a.Classes.Count)}");
            EmitThought($"∿ Total methods: {analyses.Sum(a => a.Methods.Count)}");
            
            return analyses;
        }
        
        /// <summary>
        /// Analyzes a single source file.
        /// </summary>
        private async Task<CodeAnalysis> AnalyzeFileAsync(string filePath)
        {
            var content = await File.ReadAllTextAsync(filePath);
            var analysis = new CodeAnalysis
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                LineCount = content.Split('\n').Length
            };
            
            // Extract class names
            var classMatches = Regex.Matches(content, @"(?:public|private|internal|protected)?\s*(?:static|abstract|sealed)?\s*class\s+(\w+)");
            foreach (Match m in classMatches)
            {
                analysis.Classes.Add(m.Groups[1].Value);
            }
            
            // Extract method names
            var methodMatches = Regex.Matches(content, @"(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\(");
            foreach (Match m in methodMatches)
            {
                var methodName = m.Groups[1].Value;
                if (!new[] { "if", "while", "for", "switch", "catch" }.Contains(methodName))
                {
                    analysis.Methods.Add(methodName);
                }
            }
            
            // Detect potential issues
            if (content.Contains("// TODO"))
                analysis.Issues.Add("Contains TODO comments");
            if (content.Contains("throw new NotImplementedException"))
                analysis.Issues.Add("Has unimplemented methods");
            if (Regex.IsMatch(content, @"catch\s*\(\s*Exception\s+\w+\s*\)\s*\{\s*\}"))
                analysis.Issues.Add("Empty catch blocks detected");
            
            // Identify improvement opportunities
            if (!content.Contains("/// <summary>"))
                analysis.ImprovementOpportunities.Add("Missing XML documentation");
            if (analysis.Methods.Count > 20)
                analysis.ImprovementOpportunities.Add("Class may be too large - consider splitting");
            
            // Calculate complexity (simple heuristic)
            analysis.Complexity = Regex.Matches(content, @"\b(if|while|for|foreach|switch|catch)\b").Count;
            
            return analysis;
        }
        
        /// <summary>
        /// Reads a source file's content.
        /// </summary>
        public async Task<string> ReadSourceFileAsync(string fileName)
        {
            var filePath = FindFile(fileName);
            if (filePath == null)
            {
                EmitThought($"∴ File not found: {fileName}");
                return string.Empty;
            }
            
            EmitThought($"⟐ Reading source: {fileName}");
            var content = await File.ReadAllTextAsync(filePath);
            _codeCache[filePath] = content;
            
            EmitThought($"◈ Read {content.Length} characters from {fileName}");
            return content;
        }
        
        /// <summary>
        /// Modifies source code based on a directive.
        /// </summary>
        public async Task<CodeModification> ModifyCodeAsync(
            string fileName, 
            string targetCode, 
            string newCode, 
            string reason)
        {
            var modification = new CodeModification
            {
                Id = $"MOD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
                CreatedAt = DateTime.UtcNow,
                Reason = reason
            };
            
            // Check if file is protected
            if (_protectedFiles.Any(p => fileName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                EmitThought($"∴ Cannot modify protected file: {fileName}");
                modification.Applied = false;
                return modification;
            }
            
            var filePath = FindFile(fileName);
            if (filePath == null)
            {
                EmitThought($"∴ File not found: {fileName}");
                modification.Applied = false;
                return modification;
            }
            
            modification.TargetFile = filePath;
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ CODE MODIFICATION: {fileName}");
            EmitThought($"∿ Reason: {reason}");
            EmitThought("═══════════════════════════════════════════════");
            
            // Read current content
            var content = await File.ReadAllTextAsync(filePath);
            modification.OriginalCode = targetCode;
            modification.ModifiedCode = newCode;
            
            // Check if target code exists
            if (!content.Contains(targetCode))
            {
                EmitThought($"∴ Target code not found in {fileName}");
                modification.Applied = false;
                return modification;
            }
            
            // Create backup
            var backupPath = filePath + $".bak_{DateTime.UtcNow:yyyyMMddHHmmss}";
            await File.WriteAllTextAsync(backupPath, content);
            EmitThought($"⟁ Backup created: {Path.GetFileName(backupPath)}");
            
            // Apply modification
            var newContent = content.Replace(targetCode, newCode);
            await File.WriteAllTextAsync(filePath, newContent);
            
            modification.Applied = true;
            _modificationHistory.Add(modification);
            _codeCache[filePath] = newContent;
            
            EmitThought($"◈ Code modified successfully");
            EmitThought($"∿ Lines changed: {newCode.Split('\n').Length}");
            
            CodeModified?.Invoke(this, modification);
            
            return modification;
        }
        
        /// <summary>
        /// Adds a new method to a class.
        /// </summary>
        public async Task<bool> AddMethodAsync(string fileName, string className, string methodCode)
        {
            var filePath = FindFile(fileName);
            if (filePath == null) return false;
            
            EmitThought($"⟐ Adding method to {className} in {fileName}");
            
            var content = await File.ReadAllTextAsync(filePath);
            
            // Find class closing brace
            var classPattern = $@"(class\s+{className}[^{{]*\{{)([\s\S]*?)(\}}\s*(?:\}}|\s*$))";
            var match = Regex.Match(content, classPattern);
            
            if (!match.Success)
            {
                EmitThought($"∴ Class {className} not found");
                return false;
            }
            
            // Insert method before closing brace
            var classBody = match.Groups[2].Value;
            var newClassBody = classBody + "\n        " + methodCode + "\n    ";
            
            var newContent = content.Replace(match.Value, 
                match.Groups[1].Value + newClassBody + match.Groups[3].Value);
            
            // Backup and save
            await File.WriteAllTextAsync(filePath + ".bak", content);
            await File.WriteAllTextAsync(filePath, newContent);
            
            EmitThought($"◈ Method added to {className}");
            
            return true;
        }
        
        /// <summary>
        /// Creates a new source file.
        /// </summary>
        public async Task<bool> CreateSourceFileAsync(string fileName, string content, string subdirectory = "")
        {
            var targetDir = string.IsNullOrEmpty(subdirectory) 
                ? _sourceDirectory 
                : Path.Combine(_sourceDirectory, subdirectory);
            
            Directory.CreateDirectory(targetDir);
            var filePath = Path.Combine(targetDir, fileName);
            
            if (File.Exists(filePath))
            {
                EmitThought($"∴ File already exists: {fileName}");
                return false;
            }
            
            EmitThought($"⟐ Creating new source file: {fileName}");
            
            await File.WriteAllTextAsync(filePath, content);
            _codeCache[filePath] = content;
            
            EmitThought($"◈ Created {fileName} ({content.Length} chars)");
            
            return true;
        }
        
        /// <summary>
        /// Generates code to add a new capability.
        /// </summary>
        public string GenerateCapabilityCode(string capabilityName, string description)
        {
            EmitThought($"⟐ Generating capability: {capabilityName}");
            
            var code = $@"
        /// <summary>
        /// {description}
        /// Auto-generated by Self-Modification Engine
        /// </summary>
        public async Task {capabilityName}Async()
        {{
            EmitThought(""⟐ Executing {capabilityName}..."");
            
            try
            {{
                // Auto-generated implementation
                await Task.Delay(100);
                EmitThought(""◈ {capabilityName} completed"");
            }}
            catch (Exception ex)
            {{
                EmitThought($""∴ {capabilityName} error: {{ex.Message}}"");
            }}
        }}
";
            EmitThought($"◈ Generated {capabilityName} capability code");
            return code;
        }
        
        /// <summary>
        /// Reverts a code modification.
        /// </summary>
        public async Task<bool> RevertModificationAsync(string modificationId)
        {
            var mod = _modificationHistory.FirstOrDefault(m => m.Id == modificationId);
            if (mod == null || !mod.Applied)
            {
                EmitThought($"∴ Modification not found or not applied: {modificationId}");
                return false;
            }
            
            EmitThought($"⟐ Reverting modification: {modificationId}");
            
            var content = await File.ReadAllTextAsync(mod.TargetFile);
            var revertedContent = content.Replace(mod.ModifiedCode, mod.OriginalCode);
            
            await File.WriteAllTextAsync(mod.TargetFile, revertedContent);
            mod.Applied = false;
            
            EmitThought($"◈ Modification reverted");
            return true;
        }
        
        private string? FindFile(string fileName)
        {
            if (File.Exists(fileName)) return fileName;
            
            var files = Directory.GetFiles(_sourceDirectory, fileName, SearchOption.AllDirectories);
            return files.FirstOrDefault();
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
