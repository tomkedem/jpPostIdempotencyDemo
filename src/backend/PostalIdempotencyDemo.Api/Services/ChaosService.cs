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
                ForceError = bool.TryParse(settingsDict.GetValueOrDefault("ForceError"), out var force) && force,
                IdempotencyExpirationHours = int.TryParse(settingsDict.GetValueOrDefault("IdempotencyExpirationHours"), out var hours) ? hours : 24,
                MaxRetryAttempts = int.TryParse(settingsDict.GetValueOrDefault("MaxRetryAttempts"), out var retries) ? retries : 3,
                DefaultTimeoutSeconds = int.TryParse(settingsDict.GetValueOrDefault("DefaultTimeoutSeconds"), out var timeout) ? timeout : 30,
                EnableMetricsCollection = bool.TryParse(settingsDict.GetValueOrDefault("EnableMetricsCollection"), out var metrics) && metrics,
                MetricsRetentionDays = int.TryParse(settingsDict.GetValueOrDefault("MetricsRetentionDays"), out var retention) ? retention : 30,
                EnableChaosMode = bool.TryParse(settingsDict.GetValueOrDefault("EnableChaosMode"), out var chaos) && chaos,
                SystemMaintenanceMode = bool.TryParse(settingsDict.GetValueOrDefault("SystemMaintenanceMode"), out var maintenance) && maintenance
            };
        }

        public async Task<bool> UpdateChaosSettingsAsync(ChaosSettingsDto settingsDto)
        {
            var settings = new List<SystemSetting>
            {
                new() { SettingKey = "UseIdempotencyKey", SettingValue = settingsDto.UseIdempotencyKey.ToString().ToLower() },
                new() { SettingKey = "ForceError", SettingValue = settingsDto.ForceError.ToString().ToLower() },
                new() { SettingKey = "IdempotencyExpirationHours", SettingValue = settingsDto.IdempotencyExpirationHours.ToString() },
                new() { SettingKey = "MaxRetryAttempts", SettingValue = settingsDto.MaxRetryAttempts.ToString() },
                new() { SettingKey = "DefaultTimeoutSeconds", SettingValue = settingsDto.DefaultTimeoutSeconds.ToString() },
                new() { SettingKey = "EnableMetricsCollection", SettingValue = settingsDto.EnableMetricsCollection.ToString().ToLower() },
                new() { SettingKey = "MetricsRetentionDays", SettingValue = settingsDto.MetricsRetentionDays.ToString() },
                new() { SettingKey = "EnableChaosMode", SettingValue = settingsDto.EnableChaosMode.ToString().ToLower() },
                new() { SettingKey = "SystemMaintenanceMode", SettingValue = settingsDto.SystemMaintenanceMode.ToString().ToLower() }
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
            var settings = await _settingsRepository.GetSettingsAsync();
            var timeoutSetting = settings.FirstOrDefault(s => s.SettingKey == "DefaultTimeoutSeconds")?.SettingValue;
            return int.TryParse(timeoutSetting, out var timeout) ? timeout * 1000 : 30000; // Convert to milliseconds
        }

        public async Task SimulateNetworkIssueAsync()
        {
            var delay = await GetDelayAsync();
            await Task.Delay(Math.Min(delay / 10, 1000)); // Use 1/10th of timeout or max 1 second
        }

        public async Task<int> GetIdempotencyExpirationHoursAsync()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            var expirationSetting = settings.FirstOrDefault(s => s.SettingKey == "IdempotencyExpirationHours")?.SettingValue;
            return int.TryParse(expirationSetting, out var hours) ? hours : 24;
        }

        public async Task<bool> IsMaintenanceModeAsync()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            var maintenanceMode = settings.FirstOrDefault(s => s.SettingKey == "SystemMaintenanceMode")?.SettingValue;
            return bool.TryParse(maintenanceMode, out var result) && result;
        }

        public async Task<int> GetMaxRetryAttemptsAsync()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            var retrySetting = settings.FirstOrDefault(s => s.SettingKey == "MaxRetryAttempts")?.SettingValue;
            return int.TryParse(retrySetting, out var retries) ? retries : 3;
        }

        public async Task<int> GetDefaultTimeoutSecondsAsync()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            var timeoutSetting = settings.FirstOrDefault(s => s.SettingKey == "DefaultTimeoutSeconds")?.SettingValue;
            return int.TryParse(timeoutSetting, out var timeout) ? timeout : 30;
        }
    }
}
