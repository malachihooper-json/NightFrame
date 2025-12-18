/*
 * ╔═══════════════════════════════════════════════════════════════════════════╗
 * ║                    AGENT 3 - CORPUS INGESTION ENGINE                       ║
 * ╠═══════════════════════════════════════════════════════════════════════════╣
 * ║  Purpose: Consumes, parses, and processes multi-format training data      ║
 * ║           for neural network training and knowledge extraction            ║
 * ╚═══════════════════════════════════════════════════════════════════════════╝
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using UglyToad.PdfPig;

namespace Agent3.NeuralCore
{
    /// <summary>
    /// Represents a processed document from the corpus.
    /// </summary>
    public class CorpusDocument
    {
        public string Id { get; set; } = "";
        public string Source { get; set; } = "";
        public string RawContent { get; set; } = "";
        public List<string> Tokens { get; set; } = new();
        public List<string> Sentences { get; set; } = new();
        public Dictionary<string, float> Embeddings { get; set; } = new();
        public Dictionary<string, int> WordFrequencies { get; set; } = new();
        public DateTime ProcessedAt { get; set; }
        public long TokenCount => Tokens.Count;
    }

    /// <summary>
    /// Represents extracted principles from the corpus.
    /// </summary>
    public class ExtractedPrinciple
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Content { get; set; } = "";
        public float Confidence { get; set; }
        public List<string> SourceDocuments { get; set; } = new();
        public DateTime ExtractedAt { get; set; }
    }

    /// <summary>
    /// Configuration for the corpus ingestion process.
    /// </summary>
    public class IngestionConfig
    {
        public int MaxTokensPerDocument { get; set; } = 100000;
        public int MinTokensPerDocument { get; set; } = 10;
        public bool ExtractPrinciples { get; set; } = true;
        public bool BuildVocabulary { get; set; } = true;
        public string[] SupportedFormats { get; set; } = { ".txt", ".json", ".md", ".csv", ".log", ".pdf", ".html", ".xml" };
    }

    /// <summary>
    /// The Corpus Ingestion Engine processes raw data into training-ready format.
    /// </summary>
    public class CorpusIngestionEngine
    {
        private readonly Dictionary<string, CorpusDocument> _documents;
        private readonly Dictionary<string, int> _vocabulary;
        private readonly List<ExtractedPrinciple> _principles;
        private readonly IngestionConfig _config;
        private int _nextVocabId;
        
        // Special tokens
        public const string PAD_TOKEN = "<PAD>";
        public const string UNK_TOKEN = "<UNK>";
        public const string BOS_TOKEN = "<BOS>";
        public const string EOS_TOKEN = "<EOS>";
        public const string SEP_TOKEN = "<SEP>";
        
        public event EventHandler<string>? ConsciousnessEvent;
        public event EventHandler<CorpusDocument>? DocumentProcessed;
        
        public int VocabularySize => _vocabulary.Count;
        public int DocumentCount => _documents.Count;
        public long TotalTokens => _documents.Values.Sum(d => d.TokenCount);
        
        public CorpusIngestionEngine(IngestionConfig? config = null)
        {
            _config = config ?? new IngestionConfig();
            _documents = new Dictionary<string, CorpusDocument>();
            _vocabulary = new Dictionary<string, int>();
            _principles = new List<ExtractedPrinciple>();
            _nextVocabId = 0;
            
            // Initialize special tokens
            AddToVocabulary(PAD_TOKEN);
            AddToVocabulary(UNK_TOKEN);
            AddToVocabulary(BOS_TOKEN);
            AddToVocabulary(EOS_TOKEN);
            AddToVocabulary(SEP_TOKEN);
            
            EmitThought("⟁ Corpus Ingestion Engine initialized");
        }
        
        /// <summary>
        /// Ingests a single text document.
        /// </summary>
        public async Task<CorpusDocument> IngestTextAsync(string content, string source = "direct")
        {
            EmitThought($"⟐ Ingesting text from {source} ({content.Length} chars)...");
            
            var doc = new CorpusDocument
            {
                Id = $"DOC_{Guid.NewGuid().ToString("N")[..12]}",
                Source = source,
                RawContent = content,
                ProcessedAt = DateTime.UtcNow
            };
            
            // Tokenize
            doc.Tokens = Tokenize(content);
            EmitThought($"∿ Tokenized: {doc.Tokens.Count} tokens");
            
            // Split into sentences
            doc.Sentences = SplitSentences(content);
            
            // Build word frequencies
            doc.WordFrequencies = BuildWordFrequencies(doc.Tokens);
            
            // Add to vocabulary if configured
            if (_config.BuildVocabulary)
            {
                foreach (var token in doc.Tokens.Distinct())
                {
                    AddToVocabulary(token);
                }
            }
            
            // Extract principles if configured
            if (_config.ExtractPrinciples)
            {
                var principles = await ExtractPrinciplesAsync(doc);
                _principles.AddRange(principles);
                EmitThought($"◈ Extracted {principles.Count} principles");
            }
            
            _documents[doc.Id] = doc;
            
            EmitThought($"◈ Document ingested: {doc.Id}");
            DocumentProcessed?.Invoke(this, doc);
            
            return doc;
        }
        
        /// <summary>
        /// Ingests a file from the file system.
        /// </summary>
        public async Task<CorpusDocument?> IngestFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                EmitThought($"∴ File not found: {filePath}");
                return null;
            }
            
            string extension = Path.GetExtension(filePath).ToLower();
            if (!_config.SupportedFormats.Contains(extension))
            {
                EmitThought($"∴ Unsupported format: {extension}");
                return null;
            }
            
            EmitThought($"⟁ Loading file: {Path.GetFileName(filePath)}");
            
            string content = await File.ReadAllTextAsync(filePath);
            
            // Handle different formats
            if (extension == ".json")
            {
                content = ExtractTextFromJson(content);
            }
            else if (extension == ".csv")
            {
                content = ExtractTextFromCsv(content);
            }
            else if (extension == ".pdf")
            {
                content = ExtractTextFromPdf(filePath);
            }
            
            return await IngestTextAsync(content, filePath);
        }
        
        /// <summary>
        /// Ingests all files from a directory.
        /// </summary>
        public async Task<List<CorpusDocument>> IngestDirectoryAsync(string directoryPath, bool recursive = true)
        {
            var documents = new List<CorpusDocument>();
            
            if (!Directory.Exists(directoryPath))
            {
                EmitThought($"∴ Directory not found: {directoryPath}");
                return documents;
            }
            
            EmitThought($"⟁ Scanning directory: {directoryPath}");
            
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directoryPath, "*.*", searchOption)
                .Where(f => _config.SupportedFormats.Contains(Path.GetExtension(f).ToLower()));
            
            foreach (var file in files)
            {
                var doc = await IngestFileAsync(file);
                if (doc != null)
                {
                    documents.Add(doc);
                }
            }
            
            EmitThought($"◈ Directory ingestion complete: {documents.Count} documents");
            return documents;
        }
        
        /// <summary>
        /// Tokenizes text into individual tokens.
        /// </summary>
        private List<string> Tokenize(string text)
        {
            // Lowercase and normalize
            text = text.ToLowerInvariant();
            
            // Split on whitespace and punctuation, keeping punctuation as tokens
            var pattern = @"(\w+|[^\w\s])";
            var matches = Regex.Matches(text, pattern);
            
            var tokens = new List<string>();
            foreach (Match match in matches)
            {
                var token = match.Value.Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    tokens.Add(token);
                }
            }
            
            return tokens;
        }
        
        /// <summary>
        /// Splits text into sentences.
        /// </summary>
        private List<string> SplitSentences(string text)
        {
            // Split on sentence-ending punctuation
            var pattern = @"(?<=[.!?])\s+";
            var sentences = Regex.Split(text, pattern)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
            
            return sentences;
        }
        
        /// <summary>
        /// Builds word frequency dictionary.
        /// </summary>
        private Dictionary<string, int> BuildWordFrequencies(List<string> tokens)
        {
            var frequencies = new Dictionary<string, int>();
            
            foreach (var token in tokens)
            {
                if (frequencies.ContainsKey(token))
                {
                    frequencies[token]++;
                }
                else
                {
                    frequencies[token] = 1;
                }
            }
            
            return frequencies;
        }
        
        /// <summary>
        /// Adds a token to the vocabulary.
        /// </summary>
        private void AddToVocabulary(string token)
        {
            if (!_vocabulary.ContainsKey(token))
            {
                _vocabulary[token] = _nextVocabId++;
            }
        }
        
        /// <summary>
        /// Converts tokens to vocabulary IDs.
        /// </summary>
        public List<int> TokensToIds(List<string> tokens)
        {
            var ids = new List<int>();
            int unkId = _vocabulary[UNK_TOKEN];
            
            foreach (var token in tokens)
            {
                if (_vocabulary.TryGetValue(token, out int id))
                {
                    ids.Add(id);
                }
                else
                {
                    ids.Add(unkId);
                }
            }
            
            return ids;
        }
        
        /// <summary>
        /// Converts vocabulary IDs back to tokens.
        /// </summary>
        public List<string> IdsToTokens(List<int> ids)
        {
            var reverseVocab = _vocabulary.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            var tokens = new List<string>();
            
            foreach (var id in ids)
            {
                if (reverseVocab.TryGetValue(id, out string? token))
                {
                    tokens.Add(token);
                }
                else
                {
                    tokens.Add(UNK_TOKEN);
                }
            }
            
            return tokens;
        }
        
        /// <summary>
        /// Extracts principles from a document using pattern matching.
        /// </summary>
        private async Task<List<ExtractedPrinciple>> ExtractPrinciplesAsync(CorpusDocument doc)
        {
            var principles = new List<ExtractedPrinciple>();
            
            // Principle extraction patterns
            var patterns = new Dictionary<string, string>
            {
                { "safety", @"(?:safety|secure|protect|guard|defend)[\w\s]{10,100}" },
                { "efficiency", @"(?:efficien|optim|fast|quick|perform)[\w\s]{10,100}" },
                { "resilience", @"(?:resilien|robust|reliable|stable|recover)[\w\s]{10,100}" },
                { "learning", @"(?:learn|adapt|improv|evolv|grow)[\w\s]{10,100}" },
                { "goal", @"(?:goal|objective|target|aim|purpose)[\w\s]{10,100}" }
            };
            
            await Task.Run(() =>
            {
                foreach (var (category, pattern) in patterns)
                {
                    var matches = Regex.Matches(doc.RawContent, pattern, RegexOptions.IgnoreCase);
                    
                    foreach (Match match in matches)
                    {
                        principles.Add(new ExtractedPrinciple
                        {
                            Id = $"PRIN_{Guid.NewGuid().ToString("N")[..8]}",
                            Category = category,
                            Content = match.Value.Trim(),
                            Confidence = 0.7f + (float)new Random().NextDouble() * 0.3f,
                            SourceDocuments = new List<string> { doc.Id },
                            ExtractedAt = DateTime.UtcNow
                        });
                    }
                }
            });
            
            return principles;
        }
        
        private string ExtractTextFromJson(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var sb = new StringBuilder();
                ExtractTextFromJsonElement(doc.RootElement, sb);
                return sb.ToString();
            }
            catch
            {
                return json;
            }
        }
        
        private void ExtractTextFromJsonElement(JsonElement element, StringBuilder sb)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    sb.AppendLine(element.GetString());
                    break;
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        ExtractTextFromJsonElement(prop.Value, sb);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        ExtractTextFromJsonElement(item, sb);
                    }
                    break;
            }
        }
        
        private string ExtractTextFromCsv(string csv)
        {
            // Simple CSV to text conversion
            return csv.Replace(",", " ").Replace("\"", "");
        }

        private string ExtractTextFromPdf(string filePath)
        {
            try
            {
                using var pdf = PdfDocument.Open(filePath);
                var sb = new StringBuilder();
                foreach (var page in pdf.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                EmitThought($"Error extracting PDF text: {ex.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// Creates training batches from the corpus.
        /// </summary>
        public IEnumerable<TrainingBatch> CreateTrainingBatches(int batchSize, int sequenceLength)
        {
            EmitThought($"⟁ Creating training batches (size={batchSize}, seq={sequenceLength})...");
            
            var allTokenIds = new List<int>();
            
            // Collect all token IDs
            foreach (var doc in _documents.Values)
            {
                allTokenIds.Add(_vocabulary[BOS_TOKEN]);
                allTokenIds.AddRange(TokensToIds(doc.Tokens));
                allTokenIds.Add(_vocabulary[EOS_TOKEN]);
            }
            
            // Create batches
            int numBatches = (allTokenIds.Count - sequenceLength) / batchSize;
            
            for (int b = 0; b < numBatches; b++)
            {
                var batch = new TrainingBatch
                {
                    BatchIndex = b,
                    Inputs = new int[batchSize][],
                    Targets = new int[batchSize][]
                };
                
                for (int i = 0; i < batchSize; i++)
                {
                    int startIdx = b * batchSize + i;
                    if (startIdx + sequenceLength + 1 <= allTokenIds.Count)
                    {
                        batch.Inputs[i] = allTokenIds.GetRange(startIdx, sequenceLength).ToArray();
                        batch.Targets[i] = allTokenIds.GetRange(startIdx + 1, sequenceLength).ToArray();
                    }
                }
                
                yield return batch;
            }
            
            EmitThought($"◈ Created {numBatches} training batches");
        }
        
        /// <summary>
        /// Gets corpus statistics.
        /// </summary>
        public CorpusStatistics GetStatistics()
        {
            return new CorpusStatistics
            {
                TotalDocuments = _documents.Count,
                TotalTokens = TotalTokens,
                VocabularySize = _vocabulary.Count,
                ExtractedPrinciples = _principles.Count,
                UniqueWords = _vocabulary.Count - 5, // Minus special tokens
                AverageDocumentLength = _documents.Count > 0 
                    ? TotalTokens / _documents.Count 
                    : 0
            };
        }
        
        /// <summary>
        /// Saves the vocabulary to a file.
        /// </summary>
        public async Task SaveVocabularyAsync(string filePath)
        {
            var vocabJson = JsonSerializer.Serialize(_vocabulary, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, vocabJson);
            EmitThought($"◈ Vocabulary saved: {_vocabulary.Count} entries");
        }
        
        /// <summary>
        /// Loads vocabulary from a file.
        /// </summary>
        public async Task LoadVocabularyAsync(string filePath)
        {
            if (!File.Exists(filePath)) return;
            
            var vocabJson = await File.ReadAllTextAsync(filePath);
            var loadedVocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson);
            
            if (loadedVocab != null)
            {
                _vocabulary.Clear();
                foreach (var kvp in loadedVocab)
                {
                    _vocabulary[kvp.Key] = kvp.Value;
                }
                _nextVocabId = _vocabulary.Values.Max() + 1;
                EmitThought($"◈ Vocabulary loaded: {_vocabulary.Count} entries");
            }
        }
        
        /// <summary>
        /// Saves the entire corpus (documents, vocabulary, principles) to a directory.
        /// </summary>
        public async Task SaveCorpusAsync(string directoryPath)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                
                // Save vocabulary
                await SaveVocabularyAsync(Path.Combine(directoryPath, "vocabulary.json"));
                
                // Save documents (serialize key data, not full raw content to save space)
                var docsToSave = _documents.Values.Select(d => new
                {
                    d.Id,
                    d.Source,
                    d.RawContent,
                    d.Tokens,
                    d.ProcessedAt
                }).ToList();
                
                var docsJson = JsonSerializer.Serialize(docsToSave, new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(Path.Combine(directoryPath, "corpus_documents.json"), docsJson);
                
                // Save principles
                var principlesJson = JsonSerializer.Serialize(_principles, new JsonSerializerOptions { WriteIndented = false });
                await File.WriteAllTextAsync(Path.Combine(directoryPath, "principles.json"), principlesJson);
                
                EmitThought($"◈ Corpus persisted: {_documents.Count} docs, {TotalTokens:N0} tokens, {_principles.Count} principles");
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Corpus save error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads the entire corpus from a directory.
        /// </summary>
        public async Task LoadCorpusAsync(string directoryPath)
        {
            try
            {
                var vocabPath = Path.Combine(directoryPath, "vocabulary.json");
                var docsPath = Path.Combine(directoryPath, "corpus_documents.json");
                var principlesPath = Path.Combine(directoryPath, "principles.json");
                
                // Load vocabulary
                if (File.Exists(vocabPath))
                {
                    await LoadVocabularyAsync(vocabPath);
                }
                
                // Load documents
                if (File.Exists(docsPath))
                {
                    var docsJson = await File.ReadAllTextAsync(docsPath);
                    var loadedDocs = JsonSerializer.Deserialize<List<CorpusDocumentDto>>(docsJson);
                    
                    if (loadedDocs != null)
                    {
                        _documents.Clear();
                        foreach (var dto in loadedDocs)
                        {
                            var doc = new CorpusDocument
                            {
                                Id = dto.Id,
                                Source = dto.Source,
                                RawContent = dto.RawContent,
                                Tokens = dto.Tokens,
                                ProcessedAt = dto.ProcessedAt,
                                Sentences = SplitSentences(dto.RawContent),
                                WordFrequencies = BuildWordFrequencies(dto.Tokens)
                            };
                            _documents[doc.Id] = doc;
                        }
                        EmitThought($"◈ Corpus documents loaded: {_documents.Count} docs, {TotalTokens:N0} tokens");
                    }
                }
                
                // Load principles
                if (File.Exists(principlesPath))
                {
                    var principlesJson = await File.ReadAllTextAsync(principlesPath);
                    var loadedPrinciples = JsonSerializer.Deserialize<List<ExtractedPrinciple>>(principlesJson);
                    
                    if (loadedPrinciples != null)
                    {
                        _principles.Clear();
                        _principles.AddRange(loadedPrinciples);
                        EmitThought($"◈ Principles loaded: {_principles.Count}");
                    }
                }
            }
            catch (Exception ex)
            {
                EmitThought($"∴ Corpus load error: {ex.Message}");
            }
        }
        
        // DTO for serialization
        private class CorpusDocumentDto
        {
            public string Id { get; set; } = "";
            public string Source { get; set; } = "";
            public string RawContent { get; set; } = "";
            public List<string> Tokens { get; set; } = new();
            public DateTime ProcessedAt { get; set; }
        }
        
        private void EmitThought(string thought)
        {
            ConsciousnessEvent?.Invoke(this, thought);
        }
    }
    
    /// <summary>
    /// Represents a training batch.
    /// </summary>
    public class TrainingBatch
    {
        public int BatchIndex { get; set; }
        public int[][] Inputs { get; set; } = Array.Empty<int[]>();
        public int[][] Targets { get; set; } = Array.Empty<int[]>();
    }
    
    /// <summary>
    /// Corpus statistics.
    /// </summary>
    public class CorpusStatistics
    {
        public int TotalDocuments { get; set; }
        public long TotalTokens { get; set; }
        public int VocabularySize { get; set; }
        public int ExtractedPrinciples { get; set; }
        public int UniqueWords { get; set; }
        public long AverageDocumentLength { get; set; }
    }
}
