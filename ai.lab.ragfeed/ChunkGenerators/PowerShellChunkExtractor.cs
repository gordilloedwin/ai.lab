using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class PowerShellChunkExtractor
{
    public List<string> ExtractPs1Chunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        
        // Remove comments while preserving help comments
        var codeWithoutComments = RemoveComments(code, out var helpComments);
        
        var chunks = new List<string>();

        // Extract script header/metadata (requires, param block)
        ExtractScriptMetadata(code, chunks);
        
        // Extract functions
        ExtractFunctions(code, helpComments, chunks);
        
        // Extract advanced functions (with CmdletBinding)
        ExtractAdvancedFunctions(code, helpComments, chunks);
        
        // Extract script blocks and classes
        ExtractClasses(code, chunks);
        
        // Extract top-level variables and configuration
        ExtractTopLevelVariables(codeWithoutComments, chunks);

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    }

    /// <summary>
    /// Removes comments while preserving help comments (comment-based help)
    /// </summary>
    private string RemoveComments(string code, out Dictionary<int, string> helpComments)
    {
        helpComments = new Dictionary<int, string>();
        var result = new StringBuilder();
        var lines = code.Split('\n');
        bool inMultiLineComment = false;
        bool inHelpComment = false;
        var currentHelpComment = new StringBuilder();
        int helpCommentStartPosition = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.TrimStart();

            // Handle multi-line comments <# ... #>
            if (trimmedLine.StartsWith("<#"))
            {
                inMultiLineComment = true;
                
                // Check if it's a help comment (.SYNOPSIS, .DESCRIPTION, etc.)
                if (i + 1 < lines.Length)
                {
                    var nextLines = string.Join("\n", lines.Skip(i).Take(10));
                    if (Regex.IsMatch(nextLines, @"\.(SYNOPSIS|DESCRIPTION|PARAMETER|EXAMPLE|NOTES|LINK)", RegexOptions.IgnoreCase))
                    {
                        inHelpComment = true;
                        helpCommentStartPosition = result.Length;
                        currentHelpComment.Clear();
                    }
                }
                
                if (line.Contains("#>"))
                {
                    if (inHelpComment)
                    {
                        helpComments[helpCommentStartPosition] = currentHelpComment.ToString();
                        inHelpComment = false;
                    }
                    inMultiLineComment = false;
                }
                continue;
            }

            if (inMultiLineComment)
            {
                if (inHelpComment)
                {
                    currentHelpComment.AppendLine(line);
                }
                
                if (line.Contains("#>"))
                {
                    if (inHelpComment)
                    {
                        helpComments[helpCommentStartPosition] = currentHelpComment.ToString();
                        inHelpComment = false;
                    }
                    inMultiLineComment = false;
                }
                continue;
            }

            // Handle single-line comments
            if (trimmedLine.StartsWith("#"))
            {
                // Skip comment lines
                result.AppendLine(); // Preserve line structure
                continue;
            }

            // Remove inline comments (but preserve # in strings)
            var processedLine = RemoveInlineComments(line);
            result.AppendLine(processedLine);
        }

        return result.ToString();
    }

    private string RemoveInlineComments(string line)
    {
        bool inString = false;
        char stringChar = '\0';
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            // Handle strings
            if ((c == '"' || c == '\'') && (i == 0 || line[i - 1] != '`'))
            {
                if (!inString)
                {
                    inString = true;
                    stringChar = c;
                }
                else if (c == stringChar)
                {
                    inString = false;
                }
            }

            // Found comment outside string
            if (c == '#' && !inString)
            {
                return line.Substring(0, i);
            }
        }

        return line;
    }

    /// <summary>
    /// Extracts script metadata like #Requires and param() blocks
    /// </summary>
    private void ExtractScriptMetadata(string code, List<string> chunks)
    {
        var metadata = new StringBuilder();
        bool hasMetadata = false;

        // Extract #Requires statements
        var requiresPattern = @"#Requires\s+.*";
        var requiresMatches = Regex.Matches(code, requiresPattern, RegexOptions.IgnoreCase);
        if (requiresMatches.Count > 0)
        {
            hasMetadata = true;
            metadata.AppendLine("# Script Requirements");
            foreach (Match match in requiresMatches)
            {
                metadata.AppendLine(match.Value.Trim());
            }
        }

        // Extract top-level param block
        var paramPattern = @"^\s*[Pp]aram\s*\(";
        var paramMatch = Regex.Match(code, paramPattern, RegexOptions.Multiline);
        if (paramMatch.Success)
        {
            var paramBlock = ExtractParamBlock(code, paramMatch.Index);
            if (!string.IsNullOrWhiteSpace(paramBlock))
            {
                hasMetadata = true;
                metadata.AppendLine("\n# Script Parameters");
                metadata.AppendLine(paramBlock.Trim());
            }
        }

        if (hasMetadata)
        {
            chunks.Add(metadata.ToString().Trim());
        }
    }

    /// <summary>
    /// Extracts basic PowerShell functions
    /// </summary>
    private void ExtractFunctions(string code, Dictionary<int, string> helpComments, List<string> chunks)
    {
        // Match: function FunctionName { ... }
        var functionPattern = @"[Ff]unction\s+([\w-]+)\s*\{";
        var matches = Regex.Matches(code, functionPattern);

        foreach (Match match in matches)
        {
            var functionName = match.Groups[1].Value;
            var startIndex = match.Index;
            
            // Skip if this is an advanced function (has [CmdletBinding])
            var beforeFunction = code.Substring(Math.Max(0, startIndex - 200), Math.Min(200, startIndex));
            if (beforeFunction.Contains("[CmdletBinding"))
                continue;
            
            var helpComment = FindHelpCommentBefore(helpComments, startIndex);
            var functionBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(functionBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"# Function: {functionName}");
                
                if (!string.IsNullOrWhiteSpace(helpComment))
                {
                    enriched.AppendLine("<#");
                    enriched.AppendLine(helpComment.Trim());
                    enriched.AppendLine("#>");
                }
                
                enriched.Append(functionBody.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Extracts advanced functions with [CmdletBinding] attribute
    /// </summary>
    private void ExtractAdvancedFunctions(string code, Dictionary<int, string> helpComments, List<string> chunks)
    {
        // Match: [CmdletBinding(...)] followed by function
        var advancedFunctionPattern = @"\[CmdletBinding[^\]]*\]\s*[Pp]aram\s*\([^\)]*\)\s*[Ff]unction\s+([\w-]+)|\[CmdletBinding[^\]]*\][^\{]*[Ff]unction\s+([\w-]+)\s*\{";
        var matches = Regex.Matches(code, advancedFunctionPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var functionName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            var startIndex = match.Index;
            var helpComment = FindHelpCommentBefore(helpComments, startIndex);
            
            // Extract from [CmdletBinding] to end of function
            var functionBody = ExtractAdvancedFunctionBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(functionBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"# Advanced Function: {functionName}");
                enriched.AppendLine("# Type: Cmdlet (Advanced Function)");
                
                if (!string.IsNullOrWhiteSpace(helpComment))
                {
                    enriched.AppendLine("<#");
                    enriched.AppendLine(helpComment.Trim());
                    enriched.AppendLine("#>");
                }
                
                enriched.Append(functionBody.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Extracts PowerShell classes (PS 5.0+)
    /// </summary>
    private void ExtractClasses(string code, List<string> chunks)
    {
        var classPattern = @"[Cc]lass\s+([\w-]+)(?:\s*:\s*([\w-]+))?\s*\{";
        var matches = Regex.Matches(code, classPattern);

        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            var baseClass = match.Groups[2].Success ? match.Groups[2].Value : null;
            var startIndex = match.Index;
            var classBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(classBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"# Class: {className}");
                if (!string.IsNullOrWhiteSpace(baseClass))
                {
                    enriched.AppendLine($"# Inherits: {baseClass}");
                }
                enriched.Append(classBody.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Extracts top-level variable assignments and configuration
    /// </summary>
    private void ExtractTopLevelVariables(string code, List<string> chunks)
    {
        // Match variable assignments outside functions
        var varPattern = @"^\s*\$([\w]+)\s*=\s*(.+)$";
        var matches = Regex.Matches(code, varPattern, RegexOptions.Multiline);

        var variables = new StringBuilder();
        bool hasVariables = false;

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();
            
            // Skip if it looks like it's inside a function (basic check)
            var beforeVar = code.Substring(0, match.Index);
            var openBraces = beforeVar.Count(c => c == '{');
            var closeBraces = beforeVar.Count(c => c == '}');
            
            if (openBraces > closeBraces)
                continue; // Inside a block
            
            hasVariables = true;
            variables.AppendLine($"# Variable: ${varName}");
            variables.AppendLine($"${varName} = {value}");
            variables.AppendLine();
        }

        if (hasVariables)
        {
            chunks.Add($"# Script Variables\n{variables.ToString().Trim()}");
        }
    }

    /// <summary>
    /// Extracts param() block
    /// </summary>
    private string ExtractParamBlock(string code, int startIndex)
    {
        int paramStart = code.IndexOf('(', startIndex);
        if (paramStart == -1)
            return string.Empty;

        int depth = 0;
        bool inString = false;
        char stringChar = '\0';

        for (int i = paramStart; i < code.Length; i++)
        {
            char c = code[i];
            char prev = i > 0 ? code[i - 1] : '\0';

            // Handle escape sequences
            if (inString && prev == '`')
                continue;

            // Handle strings
            if ((c == '"' || c == '\'') && !inString)
            {
                inString = true;
                stringChar = c;
            }
            else if (inString && c == stringChar)
            {
                inString = false;
            }

            if (inString)
                continue;

            if (c == '(')
                depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return code.Substring(startIndex, i - startIndex + 1);
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Extracts advanced function with [CmdletBinding] and param block
    /// </summary>
    private string ExtractAdvancedFunctionBlock(string code, int startIndex)
    {
        // Find the function keyword
        int functionIndex = code.IndexOf("function", startIndex, StringComparison.OrdinalIgnoreCase);
        if (functionIndex == -1)
            return string.Empty;

        // Find opening brace
        int braceIndex = code.IndexOf('{', functionIndex);
        if (braceIndex == -1)
            return string.Empty;

        // Extract from [CmdletBinding] to end of function block
        int depth = 0;
        bool inString = false;
        char stringChar = '\0';

        for (int i = braceIndex; i < code.Length; i++)
        {
            char c = code[i];
            char prev = i > 0 ? code[i - 1] : '\0';

            if (inString && prev == '`')
                continue;

            if ((c == '"' || c == '\'') && !inString)
            {
                inString = true;
                stringChar = c;
            }
            else if (inString && c == stringChar)
            {
                inString = false;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return code.Substring(startIndex, i - startIndex + 1);
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Extracts a code block by matching braces
    /// </summary>
    private string ExtractBlock(string code, int startIndex)
    {
        int openBraceIndex = code.IndexOf('{', startIndex);
        if (openBraceIndex == -1)
            return string.Empty;

        int depth = 0;
        bool inString = false;
        char stringChar = '\0';
        bool inHereString = false;

        for (int i = openBraceIndex; i < code.Length; i++)
        {
            char c = code[i];
            char prev = i > 0 ? code[i - 1] : '\0';
            char next = i + 1 < code.Length ? code[i + 1] : '\0';

            // Handle here-strings @" "@ or @' '@
            if (c == '@' && (next == '"' || next == '\''))
            {
                inHereString = true;
                stringChar = next;
                i++;
                continue;
            }

            if (inHereString)
            {
                if (c == stringChar && next == '@')
                {
                    inHereString = false;
                    i++;
                }
                continue;
            }

            // Handle escape sequences
            if (inString && prev == '`')
                continue;

            // Handle strings
            if ((c == '"' || c == '\'') && !inString)
            {
                inString = true;
                stringChar = c;
            }
            else if (inString && c == stringChar)
            {
                inString = false;
            }

            if (inString)
                continue;

            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return code.Substring(startIndex, i - startIndex + 1);
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Finds help comment before a given position
    /// </summary>
    private string FindHelpCommentBefore(Dictionary<int, string> helpComments, int position)
    {
        var closestPosition = helpComments.Keys
            .Where(k => k < position)
            .OrderByDescending(k => k)
            .FirstOrDefault();

        return closestPosition > 0 && helpComments.ContainsKey(closestPosition)
            ? helpComments[closestPosition]
            : string.Empty;
    }
}
