using ai.lab.ragfeed.Output;

namespace ai.lab.ragfeed;

public class FileChunkGenerator
{
    public List<ChunkEmbedding> GenerateChunks(string filePath)
    {
        string content = File.ReadAllText(filePath);
        string fileName = Path.GetFileName(filePath);

        var chunks = new List<string>();

        var result = new List<ChunkEmbedding>();
        foreach (var chunkText in chunks)
        {
            var tags = new List<string> { Path.GetExtension(filePath).TrimStart('.'), "code" };

            result.Add(new ChunkEmbedding
            {
                ChunkText = chunkText,
                FileName = fileName,
                Tags = tags,
                Model = model,
                Embedding = Array.Empty<float>()
            });
        }

        return result;
    }

    private string NormalizeChunk(string chunk) => chunk
        .Replace("\t", "    ") // convert tabs to spaces
        .Replace("\r", "")     // unify line endings
        .Trim();               // remove leading/trailing whitespace
}
