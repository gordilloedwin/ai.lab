namespace ai.lab.ragfeed.ChunkGenerators;

public class TextChunkGenerator
{
    public List<string> GenerateChunks(string filePath, int maxLinesPerChunk = 20)
    {        
        var chunks = new List<string>();
        var buffer = new List<string>();
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue; // skip empty lines
            }

            buffer.Add(line.Trim());
            if (buffer.Count >= maxLinesPerChunk)
            {
                chunks.Add(string.Join("\n", buffer));
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            chunks.Add(string.Join("\n", buffer));
        }

        return chunks;
    }
}
