namespace ai.lab.ragfeed.ChunkGenerators.Common;

internal interface IFileChunkGenerator
{
    List<string> GenerateChunks(string filepath);
}
