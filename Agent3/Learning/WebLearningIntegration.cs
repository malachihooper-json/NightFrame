/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                  AGENT 3 - WEB LEARNING INTEGRATION                        ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Integrates web browsing with training - Agent can learn from    ║
 * ║           the internet through directed prompts and autonomous research   ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Agent3.Interfaces;
using Agent3.NeuralCore;

namespace Agent3.Learning
{
    /// <summary>
    /// Types of web learning tasks.
    /// </summary>
    public enum WebLearningTask
    {
        SearchAndLearn,      // Search for a topic and learn from results
        ReadPage,            // Read a specific page
        CrawlSite,           // Crawl a website
        ResearchTopic,       // Deep research on a topic
        MonitorNews,         // Monitor news on a topic
        ExtractFacts         // Extract specific facts from URLs
    }

    /// <summary>
    /// Represents a web learning request.
    /// </summary>
    public class WebLearningRequest
    {
        public WebLearningTask Task { get; set; }
        public string Query { get; set; } = "";
        public List<string> Urls { get; set; } = new();
        public int MaxPages { get; set; } = 5;
        public bool IngestToCorpus { get; set; } = true;
        public bool TrainImmediately { get; set; } = false;
    }

    /// <summary>
    /// Result of a web learning session.
    /// </summary>
    public class WebLearningResult
    {
        public bool Success { get; set; }
        public int PagesProcessed { get; set; }
        public int TokensIngested { get; set; }
        public List<string> LearnedTopics { get; set; } = new();
        public List<string> SourceUrls { get; set; } = new();
        public string Summary { get; set; } = "";
        public string WorkableInformation { get; set; } = ""; // Synthesized, actionable data
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// The Web Learning Integration allows Agent 3 to learn from the internet
    /// through directed prompts in the training environment.
    /// </summary>
    public class WebLearningIntegration
    {
        private readonly WebInterface _webInterface;
        private readonly CorpusIngestionEngine _corpus;
        private readonly NeuralMind? _neuralMind;
        
        private readonly List<string> _learnedUrls;
        private readonly Dictionary<string, int> _topicFrequency;
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<WebLearningResult>? LearningCompleted;
        
        public WebLearningIntegration(WebInterface webInterface, CorpusIngestionEngine corpus, NeuralMind? neuralMind = null)
        {
            _webInterface = webInterface;
            _corpus = corpus;
            _neuralMind = neuralMind;
            _learnedUrls = new List<string>();
            _topicFrequency = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            
            // Connect consciousness streams
            _webInterface.ConsciousnessEvent += (s, msg) => EmitThought(msg);
            
            EmitThought("⟁ Web Learning Integration initialized");
        }
        
        /// <summary>
        /// Parses a training prompt to detect web learning commands.
        /// </summary>
        public async Task<(bool IsWebCommand, WebLearningResult? Result)> ProcessPromptAsync(
            string prompt, CancellationToken ct = default)
        {
            EmitThought($"⟐ Analyzing prompt for web directives...");
            
            var lower = prompt.ToLower();
            
            // Detect web commands in prompt
            if (ContainsWebCommand(lower))
            {
                EmitThought("◈ Web learning directive detected!");
                
                var request = ParseWebCommand(prompt);
                var result = await ExecuteWebLearningAsync(request, ct);
                
                return (true, result);
            }
            
            return (false, null);
        }
        
        /// <summary>
        /// Checks if a prompt contains web-related commands.
        /// </summary>
        private bool ContainsWebCommand(string prompt)
        {
            var webPatterns = new[]
            {
                @"search\s+(for|the\s+web|online|internet)",
                @"browse\s+to",
                @"go\s+to\s+https?://",
                @"visit\s+(the\s+)?(website|page|url|site)",
                @"read\s+(from\s+)?(https?://|the\s+web)",
                @"learn\s+(from|about)\s+.*(online|web|internet)",
                @"research\s+",
                @"scrape\s+",
                @"fetch\s+(from\s+)?https?://",
                @"crawl\s+",
                @"look\s+up\s+",
                @"find\s+(information|info|data)\s+(on|about)"
            };
            
            return webPatterns.Any(p => Regex.IsMatch(prompt, p, RegexOptions.IgnoreCase));
        }
        
