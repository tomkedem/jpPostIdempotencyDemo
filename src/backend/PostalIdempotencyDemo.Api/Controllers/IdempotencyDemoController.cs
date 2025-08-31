using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PostalIdempotencyDemo.Api.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services.Interfaces;
using PostalIdempotencyDemo.Api.Services;
using System.Text.Json;

namespace PostalIdempotencyDemo.Api.Controllers;

[ApiController]
[Route("api/idempotency-demo")]
public class IdempotencyDemoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdempotencyDemoController> _logger;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IMetricsRepository _metricsRepository;
    private readonly IDeliveryService _deliveryService;
    private static readonly Random _random = new();

    public IdempotencyDemoController(IConfiguration configuration, ILogger<IdempotencyDemoController> logger, IIdempotencyService idempotencyService, IDeliveryRepository deliveryRepository, IMetricsRepository metricsRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _idempotencyService = idempotencyService;
        _metricsRepository = metricsRepository;
        _deliveryService = new DeliveryService(deliveryRepository);
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

        // שמירה לטבלת idempotency_entries בפעם הראשונה
        if (entry == null)
        {
            var newEntry = new IdempotencyEntry
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = ComputeSha256Hash(JsonSerializer.Serialize(request)),
                Endpoint = HttpContext.Request.Path,
                HttpMethod = HttpContext.Request.Method,
                StatusCode = 0,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Operation = "create_delivery",
                CorrelationId = HttpContext.TraceIdentifier,
                RelatedEntityId = null // אפשר להוסיף מזהה משלוח אם יש
            };
            await _idempotencyService.StoreIdempotencyEntryAsync(newEntry);
        }
        // יצירת משלוח חדש בלבד
        var response = await _deliveryService.CreateDeliveryAsync(request);
        await _idempotencyService.CacheResponseAsync(idempotencyKey, response);
        return Ok(response);
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
     
        // שמירה לטבלת idempotency_entries בפעם הראשונה
        if (entry == null)
        {
            var newEntry = new IdempotencyEntry
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = ComputeSha256Hash(JsonSerializer.Serialize(request)),
                Endpoint = HttpContext.Request.Path,
                HttpMethod = HttpContext.Request.Method,
                StatusCode = 0,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Operation = "update_delivery_status",
                CorrelationId = HttpContext.TraceIdentifier,
                RelatedEntityId = null // אפשר להוסיף מזהה משלוח אם יש
            };
            await _idempotencyService.StoreIdempotencyEntryAsync(newEntry);
        }
        // עדכון סטטוס בלבד
        var response = await _deliveryService.UpdateDeliveryStatusAsync(barcode, request.StatusId);
        await _idempotencyService.CacheResponseAsync(idempotencyKey, response);
        await _metricsRepository.LogMetricsAsync("update_status", HttpContext.Request.Path, response.ExecutionTimeMs != 0 ? response.ExecutionTimeMs : 0, false, idempotencyKey);
        if (!response.Success)
        {
            return NotFound(response);
        }
        return Ok(response);
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

    private string ComputeSha256Hash(string input)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }
}
