using System.Text.Json;
using System.Text.Json.Serialization;

namespace ai.lab.service.Models.Ollama;

public sealed record OllamaTagsResponse([property: JsonPropertyName("models")] List<OllamaModelInfo> Models)
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static OllamaTagsResponse? FromJson(string json, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<OllamaTagsResponse>(json, options ?? DefaultOptions);
}

public sealed record OllamaModelInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("modified_at")] DateTimeOffset ModifiedAt,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("digest")] string Digest,
    [property: JsonPropertyName("details")] OllamaModelDetails Details
);

public sealed record OllamaModelDetails(
    [property: JsonPropertyName("parent_model")] string ParentModel,
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("families")] List<string> Families,
    [property: JsonPropertyName("parameter_size")] string ParameterSize,
    [property: JsonPropertyName("quantization_level")] string QuantizationLevel
);