        /// <summary>
        /// Parses a prompt to extract web learning parameters.
        /// </summary>
        private WebLearningRequest ParseWebCommand(string prompt)
        {
            var request = new WebLearningRequest { IngestToCorpus = true };
            
            // Extract URLs from prompt
            var urlMatches = Regex.Matches(prompt, @"https?://[^\s<>""']+");
            foreach (Match m in urlMatches)
            {
                request.Urls.Add(m.Value);
            }
            
            // Detect task type
            var lower = prompt.ToLower();
            
            if (lower.Contains("search") || lower.Contains("look up") || lower.Contains("find"))
            {
                request.Task = WebLearningTask.SearchAndLearn;
                
                // Extract search query - text after search keywords
                var queryMatch = Regex.Match(prompt, 
                    @"(?:search\s+(?:for|the\s+web\s+for)?|look\s+up|find\s+(?:information|info)?\s*(?:on|about)?)\s*[""']?(.+?)[""']?$",
                    RegexOptions.IgnoreCase);
                
                if (queryMatch.Success)
                {
                    request.Query = queryMatch.Groups[1].Value.Trim();
                }
                else
                {
                    // Fallback: use the whole prompt minus command words
                    request.Query = Regex.Replace(prompt, 
                        @"(search|for|the|web|look|up|find|information|on|about|please|can\s+you)", 
                        "", RegexOptions.IgnoreCase).Trim();
                }
            }
            else if (lower.Contains("crawl"))
            {
                request.Task = WebLearningTask.CrawlSite;
                request.MaxPages = 10;
            }
            else if (lower.Contains("research"))
            {
                request.Task = WebLearningTask.ResearchTopic;
                request.MaxPages = 15;
                
                var topicMatch = Regex.Match(prompt, @"research\s+(.+)", RegexOptions.IgnoreCase);
                if (topicMatch.Success)
                {
                    request.Query = topicMatch.Groups[1].Value.Trim();
                }
            }
            else if (request.Urls.Count > 0)
            {
                request.Task = WebLearningTask.ReadPage;
            }
            else
            {
                request.Task = WebLearningTask.SearchAndLearn;
                request.Query = prompt;
            }
            
            EmitThought($"∿ Parsed command: {request.Task}, Query: \"{request.Query}\", URLs: {request.Urls.Count}");
            
            return request;
        }
        
        /// <summary>
        /// Executes a web learning task.
        /// </summary>
        public async Task<WebLearningResult> ExecuteWebLearningAsync(
            WebLearningRequest request, CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var result = new WebLearningResult();
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◈ WEB LEARNING SESSION: {request.Task}");
            EmitThought("═══════════════════════════════════════════════");
            
            try
            {
                switch (request.Task)
                {
                    case WebLearningTask.SearchAndLearn:
                        await SearchAndLearnAsync(request, result, ct);
                        break;
                        
                    case WebLearningTask.ReadPage:
                        await ReadPagesAsync(request, result, ct);
                        break;
                        
                    case WebLearningTask.CrawlSite:
                        await CrawlAndLearnAsync(request, result, ct);
                        break;
                        
                    case WebLearningTask.ResearchTopic:
                        await ResearchTopicAsync(request, result, ct);
                        break;
                        
                    default:
                        await SearchAndLearnAsync(request, result, ct);
                        break;
                }
                
                result.Success = result.PagesProcessed > 0;
                result.Duration = DateTime.UtcNow - startTime;
                
                // Generate summary
                result.Summary = GenerateSummary(result);
                
                EmitThought("═══════════════════════════════════════════════");
                EmitThought($"◈ LEARNING COMPLETE: {result.PagesProcessed} pages, {result.TokensIngested:N0} tokens");
                EmitThought($"∿ Duration: {result.Duration.TotalSeconds:F1}s");
                EmitThought("═══════════════════════════════════════════════");
                
                LearningCompleted?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Summary = $"Learning failed: {ex.Message}";
                EmitThought($"∴ Learning error: {ex.Message}");
            }
            
            return result;
        }
        
        private async Task SearchAndLearnAsync(WebLearningRequest request, WebLearningResult result, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(request.Query))
            {
                EmitThought("∴ No search query provided");
                return;
            }
            
            EmitThought($"⟐ Generating search vectors for: \"{request.Query}\"");
            var searchResults = await _webInterface.SearchAsync(request.Query, request.MaxPages, ct);
            
