using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class TextChunkExtractor
{
    private const int DefaultMaxCharsPerChunk = 1000;
    private const int DefaultOverlapChars = 200;

    /// <summary>
    /// Generates semantic chunks from a text file using multiple strategies:
    /// 1. Markdown section detection (headers)
    /// 2. Paragraph-based chunking
    /// 3. Sentence boundary detection
    /// 4. Character-based windowing with overlap
    /// </summary>
    public List<string> GenerateChunks(string filePath, int maxCharsPerChunk = DefaultMaxCharsPerChunk, int overlapChars = DefaultOverlapChars)
    {
        var content = File.ReadAllText(filePath);
        var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

        return fileExtension switch
        {
            ".md" or ".markdown" => ExtractMarkdownChunks(content, maxCharsPerChunk, overlapChars),
            ".json" => ExtractJsonChunks(content, maxCharsPerChunk),
            ".xml" => ExtractXmlChunks(content, maxCharsPerChunk),
            ".txt" or ".log" => ExtractTextChunks(content, maxCharsPerChunk, overlapChars),
            _ => ExtractTextChunks(content, maxCharsPerChunk, overlapChars)
        };
    }

    /// <summary>
    /// Extracts chunks from Markdown by respecting header hierarchy and sections
    /// </summary>
    private List<string> ExtractMarkdownChunks(string content, int maxChars, int overlap)
    {
        var chunks = new List<string>();
        
        // Split by headers (# ## ### etc.)
        var headerPattern = @"^(#{1,6})\s+(.+)$";
        var lines = content.Split('\n');
        var currentChunk = new StringBuilder();
        var currentHeader = "";

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = Regex.Match(line, headerPattern, RegexOptions.Multiline);

            if (match.Success)
            {
                // New header found - save previous chunk
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }
                currentHeader = line;
                currentChunk.AppendLine(line);
            }
            else
            {
                currentChunk.AppendLine(line);

                // Check if chunk is getting too large
                if (currentChunk.Length > maxChars)
                {
                    var chunkText = currentChunk.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunkText))
                    {
                        chunks.Add(chunkText);
                    }
                    
                    // Start new chunk with header context + overlap
                    currentChunk.Clear();
                    if (!string.IsNullOrEmpty(currentHeader))
                    {
                        currentChunk.AppendLine(currentHeader);
                    }
                    
                    // Add overlap from previous chunk
                    var overlapText = GetOverlap(chunkText, overlap);
                    if (!string.IsNullOrWhiteSpace(overlapText))
                    {
                        currentChunk.AppendLine(overlapText);
                    }
                }
            }
        }

        // Add final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    /// <summary>
    /// Extracts chunks from JSON by preserving object boundaries
    /// </summary>
    private List<string> ExtractJsonChunks(string content, int maxChars)
    {
        var chunks = new List<string>();
        
        // Try to chunk by top-level objects/arrays
        var depth = 0;
        var currentChunk = new StringBuilder();
        
        foreach (var ch in content)
        {
            currentChunk.Append(ch);
            
            if (ch == '{' || ch == '[') depth++;
            if (ch == '}' || ch == ']') depth--;
            
            // At root level and chunk is large enough
            if (depth == 0 && currentChunk.Length > maxChars)
            {
                var chunk = currentChunk.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                {
                    chunks.Add(chunk);
                }
                currentChunk.Clear();
            }
        }
        
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }
        
        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    /// <summary>
    /// Extracts chunks from XML by preserving element boundaries
    /// </summary>
    private List<string> ExtractXmlChunks(string content, int maxChars)
    {
        var chunks = new List<string>();
        
        // Split by top-level elements
        var elementPattern = @"<(\w+)[^>]*>.*?</\1>";
        var matches = Regex.Matches(content, elementPattern, RegexOptions.Singleline);
        
        var currentChunk = new StringBuilder();
        
        foreach (Match match in matches)
        {
            var element = match.Value;
            
            if (currentChunk.Length + element.Length > maxChars && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
                currentChunk.Clear();
            }
            
            currentChunk.AppendLine(element);
        }
        
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }
        
        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    /// <summary>
    /// Extracts chunks from plain text using paragraph and sentence boundaries
    /// </summary>
    private List<string> ExtractTextChunks(string content, int maxChars, int overlap)
    {
        var chunks = new List<string>();
        
        // Split by paragraphs (double newline)
        var paragraphs = Regex.Split(content, @"\n\s*\n")
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var currentChunk = new StringBuilder();
        
        foreach (var paragraph in paragraphs)
        {
            // If paragraph itself is too large, split by sentences
            if (paragraph.Length > maxChars)
            {
                // Save current chunk first
                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }
                
                // Split large paragraph by sentences
                var sentences = SplitIntoSentences(paragraph);
                foreach (var sentence in sentences)
                {
                    if (currentChunk.Length + sentence.Length > maxChars && currentChunk.Length > 0)
                    {
                        var chunkText = currentChunk.ToString().Trim();
                        chunks.Add(chunkText);
                        
                        // Add overlap
                        currentChunk.Clear();
                        var overlapText = GetOverlap(chunkText, overlap);
                        if (!string.IsNullOrWhiteSpace(overlapText))
                        {
                            currentChunk.Append(overlapText).Append(" ");
                        }
                    }
                    currentChunk.Append(sentence).Append(" ");
                }
            }
            else
            {
                // Check if adding this paragraph exceeds limit
                if (currentChunk.Length + paragraph.Length > maxChars && currentChunk.Length > 0)
                {
                    var chunkText = currentChunk.ToString().Trim();
                    chunks.Add(chunkText);
                    
                    // Start new chunk with overlap
                    currentChunk.Clear();
                    var overlapText = GetOverlap(chunkText, overlap);
                    if (!string.IsNullOrWhiteSpace(overlapText))
                    {
                        currentChunk.Append(overlapText).Append("\n\n");
                    }
                }
                
                currentChunk.AppendLine(paragraph).AppendLine();
            }
        }
        
        // Add final chunk
        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }
        
        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    /// <summary>
    /// Splits text into sentences using common sentence boundaries
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        // Split on sentence endings but preserve them
        var sentencePattern = @"(?<=[.!?])\s+(?=[A-Z])";
        var sentences = Regex.Split(text, sentencePattern)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        
        return sentences;
    }

    /// <summary>
    /// Gets the last N characters as overlap for context continuity
    /// </summary>
    private string GetOverlap(string text, int overlapChars)
    {
        if (string.IsNullOrEmpty(text) || overlapChars <= 0 || text.Length <= overlapChars)
        {
            return text;
        }
        
        // Try to get overlap at sentence boundary
        var overlapText = text.Substring(text.Length - overlapChars);
        var sentenceStart = overlapText.LastIndexOfAny(new[] { '.', '!', '?' });
        
        if (sentenceStart > 0 && sentenceStart < overlapText.Length - 1)
        {
            return overlapText.Substring(sentenceStart + 1).Trim();
        }
        
        return overlapText.Trim();
    }
}
