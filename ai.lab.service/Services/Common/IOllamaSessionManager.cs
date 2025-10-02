namespace ai.lab.service.Services.Common;

public interface IOllamaSessionManager
{
    void StoreContext(string chatId, List<int> context);

    List<int>? GetContext(string chatId);

    void ClearContext(string chatId);
}
