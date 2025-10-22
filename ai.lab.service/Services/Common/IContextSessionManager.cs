namespace ai.lab.service.Services.Common;

public interface IContextSessionManager
{
    Task StoreContextAsync(string email, string model, List<int> context, CancellationToken cancellationToken);

    Task<List<int>?> GetContextAsync(string email, string model, CancellationToken cancellationToken);

    void ClearContext(string email);
}
