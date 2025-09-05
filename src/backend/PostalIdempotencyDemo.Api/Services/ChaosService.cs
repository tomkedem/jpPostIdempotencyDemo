using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Models.DTO;
using PostalIdempotencyDemo.Api.Repositories;


namespace PostalIdempotencyDemo.Api.Services
{
    using PostalIdempotencyDemo.Api.Services.Interfaces;

    public class ChaosService : IChaosService
    {
        private readonly ISettingsRepository _settingsRepository;

        public ChaosService(ISettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        public async Task<ChaosSettingsDto> GetChaosSettingsAsync()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            var settingsDict = settings.ToDictionary(s => s.SettingKey, s => s.SettingValue);

            return new ChaosSettingsDto
            {
                UseIdempotencyKey = bool.TryParse(settingsDict.GetValueOrDefault("UseIdempotencyKey"), out var use) && use,
                ForceError = bool.TryParse(settingsDict.GetValueOrDefault("ForceError"), out var force) && force
            };
        }

        public async Task<bool> UpdateChaosSettingsAsync(ChaosSettingsDto settingsDto)
        {
            var settings = new List<SystemSetting>
            {
                new() { SettingKey = "UseIdempotencyKey", SettingValue = settingsDto.UseIdempotencyKey.ToString().ToLower() },
                new() { SettingKey = "ForceError", SettingValue = settingsDto.ForceError.ToString().ToLower() }
            };

            return await _settingsRepository.UpdateSettingsAsync(settings);
        }

        public async Task<bool> ShouldIntroduceFailureAsync()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            var forceError = settings.FirstOrDefault(s => s.SettingKey == "ForceError")?.SettingValue;
            return bool.TryParse(forceError, out var result) && result;
        }

        public async Task<int> GetDelayAsync()
        {
            return await Task.FromResult(0);
        }

        public async Task SimulateNetworkIssueAsync()
        {
            await Task.Delay(100);
        }
    }
}
