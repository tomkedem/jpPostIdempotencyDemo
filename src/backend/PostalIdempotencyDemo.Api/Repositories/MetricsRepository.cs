using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace PostalIdempotencyDemo.Api.Repositories
{
    public interface IMetricsRepository
    {
        Task LogMetricsAsync(string operationType, string endpoint, long executionTimeMs, bool isIdempotentHit, string? idempotencyKey, bool isError = false);
    }

    public class MetricsRepository : IMetricsRepository
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MetricsRepository> _logger;

        public MetricsRepository(IConfiguration configuration, ILogger<MetricsRepository> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task LogMetricsAsync(string operationType, string endpoint, long executionTimeMs, bool isIdempotentHit, string? idempotencyKey, bool isError = false)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                const string sql = @"
                    INSERT INTO operation_metrics (id, operation_type, endpoint, execution_time_ms, 
                                                 is_idempotent_hit, idempotency_key, is_error, created_at)
                    VALUES (@id, @operation_type, @endpoint, @execution_time_ms, 
                            @is_idempotent_hit, @idempotency_key, @is_error, @created_at)";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@id", Guid.NewGuid());
                command.Parameters.AddWithValue("@operation_type", operationType);
                command.Parameters.AddWithValue("@endpoint", endpoint);
                command.Parameters.AddWithValue("@execution_time_ms", executionTimeMs);
                command.Parameters.AddWithValue("@is_idempotent_hit", isIdempotentHit);
                command.Parameters.AddWithValue("@idempotency_key", idempotencyKey ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@is_error", isError);
                command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging metrics");
            }
        }
    }
}
