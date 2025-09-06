using PostalIdempotencyDemo.Api.Models.DTO;

namespace PostalIdempotencyDemo.Api.Services.Interfaces;

public interface IChaosService
{
    Task<ChaosSettingsDto> GetChaosSettingsAsync();
    Task<bool> UpdateChaosSettingsAsync(ChaosSettingsDto settingsDto);
    Task<bool> ShouldIntroduceFailureAsync();
    Task<int> GetDelayAsync();
    Task SimulateNetworkIssueAsync();
    Task<int> GetIdempotencyExpirationHoursAsync();
    Task<bool> IsMaintenanceModeAsync();
    Task<int> GetMaxRetryAttemptsAsync();
    Task<int> GetDefaultTimeoutSecondsAsync();
}
