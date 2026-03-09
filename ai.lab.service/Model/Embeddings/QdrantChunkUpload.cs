namespace ai.lab.service.Model.Embeddings;

public class QdrantChunkUpload
{
    public string ChunkId { get; set; } = string.Empty;

    public float[] Vector { get; set; } = [];

    public string FileName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];
}
