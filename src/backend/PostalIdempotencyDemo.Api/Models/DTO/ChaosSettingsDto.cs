namespace PostalIdempotencyDemo.Api.Models.DTO;

public class ChaosSettingsDto
{
    public bool UseIdempotencyKey { get; set; }
    public bool ForceError { get; set; }
}
