using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PostalIdempotencyDemo.Api.Services
{
    public class IdempotencyService : IIdempotencyService
    {
        private readonly IIdempotencyRepository _repository;
        private readonly ILogger<IdempotencyService> _logger;
        private readonly IChaosService _chaosService;

        public IdempotencyService(IIdempotencyRepository repository, ILogger<IdempotencyService> logger, IChaosService chaosService)
        {
            _repository = repository;
            _logger = logger;
            _chaosService = chaosService;
        }

        public string GenerateIdempotencyKey(string requestContent)
        {
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(requestContent));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        public async Task<IdempotencyEntry?> GetIdempotencyEntryAsync(string idempotencyKey)
        {
            return await _repository.GetByKeyAsync(idempotencyKey);
        }

        public async Task<bool> StoreIdempotencyEntryAsync(IdempotencyEntry entry)
        {
            var success = await _repository.CreateAsync(entry);
            if (success)
            {
                _logger.LogInformation("Saved idempotency entry for key {IdempotencyKey}", entry.IdempotencyKey);
            }
            else
            {
                _logger.LogError("Failed to save idempotency entry for key {IdempotencyKey}", entry.IdempotencyKey);
            }
            return success;
        }

        public async Task CleanupExpiredEntriesAsync()
        {
            var success = await _repository.DeleteExpiredAsync();
            if (success)
            {
                _logger.LogInformation("Cleaned up expired idempotency entries");
            }
        }

        public async Task<(object? cachedResponse, IdempotencyEntry? entry)> GetCachedResponseAsync(string idempotencyKey)
        {
            var chaosSettings = await _chaosService.GetChaosSettingsAsync();
            if (idempotencyKey == null || !chaosSettings.UseIdempotencyKey)
            {
                return (null, null); // Idempotency is disabled or no key, bypass cache check
            }

            var entry = await _repository.GetByKeyAsync(idempotencyKey);
            if (entry != null && entry.ResponseData != null)
            {
                // Check if entry has expired based on configurable expiration time
                var expirationHours = await _chaosService.GetIdempotencyExpirationHoursAsync();
                var expirationTime = entry.CreatedAt.AddHours(expirationHours);
                
                if (DateTime.UtcNow > expirationTime)
                {
                    _logger.LogInformation("Idempotency entry for key {IdempotencyKey} has expired after {ExpirationHours} hours", 
                        idempotencyKey, expirationHours);
                    return (null, null);
                }

                try
                {
                    var cachedResponse = JsonSerializer.Deserialize<object>(entry.ResponseData);
                    return (cachedResponse, entry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize cached response for key {IdempotencyKey}", idempotencyKey);
                }
            }
            return (null, entry);
        }

        public async Task CacheResponseAsync(string idempotencyKey, object response)
        {
            var chaosSettings = await _chaosService.GetChaosSettingsAsync();
            if (idempotencyKey == null || !chaosSettings.UseIdempotencyKey) return; // Do not cache if disabled

            var responseData = JsonSerializer.Serialize(response);
            // StatusCode can be set as needed, here using 200 as default
            await _repository.UpdateResponseAsync(idempotencyKey, responseData, 200);
        }

        public async Task<IdempotencyEntry?> GetLatestEntryByCorrelationIdAsync(string correlationId)
        {
            return await _repository.GetLatestByCorrelationIdAsync(correlationId);
        }
    }
}
