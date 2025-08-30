using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace PostalIdempotencyDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseSetupController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSetupController> _logger;

    public DatabaseSetupController(IConfiguration configuration, ILogger<DatabaseSetupController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("initialize-demo-data")]
    public async Task<ActionResult> InitializeDemoData()
    {
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            _logger.LogInformation("Starting demo data initialization...");

            // Clear existing demo data
            await ExecuteNonQuery(connection, "DELETE FROM operation_metrics");
            await ExecuteNonQuery(connection, "DELETE FROM delivery_with_date");
            await ExecuteNonQuery(connection, "DELETE FROM delivery_checks");
            await ExecuteNonQuery(connection, "DELETE FROM signatures");
            await ExecuteNonQuery(connection, "DELETE FROM deliveries");
            await ExecuteNonQuery(connection, "DELETE FROM shipments");
            await ExecuteNonQuery(connection, "DELETE FROM idempotency_entries");

            // Generate sample data
            await GenerateShipments(connection);
            await GenerateDeliveries(connection);
            await GenerateSignatures(connection);
            await GenerateDeliveryChecks(connection);
            await GenerateDeliveryWithDate(connection);
            await GenerateOperationMetrics(connection);
            await GenerateIdempotencyEntries(connection);

            // Get summary
            var summary = await GetDataSummary(connection);

            _logger.LogInformation("Demo data initialization completed successfully");

            return Ok(new
            {
                success = true,
                message = "Demo data initialized successfully",
                summary = summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing demo data");
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to initialize demo data",
                error = ex.Message
            });
        }
    }

    private async Task ExecuteNonQuery(SqlConnection connection, string sql)
    {
        using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task GenerateShipments(SqlConnection connection)
    {
        var customerNames = new[] { "יוסי כהן", "שרה לוי", "דוד אברהם", "רחל גולדברג", "משה רוזן" };
        var addresses = new[] { "רחוב הרצל 10, תל אביב", "שדרות בן גוריון 25, חיפה", "רחוב יפו 15, ירושלים" };
        var random = new Random();

        for (int i = 1; i <= 150; i++)
        {
            var barcode = $"DEMO{i:D6}";
            var statusId = i % 10 == 0 ? 5 : (i % 8 == 0 ? 4 : (i % 6 == 0 ? 3 : (i % 4 == 0 ? 2 : 1)));

            const string sql = @"
                INSERT INTO shipments (barcode, customer_name, address, weight, price, status_id, notes)
                VALUES (@barcode, @customer_name, @address, @weight, @price, @status_id, @notes)";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@barcode", barcode);
            command.Parameters.AddWithValue("@customer_name", customerNames[random.Next(customerNames.Length)]);
            command.Parameters.AddWithValue("@address", addresses[random.Next(addresses.Length)]);
            command.Parameters.AddWithValue("@weight", Math.Round(random.NextDouble() * 5 + 0.1, 3));
            command.Parameters.AddWithValue("@price", Math.Round(random.NextDouble() * 100 + 10, 2));
            command.Parameters.AddWithValue("@status_id", statusId);
            command.Parameters.AddWithValue("@notes", "הערות לדוגמה");

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 150 shipments");
    }

    private async Task GenerateDeliveries(SqlConnection connection)
    {
        var random = new Random();

        for (int i = 1; i <= 200; i++)
        {
            var statusId = i % 10 == 0 ? 2 : (i % 15 == 0 ? 3 : 1);

            const string sql = @"
                INSERT INTO deliveries (barcode, employee_id, delivery_date, location_lat, location_lng, recipient_name, status_id, notes)
                VALUES (@barcode, @employee_id, GETUTCDATE(), @location_lat, @location_lng, @recipient_name, @status_id, @notes)";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@barcode", $"DEMO{random.Next(1, 151):D6}");
            command.Parameters.AddWithValue("@employee_id", $"EMP{random.Next(1, 51):D3}");
            command.Parameters.AddWithValue("@location_lat", 32.0 + random.NextDouble() * 2);
            command.Parameters.AddWithValue("@location_lng", 34.7 + random.NextDouble() * 1.5);
            command.Parameters.AddWithValue("@recipient_name", "נמען לדוגמה");
            command.Parameters.AddWithValue("@status_id", statusId);
            command.Parameters.AddWithValue("@notes", statusId == 2 ? "לא נמצא בבית" : "נמסר בהצלחה");

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 200 deliveries");
    }

    private async Task GenerateSignatures(SqlConnection connection)
    {
        var random = new Random();

        for (int i = 1; i <= 180; i++)
        {
            const string sql = @"
                INSERT INTO signatures (barcode, employee_id, signature_data, signature_type, signer_name, signed_at)
                VALUES (@barcode, @employee_id, @signature_data, @signature_type, @signer_name, GETUTCDATE())";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@barcode", $"DEMO{random.Next(1, 151):D6}");
            command.Parameters.AddWithValue("@employee_id", $"EMP{random.Next(1, 51):D3}");
            command.Parameters.AddWithValue("@signature_data", "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
            command.Parameters.AddWithValue("@signature_type", random.Next(1, 4));
            command.Parameters.AddWithValue("@signer_name", "חותם לדוגמה");

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 180 signatures");
    }

    private async Task GenerateDeliveryChecks(SqlConnection connection)
    {
        var random = new Random();

        for (int i = 1; i <= 220; i++)
        {
            var isSuccessful = i % 8 != 0;
            const string sql = @"
                INSERT INTO delivery_checks (barcode, employee_id, check_type, check_result, is_successful, checked_at)
                VALUES (@barcode, @employee_id, @check_type, @check_result, @is_successful, GETUTCDATE())";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@barcode", $"DEMO{random.Next(1, 151):D6}");
            command.Parameters.AddWithValue("@employee_id", $"EMP{random.Next(1, 51):D3}");
            command.Parameters.AddWithValue("@check_type", random.Next(1, 5));
            command.Parameters.AddWithValue("@check_result", isSuccessful ? "הצליחה" : "נכשלה");
            command.Parameters.AddWithValue("@is_successful", isSuccessful);

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 220 delivery checks");
    }

    private async Task GenerateDeliveryWithDate(SqlConnection connection)
    {
        var random = new Random();

        for (int i = 1; i <= 160; i++)
        {
            const string sql = @"
                INSERT INTO delivery_with_date (barcode, employee_id, scheduled_date, status_id)
                VALUES (@barcode, @employee_id, @scheduled_date, @status_id)";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@barcode", $"DEMO{random.Next(1, 151):D6}");
            command.Parameters.AddWithValue("@employee_id", $"EMP{random.Next(1, 51):D3}");
            command.Parameters.AddWithValue("@scheduled_date", DateTime.Today.AddDays(random.Next(0, 14)));
            command.Parameters.AddWithValue("@status_id", random.Next(1, 5));

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 160 delivery with date records");
    }

    private async Task GenerateOperationMetrics(SqlConnection connection)
    {
        var random = new Random();
        var operations = new[] { "delivery", "signature", "shipment" };

        for (int i = 1; i <= 500; i++)
        {
            var operation = operations[random.Next(operations.Length)];
            const string sql = @"
                INSERT INTO operation_metrics (operation_type, endpoint, execution_time_ms, is_idempotent_hit, idempotency_key, created_at)
                VALUES (@operation_type, @endpoint, @execution_time_ms, @is_idempotent_hit, @idempotency_key, GETUTCDATE())";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@operation_type", operation);
            command.Parameters.AddWithValue("@endpoint", $"/api/idempotency-demo/{operation}");
            command.Parameters.AddWithValue("@execution_time_ms", random.Next(5, 250));
            command.Parameters.AddWithValue("@is_idempotent_hit", i % 5 == 0);
            command.Parameters.AddWithValue("@idempotency_key", i % 5 == 0 ? $"key-{Guid.NewGuid()}" : (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 500 operation metrics");
    }

    private async Task GenerateIdempotencyEntries(SqlConnection connection)
    {
        var random = new Random();
        var httpMethods = new[] { "POST", "PATCH" };

        for (int i = 1; i <= 50; i++)
        {
            var httpMethod = httpMethods[random.Next(httpMethods.Length)];
            const string sql = @"
                INSERT INTO idempotency_entries (id, request_signature, response_data, endpoint, http_method, status_code, created_at)
                VALUES (@id, @request_signature, @response_data, @endpoint, @http_method, @status_code, GETUTCDATE())";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", $"key-{Guid.NewGuid()}");
            command.Parameters.AddWithValue("@request_signature", $"sig-{Guid.NewGuid()}");
            command.Parameters.AddWithValue("@response_data", "{}");
            command.Parameters.AddWithValue("@endpoint", "/api/idempotency-demo/delivery");
            command.Parameters.AddWithValue("@http_method", httpMethod);
            command.Parameters.AddWithValue("@status_code", httpMethod == "POST" ? 201 : 200);

            await command.ExecuteNonQueryAsync();
        }
        _logger.LogInformation("Generated 50 idempotency entries");
    }

    private async Task<object> GetDataSummary(SqlConnection connection)
    {
        const string sql = @"
            SELECT 'shipments' as table_name, COUNT(*) as record_count FROM shipments
            UNION ALL
            SELECT 'deliveries', COUNT(*) FROM deliveries
            UNION ALL
            SELECT 'signatures', COUNT(*) FROM signatures  
            UNION ALL
            SELECT 'delivery_checks', COUNT(*) FROM delivery_checks
            UNION ALL
            SELECT 'delivery_with_date', COUNT(*) FROM delivery_with_date
            UNION ALL
            SELECT 'operation_metrics', COUNT(*) FROM operation_metrics
            UNION ALL
            SELECT 'idempotency_entries', COUNT(*) FROM idempotency_entries";

        using var command = new SqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        var summary = new List<object>();
        while (await reader.ReadAsync())
        {
            summary.Add(new
            {
                table_name = reader.GetString(0),
                record_count = reader.GetInt32(1)
            });
        }
        return summary;
    }
}
