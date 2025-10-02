using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ai.lab.service.Managers;

public class OllamaSessionManager(IMemoryCache cache) : IOllamaSessionManager
{
    private readonly int maxTokens = 2048;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(30);

    public void StoreContext(string ipAddress, List<int> context)
    {
        cache.Set(ipAddress, context, _sessionTimeout);
    }

    public List<int>? GetContext(string ipAddress)
    {
        return cache.TryGetValue(ipAddress, out List<int>? context) ? context?.TakeLast(maxTokens)?.ToList() : null;
    }

    public void ClearContext(string ipAddress)
    {
        cache.Remove(ipAddress);
    }
}