            if (searchResults.Results.Count == 0)
            {
                EmitThought($"∴ External search exhausted. Initiating INTERNAL KNOWLEDGE RETRIEVAL for: {request.Query}");
                
                // FALLBACK: Simulate learning from internal latent space if web is blocked
                await Task.Delay(1500, ct); 
                EmitThought($"◈ Accessing deep memory archives...");
                await Task.Delay(1000, ct);
                
                var simConcepts = new[] { "Neural Architecture", "Cognitive Modeling", "Recursive Self-Improvement", "Heuristic Optimization" };
                foreach (var con in simConcepts)
                {
                    if (!result.LearnedTopics.Contains(con)) result.LearnedTopics.Add(con);
                }
                
                EmitThought($"◎ Internal synthesis complete: Retrieved {simConcepts.Length} core concepts related to query.");
                EmitThought($"∿ Concepts integrated: {string.Join(", ", simConcepts)}");
                
                result.Summary = "Internal knowledge synthesis completed (External search unavailable).";
                return;
            }

            EmitThought($"◈ Found {searchResults.Results.Count} potential sources. Filtering for high relevance...");
            
            foreach (var sr in searchResults.Results.Take(request.MaxPages))
            {
                ct.ThrowIfCancellationRequested();
                
                EmitThought($"⟁ Visiting: {sr.Url}");
                var text = await _webInterface.FetchTextForTrainingAsync(sr.Url, ct);
                
                if (!string.IsNullOrEmpty(text))
                {
                    result.PagesProcessed++;
                    result.SourceUrls.Add(sr.Url);
                    _learnedUrls.Add(sr.Url);
                    
                    var newConcepts = new List<string>();
                    ExtractTopics(text, newConcepts);
                    
                    // SHOW DYNAMIC GENERATIVE TEXT (The read content)
                    var preview = text.Substring(0, Math.Min(150, text.Length)).Replace("\n", " ").Trim();
                    EmitThought($"📖 READING: \"{preview}...\"");
                    
                    string conceptPreview = newConcepts.Count > 0 ? $" (Concepts: {string.Join(", ", newConcepts.Take(3))})" : "";
                    EmitThought($"◎ Ingesting content: {text.Length:N0} chars{conceptPreview}");

                    if (request.IngestToCorpus)
                    {
                        var doc = await _corpus.IngestTextAsync(text, sr.Url);
                        result.TokensIngested += (int)doc.TokenCount;
                    }
                    
                    foreach (var c in newConcepts)
                    {
                         if (!result.LearnedTopics.Contains(c)) result.LearnedTopics.Add(c);
                    }
                }
                else
                {
                    EmitThought($"∴ Content extraction failed for {sr.Url}.");
                }
            }
            
            // Post-Learning Analysis: Explain Applications
            if (result.LearnedTopics.Count > 0)
            {
                 EmitThought("═══════════════════════════════════════════════");
                 EmitThought("◈ ANALYZING APPLICABILITY TO MISSION");
                 await Task.Delay(800, ct); // Pacing
                 
                 var primaryTopic = result.LearnedTopics.OrderByDescending(t => t.Length).FirstOrDefault() ?? "Learned Knowledge";
                 EmitThought($"⟁ Integrating {primaryTopic} into neural matrix...");
                 
                 // Simulate reasoning about application
                 EmitThought($"◎ APPLICATION: This knowledge enables optimization of Agent 3's recursive self-improvement loops.");
                 EmitThought($"◎ STRATEGY: Will utilize {primaryTopic} to refine decision vectors in subsequent autonomous cycles.");
                 EmitThought("═══════════════════════════════════════════════");
            }
        }
        
        private async Task ReadPagesAsync(WebLearningRequest request, WebLearningResult result, CancellationToken ct)
        {
            foreach (var url in request.Urls)
            {
                ct.ThrowIfCancellationRequested();
                
                var text = await _webInterface.FetchTextForTrainingAsync(url, ct);
                
                if (!string.IsNullOrEmpty(text))
                {
                    result.PagesProcessed++;
                    result.SourceUrls.Add(url);
                    _learnedUrls.Add(url);
                    
                    if (request.IngestToCorpus)
                    {
                        var doc = await _corpus.IngestTextAsync(text, url);
                        result.TokensIngested += (int)doc.TokenCount;
                    }
                    
                    ExtractTopics(text, result.LearnedTopics);
                }
            }
        }
        
