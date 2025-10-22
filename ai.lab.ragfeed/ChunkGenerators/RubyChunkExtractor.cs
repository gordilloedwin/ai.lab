using ai.lab.ragfeed.ChunkGenerators.Common;
using System.Text;
using System.Text.RegularExpressions;

namespace ai.lab.ragfeed.ChunkGenerators;

/// <summary>
/// Enriched Ruby chunk extractor intended for RAG ingestion.
/// Produces semantic chunks for: classes, modules, singleton classes, methods (instance & singleton),
/// DSL blocks (RSpec describe/context/it etc.), attribute declarations, constants, require statements.
/// Adds metadata headers to each chunk to improve retrieval ranking.
/// Parsing strategy: single-pass line scanner with a block stack tracking starts and matching 'end'.
/// NOTE: This is a heuristic parser (not an AST) but resilient to nested structures & common Ruby idioms.
/// </summary>
public class RubyChunkExtractor : IFileChunkGenerator
{
    private static readonly Regex ClassRegex = new(@"^\s*class\s+([A-Z]\w*(?:::\w+)*)(?:\s*<\s*([A-Z]\w*(?:::\w+)*))?", RegexOptions.Compiled);
    private static readonly Regex ModuleRegex = new(@"^\s*module\s+([A-Z]\w*(?:::\w+)*)", RegexOptions.Compiled);
    private static readonly Regex SingletonClassRegex = new(@"^\s*class\s+<<\s*self\b", RegexOptions.Compiled);
    private static readonly Regex MethodRegex = new(@"^\s*def\s+((?:self\.|[A-Z]\w*(?:::\w+)*\.)?[A-Za-z_][A-Za-z0-9_]*[!?=]?|\[[^\]]+\]|\+|\-|\*|\/|%|==|!=|<=|>=|<|>|=~|\[]=|\[])\s*(?:\(([^)]*)\))?", RegexOptions.Compiled);
    private static readonly Regex VisibilityRegex = new(@"^\s*(public|private|protected)\b", RegexOptions.Compiled);
    private static readonly Regex IncludeExtendRegex = new(@"^\s*(include|extend)\s+([A-Z]\w*(?:::\w+)*)", RegexOptions.Compiled);
    private static readonly Regex AttrRegex = new(@"^\s*attr_(?:accessor|reader|writer)\s+(.+)$", RegexOptions.Compiled);
    private static readonly Regex ConstantAssignRegex = new(@"^\s*([A-Z][A-Z0-9_]*)\s*=\s*.+", RegexOptions.Compiled);
    // Matches: require 'foo/bar' OR require_relative "../baz" capturing the path
    private static readonly Regex RequireRegex = new("^\\s*require(?:_relative)?\\s+['\\\"]([^'\\\"]+)['\\\"]", RegexOptions.Compiled);
    private static readonly Regex DslBlockRegex = new(@"^\s*(describe|context|it|before|after|let|feature|scenario|shared_examples|shared_context)\b.*\bdo\b", RegexOptions.Compiled);
    private static readonly Regex BeginBlockRegex = new(@"^\s*begin\b", RegexOptions.Compiled);
    private static readonly Regex MultiLineCommentStart = new(@"^\s*=begin\b", RegexOptions.Compiled);
    private static readonly Regex MultiLineCommentEnd = new(@"^\s*=end\b", RegexOptions.Compiled);
    private static readonly Regex EndRegex = new(@"^\s*end\b", RegexOptions.Compiled);

    private class BlockInfo
    {
        public string Type { get; init; } = string.Empty; // Class, Module, Method, SingletonClass, DSL, Begin
        public string Name { get; init; } = string.Empty; // For DSL maybe the keyword or first argument
        public int StartLine { get; init; }
        public StringBuilder Builder { get; } = new();
        public string? SuperClass { get; set; }
        public List<string> Mixins { get; } = new();
        public List<string> Attributes { get; } = new();
        public List<string> Constants { get; } = new();
        public List<string> Requires { get; } = new();
        public string VisibilityAtStart { get; set; } = "public"; // Ruby default
        public string? ParamsSignature { get; set; }
    }

    public string Filetype => "code ruby";

    public List<string> GenerateChunks(string filepath) => ExtractRubyChunks(filepath);

