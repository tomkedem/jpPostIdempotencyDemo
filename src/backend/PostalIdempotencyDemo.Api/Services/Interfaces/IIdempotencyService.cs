using PostalIdempotencyDemo.Api.Models;

namespace PostalIdempotencyDemo.Api.Services.Interfaces
{
    public interface IIdempotencyService
    {
        Task<IdempotencyEntry?> GetLatestEntryByCorrelationIdAsync(string correlationId);
        Task<IdempotencyEntry?> GetIdempotencyEntryAsync(string idempotencyKey);
        Task<bool> StoreIdempotencyEntryAsync(IdempotencyEntry entry);
        Task CleanupExpiredEntriesAsync();
        string GenerateIdempotencyKey(string requestContent);
        Task<(object? cachedResponse, IdempotencyEntry? entry)> GetCachedResponseAsync(string idempotencyKey);
        Task CacheResponseAsync(string idempotencyKey, object response);
    }
}
