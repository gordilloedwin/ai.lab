namespace ai.lab.service.Services.Common;

public interface IContextSessionManager
{
    Task StoreContextAsync(string email, List<int> context, CancellationToken cancellationToken);

    Task<List<int>?> GetContextAsync(string email, CancellationToken cancellationToken);

    void ClearContext(string email);
}
