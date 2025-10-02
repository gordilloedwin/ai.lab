using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ai.lab.service.Managers;

public class OllamaSessionManager(IMemoryCache cache) : IOllamaSessionManager
{
    private readonly int maxTokens = 2048;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public void StoreContext(string ipAddress, List<int> context)
    {
        if (cache.TryGetValue(ipAddress, out List<int>? existing) && existing is not null)
        {
            var trimmed = existing
                .Concat(context)
                .TakeLast(maxTokens)
                .ToList();

            cache.Set(ipAddress, trimmed, _sessionTimeout);
        }
        else
        {
            var trimmed = context.Count > maxTokens
                ? context.TakeLast(maxTokens).ToList()
                : context;

            cache.Set(ipAddress, trimmed, _sessionTimeout);
        }
    }

    // GetContext still trims defensively (idempotent + safety).
    public List<int>? GetContext(string ipAddress) => cache.TryGetValue(ipAddress, out List<int>? context)
            ? context?.TakeLast(maxTokens)?.ToList() : null;

    public void ClearContext(string ipAddress)
    {
        cache.Remove(ipAddress);
    }
}