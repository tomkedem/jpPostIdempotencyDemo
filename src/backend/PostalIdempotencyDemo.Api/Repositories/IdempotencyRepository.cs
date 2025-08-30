using System.Data;
using Microsoft.Data.SqlClient;
using PostalIdempotencyDemo.Api.Data;
using PostalIdempotencyDemo.Api.Models;

namespace PostalIdempotencyDemo.Api.Repositories;

public class IdempotencyRepository : IIdempotencyRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public IdempotencyRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IdempotencyEntry?> GetByKeyAsync(string key)
    {
        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            SELECT idempotency_key, request_hash, response_body, status_code, 
                   created_at, expires_at, correlation_id, operation, related_entity_id 
            FROM idempotency_entries 
            WHERE idempotency_key = @key AND expires_at > @now";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new IdempotencyEntry
            {
                IdempotencyKey = reader.GetString("idempotency_key"),
                RequestHash = reader.GetString("request_hash"),
                ResponseData = reader.IsDBNull("response_body") ? null : reader.GetString("response_body"),
                StatusCode = reader.GetInt32("status_code"),
                CreatedAt = reader.GetDateTime("created_at"),
                ExpiresAt = reader.GetDateTime("expires_at"),
                Endpoint = reader.IsDBNull("correlation_id") ? string.Empty : reader.GetString("correlation_id"),
                HttpMethod = reader.GetString("operation")
            };
        }

        return null;
    }

    public async Task<bool> CreateAsync(IdempotencyEntry entry)
    {
        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO idempotency_entries 
            (idempotency_key, request_hash, response_body, status_code, 
             created_at, expires_at, correlation_id, operation)
            VALUES (@key, @hash, @response, @statusCode, @createdAt, @expiresAt, @correlationId, @operation)";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@key", entry.IdempotencyKey);
        command.Parameters.AddWithValue("@hash", entry.RequestHash);
        command.Parameters.AddWithValue("@response", (object?)entry.ResponseData ?? DBNull.Value);
        command.Parameters.AddWithValue("@statusCode", entry.StatusCode);
        command.Parameters.AddWithValue("@createdAt", entry.CreatedAt);
        command.Parameters.AddWithValue("@expiresAt", entry.ExpiresAt);
        command.Parameters.AddWithValue("@correlationId", (object?)entry.Endpoint ?? DBNull.Value);
        command.Parameters.AddWithValue("@operation", entry.HttpMethod);

        var result = await command.ExecuteNonQueryAsync();
        return result > 0;
    }

    public async Task<bool> UpdateResponseAsync(string key, string responseData, int statusCode)
    {
        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = @"
            UPDATE idempotency_entries 
            SET response_body = @response, status_code = @statusCode 
            WHERE idempotency_key = @key";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@response", responseData);
        command.Parameters.AddWithValue("@statusCode", statusCode);
        command.Parameters.AddWithValue("@key", key);

        var result = await command.ExecuteNonQueryAsync();
        return result > 0;
    }

    public async Task<bool> DeleteExpiredAsync()
    {
        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        await connection.OpenAsync();

        const string sql = "DELETE FROM idempotency_entries WHERE expires_at <= @now";

        using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@now", DateTime.UtcNow);

        var result = await command.ExecuteNonQueryAsync();
        return result > 0;
    }
}
