namespace ai.lab.service.Services.Common;

public interface IContextSessionManager
{
    void StoreContext(string chatId, List<int> context);

    List<int>? GetContext(string chatId);

    void ClearContext(string chatId);
}
