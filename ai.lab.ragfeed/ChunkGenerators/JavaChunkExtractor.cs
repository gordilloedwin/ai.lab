using ai.lab.ragfeed.ChunkGenerators.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class JavaChunkExtractor : IFileChunkGenerator
{
    public List<string> GenerateChunks(string filepath) => ExtractJavaChunks(filepath);

    public List<string> ExtractJavaChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        
        // Remove comments first (we'll extract them separately)
        var codeWithoutComments = RemoveComments(code, out var javadocComments);
        
        var chunks = new List<string>();

        // Extract package declaration
        ExtractPackageDeclaration(code, chunks);
        
        // Extract import statements
        ExtractImports(code, chunks);
        
        // Extract classes with their javadoc
        ExtractClasses(code, javadocComments, chunks);
        
        // Extract interfaces
        ExtractInterfaces(code, javadocComments, chunks);
        
        // Extract enums
        ExtractEnums(code, javadocComments, chunks);
        
        // Extract annotations
        ExtractAnnotations(code, chunks);

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    }

    /// <summary>
    /// Removes comments and extracts Javadoc separately
    /// </summary>
    private string RemoveComments(string code, out Dictionary<int, string> javadocComments)
    {
        javadocComments = new Dictionary<int, string>();
        var result = new StringBuilder();
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool inSingleLineComment = false;
        bool inMultiLineComment = false;
        bool isJavadoc = false;
        var currentComment = new StringBuilder();
        int commentStartPosition = 0;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            char next = i + 1 < code.Length ? code[i + 1] : '\0';
            char prev = i > 0 ? code[i - 1] : '\0';

            // Handle escape sequences in strings
            if ((inSingleQuote || inDoubleQuote) && c == '\\' && next != '\0')
            {
                result.Append(c);
                result.Append(next);
                i++;
                continue;
            }

            // Handle string literals
            if (c == '\'' && !inDoubleQuote && !inMultiLineComment && !inSingleLineComment)
            {
                inSingleQuote = !inSingleQuote;
                result.Append(c);
                continue;
            }

            if (c == '"' && !inSingleQuote && !inMultiLineComment && !inSingleLineComment)
            {
                inDoubleQuote = !inDoubleQuote;
                result.Append(c);
                continue;
            }

            if (inSingleQuote || inDoubleQuote)
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

            // Handle multi-line comments and Javadoc
            if (c == '/' && next == '*')
            {
                inMultiLineComment = true;
                commentStartPosition = result.Length;
                
                // Check if it's Javadoc (/**)
                if (i + 2 < code.Length && code[i + 2] == '*')
                {
                    isJavadoc = true;
                }
                
                currentComment.Clear();
                i++;
                continue;
            }

            if (inMultiLineComment)
            {
                if (c == '*' && next == '/')
                {
                    if (isJavadoc)
                    {
                        javadocComments[commentStartPosition] = currentComment.ToString();
                    }
                    
                    inMultiLineComment = false;
                    isJavadoc = false;
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
    /// Extracts package declaration
    /// </summary>
    private void ExtractPackageDeclaration(string code, List<string> chunks)
    {
        var packagePattern = @"package\s+([\w\.]+)\s*;";
        var match = Regex.Match(code, packagePattern);
        if (match.Success)
        {
            var packageName = match.Groups[1].Value;
            chunks.Add($"// Package Declaration\npackage {packageName};");
        }
    }

    /// <summary>
    /// Extracts import statements grouped together
    /// </summary>
    private void ExtractImports(string code, List<string> chunks)
    {
        var importPattern = @"import\s+(?:static\s+)?([\w\.\*]+)\s*;";
        var matches = Regex.Matches(code, importPattern);
        
        if (matches.Count > 0)
        {
            var imports = new StringBuilder("// Import Statements\n");
            foreach (Match match in matches)
            {
                imports.AppendLine(match.Value.Trim());
            }
            chunks.Add(imports.ToString().Trim());
        }
    }

    /// <summary>
    /// Extracts class declarations with their content and Javadoc
    /// </summary>
    private void ExtractClasses(string code, Dictionary<int, string> javadocComments, List<string> chunks)
    {
        // Match: [modifiers] class ClassName [extends] [implements] {
        var classPattern = @"((?:@\w+(?:\([^\)]*\))?\s*)*)(?:public|protected|private)?\s*(?:static|final|abstract)?\s*class\s+(\w+)(?:\s+extends\s+[\w\.<>]+)?(?:\s+implements\s+[\w\s,.<>]+)?\s*\{";
        var matches = Regex.Matches(code, classPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var annotations = match.Groups[1].Value.Trim();
            var className = match.Groups[2].Value;
            var startIndex = match.Index;
            
            // Find associated Javadoc
            var javadoc = FindJavadocBefore(javadocComments, startIndex);
            
            // Extract full class body
            var classBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(classBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"// Class: {className}");
                
                if (!string.IsNullOrWhiteSpace(javadoc))
                {
                    enriched.AppendLine($"/**{javadoc}*/");
                }
                
                if (!string.IsNullOrWhiteSpace(annotations))
                {
                    enriched.AppendLine(annotations);
                }
                
                enriched.Append(classBody.Trim());
                chunks.Add(enriched.ToString());
                
                // Extract individual methods from the class
                ExtractMethodsFromClass(classBody, className, javadocComments, chunks);
            }
        }
    }

    /// <summary>
    /// Extracts interface declarations
    /// </summary>
    private void ExtractInterfaces(string code, Dictionary<int, string> javadocComments, List<string> chunks)
    {
        var interfacePattern = @"((?:@\w+(?:\([^\)]*\))?\s*)*)(?:public|protected|private)?\s*interface\s+(\w+)(?:\s+extends\s+[\w\s,.<>]+)?\s*\{";
        var matches = Regex.Matches(code, interfacePattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var annotations = match.Groups[1].Value.Trim();
            var interfaceName = match.Groups[2].Value;
            var startIndex = match.Index;
            var javadoc = FindJavadocBefore(javadocComments, startIndex);
            var interfaceBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(interfaceBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"// Interface: {interfaceName}");
                
                if (!string.IsNullOrWhiteSpace(javadoc))
                {
                    enriched.AppendLine($"/**{javadoc}*/");
                }
                
                if (!string.IsNullOrWhiteSpace(annotations))
                {
                    enriched.AppendLine(annotations);
                }
                
                enriched.Append(interfaceBody.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Extracts enum declarations
    /// </summary>
    private void ExtractEnums(string code, Dictionary<int, string> javadocComments, List<string> chunks)
    {
        var enumPattern = @"((?:@\w+(?:\([^\)]*\))?\s*)*)(?:public|protected|private)?\s*enum\s+(\w+)(?:\s+implements\s+[\w\s,.<>]+)?\s*\{";
        var matches = Regex.Matches(code, enumPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var annotations = match.Groups[1].Value.Trim();
            var enumName = match.Groups[2].Value;
            var startIndex = match.Index;
            var javadoc = FindJavadocBefore(javadocComments, startIndex);
            var enumBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(enumBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"// Enum: {enumName}");
                
                if (!string.IsNullOrWhiteSpace(javadoc))
                {
                    enriched.AppendLine($"/**{javadoc}*/");
                }
                
                if (!string.IsNullOrWhiteSpace(annotations))
                {
                    enriched.AppendLine(annotations);
                }
                
                enriched.Append(enumBody.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Extracts annotation declarations
    /// </summary>
    private void ExtractAnnotations(string code, List<string> chunks)
    {
        var annotationPattern = @"(?:public|protected|private)?\s*@interface\s+(\w+)\s*\{";
        var matches = Regex.Matches(code, annotationPattern);

        foreach (Match match in matches)
        {
            var annotationName = match.Groups[1].Value;
            var startIndex = match.Index;
            var annotationBody = ExtractBlock(code, startIndex);
            
            if (!string.IsNullOrWhiteSpace(annotationBody))
            {
                chunks.Add($"// Annotation: {annotationName}\n{annotationBody.Trim()}");
            }
        }
    }

    /// <summary>
    /// Extracts methods from within a class
    /// </summary>
    private void ExtractMethodsFromClass(string classBody, string className, Dictionary<int, string> javadocComments, List<string> chunks)
    {
        // Match: [modifiers] returnType methodName(params) [throws] {
        var methodPattern = @"((?:@\w+(?:\([^\)]*\))?\s*)*)(?:public|protected|private)?\s*(?:static|final|abstract|synchronized|native)?\s*(?:<[\w\s,<>]+>\s+)?(\w+(?:<[\w\s,<>]+>)?)\s+(\w+)\s*\([^\)]*\)(?:\s+throws\s+[\w\s,]+)?\s*\{";
        var matches = Regex.Matches(classBody, methodPattern, RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var annotations = match.Groups[1].Value.Trim();
            var returnType = match.Groups[2].Value;
            var methodName = match.Groups[3].Value;
            var startIndex = match.Index;
            
            // Skip constructors (same name as class)
            if (methodName == className)
                continue;
            
            var methodBody = ExtractBlock(classBody, startIndex);
            
            if (!string.IsNullOrWhiteSpace(methodBody))
            {
                var enriched = new StringBuilder();
                enriched.AppendLine($"// Method: {className}.{methodName}");
                enriched.AppendLine($"// Return Type: {returnType}");
                
                if (!string.IsNullOrWhiteSpace(annotations))
                {
                    enriched.AppendLine(annotations);
                }
                
                enriched.Append(methodBody.Trim());
                chunks.Add(enriched.ToString());
            }
        }
    }

    /// <summary>
    /// Finds Javadoc comment that appears before a given position
    /// </summary>
    private string FindJavadocBefore(Dictionary<int, string> javadocComments, int position)
    {
        var closestPosition = javadocComments.Keys
            .Where(k => k < position)
            .OrderByDescending(k => k)
            .FirstOrDefault();
        
        return closestPosition > 0 && javadocComments.ContainsKey(closestPosition)
            ? javadocComments[closestPosition]
            : string.Empty;
    }

    /// <summary>
    /// Extracts a code block by matching braces, handling nested blocks
    /// </summary>
    private string ExtractBlock(string code, int startIndex)
    {
        int openBraceIndex = code.IndexOf('{', startIndex);
        if (openBraceIndex == -1)
            return string.Empty;

        int depth = 0;
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        
        for (int i = openBraceIndex; i < code.Length; i++)
        {
            char c = code[i];
            char prev = i > 0 ? code[i - 1] : '\0';

            if ((inSingleQuote || inDoubleQuote) && prev == '\\')
                continue;

            if (c == '\'' && !inDoubleQuote)
                inSingleQuote = !inSingleQuote;
            else if (c == '"' && !inSingleQuote)
                inDoubleQuote = !inDoubleQuote;

            if (inSingleQuote || inDoubleQuote)
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
}
