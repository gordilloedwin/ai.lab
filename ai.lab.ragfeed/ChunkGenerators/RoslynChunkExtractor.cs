using ai.lab.ragfeed.ChunkGenerators.Common;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace ai.lab.ragfeed.ChunkGenerators;

public class RoslynChunkExtractor : IFileChunkGenerator
{
    public string Filetype => "microsoft csharp c# code";

    public List<string> GenerateChunks(string filepath) => 
        filepath.ToLowerInvariant().EndsWith("cshtml") ? ExtractRazorChunks(filepath) : ExtractCsChunks(filepath);

    public List<string> ExtractCsChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        var chunks = new List<string>();

        // Extract usings block (if present)
        var usings = root.Usings;
        if (usings.Any())
        {
            var usingBlock = string.Join("\n", usings.Select(u => u.ToFullString().Trim()));
            if (!string.IsNullOrWhiteSpace(usingBlock))
            {
                chunks.Add($"// Using directives\n{usingBlock}");
            }
        }

        // Extract namespace declarations with their types
        var namespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
        foreach (var ns in namespaces)
        {
            chunks.Add($"// Namespace: {ns.Name}\nnamespace {ns.Name};");
        }

        // Extract file-scoped namespace (C# 10+)
        var fileScopedNamespaces = root.DescendantNodes().OfType<FileScopedNamespaceDeclarationSyntax>();
        foreach (var ns in fileScopedNamespaces)
        {
            chunks.Add($"// File-scoped Namespace: {ns.Name}\nnamespace {ns.Name};");
        }

        // Extract all type declarations (classes, interfaces, structs, records, enums)
        var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var type in types)
        {
            // Add type declaration with XML docs if present
            var typeHeader = GetMemberWithDocumentation(type);
            if (!string.IsNullOrWhiteSpace(typeHeader))
            {
                chunks.Add($"// Type: {type.Identifier.Text}\n{typeHeader}");
            }

            // Extract individual members from the type
            foreach (var member in type.Members)
            {
                string memberChunk = GetMemberWithDocumentation(member);
                if (!string.IsNullOrWhiteSpace(memberChunk))
                {
                    var memberType = GetMemberTypeDescription(member);
                    chunks.Add($"// {memberType} in {type.Identifier.Text}\n{memberChunk}");
                }
            }
        }

        // Extract top-level statements (C# 9+)
        var topLevelStatements = root.Members.OfType<GlobalStatementSyntax>();
        if (topLevelStatements.Any())
        {
            var statementsText = string.Join("\n", topLevelStatements.Select(s => s.ToFullString().Trim()));
            if (!string.IsNullOrWhiteSpace(statementsText))
            {
                chunks.Add($"// Top-level statements\n{statementsText}");
            }
        }

        // Extract enum declarations separately for better context
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        foreach (var enumDecl in enums)
        {
            var enumText = GetMemberWithDocumentation(enumDecl);
            if (!string.IsNullOrWhiteSpace(enumText))
            {
                chunks.Add($"// Enum: {enumDecl.Identifier.Text}\n{enumText}");
            }
        }

        return chunks;
    }

    private string GetMemberWithDocumentation(SyntaxNode member)
    {
        // Get leading trivia (includes XML documentation comments)
        var leadingTrivia = member.GetLeadingTrivia();
        var docComments = leadingTrivia
            .Where(t => t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                       t.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineDocumentationCommentTrivia))
            .Select(t => t.ToFullString().Trim())
            .ToList();

        var memberText = member.ToFullString().Trim();
        
        if (docComments.Any())
        {
            return $"{string.Join("\n", docComments)}\n{memberText}";
        }

        return memberText;
    }

    private string GetMemberTypeDescription(MemberDeclarationSyntax member)
    {
        return member switch
        {
            MethodDeclarationSyntax method => $"Method: {method.Identifier.Text}",
            PropertyDeclarationSyntax prop => $"Property: {prop.Identifier.Text}",
            FieldDeclarationSyntax field => $"Field: {string.Join(", ", field.Declaration.Variables.Select(v => v.Identifier.Text))}",
            ConstructorDeclarationSyntax ctor => $"Constructor: {ctor.Identifier.Text}",
            EventDeclarationSyntax evt => $"Event: {evt.Identifier.Text}",
            IndexerDeclarationSyntax _ => "Indexer",
            OperatorDeclarationSyntax op => $"Operator: {op.OperatorToken.Text}",
            ConversionOperatorDeclarationSyntax conv => "Conversion Operator",
            DestructorDeclarationSyntax dtor => $"Destructor: ~{dtor.Identifier.Text}",
            _ => "Member"
        };
    }

    public List<string> ExtractRazorChunks(string filePath)
    {
        var fileContent = File.ReadAllText(filePath);
        var chunks = new List<string>();

        try
        {
            var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem.Create("."), builder => { });
            var sourceDocument = RazorSourceDocument.Create(fileContent, Path.GetFileName(filePath));
            var codeDocument = projectEngine.Process(sourceDocument, null, new List<RazorSourceDocument>(), new List<TagHelperDescriptor>());
            
            // Get the generated C# code from Razor
            var generatedCode = codeDocument.GetCSharpDocument();
            if (generatedCode != null && !string.IsNullOrWhiteSpace(generatedCode.GeneratedCode))
            {
                // Parse the generated C# code to extract meaningful chunks
                var tree = CSharpSyntaxTree.ParseText(generatedCode.GeneratedCode);
                var root = tree.GetCompilationUnitRoot();
                
                // Extract methods from the generated Razor class
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                foreach (var method in methods)
                {
                    string methodText = method.ToFullString().Trim();
                    if (!string.IsNullOrWhiteSpace(methodText))
                    {
                        chunks.Add(methodText);
                    }
                }
                
                // Also extract properties
                var properties = root.DescendantNodes().OfType<PropertyDeclarationSyntax>();
                foreach (var prop in properties)
                {
                    string propText = prop.ToFullString().Trim();
                    if (!string.IsNullOrWhiteSpace(propText))
                    {
                        chunks.Add(propText);
                    }
                }
            }

            // Fallback: if no chunks extracted from generated code, chunk the raw Razor content by logical sections
            if (chunks.Count == 0)
            {
                chunks = ChunkRazorContentByPatterns(fileContent);
            }
        }
        catch (Exception ex)
        {
            // Fallback to simple chunking if Razor parsing fails
            Console.WriteLine($"Warning: Razor parsing failed, using simple chunking. Error: {ex.Message}");
            chunks = ChunkRazorContentByPatterns(fileContent);
        }

        return chunks;
    }

    private List<string> ChunkRazorContentByPatterns(string content)
    {
        var chunks = new List<string>();
        var lines = content.Split('\n');
        var currentChunk = new StringBuilder();
        
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Start new chunk on major boundaries
            if (trimmedLine.StartsWith("@page") || 
                trimmedLine.StartsWith("@model") ||
                trimmedLine.StartsWith("@code") ||
                trimmedLine.StartsWith("@functions") ||
                trimmedLine.StartsWith("<div") ||
                trimmedLine.StartsWith("<section"))
            {
                // Save previous chunk if it has content
                if (currentChunk.Length > 0)
                {
                    var chunk = currentChunk.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(chunk))
                    {
                        chunks.Add(chunk);
                    }
                    currentChunk.Clear();
                }
            }
            
            currentChunk.AppendLine(line);
        }
        
        // Add final chunk
        if (currentChunk.Length > 0)
        {
            var chunk = currentChunk.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
        }
        
        return chunks;
    }
}

