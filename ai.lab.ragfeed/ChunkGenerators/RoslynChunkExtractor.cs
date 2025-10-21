using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ai.lab.ragfeed.ChunkGenerators;

public class RoslynChunkExtractor
{
    public List<string> ExtractCsChunks(string filePath)
    {
        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetCompilationUnitRoot();

        var chunks = new List<string>();
        var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var type in types)
        {
            var members = type.Members;
            foreach (var member in members)
            {
                string chunkText = member.ToFullString().Trim();
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(chunkText);
                }
            }
        }

        return chunks;
    }

    public List<string> ExtractRazorChunks(string filePath)
    {
        var projectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, RazorProjectFileSystem.Create("."), builder => { });
        var sourceDocument = RazorSourceDocument.ReadFrom(filePath);
        var codeDocument = projectEngine.Process(sourceDocument);
        var syntaxTree = codeDocument.GetSyntaxTree();

        var chunks = new List<string>();
        foreach (var node in syntaxTree.Root.DescendantNodes())
        {
            if (node is RazorDirectiveSyntax || node is CSharpCodeBlockSyntax || node is MarkupBlockSyntax)
            {
                string chunk = node.GetContent().Trim();
                if (!string.IsNullOrWhiteSpace(chunk))
                { 
                    chunks.Add(chunk);
                }            
            }
        }

        return chunks;
    }


}

