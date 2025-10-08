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

    /// <summary>
    /// Asynchronously retrieves a user by their email address.
    /// </summary>
    /// <param name="email">The email address of the user to retrieve. Cannot be null or empty.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the user associated with the
    /// specified email address, or null if no user is found.</returns>
    Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously adds a new user to the system.
    /// </summary>
    /// <param name="user">The user to add. Cannot be null. The user's properties must meet any required validation criteria.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the add operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result is <see langword="true"/> if the user was
    /// added successfully; otherwise, <see langword="false"/>.</returns>
    Task<bool> AddUserAsync(User user, CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously updates the last seen timestamp for the specified user within the given context.
    /// </summary>
    /// <remarks>If the operation is cancelled via the <paramref name="cancellationToken"/>, the update will
    /// not be completed. This method does not return a result; callers should await the returned task to ensure
    /// completion.</remarks>
    /// <param name="userId">The unique identifier of the user whose last seen timestamp is to be updated.</param>
    /// <param name="lastSeen">The date and time to record as the user's most recent activity.</param>
    /// <param name="context">A list of context identifiers that specify the scope or environment in which the last seen timestamp should be
    /// updated. Cannot be null.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous update operation.</returns>
    Task UpdateUserLastSeenAsync(long userId, DateTime lastSeen, List<int> context, CancellationToken cancellationToken);
}
