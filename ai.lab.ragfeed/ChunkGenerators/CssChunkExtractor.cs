using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class CssChunkExtractor
{
    public List<string> ExtractCssChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        
        // Remove comments first (preserving important ones separately)
        var codeWithoutComments = RemoveComments(code, out var importantComments);
        
        var chunks = new List<string>();

        // Extract @import statements
        ExtractImports(code, chunks);
        
        // Extract CSS variables (custom properties)
        ExtractCssVariables(code, chunks);
        
        // Extract @font-face rules
        ExtractFontFaces(code, chunks);
        
        // Extract @keyframes animations
        ExtractKeyframes(code, chunks);
        
        // Extract @media queries with their rules
        ExtractMediaQueries(code, importantComments, chunks);
        
        // Extract regular CSS rules
        ExtractCssRules(codeWithoutComments, importantComments, chunks);

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    /// <summary>
    /// Removes comments while preserving important/documentation comments separately
    /// </summary>
    private string RemoveComments(string css, out Dictionary<int, string> importantComments)
    {
        importantComments = new Dictionary<int, string>();
        var result = new StringBuilder();
        bool inString = false;
        bool inComment = false;
        char stringChar = '\0';
        var currentComment = new StringBuilder();
        int commentStartPosition = 0;
        bool isImportant = false;

        for (int i = 0; i < css.Length; i++)
        {
            char c = css[i];
            char next = i + 1 < css.Length ? css[i + 1] : '\0';
            char prev = i > 0 ? css[i - 1] : '\0';

            // Handle escape sequences
            if (inString && c == '\\' && next != '\0')
            {
                result.Append(c);
                result.Append(next);
                i++;
                continue;
            }

            // Handle strings
            if ((c == '"' || c == '\'') && !inComment)
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
                result.Append(c);
                continue;
            }

            if (inString)
            {
                result.Append(c);
                continue;
            }

            // Handle comments
            if (c == '/' && next == '*' && !inComment)
            {
                inComment = true;
                commentStartPosition = result.Length;
                currentComment.Clear();
                
                // Check for important comment (/*! or /**)
                if (i + 2 < css.Length && (css[i + 2] == '!' || css[i + 2] == '*'))
                {
                    isImportant = true;
                }
                
                i++;
                continue;
            }

            if (inComment)
            {
                if (c == '*' && next == '/')
                {
                    if (isImportant)
                    {
                        importantComments[commentStartPosition] = currentComment.ToString();
                    }
                    
                    inComment = false;
                    isImportant = false;
                    currentComment.Clear();
                    i++;
                }
                else
                {
                    currentComment.Append(c);
                }
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// Extracts @import statements
    /// </summary>
    private void ExtractImports(string css, List<string> chunks)
    {
        var importPattern = @"@import\s+(?:url\()?['""]?([^'"")\s]+)['""]?\)?(?:\s+[^;]+)?;";
        var matches = Regex.Matches(css, importPattern);

        foreach (Match match in matches)
        {
            var importStatement = match.Value.Trim();
            chunks.Add($"// CSS Import\n{importStatement}");
        }
    }

    /// <summary>
    /// Extracts CSS custom properties (variables) from :root or other selectors
    /// </summary>
    private void ExtractCssVariables(string css, List<string> chunks)
    {
        // Match :root { --var: value; }
        var rootVarsPattern = @":root\s*\{([^}]+)\}";
        var matches = Regex.Matches(css, rootVarsPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var varsBlock = match.Value.Trim();
            var variables = ExtractVariablesFromBlock(match.Groups[1].Value);
            
            if (variables.Any())
            {
                var enriched = new StringBuilder();
                enriched.AppendLine("// CSS Variables (Custom Properties)");
                enriched.AppendLine(":root {");
                foreach (var variable in variables)
                {
                    enriched.AppendLine($"  {variable}");
                }
                enriched.Append("}");
                chunks.Add(enriched.ToString());
            }
        }
    }

    private List<string> ExtractVariablesFromBlock(string block)
    {
        var variables = new List<string>();
        var varPattern = @"(--[\w-]+)\s*:\s*([^;]+);";
        var matches = Regex.Matches(block, varPattern);

        foreach (Match match in matches)
        {
            variables.Add($"{match.Groups[1].Value}: {match.Groups[2].Value.Trim()};");
        }

        return variables;
    }

    /// <summary>
    /// Extracts @font-face rules
    /// </summary>
    private void ExtractFontFaces(string css, List<string> chunks)
    {
        var fontFacePattern = @"@font-face\s*\{([^}]+)\}";
        var matches = Regex.Matches(css, fontFacePattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var fontFace = match.Value.Trim();
            var fontFamily = ExtractPropertyValue(match.Groups[1].Value, "font-family");
            
            var enriched = $"// Font Face: {fontFamily}\n{fontFace}";
            chunks.Add(enriched);
        }
    }

    /// <summary>
    /// Extracts @keyframes animations
    /// </summary>
    private void ExtractKeyframes(string css, List<string> chunks)
    {
        var keyframesPattern = @"@(?:-webkit-)?keyframes\s+([\w-]+)\s*\{([^}]+(?:\{[^}]*\}[^}]*)*)\}";
        var matches = Regex.Matches(css, keyframesPattern, RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            var animationName = match.Groups[1].Value;
            var keyframesBody = match.Value.Trim();
            
            var enriched = $"// Animation: {animationName}\n{keyframesBody}";
            chunks.Add(enriched);
        }
    }

    /// <summary>
    /// Extracts @media queries with all their rules
    /// </summary>
    private void ExtractMediaQueries(string css, Dictionary<int, string> importantComments, List<string> chunks)
    {
        var mediaPattern = @"@media\s*([^{]+)\s*\{";
        var matches = Regex.Matches(css, mediaPattern);

        foreach (Match match in matches)
        {
            var mediaQuery = match.Groups[1].Value.Trim();
            var startIndex = match.Index;
            var mediaBlock = ExtractBlock(css, startIndex);
            
            if (!string.IsNullOrWhiteSpace(mediaBlock))
            {
                var comment = FindCommentBefore(importantComments, startIndex);
                var enriched = new StringBuilder();
                enriched.AppendLine($"// Media Query: {mediaQuery}");
                
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    enriched.AppendLine($"/* {comment.Trim()} */");
                }
                
                enriched.Append(mediaBlock.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Extracts regular CSS rules with selectors
    /// </summary>
    private void ExtractCssRules(string css, Dictionary<int, string> importantComments, List<string> chunks)
    {
        // Remove @rules and media queries to avoid duplicates
        var cleanCss = Regex.Replace(css, @"@(?:import|font-face|keyframes|media)[^{]*\{(?:[^{}]|\{[^}]*\})*\}", "", RegexOptions.Singleline);
        
        // Match selector { properties }
        var rulePattern = @"([^{@]+)\{([^}]+)\}";
        var matches = Regex.Matches(cleanCss, rulePattern);

        foreach (Match match in matches)
        {
            var selector = match.Groups[1].Value.Trim();
            var properties = match.Groups[2].Value.Trim();
            
            // Skip empty rules or :root (already extracted)
            if (string.IsNullOrWhiteSpace(selector) || 
                string.IsNullOrWhiteSpace(properties) || 
                selector == ":root")
                continue;
            
            var startIndex = match.Index;
            var comment = FindCommentBefore(importantComments, startIndex);
            
            var enriched = new StringBuilder();
            
            // Categorize selector type
            var selectorType = CategorizeSelector(selector);
            enriched.AppendLine($"// CSS Rule ({selectorType}): {selector}");
            
            if (!string.IsNullOrWhiteSpace(comment))
            {
                enriched.AppendLine($"/* {comment.Trim()} */");
            }
            
            enriched.AppendLine($"{selector} {{");
            
            // Format properties nicely
            var propertyList = properties.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var prop in propertyList)
            {
                var trimmedProp = prop.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedProp))
                {
                    enriched.AppendLine($"  {trimmedProp};");
                }
            }
            
            enriched.Append("}");
            chunks.Add(enriched.ToString());
        }
    }

    /// <summary>
    /// Categorizes CSS selectors for better semantic understanding
    /// </summary>
    private string CategorizeSelector(string selector)
    {
        if (selector.StartsWith("#")) return "ID Selector";
        if (selector.StartsWith(".")) return "Class Selector";
        if (selector.Contains(":hover") || selector.Contains(":focus") || 
            selector.Contains(":active") || selector.Contains(":visited"))
            return "Pseudo-class";
        if (selector.Contains("::before") || selector.Contains("::after") || 
            selector.Contains("::first-line") || selector.Contains("::first-letter"))
            return "Pseudo-element";
        if (selector.Contains("[") && selector.Contains("]")) return "Attribute Selector";
        if (selector.Contains(">")) return "Child Selector";
        if (selector.Contains("+")) return "Adjacent Sibling Selector";
        if (selector.Contains("~")) return "General Sibling Selector";
        if (Regex.IsMatch(selector, @"^[a-z]+$", RegexOptions.IgnoreCase)) return "Element Selector";
        return "Complex Selector";
    }

    /// <summary>
    /// Extracts property value from CSS block
    /// </summary>
    private string ExtractPropertyValue(string block, string propertyName)
    {
        var pattern = $@"{propertyName}\s*:\s*([^;]+)";
        var match = Regex.Match(block, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim().Trim('"', '\'') : "unknown";
    }

    /// <summary>
    /// Finds comment that appears before a given position
    /// </summary>
    private string FindCommentBefore(Dictionary<int, string> comments, int position)
    {
        var closestPosition = comments.Keys
            .Where(k => k < position)
            .OrderByDescending(k => k)
            .FirstOrDefault();
        
        return closestPosition > 0 && comments.ContainsKey(closestPosition)
            ? comments[closestPosition]
            : string.Empty;
    }

    /// <summary>
    /// Extracts a CSS block (handles nested braces in @media, @keyframes, etc.)
    /// </summary>
    private string ExtractBlock(string css, int startIndex)
    {
        int openBraceIndex = css.IndexOf('{', startIndex);
        if (openBraceIndex == -1)
            return string.Empty;

        int depth = 0;
        bool inString = false;
        char stringChar = '\0';
        
        for (int i = openBraceIndex; i < css.Length; i++)
        {
            char c = css[i];
            char prev = i > 0 ? css[i - 1] : '\0';

            // Handle escape sequences
            if (inString && prev == '\\')
                continue;

            // Track strings
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
                    return css.Substring(startIndex, i - startIndex + 1);
                }
            }
        }

        return string.Empty;
    }
}