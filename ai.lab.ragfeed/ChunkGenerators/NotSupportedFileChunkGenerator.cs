using ai.lab.ragfeed.ChunkGenerators.Common;

namespace ai.lab.ragfeed.ChunkGenerators;

public class NotSupportedFileChunkGenerator : IFileChunkGenerator
{
    public string Filetype => string.Empty;

    public List<string> GenerateChunks(string filepath) => new List<string>();
}
