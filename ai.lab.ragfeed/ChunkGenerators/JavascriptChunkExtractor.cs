using ai.lab.ragfeed.ChunkGenerators.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class JavascriptChunkExtractor : IFileChunkGenerator
{
    public List<string> GenerateChunks(string filepath) => ExtractJsChunks(filepath);

    public List<string> ExtractJsChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        
        // Remove comments first
        code = RemoveComments(code);
        
        var chunks = new List<string>();
        
        // Extract top-level functions, classes, and variable declarations
        ExtractFunctions(code, chunks);
        ExtractClasses(code, chunks);
        ExtractTopLevelDeclarations(code, chunks);
        ExtractArrowFunctions(code, chunks);
        
        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    }

    /// <summary>
    /// Removes JavaScript comments (both // and /* */ styles) while preserving strings
    /// </summary>
    private string RemoveComments(string code)
    {
        var result = new StringBuilder();
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inTemplate = false;
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            char next = i + 1 < code.Length ? code[i + 1] : '\0';
            char prev = i > 0 ? code[i - 1] : '\0';

            // Handle escape sequences
            if ((inSingleQuote || inDoubleQuote || inTemplate) && c == '\\' && next != '\0')
            {
                result.Append(c);
                result.Append(next);
                i++;
                continue;
            }

            // Handle string literals
            if (c == '\'' && !inDoubleQuote && !inTemplate && !inMultiLineComment && !inSingleLineComment)
            {
                inSingleQuote = !inSingleQuote;
                result.Append(c);
                continue;
            }

            if (c == '"' && !inSingleQuote && !inTemplate && !inMultiLineComment && !inSingleLineComment)
            {
                inDoubleQuote = !inDoubleQuote;
                result.Append(c);
                continue;
            }

            if (c == '`' && !inSingleQuote && !inDoubleQuote && !inMultiLineComment && !inSingleLineComment)
            {
                inTemplate = !inTemplate;
                result.Append(c);
                continue;
            }

            if (inSingleQuote || inDoubleQuote || inTemplate)
            {
                result.Append(c);
                continue;
            }

            // Handle single-line comments
            if (c == '/' && next == '/' && !inMultiLineComment)
            {
                inSingleLineComment = true;
                i++;
                continue;
            }

            if (inSingleLineComment)
            {
                if (c == '\n' || c == '\r')
                {
                    inSingleLineComment = false;
                    result.Append(c);
                }
                continue;
            }

            // Handle multi-line comments
            if (c == '/' && next == '*')
            {
                inMultiLineComment = true;
                i++;
                continue;
            }

            if (inMultiLineComment)
            {
                if (c == '*' && next == '/')
                {
                    inMultiLineComment = false;
                    i++;
                }
                continue;
            }

            result.Append(c);
        }

        return result.ToString();
    }

    /// <summary>
    /// Extracts traditional function declarations and expressions
    /// </summary>
    private void ExtractFunctions(string code, List<string> chunks)
    {
        // Match: function name(...) { ... }
        var functionPattern = @"(?:export\s+)?(?:async\s+)?function\s+(\w+)\s*\([^)]*\)\s*\{";
        var matches = Regex.Matches(code, functionPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var functionName = match.Groups[1].Value;
            var startIndex = match.Index;
            var functionBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(functionBody))
            {
                var enriched = $"// Function: {functionName}\n{functionBody.Trim()}";
                chunks.Add(enriched);
            }
        }

        // Match anonymous function expressions: const name = function(...) { ... }
        var funcExprPattern = @"(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?function\s*\([^)]*\)\s*\{";
        matches = Regex.Matches(code, funcExprPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var startIndex = match.Index;
            var functionBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(functionBody))
            {
                var enriched = $"// Function Expression: {varName}\n{functionBody.Trim()}";
                chunks.Add(enriched);
            }
        }
    }

    /// <summary>
    /// Extracts ES6 class declarations
    /// </summary>
    private void ExtractClasses(string code, List<string> chunks)
    {
        // Match: class ClassName { ... }
        var classPattern = @"(?:export\s+)?(?:default\s+)?class\s+(\w+)(?:\s+extends\s+\w+)?\s*\{";
        var matches = Regex.Matches(code, classPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            var startIndex = match.Index;
            var classBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(classBody))
            {
                var enriched = $"// Class: {className}\n{classBody.Trim()}";
                chunks.Add(enriched);
                
                // Also extract individual methods from the class
                ExtractClassMethods(classBody, className, chunks);
            }
        }
    }

    /// <summary>
    /// Extracts methods from within a class
    /// </summary>
    private void ExtractClassMethods(string classBody, string className, List<string> chunks)
    {
        // Match: methodName(...) { ... } or async methodName(...) { ... }
        var methodPattern = @"(?:async\s+)?(\w+)\s*\([^)]*\)\s*\{";
        var matches = Regex.Matches(classBody, methodPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var methodName = match.Groups[1].Value;
            
            // Skip constructor as it's part of class definition
            if (methodName == "constructor")
                continue;
                
            var startIndex = match.Index;
            var methodBody = ExtractBlock(classBody, startIndex);
            
            if (!string.IsNullOrWhiteSpace(methodBody))
            {
                var enriched = $"// Method: {className}.{methodName}\n{methodBody.Trim()}";
                chunks.Add(enriched);
            }
        }
    }

    /// <summary>
    /// Extracts arrow functions and const/let/var declarations with arrow functions
    /// </summary>
    private void ExtractArrowFunctions(string code, List<string> chunks)
    {
        // Match: const name = (...) => { ... }
        var arrowPattern = @"(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\([^)]*\)\s*=>\s*\{";
        var matches = Regex.Matches(code, arrowPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var startIndex = match.Index;
            var functionBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(functionBody))
            {
                var enriched = $"// Arrow Function: {varName}\n{functionBody.Trim()}";
                chunks.Add(enriched);
            }
        }

        // Match single-line arrow functions: const name = (...) => expression;
        var singleLineArrowPattern = @"(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\([^)]*\)\s*=>\s*([^;{]+);";
        matches = Regex.Matches(code, singleLineArrowPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var expression = match.Groups[2].Value.Trim();
            var enriched = $"// Arrow Function: {varName}\nconst {varName} = (...) => {expression};";
            chunks.Add(enriched);
        }
    }

    /// <summary>
    /// Extracts top-level variable declarations (const, let, var) that aren't functions
    /// </summary>
    private void ExtractTopLevelDeclarations(string code, List<string> chunks)
    {
        // Match const/let/var that are not arrow functions
        var declPattern = @"(?:export\s+)?(?:const|let|var)\s+(\w+)\s*=\s*(?![^=]*=>)([^;]+);";
        var matches = Regex.Matches(code, declPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();
            
            // Skip if it looks like a function
            if (value.Contains("function") || value.Contains("=>"))
                continue;
                
            var enriched = $"// Variable: {varName}\nconst {varName} = {value};";
            chunks.Add(enriched);
        }
    }

    /// <summary>
    /// Extracts a code block by matching braces, handling nested blocks
    /// </summary>
    private string ExtractBlock(string code, int startIndex)
    {
        // Find the opening brace
        int openBraceIndex = code.IndexOf('{', startIndex);
        if (openBraceIndex == -1)
            return string.Empty;

        int depth = 0;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inTemplate = false;
        
        for (int i = openBraceIndex; i < code.Length; i++)
        {
            char c = code[i];
            char prev = i > 0 ? code[i - 1] : '\0';

            // Handle escape sequences
            if ((inSingleQuote || inDoubleQuote || inTemplate) && prev == '\\')
                continue;

            // Track string literals
            if (c == '\'' && !inDoubleQuote && !inTemplate)
                inSingleQuote = !inSingleQuote;
            else if (c == '"' && !inSingleQuote && !inTemplate)
                inDoubleQuote = !inDoubleQuote;
            else if (c == '`' && !inSingleQuote && !inDoubleQuote)
                inTemplate = !inTemplate;

            if (inSingleQuote || inDoubleQuote || inTemplate)
                continue;

            // Track brace depth
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    // Extract from start of declaration to closing brace
                    return code.Substring(startIndex, i - startIndex + 1);
                }
            }
        }

        return string.Empty;
    }
}