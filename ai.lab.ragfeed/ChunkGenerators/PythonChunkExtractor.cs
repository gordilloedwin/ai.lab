using ai.lab.ragfeed.ChunkGenerators.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

public class PythonChunkExtractor : IFileChunkGenerator 
{
    public string Filetype => "code python";

    public List<string> GenerateChunks(string filepath) => ExtractPythonChunks(filepath);

    public List<string> ExtractPythonChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var chunks = new List<string>();

        // Extract module docstring
        ExtractModuleDocstring(code, chunks);
        
        // Extract imports
        ExtractImports(code, chunks);
        
        // Extract classes with their methods
        ExtractClasses(code, chunks);
        
        // Extract standalone functions
        ExtractFunctions(code, chunks);
        
        // Extract decorators and their targets
        ExtractDecorators(code, chunks);
        
        // Extract global constants and configuration
        ExtractGlobalVariables(code, chunks);

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
    }

    /// <summary>
    /// Extracts module-level docstring
    /// </summary>
    private void ExtractModuleDocstring(string code, List<string> chunks)
    {
        var lines = code.Split('\n');
        var docstringPattern = @"^(""""""|''')";
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            
            // Skip shebang and encoding declarations
            if (line.StartsWith("#"))
                continue;
            
            // Found module docstring
            if (Regex.IsMatch(line, docstringPattern))
            {
                var docstring = ExtractDocstring(lines, i);
                if (!string.IsNullOrWhiteSpace(docstring))
                {
                    chunks.Add($"# Module Documentation\n{docstring}");
                }
                break;
            }
            
            // If we hit code before docstring, no module docstring exists
            if (!string.IsNullOrWhiteSpace(line))
                break;
        }
    }

    /// <summary>
    /// Extracts import statements grouped together
    /// </summary>
    private void ExtractImports(string code, List<string> chunks)
    {
        var lines = code.Split('\n');
        var imports = new StringBuilder();
        bool hasImports = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("import ") || trimmed.StartsWith("from "))
            {
                hasImports = true;
                imports.AppendLine(line);
            }
            else if (hasImports && string.IsNullOrWhiteSpace(trimmed))
            {
                // Continue accumulating if blank line between imports
                continue;
            }
            else if (hasImports && !trimmed.StartsWith("#"))
            {
                // Hit non-import code, stop
                break;
            }
        }

        if (hasImports)
        {
            chunks.Add($"# Import Statements\n{imports.ToString().Trim()}");
        }
    }

    /// <summary>
    /// Extracts class definitions with their methods
    /// </summary>
    private void ExtractClasses(string code, List<string> chunks)
    {
        var lines = code.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("class "))
            {
                var match = Regex.Match(trimmed, @"class\s+(\w+)(?:\(([^)]+)\))?:");
                if (match.Success)
                {
                    var className = match.Groups[1].Value;
                    var baseClasses = match.Groups[2].Success ? match.Groups[2].Value : null;
                    var indentLevel = GetIndentLevel(line);
                    
                    // Check for decorators before class
                    var decorators = ExtractDecoratorsBeforeLine(lines, i);
                    
                    // Extract class docstring
                    var docstring = "";
                    if (i + 1 < lines.Length)
                    {
                        docstring = ExtractDocstring(lines, i + 1);
                    }
                    
                    // Extract full class body
                    var classBody = ExtractIndentedBlock(lines, i, indentLevel);
                    
                    var enriched = new StringBuilder();
                    enriched.AppendLine($"# Class: {className}");
                    if (!string.IsNullOrWhiteSpace(baseClasses))
                    {
                        enriched.AppendLine($"# Inherits: {baseClasses}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(decorators))
                    {
                        enriched.AppendLine(decorators);
                    }
                    
                    enriched.AppendLine(line);
                    
                    if (!string.IsNullOrWhiteSpace(docstring))
                    {
                        enriched.AppendLine(docstring);
                    }
                    
                    enriched.Append(classBody);
                    chunks.Add(enriched.ToString().Trim());
                    
                    // Extract individual methods from class
                    ExtractMethodsFromClass(lines, i, indentLevel, className, chunks);
                }
            }
        }
    }

    /// <summary>
    /// Extracts methods from within a class
    /// </summary>
    private void ExtractMethodsFromClass(string[] lines, int classStartLine, int classIndent, string className, List<string> chunks)
    {
        for (int i = classStartLine + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var indent = GetIndentLevel(line);
            var trimmed = line.Trim();
            
            // Outside class body
            if (indent <= classIndent && !string.IsNullOrWhiteSpace(trimmed))
                break;
            
            // Found method definition
            if (trimmed.StartsWith("def ") && indent > classIndent)
            {
                var match = Regex.Match(trimmed, @"def\s+(\w+)\s*\(([^)]*)\)");
                if (match.Success)
                {
                    var methodName = match.Groups[1].Value;
                    var parameters = match.Groups[2].Value;
                    
                    // Determine method type
                    var methodType = "Method";
                    if (methodName == "__init__") methodType = "Constructor";
                    else if (methodName.StartsWith("__") && methodName.EndsWith("__")) methodType = "Magic Method";
                    else if (parameters.StartsWith("cls")) methodType = "Class Method";
                    else if (!parameters.StartsWith("self")) methodType = "Static Method";
                    
                    var decorators = ExtractDecoratorsBeforeLine(lines, i);
                    var docstring = ExtractDocstring(lines, i + 1);
                    var methodBody = ExtractIndentedBlock(lines, i, indent);
                    
                    var enriched = new StringBuilder();
                    enriched.AppendLine($"# {methodType}: {className}.{methodName}");
                    enriched.AppendLine($"# Parameters: ({parameters})");
                    
                    if (!string.IsNullOrWhiteSpace(decorators))
                    {
                        enriched.AppendLine(decorators);
                    }
                    
                    enriched.AppendLine(line);
                    
                    if (!string.IsNullOrWhiteSpace(docstring))
                    {
                        enriched.AppendLine(docstring);
                    }
                    
                    enriched.Append(methodBody);
                    chunks.Add(enriched.ToString().Trim());
                }
            }
        }
    }

    /// <summary>
    /// Extracts standalone functions (not in classes)
    /// </summary>
    private void ExtractFunctions(string code, List<string> chunks)
    {
        var lines = code.Split('\n');
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            var indent = GetIndentLevel(line);
            
            // Only top-level functions (indent 0)
            if (trimmed.StartsWith("def ") && indent == 0)
            {
                var match = Regex.Match(trimmed, @"def\s+(\w+)\s*\(([^)]*)\)(?:\s*->\s*(.+?))?:");
                if (match.Success)
                {
                    var functionName = match.Groups[1].Value;
                    var parameters = match.Groups[2].Value;
                    var returnType = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;
                    
                    var decorators = ExtractDecoratorsBeforeLine(lines, i);
                    var docstring = ExtractDocstring(lines, i + 1);
                    var functionBody = ExtractIndentedBlock(lines, i, indent);
                    
                    var enriched = new StringBuilder();
                    enriched.AppendLine($"# Function: {functionName}");
                    enriched.AppendLine($"# Parameters: ({parameters})");
                    if (!string.IsNullOrWhiteSpace(returnType))
                    {
                        enriched.AppendLine($"# Return Type: {returnType}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(decorators))
                    {
                        enriched.AppendLine(decorators);
                    }
                    
                    enriched.AppendLine(line);
                    
                    if (!string.IsNullOrWhiteSpace(docstring))
                    {
                        enriched.AppendLine(docstring);
                    }
                    
                    enriched.Append(functionBody);
                    chunks.Add(enriched.ToString().Trim());
                }
            }
        }
    }

    /// <summary>
    /// Extracts decorators as separate chunks
    /// </summary>
    private void ExtractDecorators(string code, List<string> chunks)
    {
        var decoratorPattern = @"@(\w+)(?:\(([^)]*)\))?";
        var matches = Regex.Matches(code, decoratorPattern);
        var decoratorUsage = new Dictionary<string, int>();

        foreach (Match match in matches)
        {
            var decoratorName = match.Groups[1].Value;
            if (!decoratorUsage.ContainsKey(decoratorName))
            {
                decoratorUsage[decoratorName] = 0;
            }
            decoratorUsage[decoratorName]++;
        }

        if (decoratorUsage.Any())
        {
            var summary = new StringBuilder();
            summary.AppendLine("# Decorator Usage Summary");
            foreach (var kvp in decoratorUsage.OrderByDescending(x => x.Value))
            {
                summary.AppendLine($"# @{kvp.Key}: {kvp.Value} usage(s)");
            }
            chunks.Add(summary.ToString().Trim());
        }
    }

    /// <summary>
    /// Extracts global constants and configuration variables
    /// </summary>
    private void ExtractGlobalVariables(string code, List<string> chunks)
    {
        var lines = code.Split('\n');
        var constants = new StringBuilder();
        bool hasConstants = false;
        bool inImports = true;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var indent = GetIndentLevel(line);
            
            // Skip imports section
            if (trimmed.StartsWith("import ") || trimmed.StartsWith("from "))
                continue;
            
            if (inImports && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("#"))
                inImports = false;
            
            if (inImports)
                continue;
            
            // Top-level variable assignments (constants/config)
            if (indent == 0 && Regex.IsMatch(trimmed, @"^[A-Z_][A-Z0-9_]*\s*="))
            {
                hasConstants = true;
                constants.AppendLine(line);
            }
            
            // Stop at first function/class definition
            if (indent == 0 && (trimmed.StartsWith("def ") || trimmed.StartsWith("class ")))
                break;
        }

        if (hasConstants)
        {
            chunks.Add($"# Global Constants\n{constants.ToString().Trim()}");
        }
    }

    /// <summary>
    /// Extracts docstring starting at a specific line
    /// </summary>
    private string ExtractDocstring(string[] lines, int startLine)
    {
        if (startLine >= lines.Length)
            return string.Empty;
        
        var line = lines[startLine].Trim();
        
        // Check for docstring
        if (!line.StartsWith("\"\"\"") && !line.StartsWith("'''"))
            return string.Empty;
        
        var delimiter = line.StartsWith("\"\"\"") ? "\"\"\"" : "'''";
        var docstring = new StringBuilder();
        
        // Single-line docstring
        if (line.IndexOf(delimiter, delimiter.Length) != -1)
        {
            return lines[startLine];
        }
        
        // Multi-line docstring
        docstring.AppendLine(lines[startLine]);
        for (int i = startLine + 1; i < lines.Length; i++)
        {
            docstring.AppendLine(lines[i]);
            if (lines[i].Trim().Contains(delimiter))
            {
                break;
            }
        }
        
        return docstring.ToString().TrimEnd();
    }

    /// <summary>
    /// Extracts decorators before a line
    /// </summary>
    private string ExtractDecoratorsBeforeLine(string[] lines, int lineIndex)
    {
        var decorators = new StringBuilder();
        
        for (int i = lineIndex - 1; i >= 0; i--)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("@"))
            {
                decorators.Insert(0, line + "\n");
            }
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }
        }
        
        return decorators.ToString().TrimEnd();
    }

    /// <summary>
    /// Extracts an indented block of code
    /// </summary>
    private string ExtractIndentedBlock(string[] lines, int startLine, int baseIndent)
    {
        var block = new StringBuilder();
        
        for (int i = startLine + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            var indent = GetIndentLevel(line);
            var trimmed = line.Trim();
            
            // Empty lines are part of the block
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                block.AppendLine(line);
                continue;
            }
            
            // Dedent means end of block
            if (indent <= baseIndent)
            {
                break;
            }
            
            block.AppendLine(line);
        }
        
        return block.ToString().TrimEnd();
    }

    /// <summary>
    /// Gets indentation level (number of leading spaces, tabs count as 4)
    /// </summary>
    private int GetIndentLevel(string line)
    {
        int spaces = 0;
        foreach (char c in line)
        {
            if (c == ' ')
                spaces++;
            else if (c == '\t')
                spaces += 4;
            else
                break;
        }
        return spaces;
    }
}
