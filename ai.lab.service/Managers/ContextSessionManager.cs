using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ai.lab.service.Managers;

public class ContextSessionManager(IMemoryCache cache, IDatabaseService databaseService) : IContextSessionManager
{
    private readonly int maxTokens = 2048;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public void StoreContext(string chatId, List<int> context)
    {
        if (cache.TryGetValue(chatId, out List<int>? existing) && existing is not null)
        {
            var trimmed = existing
                .Concat(context)
                .TakeLast(maxTokens)
                .ToList();

            cache.Set(chatId, trimmed, _sessionTimeout);
            // todo: persist to db async
        }
        else
        {
            var trimmed = context.Count > maxTokens
                ? context.TakeLast(maxTokens).ToList()
                : context;

            cache.Set(chatId, trimmed, _sessionTimeout);
            // todo: persist to db async
        }
    }

    // GetContext still trims defensively (idempotent + safety).
    public List<int>? GetContext(string chatId)
    {
        // todo: load from db if not in cache
        return cache.TryGetValue(chatId, out List<int>? context)
            ? context?.TakeLast(maxTokens)?.ToList() : null;
    }

    public void ClearContext(string chatId)
    {
        cache.Remove(chatId);
    }
}