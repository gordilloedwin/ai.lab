using ai.lab.ragfeed.ChunkGenerators.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

/// <summary>
/// Enriched C++ chunk extractor for RAG ingestion.
/// Produces semantic chunks for: preprocessor directives, namespaces, classes/structs/unions, 
/// functions (free & member), templates, enums, typedefs, access specifiers.
/// Parsing strategy: line-by-line scanner with brace depth tracking and state machine.
/// Metadata headers enrich each chunk for improved retrieval relevance.
/// NOTE: Heuristic parser (not full AST); handles common C++ patterns and nested structures.
/// </summary>
public class CppChunkExtractor : IFileChunkGenerator
{
    private static readonly Regex PreprocessorRegex = new(@"^\s*#\s*(\w+)\s*(.*)", RegexOptions.Compiled);
    private static readonly Regex NamespaceRegex = new(@"^\s*namespace\s+([A-Za-z_][\w:]*)\s*\{?", RegexOptions.Compiled);
    private static readonly Regex ClassRegex = new(@"^\s*(class|struct|union)\s+([A-Za-z_]\w*)(?:\s*:\s*(.+?))?\s*\{?", RegexOptions.Compiled);
    private static readonly Regex EnumRegex = new(@"^\s*enum\s+(class\s+)?([A-Za-z_]\w*)(?:\s*:\s*([A-Za-z_]\w*))?\s*\{?", RegexOptions.Compiled);
    private static readonly Regex FunctionRegex = new(@"^\s*(?:(?:virtual|static|inline|explicit|constexpr|extern)\s+)*(?:template\s*<[^>]*>\s*)?([A-Za-z_][\w:]*(?:\s*<[^>]*>)?(?:\s*\*|\s*&)?)\s+([A-Za-z_~][\w:]*)\s*\(([^)]*)\)\s*(const)?\s*(noexcept|override|final|=\s*0|=\s*delete|=\s*default)?", RegexOptions.Compiled);
    private static readonly Regex TemplateRegex = new(@"^\s*template\s*<(.+)>", RegexOptions.Compiled);
    private static readonly Regex TypedefRegex = new(@"^\s*typedef\s+(.+?)\s+([A-Za-z_]\w*)\s*;", RegexOptions.Compiled);
    private static readonly Regex UsingRegex = new(@"^\s*using\s+([A-Za-z_]\w*)\s*=\s*(.+?)\s*;", RegexOptions.Compiled);
    private static readonly Regex AccessRegex = new(@"^\s*(public|protected|private)\s*:", RegexOptions.Compiled);
    private static readonly Regex SingleLineCommentRegex = new(@"//.*$", RegexOptions.Compiled);
    private static readonly Regex MultiLineCommentStart = new(@"/\*", RegexOptions.Compiled);
    private static readonly Regex MultiLineCommentEnd = new(@"\*/", RegexOptions.Compiled);

    private class BlockInfo
    {
        public string Type { get; init; } = string.Empty; // Namespace, Class, Struct, Union, Enum, Function, Template
        public string Name { get; init; } = string.Empty;
        public int StartLine { get; init; }
        public StringBuilder Builder { get; } = new();
        public string? BaseClasses { get; set; }
        public string? ReturnType { get; set; }
        public string? Parameters { get; set; }
        public string? Modifiers { get; set; }
        public string? TemplateParams { get; set; }
        public string? AccessLevel { get; set; }
        public List<string> PreprocessorDirectives { get; } = new();
        public List<string> Typedefs { get; } = new();
        public int BraceDepth { get; set; }
    }

    public List<string> GenerateChunks(string filepath) => ExtractCppChunks(filepath);

