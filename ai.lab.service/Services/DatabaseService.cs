using ai.lab.service.Helpers;
using ai.lab.service.Model.Database;
using ai.lab.service.Options;
using ai.lab.service.Services.Common;
using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace ai.lab.service.Services;

public class DatabaseService(IOptionsMonitor<DatabaseOptions> options, ILogger<DatabaseService> logger) : IDatabaseService
{
    public async Task TestDataBaseAccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }
            
            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            using var command = new MySqlCommand("SELECT 1", connection);
            var result = await command.ExecuteScalarAsync(cancellationToken);

            if (result == null || Convert.ToInt32(result) != 1)
            {
                throw new InvalidOperationException("Database test query did not return the expected result.");
            }

            logger.LogInformation("Successfully connected to the database and executed test query.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to access the database.");
            throw;
        }
    }

    public async Task<long> InsertChunkAsync(MariaDbChunkEmbedding chunk, string connectionString, CancellationToken cancellationToken)
    {
        try
        {
            const string sql = @"
            INSERT INTO chunk_embeddings 
            (
                chunk_id,
                chunk_text,
                file_name,
                tags,
                embedding
            )
            VALUES
            (
                @ChunkId,
                @ChunkText,
                @FileName,
                @Tags,
                @Embedding
            );
            SELECT LAST_INSERT_ID();";

            SqlMapper.AddTypeHandler(new VectorHandler());
            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            var id = await connection.ExecuteScalarAsync<long>(sql, chunk);
            return id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to insert chunk embedding into the database.");
            throw;
        }
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            const string sql = @"
            SELECT
                id AS Id,
                email AS Email,
                name AS Name,
                password_hash AS PasswordHash,
                avatar_uri AS AvatarUri,
                is_admin AS IsAdmin,
                last_seen AS LastSeen,
                created_at AS CreatedAt,
                context_json AS ContextJson
            FROM users
            WHERE email = @Email
            LIMIT 1;";

            using var connection = new MySqlConnection(connectionString);
            connection.OpenAsync(cancellationToken).Wait(cancellationToken);
            var user = await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
            return user;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve user by email from the database.");
            throw;
        }
    }

    public async Task<bool> AddUserAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }

            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            // Insert only if email is not null and not already present
            const string sql = @"
            INSERT INTO users 
            (
                email,
                name,
                password_hash,
                avatar_uri,
                is_admin,
                last_seen,
                created_at,
                context_json
            )
            SELECT 
                @Email,
                @Name,
                @PasswordHash,
                @AvatarUri,
                @IsAdmin,                
                @LastSeen,
                @CreatedAt,
                @ContextJson
            WHERE 
                @Email IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM users WHERE email = @Email);";

            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(sql, user, cancellationToken: cancellationToken));
            return rowsAffected > 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add user to the database.");
            throw;
        }
    }

    public async Task UpdateUserLastSeenAsync(string email, DateTime lastSeen, List<int> context, CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = options.CurrentValue.MariaDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Database connection string is not configured.");
            }
            const string sql = @"
            UPDATE 
                users
            SET 
                last_seen = @LastSeen,
                context_json = @ContextJson
            WHERE 
                email = @Email;";
            
            using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql,
                new { Email = email, LastSeen = lastSeen, ContextJson = System.Text.Json.JsonSerializer.Serialize(context) }, cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update user's last seen in the database.");
            throw;
        }
    }

}
