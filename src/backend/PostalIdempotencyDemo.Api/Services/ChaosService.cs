using PostalIdempotencyDemo.Api.Services.Interfaces;

namespace PostalIdempotencyDemo.Api.Services;

public class ChaosService : IChaosService
{
    private readonly IConfiguration _configuration;
    private readonly Random _random = new();

    public ChaosService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<bool> ShouldIntroduceFailureAsync()
    {
        var enabled = _configuration.GetValue<bool>("ChaosEngineering:Enabled");
        if (!enabled) return Task.FromResult(false);

        var failureRate = _configuration.GetValue<double>("ChaosEngineering:DefaultFailureRate");
        return Task.FromResult(_random.NextDouble() < failureRate);
    }

    public Task<int> GetDelayAsync()
    {
        var enabled = _configuration.GetValue<bool>("ChaosEngineering:Enabled");
        if (!enabled) return Task.FromResult(0);

        var defaultDelay = _configuration.GetValue<int>("ChaosEngineering:DefaultDelayMs");
        var maxDelay = _configuration.GetValue<int>("ChaosEngineering:MaxDelayMs");
        
        if (defaultDelay > 0)
        {
            return Task.FromResult(_random.Next(0, Math.Min(defaultDelay * 2, maxDelay)));
        }

        return Task.FromResult(0);
    }

    public async Task SimulateNetworkIssueAsync()
    {
        if (await ShouldIntroduceFailureAsync())
        {
            var delay = await GetDelayAsync();
            if (delay > 0)
            {
                await Task.Delay(delay);
            }
            
            // Randomly throw network-related exceptions
            var exceptionType = _random.Next(0, 3);
            switch (exceptionType)
            {
                case 0:
                    throw new TimeoutException("Simulated network timeout");
                case 1:
                    throw new HttpRequestException("Simulated network error");
                case 2:
                    throw new InvalidOperationException("Simulated connection failure");
            }
        }
    }
}
