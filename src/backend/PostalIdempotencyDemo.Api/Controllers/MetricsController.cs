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
}
