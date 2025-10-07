using ai.lab.service.Model.Database;

namespace ai.lab.service.Services.Common;

public interface IDatabaseService
{
    /// <summary>
    /// Asynchronously verifies connectivity and access to the database.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the database access test operation.</param>
    /// <returns>A task that represents the asynchronous operation of testing database access.</returns>
    Task TestDataBaseAccessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously inserts a chunk embedding into the MariaDB database.
    /// </summary>
    /// <param name="chunk">The chunk embedding to insert. Cannot be null.</param>
    /// <param name="connectionString">The connection string used to connect to the MariaDB database. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the identifier of the newly inserted
    /// chunk.</returns>
    Task<long> InsertChunkAsync(MariaDbChunkEmbedding chunk, string connectionString, CancellationToken cancellationToken);
}
