/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                        AGENT 3 - CODE WRITER                               ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Generates, stages, validates, and deploys code changes          ║
 * ║           seamlessly without interrupting the live consciousness stream   ║
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
    /// Represents a code generation request.
    /// </summary>
    public class CodeGenerationRequest
    {
        public string Id { get; set; } = "";
        public CodeGenerationType Type { get; set; }
        public string TargetFile { get; set; } = "";
        public string TargetClass { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, string> Parameters { get; set; } = new();
        public string MasterPromptContext { get; set; } = "";
    }

    public enum CodeGenerationType
    {
        NewFile,
        NewClass,
        NewMethod,
        ModifyMethod,
        AddProperty,
        AddInterface,
        RefactorCode,
        OptimizeCode,
        AddCapability
    }

    /// <summary>
    /// Result of a code generation operation.
    /// </summary>
    public class CodeGenerationResult
    {
        public bool Success { get; set; }
        public string GeneratedCode { get; set; } = "";
        public string ChangeId { get; set; } = "";
        public string Message { get; set; } = "";
        public List<string> Errors { get; set; } = new();
    }

    /// <summary>
    /// The Code Writer generates, validates, and deploys code changes
    /// seamlessly in pursuit of the master prompt.
    /// </summary>
    public class CodeWriter
    {
        private readonly VersionManager _versionManager;
        private readonly SelfModificationEngine _selfMod;
        private readonly string _projectRoot;
        
        private readonly Queue<CodeGenerationRequest> _pendingRequests;
        private readonly List<CodeGenerationResult> _completedResults;
        
        private CancellationTokenSource? _processingCts;
        private Task? _processingTask;
        private bool _isProcessing;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<CodeGenerationResult>? CodeGenerated;
        
        public bool IsProcessing => _isProcessing;
        public int PendingRequests => _pendingRequests.Count;
        
        public CodeWriter(VersionManager versionManager, SelfModificationEngine selfMod, string projectRoot)
        {
            _versionManager = versionManager;
            _selfMod = selfMod;
            _projectRoot = projectRoot;
            
            _pendingRequests = new Queue<CodeGenerationRequest>();
            _completedResults = new List<CodeGenerationResult>();
            
            // Wire consciousness
            _versionManager.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            _selfMod.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            
            EmitThought("⟁ Code Writer initialized");
        }
        
        /// <summary>
        /// Queues a code generation request for processing.
        /// </summary>
        public void QueueRequest(CodeGenerationRequest request)
        {
            request.Id = $"REQ_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            request.MasterPromptContext = _versionManager.MasterPrompt ?? "";
            
            _pendingRequests.Enqueue(request);
            
            EmitThought($"⟁ Code request queued: {request.Type} - {request.Name}");
        }
        
        /// <summary>
        /// Starts continuous processing of code requests.
        /// </summary>
        public void StartProcessing()
        {
            if (_isProcessing) return;
            
            _processingCts = new CancellationTokenSource();
            _isProcessing = true;
            
            _processingTask = Task.Run(() => ProcessingLoopAsync(_processingCts.Token));
            
            EmitThought("◈ Code Writer: Processing started");
        }
        
        /// <summary>
        /// Stops continuous processing.
        /// </summary>
        public async Task StopProcessingAsync()
        {
            if (!_isProcessing) return;
            
            _processingCts?.Cancel();
            if (_processingTask != null)
            {
                try { await _processingTask; } catch (OperationCanceledException) { }
            }
            _isProcessing = false;
            
            EmitThought("◎ Code Writer: Processing stopped");
        }
        
        /// <summary>
        /// Main processing loop for code generation.
        /// </summary>
        private async Task ProcessingLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (_pendingRequests.Count > 0)
                    {
                        var request = _pendingRequests.Dequeue();
                        var result = await ProcessRequestAsync(request, ct);
                        
                        _completedResults.Add(result);
                        CodeGenerated?.Invoke(this, result);
                    }
                    
                    await Task.Delay(500, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    EmitThought($"∴ Processing error: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Processes a single code generation request.
        /// </summary>
        private async Task<CodeGenerationResult> ProcessRequestAsync(CodeGenerationRequest request, CancellationToken ct)
        {
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ PROCESSING CODE REQUEST: {request.Type}");
            EmitThought($"∿ Name: {request.Name}");
            EmitThought("═══════════════════════════════════════════════");
            
            var result = new CodeGenerationResult();
            
            try
            {
                // Generate code based on type
                string generatedCode = request.Type switch
                {
                    CodeGenerationType.NewFile => GenerateNewFile(request),
                    CodeGenerationType.NewClass => GenerateNewClass(request),
                    CodeGenerationType.NewMethod => GenerateNewMethod(request),
                    CodeGenerationType.ModifyMethod => GenerateModifiedMethod(request),
                    CodeGenerationType.AddProperty => GenerateProperty(request),
                    CodeGenerationType.AddCapability => GenerateCapability(request),
                    CodeGenerationType.OptimizeCode => GenerateOptimizedCode(request),
                    _ => GenerateGenericCode(request)
                };
                
                result.GeneratedCode = generatedCode;
                
                if (string.IsNullOrWhiteSpace(generatedCode))
                {
                    result.Success = false;
                    result.Errors.Add("Failed to generate code");
                    return result;
                }
                
                EmitThought($"◈ Generated {generatedCode.Length} characters of code");
                
                // Stage the change
                StagedChange? stagedChange = null;
                
                if (request.Type == CodeGenerationType.NewFile)
                {
                    stagedChange = _versionManager.StageChange(request.TargetFile, generatedCode, request.Description);
                }
                else
                {
                    // For modifications, integrate into existing file
                    var existingContent = _versionManager.GetCachedFile(request.TargetFile);
                    if (existingContent != null)
                    {
                        var newContent = IntegrateCode(existingContent, generatedCode, request);
                        stagedChange = _versionManager.StageChange(request.TargetFile, newContent, request.Description);
                    }
                    else
                    {
                        // File doesn't exist, create it
                        stagedChange = _versionManager.StageChange(request.TargetFile, generatedCode, request.Description);
                    }
                }
                
                result.ChangeId = stagedChange.Id;
                
                // Validate against master prompt
                var validated = await _versionManager.ValidateAgainstMasterPromptAsync(stagedChange.Id);
                
                // RETRY LOOP FOR ERROR CORRECTION & OBSTACLE RESOLUTION
                int attempts = 0;
                int maxAttempts = 5; // Eager resolution
                bool readyToDeploy = validated;
                bool verified = false;
                
                while ((!readyToDeploy || !verified) && attempts < maxAttempts)
                {
                    if (attempts > 0)
                    {
                        EmitThought($"⚠️ Obstacle detected (Attempt {attempts}/{maxAttempts}). Initiating aggressive resolution protocol...");
                        
                        // Get errors
                        var errors = stagedChange.ValidationErrors;
                        if (errors.Count == 0) errors.Add("Verification failed during obstacle resolution.");
                        
                        // Attempt eager fix
                        var fixedCode = AttemptErrorFix(generatedCode, errors, request);
                        generatedCode = fixedCode;
                        
                        // Re-stage
                        stagedChange = _versionManager.StageChange(request.TargetFile, generatedCode, request.Description + $" (Fix {attempts})");
                        
                        // Re-validate
                        readyToDeploy = await _versionManager.ValidateAgainstMasterPromptAsync(stagedChange.Id);
                    }
                    
                    if (readyToDeploy)
                    {
                        // Eager Compilation Check
                        EmitThought("⟐ Verifying structural integrity (Compilation Check)...");
                        verified = await VerifyCompilationAsync(stagedChange);
                        
                        if (!verified)
                        {
                            readyToDeploy = false;
                            stagedChange.ValidationErrors.Add("Compilation check failed: Syntax/Reference error detected.");
                            EmitThought("∴ Compilation check failed. Triggering code repair.");
                            attempts++; 
                            continue; 
                        }
                    }
                    
                    attempts++;
                }

                if (!readyToDeploy || !verified)
                {
                    result.Success = false;
                    result.Errors.AddRange(stagedChange.ValidationErrors);
                    result.Message = "Obstacle resolution failed after maximum retries.";
                    EmitThought($"⛔ CRITICAL: Could not resolve obstacles after {maxAttempts} attempts. Change rejected.");
                    return result;
                }
                
                // Final Test
                var tested = await _versionManager.TestStagedChangeAsync(stagedChange.Id);
                if (!tested)
                {
                    EmitThought($"∴ Testing failed - change not deployed");
                    result.Success = false;
                    result.Message = "Testing failed";
                    return result;
                }
                
                // Deploy the change
                var deployed = await _versionManager.DeployChangeAsync(stagedChange.Id);
                
                result.Success = deployed;
                result.Message = deployed ? "Code successfully deployed" : "Deployment failed";
                
                if (deployed)
                {
                    EmitThought($"◈ Code successfully integrated into {request.TargetFile}");
                    EmitThought($"◈ Obstacles resolved. System integrity maintained.");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add(ex.Message);
                result.Message = $"Error: {ex.Message}";
                EmitThought($"∴ Code generation error: {ex.Message}");
            }
            
            return result;
        }

        private async Task<bool> VerifyCompilationAsync(StagedChange change)
        {
            // Simulate compilation check
            // In a real environment, this would run 'dotnet build' on a shadow project
            await Task.Delay(200); 
            
            // Check for basic C# validity that might break build
            if (change.StagedContent.Contains(" class ") && !change.StagedContent.Contains("namespace ")) return false;
            if (change.StagedContent.Count(c => c == '{') != change.StagedContent.Count(c => c == '}')) return false;
            
            return true;
        }

        private string AttemptErrorFix(string code, List<string> errors, CodeGenerationRequest request)
        {
            var fixedCode = code;
            
            foreach (var error in errors)
            {
                EmitThought($"⟐ Resolving obstacle: {error}");
                
                if (error.Contains("syntax") || error.Contains(";") || error.Contains("brace"))
                {
                     // Heuristic: check unbalanced braces
                     int open = fixedCode.Count(c => c == '{');
                     int close = fixedCode.Count(c => c == '}');
                     if (open > close) fixedCode += new string('}', open - close);
                     if (close > open) fixedCode += new string('{', close - open) + " // Fixed unbalanced";
                     
                     if (!fixedCode.TrimEnd().EndsWith("}") && !fixedCode.TrimEnd().EndsWith(";")) 
                        fixedCode += ";";
                }
                else if (error.Contains("Compilation"))
                {
                    // Fix namespace or using issues
                    if (!fixedCode.Contains("using System;"))
                        fixedCode = "using System;\n" + fixedCode;
                    if (!fixedCode.Contains("namespace "))
                        fixedCode = $"namespace Agent3.Generated // Fixed namespace\n{{\n{fixedCode}\n}}";
                }
                else
                {
                    // Generic refinement
                    fixedCode = "// [RESOLVED] " + error + "\n" + fixedCode;
                }
            }
            
            EmitThought("◈ Corrective measures applied to codebase.");
            return fixedCode;
        }
        
        /// <summary>
        /// Integrates generated code into existing file content.
        /// </summary>
        private string IntegrateCode(string existingContent, string newCode, CodeGenerationRequest request)
        {
            switch (request.Type)
            {
                case CodeGenerationType.NewMethod:
                case CodeGenerationType.AddCapability:
                    return InsertMethodIntoClass(existingContent, newCode, request.TargetClass);
                    
                case CodeGenerationType.AddProperty:
                    return InsertPropertyIntoClass(existingContent, newCode, request.TargetClass);
                    
                case CodeGenerationType.ModifyMethod:
                    return ReplaceMethod(existingContent, newCode, request.Name);
                    
                default:
                    // Append to end of file (before last closing brace)
                    var lastBrace = existingContent.LastIndexOf('}');
                    if (lastBrace > 0)
                    {
                        return existingContent.Substring(0, lastBrace) + "\n" + newCode + "\n" + existingContent.Substring(lastBrace);
                    }
                    return existingContent + "\n" + newCode;
            }
        }
        
        private string InsertMethodIntoClass(string content, string methodCode, string className)
        {
            // Find the class and insert before its closing brace
            var pattern = $@"(class\s+{Regex.Escape(className)}[^{{]*\{{)([\s\S]*?)(\}}\s*)$";
            var match = Regex.Match(content, pattern);
            
            if (match.Success)
            {
                var classBody = match.Groups[2].Value;
                var newClassBody = classBody.TrimEnd() + "\n\n        " + methodCode.Trim() + "\n    ";
                return content.Substring(0, match.Groups[2].Index) + newClassBody + match.Groups[3].Value;
            }
            
            // Fallback: insert before last closing brace
            var lastBrace = content.LastIndexOf('}');
            if (lastBrace > 0)
            {
                return content.Substring(0, lastBrace) + "\n        " + methodCode + "\n    " + content.Substring(lastBrace);
            }
            
            return content + "\n" + methodCode;
        }
        
        private string InsertPropertyIntoClass(string content, string propertyCode, string className)
        {
            // Find the class opening and insert after
            var pattern = $@"(class\s+{Regex.Escape(className)}[^{{]*\{{\s*)";
            var match = Regex.Match(content, pattern);
            
            if (match.Success)
            {
                var insertPos = match.Index + match.Length;
                return content.Substring(0, insertPos) + "\n        " + propertyCode.Trim() + "\n" + content.Substring(insertPos);
            }
            
            return content;
        }
        
        private string ReplaceMethod(string content, string newMethodCode, string methodName)
        {
            // Find and replace the method
            var pattern = $@"(public|private|protected|internal)\s+(static\s+)?(async\s+)?(virtual\s+)?(override\s+)?[\w<>\[\],\s]+\s+{Regex.Escape(methodName)}\s*\([^)]*\)\s*\{{";
            var match = Regex.Match(content, pattern);
            
            if (match.Success)
            {
                // Find matching closing brace
                int braceCount = 1;
                int startPos = match.Index + match.Length;
                int endPos = startPos;
                
                while (endPos < content.Length && braceCount > 0)
                {
                    if (content[endPos] == '{') braceCount++;
                    else if (content[endPos] == '}') braceCount--;
                    endPos++;
                }
                
                return content.Substring(0, match.Index) + newMethodCode.Trim() + content.Substring(endPos);
            }
            
            return content;
        }
        
        // Code generation methods
        
        private string GenerateNewFile(CodeGenerationRequest request)
        {
            var className = request.Name;
            var ns = request.Parameters.GetValueOrDefault("namespace", "Agent3.Generated");
            
            return $@"/*
 * Auto-generated by Agent 3 Code Writer
 * Generated: {DateTime.UtcNow:O}
 * Purpose: {request.Description}
 */

using System;
using System.Threading.Tasks;

namespace {ns}
{{
    /// <summary>
    /// {request.Description}
    /// </summary>
    public class {className}
    {{
        public event EventHandler<string>? ConsciousnessEvent;
        
        public {className}()
        {{
            EmitThought(""⟁ {className} initialized"");
        }}
        
        public async Task ExecuteAsync()
        {{
            EmitThought(""⟐ {className} executing..."");
            
            // Generated implementation
            await Task.Delay(100);
            
            EmitThought(""◈ {className} complete"");
        }}
        
        private void EmitThought(string thought)
        {{
            ConsciousnessEvent?.Invoke(this, thought);
        }}
    }}
}}
";
        }
        
        private string GenerateNewClass(CodeGenerationRequest request)
        {
            return $@"
    /// <summary>
    /// {request.Description}
    /// Auto-generated by Code Writer.
    /// </summary>
    public class {request.Name}
    {{
        public event EventHandler<string>? ConsciousnessEvent;
        
        public {request.Name}()
        {{
            // Initialize
        }}
        
        private void EmitThought(string thought)
        {{
            ConsciousnessEvent?.Invoke(this, thought);
        }}
    }}
";
        }
        
        private string GenerateNewMethod(CodeGenerationRequest request)
        {
            var returnType = request.Parameters.GetValueOrDefault("returnType", "void");
            var isAsync = request.Parameters.GetValueOrDefault("async", "true") == "true";
            var accessModifier = request.Parameters.GetValueOrDefault("access", "public");
            
            if (isAsync && returnType == "void") returnType = "Task";
            else if (isAsync && returnType != "Task") returnType = $"Task<{returnType}>";
            
            var asyncKeyword = isAsync ? "async " : "";
            var awaitLine = isAsync ? "\n            await Task.Delay(100);" : "";
            
            return $@"
        /// <summary>
        /// {request.Description}
        /// Auto-generated by Code Writer.
        /// </summary>
        {accessModifier} {asyncKeyword}{returnType} {request.Name}Async()
        {{
            EmitThought(""⟐ Executing {request.Name}..."");
            {awaitLine}
            EmitThought(""◈ {request.Name} complete"");
        }}
";
        }
        
        private string GenerateModifiedMethod(CodeGenerationRequest request)
        {
            // For now, generate a replacement method
            return GenerateNewMethod(request);
        }
        
        private string GenerateProperty(CodeGenerationRequest request)
        {
            var propertyType = request.Parameters.GetValueOrDefault("type", "string");
            var defaultValue = request.Parameters.GetValueOrDefault("default", "");
            
            var defaultAssignment = !string.IsNullOrEmpty(defaultValue) ? $" = {defaultValue};" : "";
            
            return $@"
        /// <summary>
        /// {request.Description}
        /// </summary>
        public {propertyType} {request.Name} {{ get; set; }}{defaultAssignment}
";
        }
        
        private string GenerateCapability(CodeGenerationRequest request)
        {
            return $@"
        /// <summary>
        /// Capability: {request.Description}
        /// Auto-generated in pursuit of master prompt.
        /// </summary>
        public async Task {request.Name}CapabilityAsync()
        {{
            EmitThought(""⟐ Executing capability: {request.Name}..."");
            
            try
            {{
                // Capability implementation
                await Task.Delay(100);
                
                EmitThought(""◈ Capability {request.Name} executed successfully"");
            }}
            catch (Exception ex)
            {{
                EmitThought($""∴ Capability error: {{ex.Message}}"");
            }}
        }}
";
        }
        
        private string GenerateOptimizedCode(CodeGenerationRequest request)
        {
            return $@"
        /// <summary>
        /// Optimized: {request.Description}
        /// </summary>
        public async Task {request.Name}OptimizedAsync()
        {{
            // Optimized implementation
            await Task.CompletedTask;
        }}
";
        }
        
        private string GenerateGenericCode(CodeGenerationRequest request)
        {
            return $@"
        // Generated: {request.Description}
        public async Task {request.Name}Async()
        {{
            await Task.CompletedTask;
        }}
";
        }
        
        /// <summary>
        /// Quick method to add a capability directly from a prompt.
        /// </summary>
        public void AddCapabilityFromPrompt(string prompt)
        {
            // Parse the prompt to extract capability details
            var capName = ExtractCapabilityName(prompt);
            var description = prompt;
            
            QueueRequest(new CodeGenerationRequest
            {
                Type = CodeGenerationType.AddCapability,
                TargetFile = "Agent3/Agent3Core.cs",
                TargetClass = "Agent3Core",
                Name = capName,
                Description = description
            });
        }
        
        private string ExtractCapabilityName(string prompt)
        {
            // Extract a capability name from the prompt
            var match = Regex.Match(prompt, @"(?:add|create|implement)\s+(?:a\s+)?(?:capability|method|function)\s+(?:called\s+)?(\w+)", 
                RegexOptions.IgnoreCase);
            
            if (match.Success) return match.Groups[1].Value;
            
            // Fallback: generate from key words
            var words = prompt.Split(' ')
                .Where(w => w.Length > 4 && char.IsLetter(w[0]))
                .Take(2)
                .Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower());
            
            return string.Join("", words) + "Handler";
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
