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

}
