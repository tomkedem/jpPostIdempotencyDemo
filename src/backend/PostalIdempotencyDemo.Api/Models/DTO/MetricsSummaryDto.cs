namespace PostalIdempotencyDemo.Api.Models.DTO;

public class MetricsSummaryDto
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int IdempotentHits { get; set; }
    public int FailedOperations { get; set; }
    public double AverageExecutionTimeMs { get; set; }
}