    public List<string> ExtractCppChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        return ExtractFromString(code, filePath);
    }

    public List<string> ExtractFromString(string code, string? filePath = null)
    {
        var chunks = new List<string>();
        var lines = code.Replace("\r\n", "\n").Split('\n');

        var stack = new Stack<BlockInfo>();
        var topLevelIncludes = new List<string>();
        var topLevelDefines = new List<string>();
        var topLevelTypedefs = new List<string>();
        bool inMultiLineComment = false;
        string? pendingTemplateParams = null;
        string currentAccessLevel = "public"; // struct/union default; class defaults to private

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            string line = rawLine.TrimEnd();

            // Handle multi-line comments
            if (inMultiLineComment)
            {
                if (MultiLineCommentEnd.IsMatch(line))
                {
                    inMultiLineComment = false;
                }
                AppendLineToCurrent(stack, rawLine);
                continue;
            }
            if (MultiLineCommentStart.IsMatch(line))
            {
                if (!MultiLineCommentEnd.IsMatch(line))
                {
                    inMultiLineComment = true;
                }
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Remove single-line comments for analysis (keep raw for building)
            string cleanLine = SingleLineCommentRegex.Replace(line, "").Trim();
            if (string.IsNullOrWhiteSpace(cleanLine))
            {
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Preprocessor directives
            var prepMatch = PreprocessorRegex.Match(cleanLine);
            if (prepMatch.Success)
            {
                string directive = prepMatch.Groups[1].Value;
                string content = prepMatch.Groups[2].Value.Trim();

                if (stack.Count > 0)
                {
                    stack.Peek().PreprocessorDirectives.Add($"{directive} {content}");
                }
                else
                {
                    if (directive == "include")
                    {
                        topLevelIncludes.Add(content);
                    }
                    else if (directive == "define")
                    {
                        topLevelDefines.Add(content);
                    }
                }
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Template declarations
            var templateMatch = TemplateRegex.Match(cleanLine);
            if (templateMatch.Success)
            {
                pendingTemplateParams = templateMatch.Groups[1].Value;
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Typedef
            var typedefMatch = TypedefRegex.Match(cleanLine);
            if (typedefMatch.Success)
            {
                string typedefDecl = $"{typedefMatch.Groups[1].Value} -> {typedefMatch.Groups[2].Value}";
                if (stack.Count > 0)
                {
                    stack.Peek().Typedefs.Add(typedefDecl);
                }
                else
                {
                    topLevelTypedefs.Add(typedefDecl);
                }
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Using alias
            var usingMatch = UsingRegex.Match(cleanLine);
            if (usingMatch.Success)
            {
                string usingDecl = $"{usingMatch.Groups[1].Value} = {usingMatch.Groups[2].Value}";
                if (stack.Count > 0)
                {
                    stack.Peek().Typedefs.Add(usingDecl);
                }
                else
                {
                    topLevelTypedefs.Add(usingDecl);
                }
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Access specifiers (public/protected/private)
            var accessMatch = AccessRegex.Match(cleanLine);
            if (accessMatch.Success)
            {
                currentAccessLevel = accessMatch.Groups[1].Value;
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Try to start blocks: namespace, class/struct/union, enum, function
            if (TryStartBlock(cleanLine, i, stack, ref currentAccessLevel, ref pendingTemplateParams))
            {
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Track brace depth for all blocks
            AppendLineToCurrent(stack, rawLine);
            UpdateBraceDepth(stack, cleanLine);

            // Close blocks when brace depth returns to zero
            if (stack.Count > 0 && stack.Peek().BraceDepth == 0)
            {
                var finished = stack.Pop();
                var endLine = i + 1;
                var chunk = BuildChunk(finished, endLine, filePath);
                if (!string.IsNullOrEmpty(chunk))
                {
                    chunks.Add(chunk);
                }
                // Reset access level when leaving class/struct scope
                if (finished.Type == "Class" || finished.Type == "Struct" || finished.Type == "Union")
                {
                    currentAccessLevel = stack.Count > 0 && (stack.Peek().Type == "Struct" || stack.Peek().Type == "Union") ? "public" : "public";
                }
            }
        }

        // Finalize incomplete blocks
        while (stack.Count > 0)
        {
            var unfinished = stack.Pop();
            var chunk = BuildChunk(unfinished, lines.Length, filePath, incomplete: true);
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }
        }

        // Top-level metadata chunks
        if (topLevelIncludes.Count > 0)
        {
            chunks.Add(BuildSimpleMetaChunk("INCLUDES", string.Join("\n", topLevelIncludes), filePath));
        }
        if (topLevelDefines.Count > 0)
        {
            chunks.Add(BuildSimpleMetaChunk("DEFINES", string.Join("\n", topLevelDefines), filePath));
        }
        if (topLevelTypedefs.Count > 0)
        {
            chunks.Add(BuildSimpleMetaChunk("TYPEDEFS", string.Join("\n", topLevelTypedefs), filePath));
        }

        return chunks;
    }

    private static void AppendLineToCurrent(Stack<BlockInfo> stack, string line)
    {
        if (stack.Count > 0)
        {
            stack.Peek().Builder.AppendLine(line);
        }
    }

    private static bool TryStartBlock(string cleanLine, int lineIndex, Stack<BlockInfo> stack, ref string currentAccessLevel, ref string? pendingTemplateParams)
    {
        // Namespace
        var nsMatch = NamespaceRegex.Match(cleanLine);
        if (nsMatch.Success)
        {
            var blk = new BlockInfo
            {
                Type = "Namespace",
                Name = nsMatch.Groups[1].Value,
                StartLine = lineIndex + 1,
                BraceDepth = cleanLine.Contains('{') ? 1 : 0
            };
            stack.Push(blk);
            return true;
        }

        // Class/Struct/Union
        var classMatch = ClassRegex.Match(cleanLine);
        if (classMatch.Success)
        {
            string classType = classMatch.Groups[1].Value; // class, struct, union
            string className = classMatch.Groups[2].Value;
            string? baseClasses = classMatch.Groups[3].Success ? classMatch.Groups[3].Value.Trim() : null;

            var blk = new BlockInfo
            {
                Type = char.ToUpper(classType[0]) + classType.Substring(1), // Capitalize
                Name = className,
                BaseClasses = baseClasses,
                StartLine = lineIndex + 1,
                BraceDepth = cleanLine.Contains('{') ? 1 : 0,
                AccessLevel = classType == "class" ? "private" : "public", // default access
                TemplateParams = pendingTemplateParams
            };
            stack.Push(blk);
            currentAccessLevel = blk.AccessLevel!;
            pendingTemplateParams = null;
            return true;
        }

        // Enum
        var enumMatch = EnumRegex.Match(cleanLine);
        if (enumMatch.Success)
        {
            string enumName = enumMatch.Groups[2].Value;
            string? baseType = enumMatch.Groups[3].Success ? enumMatch.Groups[3].Value : null;
            bool isEnumClass = enumMatch.Groups[1].Success;

            var blk = new BlockInfo
            {
                Type = isEnumClass ? "EnumClass" : "Enum",
                Name = enumName,
                Modifiers = baseType != null ? $"base: {baseType}" : null,
                StartLine = lineIndex + 1,
                BraceDepth = cleanLine.Contains('{') ? 1 : 0
            };
            stack.Push(blk);
            return true;
        }

        // Function (free or member)
        var funcMatch = FunctionRegex.Match(cleanLine);
        if (funcMatch.Success && !cleanLine.TrimEnd().EndsWith(';')) // avoid prototypes
        {
            string returnType = funcMatch.Groups[1].Value;
            string funcName = funcMatch.Groups[2].Value;
            string parameters = funcMatch.Groups[3].Value;
            string? constModifier = funcMatch.Groups[4].Success ? "const" : null;
            string? otherModifiers = funcMatch.Groups[5].Success ? funcMatch.Groups[5].Value.Trim() : null;

            var modifiers = new List<string>();
            if (constModifier != null) modifiers.Add(constModifier);
            if (otherModifiers != null) modifiers.Add(otherModifiers);

            var blk = new BlockInfo
            {
                Type = "Function",
                Name = funcName,
                ReturnType = returnType,
                Parameters = parameters,
                Modifiers = modifiers.Count > 0 ? string.Join(", ", modifiers) : null,
                StartLine = lineIndex + 1,
                BraceDepth = cleanLine.Contains('{') ? 1 : 0,
                AccessLevel = currentAccessLevel,
                TemplateParams = pendingTemplateParams
            };
            stack.Push(blk);
            pendingTemplateParams = null;
            return true;
        }

        return false;
    }

    private static void UpdateBraceDepth(Stack<BlockInfo> stack, string cleanLine)
    {
        if (stack.Count == 0) return;

        var current = stack.Peek();
        foreach (char c in cleanLine)
        {
            if (c == '{') current.BraceDepth++;
            if (c == '}') current.BraceDepth--;
        }
    }

    private static string BuildChunk(BlockInfo blk, int endLine, string? filePath, bool incomplete = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### C++ {blk.Type.ToUpper()} {blk.Name} (lines {blk.StartLine}-{endLine}{(incomplete ? ", incomplete" : string.Empty)})");
        if (!string.IsNullOrEmpty(filePath)) sb.AppendLine($"File: {filePath}");
        if (!string.IsNullOrEmpty(blk.TemplateParams)) sb.AppendLine($"Template: <{blk.TemplateParams}>");
        if (!string.IsNullOrEmpty(blk.BaseClasses)) sb.AppendLine($"BaseClasses: {blk.BaseClasses}");
        if (!string.IsNullOrEmpty(blk.ReturnType)) sb.AppendLine($"ReturnType: {blk.ReturnType}");
        if (!string.IsNullOrEmpty(blk.Parameters)) sb.AppendLine($"Parameters: ({blk.Parameters})");
        if (!string.IsNullOrEmpty(blk.Modifiers)) sb.AppendLine($"Modifiers: {blk.Modifiers}");
        if (!string.IsNullOrEmpty(blk.AccessLevel)) sb.AppendLine($"AccessLevel: {blk.AccessLevel}");
        if (blk.PreprocessorDirectives.Count > 0) sb.AppendLine($"Preprocessor: {string.Join("; ", blk.PreprocessorDirectives)}");
        if (blk.Typedefs.Count > 0) sb.AppendLine($"Typedefs: {string.Join("; ", blk.Typedefs)}");
        sb.AppendLine("---");
        sb.Append(blk.Builder.ToString().TrimEnd());
        return sb.ToString();
    }

    private static string BuildSimpleMetaChunk(string label, string body, string? filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### C++ {label}");
        if (!string.IsNullOrEmpty(filePath)) sb.AppendLine($"File: {filePath}");
        sb.AppendLine("---");
        sb.Append(body.Trim());
        return sb.ToString();
    }
}