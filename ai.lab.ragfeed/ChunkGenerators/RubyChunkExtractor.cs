using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class RubyChunkExtractor
{
    public List<string> ExtractRubyChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var chunks = new List<string>();

        // Simple regex patterns to identify class and method definitions in Ruby
        var classPattern = @"class\s+\w+.*?(?=^end$)";
        var methodPattern = @"def\s+\w+.*?(?=^end$)";

        // Extract class definitions
        foreach (Match match in Regex.Matches(code, classPattern, RegexOptions.Singleline | RegexOptions.Multiline))
        {
            string classText = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(classText))
            {
                chunks.Add(classText);
            }
        }

        // Extract method definitions
        foreach (Match match in Regex.Matches(code, methodPattern, RegexOptions.Singleline | RegexOptions.Multiline))
        {
            string methodText = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(methodText))
            {
                chunks.Add(methodText);
            }
        }

        return chunks;
    }
}