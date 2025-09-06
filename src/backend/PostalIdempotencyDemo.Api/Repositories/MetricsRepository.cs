using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using PostalIdempotencyDemo.Api.Models.DTO;

namespace PostalIdempotencyDemo.Api.Repositories
{
    public interface IMetricsRepository
    {
        Task LogMetricsAsync(string operationType, string endpoint, long executionTimeMs, bool isIdempotentHit, string? idempotencyKey, bool isError = false);
        Task<MetricsSummaryDto> GetMetricsSummaryAsync();
    }

    public class MetricsRepository : IMetricsRepository
    {
        private readonly Data.ISqlExecutor _sqlExecutor;
        private readonly ILogger<MetricsRepository> _logger;

        public MetricsRepository(Data.ISqlExecutor sqlExecutor, ILogger<MetricsRepository> logger)
        {
            _sqlExecutor = sqlExecutor;
            _logger = logger;
        }

        public async Task LogMetricsAsync(string operationType, string endpoint, long executionTimeMs, bool isIdempotentHit, string? idempotencyKey, bool isError = false)
        {
            try
            {
                await _sqlExecutor.ExecuteAsync(async connection =>
                {
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
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging metrics");
            }
        }

        public async Task<MetricsSummaryDto> GetMetricsSummaryAsync()
        {
            var summary = new MetricsSummaryDto();
            try
            {
                await _sqlExecutor.ExecuteAsync(async connection =>
                {
                    const string sql = @"
                        SELECT 
                            COUNT(*) AS TotalOperations,
                            SUM(CASE WHEN is_idempotent_hit = 1 THEN 1 ELSE 0 END) AS IdempotentHits,
                            SUM(CASE WHEN is_error = 1 THEN 1 ELSE 0 END) AS FailedOperations,
                            AVG(CAST(execution_time_ms AS FLOAT)) AS AverageExecutionTimeMs
                        FROM operation_metrics;";

                    using var command = new SqlCommand(sql, connection);
                    using var reader = await command.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        summary.TotalOperations = reader["TotalOperations"] as int? ?? 0;
                        summary.IdempotentBlocks = reader["IdempotentHits"] as int? ?? 0;
                        summary.ErrorCount = reader["FailedOperations"] as int? ?? 0;
                        summary.AverageResponseTime = reader["AverageExecutionTimeMs"] as double? ?? 0;
                        summary.SuccessfulOperations = summary.TotalOperations - summary.ErrorCount;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting metrics summary");
            }
            return summary;
        }
    }
}
