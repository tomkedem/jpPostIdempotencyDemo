namespace PostalIdempotencyDemo.Api.Services.Interfaces;

using PostalIdempotencyDemo.Api.Models.DTO;

public interface IChaosService
{
    Task<ChaosSettingsDto> GetChaosSettingsAsync();
    Task<bool> UpdateChaosSettingsAsync(ChaosSettingsDto settingsDto);
    Task<bool> ShouldIntroduceFailureAsync();
    Task<int> GetDelayAsync();
    Task SimulateNetworkIssueAsync();
}
