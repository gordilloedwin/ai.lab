namespace ai.lab.service.Services.Common;

public interface IDatabaseService
{
    Task TestDataBaseAccessAsync(CancellationToken cancellationToken = default);
}
