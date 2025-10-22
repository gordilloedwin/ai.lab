namespace ai.lab.service.Helpers;

public class TagMatcher(IEnumerable<string> tags)
{
    private readonly HashSet<string> _tags = 
        new HashSet<string>(tags.Select(t => t.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

    private IEnumerable<string> Tokenize(string text) =>
        text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', '(', ')', '{', '}', '[', ']', '<', '>', ':', '"', '\'' },
            StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim().ToLowerInvariant());

    public List<string> MatchTags(string chunk)
    {
        var words = Tokenize(chunk);
        var matched = new HashSet<string>();

        foreach (var word in words)
        {
            if (_tags.Contains(word.ToLowerInvariant()))
            {
                matched.Add(word.ToLowerInvariant());
            }
        }

        return matched.ToList();
    }
}