using Microsoft.AspNetCore.Mvc;
using PostalIdempotencyDemo.Api.Services;
using System.Threading.Tasks;

namespace PostalIdempotencyDemo.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;

    public MetricsController(IMetricsService metricsService)
    {
        _metricsService = metricsService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var summary = await _metricsService.GetMetricsSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("realtime")]
    public IActionResult GetRealTimeMetrics()
    {
        var metrics = _metricsService.GetRealTimeMetrics();
        return Ok(metrics);
    }

    [HttpPost("reset")]
    public IActionResult ResetMetrics()
    {
        _metricsService.ResetMetrics();
        return Ok(new { message = "Metrics reset successfully", timestamp = DateTime.UtcNow });
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetSystemHealth()
    {
        var summary = await _metricsService.GetMetricsSummaryAsync();
        var realTimeMetrics = _metricsService.GetRealTimeMetrics();
        
        return Ok(new
        {
            status = realTimeMetrics["healthStatus"],
            uptime = realTimeMetrics["uptime"],
            memoryUsage = realTimeMetrics["memoryUsage"],
            operationsPerSecond = realTimeMetrics["operationsPerSecond"],
            systemLoad = realTimeMetrics["systemLoad"],
            responseTime = new
            {
                current = realTimeMetrics["currentResponseTime"],
                average = summary.AverageResponseTime,
                peak = summary.PeakResponseTime,
                min = summary.MinResponseTime
            },
            operations = new
            {
                total = summary.TotalOperations,
                successful = summary.SuccessfulOperations,
                errors = summary.ErrorCount,
                idempotentBlocks = summary.IdempotentBlocks,
                successRate = summary.SuccessRate
            },
            timestamp = DateTime.UtcNow
        });
    }
}
