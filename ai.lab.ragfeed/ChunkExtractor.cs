using ai.lab.ragfeed.ChunkGenerators;
using ai.lab.ragfeed.ChunkGenerators.Common;
using ai.lab.ragfeed.Output;
using Microsoft.Extensions.Logging;

namespace ai.lab.ragfeed;

public interface IChunkExtractor
{
    bool GenerateFileChunks(string filePath, out List<ChunkEmbedding> chunkEmbeddings);
}

public class ChunkExtractor(ILogger<ChunkExtractor> logger) : IChunkExtractor
{
    public bool GenerateFileChunks(string filePath, out List<ChunkEmbedding> chunkEmbeddings)
    {
        chunkEmbeddings = new List<ChunkEmbedding>();

        if (!File.Exists(filePath))
        {
            logger.LogWarning("File not found: {filePath}", filePath);
            return false;
        }

        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            IFileChunkGenerator chunkGenerator = extension switch
            {
                ".rb" => new RubyChunkExtractor(),
                ".css" => new CssChunkExtractor(),
                ".py" => new PythonChunkExtractor(),
                ".java" => new JavaChunkExtractor(),
                ".sql" => new PostgresChunkExtractor(),
                ".js" => new JavascriptChunkExtractor(),
                ".ps1" => new PowerShellChunkExtractor(),
                ".cpp" or ".h" or ".c" or ".hpp" => new CppChunkExtractor(), 
                ".cs" or ".cshtml" or ".vb" or ".fs" => new RoslynChunkExtractor(),
                ".md" or ".markdown" or ".json" or ".xml" or ".jrxml" or ".txt" or ".config" or ".yml" => new TextChunkExtractor(),
                _ => new NotSupportedFileChunkGenerator()
            };

            foreach (var chunk in chunkGenerator.GenerateChunks(filePath))
            {
                var chunkEmbedding = new ChunkEmbedding
                {
                    ChunkId = Guid.NewGuid().ToString(),
                    ChunkText = chunk,
                    FileName = Path.GetFileName(filePath),
                    Tags = new List<string> { extension },
                    Model = "default-model"
                };

                chunkEmbeddings.Add(chunkEmbedding);
            }

            return chunkEmbeddings.Count > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting chunks from file: {filePath}", filePath);
            return false;
        }
    }
}
