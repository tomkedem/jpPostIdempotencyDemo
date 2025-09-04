using PostalIdempotencyDemo.Api.Models.DTO;

namespace PostalIdempotencyDemo.Api.Services;

public interface IChaosService
{
    Task<ChaosSettingsDto> GetChaosSettingsAsync();
    Task<bool> UpdateChaosSettingsAsync(ChaosSettingsDto settingsDto);
}
