using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ai.lab.service.Managers;

public class ContextSessionManager(IMemoryCache cache, IDatabaseService databaseService) : IContextSessionManager
{
    private readonly int maxTokens = 2048;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(60);

    public void StoreContext(string email, List<int> context)
    {
        if (cache.TryGetValue(email, out List<int>? existing) && existing is not null)
        {
            var trimmed = existing
                .Concat(context)
                .TakeLast(maxTokens)
                .ToList();

            cache.Set(email, trimmed, _sessionTimeout);
            databaseService.UpdateUserLastSeenAsync(email, DateTime.UtcNow, trimmed, CancellationToken.None);
        }
        else
        {
            var trimmed = context.Count > maxTokens
                ? context.TakeLast(maxTokens).ToList()
                : context;

            cache.Set(email, trimmed, _sessionTimeout);
            databaseService.UpdateUserLastSeenAsync(email, DateTime.UtcNow, trimmed, CancellationToken.None);
        }
    }

    // GetContext still trims defensively (idempotent + safety).
    public List<int>? GetContext(string email)
    {
        if (cache.TryGetValue(email, out List<int>? context) && context is not null)
        {
            return context.Count > maxTokens ? context.TakeLast(maxTokens).ToList() : context;
        }

        var contextJson = databaseService.GetUserByEmailAsync(email, CancellationToken.None).Result?.ContextJson;
        if (contextJson is not null)
        {
            var dbContext = System.Text.Json.JsonSerializer.Deserialize<List<int>>(contextJson);
            if (dbContext is not null)
            {
                var trimmed = dbContext.Count > maxTokens ? dbContext.TakeLast(maxTokens).ToList() : dbContext;
                cache.Set(email, trimmed, _sessionTimeout);
                return trimmed;
            }
        }

        return null;
    }

    public void ClearContext(string email)
    {
        cache.Remove(email);
    }
}