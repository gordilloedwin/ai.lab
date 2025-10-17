namespace ai.lab.service.Helpers;

/// <summary>
/// Extension methods for List operations.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Trims a list to the specified maximum number of items by taking the last N items.
    /// This is useful for maintaining a sliding window of most recent context tokens.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The list to trim.</param>
    /// <param name="maxItems">The maximum number of items to retain.</param>
    /// <returns>A new list containing at most maxItems elements from the end of the original list.</returns>
    public static List<T> TrimmedToMaxTokens<T>(this List<T> list, int maxItems)
    {
        if (list == null || list.Count == 0)
        {
            return new List<T>();
        }

        if (list.Count <= maxItems)
        {
            return list;
        }

        // Take the last N items to maintain most recent context
        return list.TakeLast(maxItems).ToList();
    }
}
