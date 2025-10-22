using ai.lab.ragfeed.ChunkGenerators;

namespace ai.lab.ragfeed;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("AI Lab RAG Feed - Chunk Extractor");
        
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ai.lab.ragfeed <file-path>");
            Console.WriteLine("Extracts code chunks from C# or Razor files for RAG indexing.");
            return;
        }

        string filePath = args[0];
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found - {filePath}");
            return;
        }

        var extractor = new RoslynChunkExtractor();
        List<string> chunks;

        if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Extracting C# chunks from: {filePath}");
            chunks = extractor.ExtractCsChunks(filePath);
        }
        else if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) || 
                 filePath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Extracting Razor chunks from: {filePath}");
            chunks = extractor.ExtractRazorChunks(filePath);
        }
        else
        {
            Console.WriteLine("Error: Unsupported file type. Use .cs, .razor, or .cshtml files.");
            return;
        }

        Console.WriteLine($"\nExtracted {chunks.Count} chunks:\n");
        
        for (int i = 0; i < chunks.Count; i++)
        {
            Console.WriteLine($"--- Chunk {i + 1} ---");
            Console.WriteLine(chunks[i]);
            Console.WriteLine();
        }
    }
}
