using PostalIdempotencyDemo.Api.Models.DTO;
using PostalIdempotencyDemo.Api.Repositories;
using System.Threading.Tasks;

namespace PostalIdempotencyDemo.Api.Services;

public class MetricsService : IMetricsService
{
    private readonly IMetricsRepository _metricsRepository;

    public MetricsService(IMetricsRepository metricsRepository)
    {
        _metricsRepository = metricsRepository;
    }

    public async Task<MetricsSummaryDto> GetMetricsSummaryAsync()
    {
        return await _metricsRepository.GetMetricsSummaryAsync();
    }
}
