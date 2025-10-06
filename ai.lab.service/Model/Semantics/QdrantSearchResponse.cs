namespace ai.lab.service.Model.Semantics;

public class QdrantSearchResponse
{
    public List<SearchResult> result { get; set; } = new();
}

public class SearchResult
{
    public Dictionary<string, object> payload { get; set; } = new();
}