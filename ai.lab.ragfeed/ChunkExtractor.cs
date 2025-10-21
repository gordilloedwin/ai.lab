using ai.lab.ragfeed.Output;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging; // Fixes CS0246

namespace ai.lab.ragfeed;

public interface IChunkExtractor
{
    bool ChunkExtractorFactory(string filePath, out List<ChunkEmbedding> chunkEmbeddings);
}

public class ChunkExtractor(ILogger<ChunkExtractor> logger) : IChunkExtractor
{
    public bool ChunkExtractorFactory(string filePath, out List<ChunkEmbedding> chunkEmbeddings)
    {
        chunkEmbeddings = new List<ChunkEmbedding>();

        if (!File.Exists(filePath))
        {
            logger.LogWarning("File not found: {filePath}", filePath);
            chunkEmbeddings = new List<ChunkEmbedding>();
            return false;
        }

        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            IFileChunkGenerator chunkGenerator = extension switch
            {
                ".txt" => new TextFileChunkGenerator(),
                ".md" => new MarkdownFileChunkGenerator(),
                ".pdf" => new PdfFileChunkGenerator(),
                _ => throw new NotSupportedException($"File type '{extension}' is not supported for chunk extraction."),
            };
            chunkEmbeddings = chunkGenerator.GenerateChunks(filePath);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting chunks from file: {filePath}", filePath);
            return false;
        }
    }
}
