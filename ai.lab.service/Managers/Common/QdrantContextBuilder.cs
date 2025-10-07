using ai.lab.service.Model.Embeddings;
using System.Text;

namespace ai.lab.service.Managers.Common;

public class QdrantContextBuilder
{
    private readonly int _maxTokens;

    private readonly QdrantSearchResponse _response;    

    /// <summary>
    /// Initializes a new instance of the QdrantContextBuilder class using the specified search response and an optional
    /// maximum token limit.
    /// </summary>
    /// <param name="response">The QdrantSearchResponse containing the results to be used for context building. Cannot be null.</param>
    /// <param name="maxTokens">The maximum number of tokens to include in the built context. Must be a positive integer. The default value is
    /// 1500.</param>
    public QdrantContextBuilder(QdrantSearchResponse response, int maxTokens = 1500)
    {
        _response = response;
        _maxTokens = maxTokens;
    }

    public string BuildContextWindow(string contentKey = "content")
    {
        int tokenCount = 0;
        var contextBuilder = new StringBuilder();        

        foreach (var result in _response.result)
        {
            if (!result.payload.TryGetValue(contentKey, out var contentObj))
            {
                continue;
            }

            string? content = contentObj?.ToString()?.Trim();
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
