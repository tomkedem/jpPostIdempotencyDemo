using PostalIdempotencyDemo.Api.Models.DTO;
using System.Threading.Tasks;

namespace PostalIdempotencyDemo.Api.Services;

public interface IMetricsService
{
    Task<MetricsSummaryDto> GetMetricsSummaryAsync();
}
