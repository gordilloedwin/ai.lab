namespace ai.lab.service.Services.Common;

public interface IContextSessionManager
{
    void StoreContext(string email, List<int> context);

    List<int>? GetContext(string email);

    void ClearContext(string email);
}
