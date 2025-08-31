using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PostalIdempotencyDemo.Api.Models;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
    private readonly IMetricsRepository _metricsRepository;
    private static readonly Random _random = new();

    public IdempotencyDemoController(IConfiguration configuration, ILogger<IdempotencyDemoController> logger, IIdempotencyService idempotencyService, IDeliveryRepository deliveryRepository, IMetricsRepository metricsRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _idempotencyService = idempotencyService;
        _deliveryRepository = deliveryRepository;
        _metricsRepository = metricsRepository;
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

    await _deliveryRepository.CreateDeliveryAsync(delivery);
    var response = new IdempotencyDemoResponse<Delivery> { Success = true, Data = delivery };
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

        var stopwatch = Stopwatch.StartNew();
        var updatedShipment = await _deliveryRepository.UpdateDeliveryStatusAsync(barcode, request.StatusId);
        stopwatch.Stop();

        if (updatedShipment == null)
        {
            return NotFound(new IdempotencyDemoResponse<object> { Success = false, Message = $"Shipment with barcode {barcode} not found." });
        }

        var response = new IdempotencyDemoResponse<Shipment>
        {
            Success = true,
            Data = updatedShipment,
            Message = "סטטוס משלוח עודכן בהצלחה",
            ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
        };

        await _idempotencyService.CacheResponseAsync(idempotencyKey, response);
        await _metricsRepository.LogMetricsAsync("update_status", HttpContext.Request.Path, stopwatch.ElapsedMilliseconds, false, idempotencyKey);
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
