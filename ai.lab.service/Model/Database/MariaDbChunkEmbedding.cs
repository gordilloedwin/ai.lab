namespace ai.lab.service.Model.Database;

public class MariaDbChunkEmbedding
{
    public long Id { get; set; }

    public string? ChunkId { get; set; }

    public string? ChunkText { get; set; }

    public string? FileName { get; set; }

    public string? Tags { get; set; }

    public float[]? Embedding { get; set; }
}
