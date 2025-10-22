using ai.lab.service.Model.Database;
using System.Text;

namespace ai.lab.service.Managers.Common;

public class MariaDbContextBuilder
{
    private readonly int _maxTokens;

    private readonly List<MariaDbChunkEmbedding> _embeddings;

    /// <summary>
    /// Initializes a new instance of the MariaDbContextBuilder class using the specified embeddings and an optional
    /// maximum token limit.
    /// </summary>
    /// <param name="embeddings">The list of MariaDbChunkEmbedding objects containing the chunks to be used for context building. Cannot be null.</param>
    /// <param name="maxTokens">The maximum number of tokens to include in the built context. Must be a positive integer. The default value is
    /// 1500.</param>
    public MariaDbContextBuilder(List<MariaDbChunkEmbedding> embeddings, int maxTokens = 1500)
    {
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _maxTokens = maxTokens;
    }

    /// <summary>
    /// Builds a context window from the stored embeddings by concatenating chunk text up to the maximum token limit.
    /// </summary>
    /// <returns>A string containing the concatenated chunk texts, trimmed and separated by newlines.</returns>
    public string BuildContextWindow()
    {
        int tokenCount = 0;
        var contextBuilder = new StringBuilder();

        foreach (var embedding in _embeddings)
        {
            string? content = embedding.ChunkText?.Trim();
            if (string.IsNullOrEmpty(content))
            {
                continue;
            }

            int estimatedTokens = EstimateTokens(content);
            if (tokenCount + estimatedTokens > _maxTokens)
            {
                break;
            }

            contextBuilder.AppendLine(content);
            contextBuilder.AppendLine();
            tokenCount += estimatedTokens;
        }

        return contextBuilder.ToString().Trim();
    }

    // Simple token estimation: 1 token ~ 4 characters
    private int EstimateTokens(string text)
    {
        return text.Length / 4;
    }
}
