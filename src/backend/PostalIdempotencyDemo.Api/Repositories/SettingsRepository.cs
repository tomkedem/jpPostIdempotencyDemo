using Dapper;
using PostalIdempotencyDemo.Api.Data;
using PostalIdempotencyDemo.Api.Models;

namespace PostalIdempotencyDemo.Api.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<SystemSetting>> GetSettingsAsync()
    {
        const string query = "SELECT * FROM SystemSettings";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<SystemSetting>(query);
    }

    public async Task<bool> UpdateSettingsAsync(IEnumerable<SystemSetting> settings)
    {
        const string sql = @"
            MERGE INTO SystemSettings AS Target
            USING (VALUES (@SettingKey, @SettingValue, @Description))
                AS Source (SettingKey, SettingValue, Description)
            ON Target.SettingKey = Source.SettingKey
            WHEN MATCHED THEN
                UPDATE SET
                    Target.SettingValue = Source.SettingValue,
                    Target.Description = Source.Description,
                    Target.UpdatedAt = GETUTCDATE()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (SettingKey, SettingValue, Description, UpdatedAt)
                VALUES (Source.SettingKey, Source.SettingValue, Source.Description, GETUTCDATE());";

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            foreach (var setting in settings)
            {
                await connection.ExecuteAsync(sql, setting, transaction);
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            return false;
        }
    }
}