    /// <summary>
    /// Extract enriched Ruby chunks from a file path.
    /// </summary>
    public List<string> ExtractRubyChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        return ExtractFromString(code, filePath);
    }

    /// <summary>
    /// Core extraction logic operating on an in-memory string.
    /// </summary>
    public List<string> ExtractFromString(string code, string? filePath = null)
    {
        var chunks = new List<string>();
        var lines = code.Replace("\r\n", "\n").Split('\n');

        var stack = new Stack<BlockInfo>();
        var topLevelRequires = new List<string>();
        var topLevelConstants = new List<string>();
        var topLevelAttrs = new List<string>();
        var topLevelMiscBuffer = new StringBuilder();
        string currentVisibility = "public"; // visibility resets at class/module scope boundaries
        bool inMultiLineComment = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string rawLine = lines[i];
            string line = rawLine; // Keep raw for building

            if (inMultiLineComment)
            {
                if (MultiLineCommentEnd.IsMatch(line))
                {
                    inMultiLineComment = false;
                }
                // Optionally could capture comment blocks; skip for now
                continue;
            }
            if (MultiLineCommentStart.IsMatch(line))
            {
                inMultiLineComment = true;
                continue;
            }

            // Try start patterns first
            if (TryStartBlock(line, i, stack, ref currentVisibility))
            {
                AppendLineToCurrent(stack, rawLine);
                continue;
            }

            // Inside a block: capture mixins, attrs, constants, requires, visibility changes
            if (stack.Count > 0)
            {
                var current = stack.Peek();
                ParseLineMetadata(line, current, ref currentVisibility);
            }
            else
            {
                // top-level metadata
                ParseTopLevelMetadata(line, topLevelRequires, topLevelConstants, topLevelAttrs);
            }

            // Append line to current block builder if any
            AppendLineToCurrent(stack, rawLine);

            // End block detection
            if (EndRegex.IsMatch(line))
            {
                if (stack.Count > 0)
                {
                    var finished = stack.Pop();
                    var endLine = i + 1;
                    var chunk = BuildChunk(finished, endLine, filePath);
                    if (!string.IsNullOrEmpty(chunk))
                    {
                        chunks.Add(chunk);
                    }
                    // Reset visibility after leaving method scope to previous block's visibility
                    currentVisibility = stack.Count > 0 ? stack.Peek().VisibilityAtStart : "public";
                }
                else
                {
                    // stray end - ignore
                }
            }
        }

        // Incomplete blocks (missing end) - finalize them anyway
        while (stack.Count > 0)
        {
            var unfinished = stack.Pop();
            var chunk = BuildChunk(unfinished, lines.Length, filePath, incomplete: true);
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(chunk);
            }
        }

        // Produce top-level metadata chunks (requires, constants, attrs, misc)
        if (topLevelRequires.Count > 0)
        {
            chunks.Add(BuildSimpleMetaChunk("REQUIRES", string.Join("\n", topLevelRequires), filePath));
        }
        if (topLevelConstants.Count > 0)
        {
            chunks.Add(BuildSimpleMetaChunk("CONSTANTS", string.Join("\n", topLevelConstants), filePath));
        }
        if (topLevelAttrs.Count > 0)
        {
            chunks.Add(BuildSimpleMetaChunk("ATTRIBUTES", string.Join("\n", topLevelAttrs), filePath));
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

    private static bool TryStartBlock(string line, int lineIndex, Stack<BlockInfo> stack, ref string currentVisibility)
    {
        // Class
        var mClass = ClassRegex.Match(line);
        if (mClass.Success)
        {
            var blk = new BlockInfo
            {
                Type = "Class",
                Name = mClass.Groups[1].Value,
                SuperClass = mClass.Groups[2].Success ? mClass.Groups[2].Value : null,
                StartLine = lineIndex + 1,
                VisibilityAtStart = "public"
            };
            stack.Push(blk);
            currentVisibility = "public"; // reset at new scope
            return true;
        }
        // Module
        var mModule = ModuleRegex.Match(line);
        if (mModule.Success)
        {
            var blk = new BlockInfo
            {
                Type = "Module",
                Name = mModule.Groups[1].Value,
                StartLine = lineIndex + 1,
                VisibilityAtStart = "public"
            };
            stack.Push(blk);
            currentVisibility = "public";
            return true;
        }
        // Singleton class (class << self)
        if (SingletonClassRegex.IsMatch(line))
        {
            var blk = new BlockInfo
            {
                Type = "SingletonClass",
                Name = "self",
                StartLine = lineIndex + 1,
                VisibilityAtStart = "public"
            };
            stack.Push(blk);
            currentVisibility = "public";
            return true;
        }
        // Method
        var mMethod = MethodRegex.Match(line);
        if (mMethod.Success)
        {
            var blk = new BlockInfo
            {
                Type = "Method",
                Name = mMethod.Groups[1].Value,
                ParamsSignature = mMethod.Groups[2].Success ? mMethod.Groups[2].Value : null,
                StartLine = lineIndex + 1,
                VisibilityAtStart = currentVisibility
            };
            stack.Push(blk);
            return true;
        }
        // DSL block (RSpec etc.)
        var mDsl = DslBlockRegex.Match(line);
        if (mDsl.Success)
        {
            var blk = new BlockInfo
            {
                Type = "DSL",
                Name = mDsl.Groups[1].Value,
                StartLine = lineIndex + 1,
                VisibilityAtStart = currentVisibility
            };
            stack.Push(blk);
            return true;
        }
        // begin ... end blocks (error handling) - treat if large enough later
        if (BeginBlockRegex.IsMatch(line))
        {
            var blk = new BlockInfo
            {
                Type = "Begin",
                Name = "begin",
                StartLine = lineIndex + 1,
                VisibilityAtStart = currentVisibility
            };
            stack.Push(blk);
            return true;
        }
        return false;
    }

    private static void ParseLineMetadata(string line, BlockInfo current, ref string currentVisibility)
    {
        var vis = VisibilityRegex.Match(line);
        if (vis.Success)
        {
            currentVisibility = vis.Groups[1].Value;
        }
        var mix = IncludeExtendRegex.Match(line);
        if (mix.Success)
        {
            current.Mixins.Add(mix.Groups[1].Value + " " + mix.Groups[2].Value);
        }
        var attr = AttrRegex.Match(line);
        if (attr.Success)
        {
            // attr_* can list multiple symbols separated by commas
            var symbols = attr.Groups[1].Value.Split(',').Select(s => s.Trim().Trim(':')).Where(s => s.Length > 0);
            current.Attributes.AddRange(symbols);
        }
        var constant = ConstantAssignRegex.Match(line);
        if (constant.Success)
        {
            current.Constants.Add(constant.Groups[1].Value);
        }
        var req = RequireRegex.Match(line);
        if (req.Success)
        {
            current.Requires.Add(req.Groups[1].Value);
        }
    }

    private static void ParseTopLevelMetadata(string line, List<string> requires, List<string> constants, List<string> attrs)
    {
        var req = RequireRegex.Match(line);
        if (req.Success)
        {
            requires.Add(req.Groups[1].Value);
        }
        var constant = ConstantAssignRegex.Match(line);
        if (constant.Success)
        {
            constants.Add(constant.Groups[1].Value);
        }
        var attr = AttrRegex.Match(line);
        if (attr.Success)
        {
            var symbols = attr.Groups[1].Value.Split(',').Select(s => s.Trim().Trim(':')).Where(s => s.Length > 0);
            attrs.AddRange(symbols);
        }
    }

    private static string BuildChunk(BlockInfo blk, int endLine, string? filePath, bool incomplete = false)
    {
        // For small begin blocks we can skip (noise) unless > 5 lines
        if (blk.Type == "Begin")
        {
            var lineCount = blk.Builder.ToString().Split('\n').Length;
            if (lineCount < 5)
            {
                return string.Empty; // caller will still add; filter later
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"### RUBY {blk.Type.ToUpper()} {blk.Name} (lines {blk.StartLine}-{endLine}{(incomplete ? ", incomplete" : string.Empty)})");
        if (!string.IsNullOrEmpty(filePath)) sb.AppendLine($"File: {filePath}");
        if (!string.IsNullOrEmpty(blk.SuperClass)) sb.AppendLine($"Superclass: {blk.SuperClass}");
        if (blk.Type == "Method" && !string.IsNullOrEmpty(blk.ParamsSignature)) sb.AppendLine($"Params: {blk.ParamsSignature}");
        if (blk.Mixins.Count > 0) sb.AppendLine("Mixins: " + string.Join(", ", blk.Mixins));
        if (blk.Attributes.Count > 0) sb.AppendLine("Attributes: " + string.Join(", ", blk.Attributes));
        if (blk.Constants.Count > 0) sb.AppendLine("Constants: " + string.Join(", ", blk.Constants));
        if (blk.Requires.Count > 0) sb.AppendLine("Requires: " + string.Join(", ", blk.Requires));
        sb.AppendLine($"VisibilityAtStart: {blk.VisibilityAtStart}");
        sb.AppendLine("---");
        sb.Append(blk.Builder.ToString().TrimEnd());
        return sb.ToString();
    }

    private static string BuildSimpleMetaChunk(string label, string body, string? filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### RUBY {label}");
        if (!string.IsNullOrEmpty(filePath)) sb.AppendLine($"File: {filePath}");
        sb.AppendLine("---");
        sb.Append(body.Trim());
        return sb.ToString();
    }
}