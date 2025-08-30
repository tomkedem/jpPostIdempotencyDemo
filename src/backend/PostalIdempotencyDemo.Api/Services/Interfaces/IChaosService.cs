namespace PostalIdempotencyDemo.Api.Services.Interfaces;

public interface IChaosService
{
    Task<bool> ShouldIntroduceFailureAsync();
    Task<int> GetDelayAsync();
    Task SimulateNetworkIssueAsync();
}