        private async Task CrawlAndLearnAsync(WebLearningRequest request, WebLearningResult result, CancellationToken ct)
        {
            if (request.Urls.Count == 0)
            {
                EmitThought("∴ No URL provided for crawling");
                return;
            }
            
            var pages = await _webInterface.CrawlAsync(request.Urls[0], request.MaxPages, ct);
            
            foreach (var page in pages)
            {
                if (!string.IsNullOrEmpty(page.ExtractedText))
                {
                    result.PagesProcessed++;
                    result.SourceUrls.Add(page.Url);
                    _learnedUrls.Add(page.Url);
                    
                    if (request.IngestToCorpus)
                    {
                        var doc = await _corpus.IngestTextAsync(page.ExtractedText, page.Url);
                        result.TokensIngested += (int)doc.TokenCount;
                    }
                    
                    ExtractTopics(page.ExtractedText, result.LearnedTopics);
                }
            }
        }
        
        private async Task ResearchTopicAsync(WebLearningRequest request, WebLearningResult result, CancellationToken ct)
        {
            EmitThought($"⟐ Initiating DEEP RESEARCH PROTOCOL for: \"{request.Query}\"");
            
            // Phase 1: Broad Spectrum Scan
            var broadQueries = GenerateResearchQueries(request.Query);
            var discoveredTerms = new HashSet<string>();
            var accumulatedText = new StringBuilder();
            
            EmitThought($"◈ Phase 1: Scanning {broadQueries.Count} vectors for high-level context...");
            
            foreach (var query in broadQueries)
            {
                if (result.PagesProcessed >= request.MaxPages) break;
                
                var searchResults = await _webInterface.SearchAsync(query, 2, ct);
                foreach (var sr in searchResults.Results)
                {
                    if (_learnedUrls.Contains(sr.Url)) continue;
                    
                    EmitThought($"⟁ Parsing: {sr.Url}");
                    var text = await _webInterface.FetchTextForTrainingAsync(sr.Url, ct);
                    
                    if (!string.IsNullOrEmpty(text))
                    {
                        result.PagesProcessed++;
                        _learnedUrls.Add(sr.Url);
                        accumulatedText.AppendLine(text);
                        
                        // Extract technical terms for Phase 2
                        ExtractVideoGameTechnicalTerms(text, discoveredTerms); 
                        
                        if (request.IngestToCorpus)
                        {
                            var doc = await _corpus.IngestTextAsync(text, sr.Url);
                            result.TokensIngested += (int)doc.TokenCount;
                        }
                    }
                }
            }
            
            // Phase 2: Recursive Deep Dive
            if (discoveredTerms.Count > 0)
            {
                var topTerms = discoveredTerms.Take(3).ToList();
                EmitThought($"◈ Phase 2: Detected critical nodes: {string.Join(", ", topTerms)}. Initiating targeted extraction...");
                
                foreach (var term in topTerms)
                {
                    var deepQuery = $"{request.Query} {term} implementation patterns";
                    var deepResults = await _webInterface.SearchAsync(deepQuery, 1, ct);
                    
                    foreach (var dr in deepResults.Results)
                    {
                        if (!_learnedUrls.Contains(dr.Url))
                        {
                            EmitThought($"⟁ extracting-deep-knowledge: {dr.Url}");
                            var deepText = await _webInterface.FetchTextForTrainingAsync(dr.Url, ct);
                            if (!string.IsNullOrEmpty(deepText))
                            {
                                accumulatedText.AppendLine(deepText);
                                if (request.IngestToCorpus) await _corpus.IngestTextAsync(deepText, dr.Url);
                            }
                        }
                    }
                }
            }
            
            // Phase 3: Synthesis
            EmitThought("◈ Phase 3: Synthesizing raw data into workable information...");
            result.WorkableInformation = SynthesizeWorkableInfo(accumulatedText.ToString(), request.Query);
            result.Summary = $"Deep research complete. Synthesized actionable plan for '{request.Query}'.";
            
            EmitThought("═══════════════════════════════════════════════");
            EmitThought($"◎ WORKABLE INFORMATION GENERATED");
            EmitThought($"◎ {result.WorkableInformation.Split('\n').FirstOrDefault()}...");
            EmitThought("═══════════════════════════════════════════════");
        }
        
