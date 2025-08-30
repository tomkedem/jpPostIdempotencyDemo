using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Services;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services.Interfaces;

namespace PostalIdempotencyDemo.Api.Controllers;

[ApiController]
[Route("api/idempotency-demo")]
public class IdempotencyDemoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdempotencyDemoController> _logger;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ISignatureRepository _signatureRepository;
    private static readonly Random _random = new();

    public IdempotencyDemoController(IConfiguration configuration, ILogger<IdempotencyDemoController> logger, IIdempotencyService idempotencyService, IDeliveryRepository deliveryRepository, ISignatureRepository signatureRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _idempotencyService = idempotencyService;
        _deliveryRepository = deliveryRepository;
        _signatureRepository = signatureRepository;
    }

    [HttpPost("delivery")]
    public async Task<IActionResult> CreateDelivery([FromBody] CreateDeliveryRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new IdempotencyDemoResponse<object> { Success = false, Message = "Idempotency-Key header is required." });
        }

        var (cachedResponse, entry) = await _idempotencyService.GetCachedResponseAsync(idempotencyKey);
        if (cachedResponse is IActionResult actionResult)
        {
            return actionResult;
        }
        else if (cachedResponse != null)
        {
            return Ok(cachedResponse);
        }

        var delivery = new Delivery
        {
            Id = Guid.NewGuid(),
            Barcode = request.Barcode,
            EmployeeId = request.EmployeeId,
            DeliveryDate = DateTime.UtcNow,
            LocationLat = request.LocationLat,
            LocationLng = request.LocationLng,
            RecipientName = request.RecipientName,
            StatusId = request.DeliveryStatus,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _deliveryRepository.CreateDeliveryAsync(delivery);
            var response = new IdempotencyDemoResponse<Delivery> { Success = true, Data = delivery };
            await _idempotencyService.CacheResponseAsync(idempotencyKey, response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating delivery");
            return StatusCode(500, new IdempotencyDemoResponse<object> { Success = false, Message = "An error occurred while creating the delivery." });
        }
    }

    [HttpPost("signature")]
    public async Task<IActionResult> RegisterSignature([FromBody] Signature signature, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new IdempotencyDemoResponse<object> { Success = false, Message = "Idempotency-Key header is required." });
        }

        var (cachedResponse, entry) = await _idempotencyService.GetCachedResponseAsync(idempotencyKey);
        if (cachedResponse is IActionResult actionResult)
        {
            return actionResult;
        }
        else if (cachedResponse != null)
        {
            return Ok(cachedResponse);
        }

        try
        {
            signature.Id = Guid.NewGuid();
            signature.CreatedAt = DateTime.UtcNow;
            await _signatureRepository.CreateSignatureAsync(signature);
            var response = new IdempotencyDemoResponse<Signature> { Success = true, Data = signature };
            await _idempotencyService.CacheResponseAsync(idempotencyKey, response);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering signature");
            return StatusCode(500, new IdempotencyDemoResponse<object> { Success = false, Message = "An error occurred while registering the signature." });
        }
    }

    [HttpPatch("delivery/{barcode}/status")]
    public async Task<IActionResult> UpdateDeliveryStatus(
        [FromRoute] string barcode,
        [FromBody] UpdateDeliveryStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new IdempotencyDemoResponse<object> { Success = false, Message = "Idempotency-Key header is required." });
        }

        var (cachedResponse, entry) = await _idempotencyService.GetCachedResponseAsync(idempotencyKey);
        if (cachedResponse is IActionResult actionResult)
        {
            return actionResult;
        }
        else if (cachedResponse != null)
        {
            return Ok(cachedResponse);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = "UPDATE deliveries SET status_id = @statusId WHERE barcode = @barcode";
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@statusId", request.StatusId);
            command.Parameters.AddWithValue("@barcode", barcode);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                return NotFound(new IdempotencyDemoResponse<object> { Success = false, Message = $"Shipment with barcode {barcode} not found." });
            }

            var updatedShipment = await GetShipmentByBarcode(barcode, connection);

            stopwatch.Stop();

            var response = new IdempotencyDemoResponse<Shipment>
            {
                Success = true,
                Data = updatedShipment,
                Message = "סטטוס משלוח עודכן בהצלחה",
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };

            await _idempotencyService.CacheResponseAsync(idempotencyKey, response);

            await LogMetrics("update_status", HttpContext.Request.Path, stopwatch.ElapsedMilliseconds, false, idempotencyKey);
            return Ok(response);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error updating status for barcode {Barcode}", barcode);
            await LogMetrics("update_status", HttpContext.Request.Path, stopwatch.ElapsedMilliseconds, false, idempotencyKey, true);
            return StatusCode(500, new IdempotencyDemoResponse<object> { Success = false, Message = $"Error updating status: {ex.Message}" });
        }
    }

    private async Task SimulateNetworkIssues()
    {
        var enableNetworkIssues = _configuration.GetValue<bool>("NetworkSimulation:EnableNetworkIssues");
        if (!enableNetworkIssues) return;

        var minDelay = _configuration.GetValue<int>("NetworkSimulation:MinDelayMs", 100);
        var maxDelay = _configuration.GetValue<int>("NetworkSimulation:MaxDelayMs", 3000);
        var failurePercentage = _configuration.GetValue<int>("NetworkSimulation:FailurePercentage", 10);

        // Random delay
        var delay = _random.Next(minDelay, maxDelay);
        await Task.Delay(delay);

        // Random failure
        if (_random.Next(1, 101) <= failurePercentage)
        {
            throw new TimeoutException("Network timeout simulation");
        }
    }

    private async Task<Shipment?> GetShipmentByBarcode(string barcode, SqlConnection connection)
    {
        const string query = @"
            SELECT 
                s.id, s.barcode, s.customer_name, s.address, s.weight, s.price, s.notes, s.created_at, s.updated_at,
                d.status_id, st.status_name, st.status_name_he
            FROM shipments s
            LEFT JOIN deliveries d ON s.barcode = d.barcode
            LEFT JOIN shipment_statuses st ON d.status_id = st.id
            WHERE s.barcode = @barcode
            ORDER BY d.delivery_date DESC";

        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@barcode", barcode);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Shipment
            {
                Id = reader.GetGuid(reader.GetOrdinal("id")),
                Barcode = reader.GetString(reader.GetOrdinal("barcode")),
                CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? null : reader.GetString(reader.GetOrdinal("customer_name")),
                Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString(reader.GetOrdinal("address")),
                Weight = reader.IsDBNull(reader.GetOrdinal("weight")) ? null : reader.GetDecimal(reader.GetOrdinal("weight")),
                Price = reader.IsDBNull(reader.GetOrdinal("price")) ? null : reader.GetDecimal(reader.GetOrdinal("price")),
                Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                StatusId = reader.IsDBNull(reader.GetOrdinal("status_id")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("status_id")),
                StatusName = reader.IsDBNull(reader.GetOrdinal("status_name")) ? null : reader.GetString(reader.GetOrdinal("status_name")),
                StatusNameHe = reader.IsDBNull(reader.GetOrdinal("status_name_he")) ? null : reader.GetString(reader.GetOrdinal("status_name_he"))
            };
        }
        return null;
    }

    private string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private async Task LogMetrics(string operationType, string endpoint, long executionTimeMs, bool isIdempotentHit, string? idempotencyKey, bool isError = false)
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
