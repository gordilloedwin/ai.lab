using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ai.lab.service.Managers;

public class ContextSessionManager(IMemoryCache cache, IDatabaseService databaseService) : IContextSessionManager
{
    private readonly int maxTokens = 2048;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(60);

    public async Task StoreContextAsync(string email, List<int> context, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(email, out List<int>? existing) && existing is not null)
        {
            var trimmed = existing
                .Concat(context)
                .TakeLast(maxTokens)
                .ToList();

            cache.Set(email, trimmed, _sessionTimeout);
            await databaseService.UpdateUserLastSeenAsync(email, DateTime.UtcNow, trimmed, cancellationToken);
        }
        else
        {
            var trimmed = context.Count > maxTokens
                ? context.TakeLast(maxTokens).ToList()
                : context;

            cache.Set(email, trimmed, _sessionTimeout);
            await databaseService.UpdateUserLastSeenAsync(email, DateTime.UtcNow, trimmed, cancellationToken);
        }

        return;
    }

    // GetContext still trims defensively (idempotent + safety).
    public async Task<List<int>?> GetContextAsync(string email, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(email, out List<int>? context) && context is not null)
        {
            return context;
        }

        var contextJson = await databaseService.GetUserByEmailAsync(email, cancellationToken);

        if (contextJson != null && !string.IsNullOrEmpty(contextJson.ContextJson))
        {
            var dbContext = System.Text.Json.JsonSerializer.Deserialize<List<int>>(contextJson.ContextJson);
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