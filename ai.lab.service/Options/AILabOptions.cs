namespace ai.lab.service.Options;

public class AILabOptions
{
    public bool SaveChunksToQadrant { get; set; } = false;

    public bool SaveChunksToMariaDb { get; set; } = false;

    public int MaxRagChunksPerPrompt { get; set; } = 10;

    public bool UseQdrantForRag { get; set; } = false;

    public string QdrantCollectionName { get; set; } = "ai_lab";

    public string OllamaUrl { get; set; } = "http://localhost:11434";

    public string QdrantUrl { get; set; } = "http://localhost:6333";

    public string RepositoriesPath { get; set; } = string.Empty;
}