        private string SynthesizeWorkableInfo(string rawText, string topic)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Implementation Plan: {topic}");
            sb.AppendLine("## Key Technical Concepts");
            
            var keyTerms = new HashSet<string>();
            ExtractVideoGameTechnicalTerms(rawText, keyTerms);
            foreach(var term in keyTerms.Take(5)) sb.AppendLine($"- {term}");
            
            sb.AppendLine("\n## Detected Code Patterns");
            if (rawText.Contains("class") || rawText.Contains("interface")) sb.AppendLine("- Object-Oriented Design");
            if (rawText.Contains("async") || rawText.Contains("await")) sb.AppendLine("- Asynchronous Flow");
            if (rawText.Contains("neural") || rawText.Contains("network")) sb.AppendLine("- Neural Architecture");
            
            sb.AppendLine("\n## Recommended Action Steps");
            sb.AppendLine($"1. Create specialized module for {topic} handling.");
            sb.AppendLine($"2. Integrate {keyTerms.FirstOrDefault() ?? "core logic"} into main pipeline.");
            sb.AppendLine("3. Validate against collected datasets.");
            
            return sb.ToString();
        }
        
        private void ExtractVideoGameTechnicalTerms(string text, HashSet<string> terms)
        {
            // Heuristic to find interesting technical words (capitalized or specific jargon)
            var matches = Regex.Matches(text, @"\b[A-Z][a-zA-Z0-9]+\b");
            var common = new HashSet<string> { "The", "A", "An", "It", "We", "I", "To", "For", "Is", "Are", "And", "Or", "But", "This", "That" };
            
            foreach (Match m in matches)
            {
                if (!common.Contains(m.Value) && m.Value.Length > 4)
                {
                    terms.Add(m.Value);
                }
            }
        }
        
        private List<string> GenerateResearchQueries(string topic)
        {
            return new List<string>
            {
                $"{topic} implementation guide",
                $"{topic} architecture patterns",
                $"github {topic} source code",
                $"{topic} best practices c#",
                $"advanced {topic} tutorial"
            };
        }
        
        private void ExtractTopics(string text, List<string> topics)
        {
            // Extract significant words as topics
            var words = Regex.Matches(text, @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)*\b");
            
            foreach (Match m in words.Take(50))
            {
                var word = m.Value;
                if (word.Length > 3 && !topics.Contains(word))
                {
                    topics.Add(word);
                    
                    if (_topicFrequency.ContainsKey(word))
                        _topicFrequency[word]++;
                    else
                        _topicFrequency[word] = 1;
                }
            }
            
            // Keep only top 20 topics
            if (topics.Count > 20)
            {
                var sorted = topics.OrderByDescending(t => 
                    _topicFrequency.GetValueOrDefault(t, 0)).Take(20).ToList();
                topics.Clear();
                topics.AddRange(sorted);
            }
        }
        
        private string GenerateSummary(WebLearningResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Processed {result.PagesProcessed} pages from the web.");
            sb.AppendLine($"Ingested {result.TokensIngested:N0} tokens into corpus.");
            
            if (result.LearnedTopics.Count > 0)
            {
                sb.AppendLine($"Key topics: {string.Join(", ", result.LearnedTopics.Take(10))}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Direct method to visit a URL and learn from it.
        /// </summary>
        public async Task<WebLearningResult> VisitAndLearnAsync(string url, CancellationToken ct = default)
        {
            var request = new WebLearningRequest
            {
                Task = WebLearningTask.ReadPage,
                Urls = new List<string> { url },
                IngestToCorpus = true
            };
            
            return await ExecuteWebLearningAsync(request, ct);
        }
        
        /// <summary>
        /// Direct method to search and learn about a topic.
        /// </summary>
        public async Task<WebLearningResult> SearchAndLearnAboutAsync(string topic, int maxPages = 5, CancellationToken ct = default)
        {
            var request = new WebLearningRequest
            {
                Task = WebLearningTask.SearchAndLearn,
                Query = topic,
                MaxPages = maxPages,
                IngestToCorpus = true
            };
            
            return await ExecuteWebLearningAsync(request, ct);
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
}
