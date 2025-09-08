namespace PostalIdempotencyDemo.Api.Models.DTO;

public class MetricsSummaryDto
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int IdempotentBlocks { get; set; }
    public int ErrorCount { get; set; }
    public int ChaosDisabledErrors { get; set; } // NEW: שגיאות כאשר הגנה כבויה
    public double AverageResponseTime { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastUpdated { get; set; }
    public string SystemHealth { get; set; } = "Unknown";
    public double ThroughputPerMinute { get; set; }
    public double PeakResponseTime { get; set; }
    public double MinResponseTime { get; set; }

    // Legacy properties for backward compatibility
    public int IdempotentHits => IdempotentBlocks;
    public int FailedOperations => ErrorCount;
    public double AverageExecutionTimeMs => AverageResponseTime;
}
