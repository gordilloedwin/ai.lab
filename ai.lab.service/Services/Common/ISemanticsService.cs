namespace ai.lab.service.Services.Common;

public interface ISemanticsService
{
    Task UploadChunkAsync(string chunkId, string chunkText, string fileName, List<string> tags);
}