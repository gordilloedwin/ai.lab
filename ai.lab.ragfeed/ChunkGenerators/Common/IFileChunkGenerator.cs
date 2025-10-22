namespace ai.lab.ragfeed.ChunkGenerators.Common;

internal interface IFileChunkGenerator
{
    string Filetype { get; }

    List<string> GenerateChunks(string filepath);
}
