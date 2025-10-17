using ai.lab.service.Helpers;
using ai.lab.service.Model.Database;
using ai.lab.service.Services.Common;
using Microsoft.Extensions.Caching.Memory;

namespace ai.lab.service.Managers;

public class ContextSessionManager(IMemoryCache cache, IDatabaseService databaseService) : IContextSessionManager
{
    private readonly int maxTokens = 2048;
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromMinutes(60);

    public async Task StoreContextAsync(string email, string model, List<int> context, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(email, out List<UserChatContext>? existing) && (existing?.Any(m => m.Model == model) ?? false))
        {
            var updated = existing.Where(c => c.Model != model).ToList();
            var newEntries = context.Select(id =>
                new UserChatContext { Model = model, AiContext = context.TrimmedToMaxTokens(maxTokens) }).ToList();

            updated.AddRange(newEntries);
            cache.Set(email, updated, _sessionTimeout);
            await databaseService.UpdateUserLastSeenAsync(email, DateTime.UtcNow, updated, cancellationToken);
        }
        else
        {
            // Check if context exists in database and merge if found
            var user = await databaseService.GetUserByEmailAsync(email, cancellationToken);
            List<UserChatContext> contextToStore;

            if (user?.ContextJson is not null)
            {
                // Deserialize existing context from database
                var dbAiContext = System.Text.Json.JsonSerializer.Deserialize<List<UserChatContext>>(user.ContextJson);
                
                if (dbAiContext is not null && dbAiContext.Any())
                {
                    // Merge: Remove existing context for this model and add new one
                    var merged = dbAiContext.Where(c => c.Model != model).ToList();
                    merged.Add(new UserChatContext
                    {
                        Model = model,
                        AiContext = context.TrimmedToMaxTokens(maxTokens)
                    });
                    contextToStore = merged;
                }
                else
                {
                    // Database context was null or empty, create new
                    contextToStore = new List<UserChatContext>
                    {
                        new UserChatContext
                        {
                            Model = model,
                            AiContext = context.TrimmedToMaxTokens(maxTokens)
                        }
                    };
                }
            }
            else
            {
                // No existing context in database, create new
                contextToStore = new List<UserChatContext>
                {
                    new UserChatContext
                    {
                        Model = model,
                        AiContext = context.TrimmedToMaxTokens(maxTokens)
                    }
                };
            }

            cache.Set(email, contextToStore, _sessionTimeout);
            await databaseService.UpdateUserLastSeenAsync(email, DateTime.UtcNow, contextToStore, cancellationToken);
        }

        return;
    }

    // GetContext still trims defensively (idempotent + safety).
    public async Task<List<int>?> GetContextAsync(string email, string model, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(email, out List<UserChatContext>? context) && context is not null)
        {
            var modelContext = context.FirstOrDefault(c => c.Model == model);
            return modelContext?.AiContext.TrimmedToMaxTokens(maxTokens);
        }

        var user = await databaseService.GetUserByEmailAsync(email, cancellationToken);
        if (user?.ContextJson is not null)
        {
            var dbAiContext = System.Text.Json.JsonSerializer.Deserialize<List<UserChatContext>>(user.ContextJson);
            if (dbAiContext is not null)
            {
                var modelContext = dbAiContext.FirstOrDefault(c => c.Model == model);
                if (modelContext is not null)
                {
                    cache.Set(email, dbAiContext, _sessionTimeout);
                    return modelContext.AiContext.TrimmedToMaxTokens(maxTokens);;
                }
            }
        }

        return null;
    }

    public void ClearContext(string email)
    {
        cache.Remove(email);
    }
}