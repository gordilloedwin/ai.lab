namespace ai.lab.ragfeed.Output;

public class ChunkEmbedding
{
    public string ChunkId { get; set; } = string.Empty;           // Unique SHA-256 hash

    public float[] Embedding { get; set; } = Array.Empty<float>(); // 4096-dim vector

    public string ChunkText { get; set; } = string.Empty;         // Raw chunk content

    public string FileName { get; set; } = string.Empty;          // Source file

    public List<string> Tags { get; set; } = new();               // Metadata tags

    public string Model { get; set; } = string.Empty;             // Embedding model
}
