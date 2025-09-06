namespace PostalIdempotencyDemo.Api.Models.DTO;

public class ChaosSettingsDto
{
    public bool UseIdempotencyKey { get; set; }
    public bool ForceError { get; set; }
    public int IdempotencyExpirationHours { get; set; } = 24;
    public int MaxRetryAttempts { get; set; } = 3;
    public int DefaultTimeoutSeconds { get; set; } = 30;
    public bool EnableMetricsCollection { get; set; } = true;
    public int MetricsRetentionDays { get; set; } = 30;
    public bool EnableChaosMode { get; set; } = false;
    public bool SystemMaintenanceMode { get; set; } = false;
}
