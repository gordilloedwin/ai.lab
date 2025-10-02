namespace ai.lab.service.Services.Common;

public interface IOllamaSessionManager
{
    void StoreContext(string ipAddress, List<int> context);

    List<int>? GetContext(string ipAddress);

    void ClearContext(string ipAddress);
}
