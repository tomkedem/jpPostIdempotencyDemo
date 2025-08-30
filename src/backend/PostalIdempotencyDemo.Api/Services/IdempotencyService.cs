using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace PostalIdempotencyDemo.Api.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly IIdempotencyRepository _repository;
    private readonly ILogger<IdempotencyService> _logger;
    private readonly TimeSpan _defaultTtl = TimeSpan.FromHours(24);

    public IdempotencyService(IIdempotencyRepository repository, ILogger<IdempotencyService> logger)
    {
        _repository = repository;
        _logger = logger;
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
        var entry = await _repository.GetByKeyAsync(idempotencyKey);
        if (entry != null && entry.ResponseData != null)
        {
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
        var responseData = JsonSerializer.Serialize(response);
        // StatusCode can be set as needed, here using 200 as default
        await _repository.UpdateResponseAsync(idempotencyKey, responseData, 200);
    }
}
