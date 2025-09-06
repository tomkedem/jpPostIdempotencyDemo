using PostalIdempotencyDemo.Api.Models.DTO;
using PostalIdempotencyDemo.Api.Repositories;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PostalIdempotencyDemo.Api.Services;

public class MetricsService : IMetricsService
{
    private readonly IMetricsRepository _metricsRepository;
    private readonly ILogger<MetricsService> _logger;
    
    // In-memory metrics for real-time tracking
    private static readonly ConcurrentQueue<double> _responseTimeHistory = new();
    private static readonly ConcurrentDictionary<string, long> _operationCounters = new();
    private static readonly ConcurrentDictionary<string, DateTime> _lastOperationTimes = new();
    
    // Performance tracking
    private static double _averageResponseTime = 0.0;
    private static int _totalOperations = 0;
    private static int _successfulOperations = 0;
    private static int _idempotentBlocks = 0;
    private static int _errorCount = 0;
    private static DateTime _lastResetTime = DateTime.UtcNow;

    public MetricsService(IMetricsRepository metricsRepository, ILogger<MetricsService> logger)
    {
        _metricsRepository = metricsRepository;
        _logger = logger;
    }

    public async Task<MetricsSummaryDto> GetMetricsSummaryAsync()
    {
        var dbSummary = await _metricsRepository.GetMetricsSummaryAsync();
        
        // Combine database metrics with real-time in-memory metrics
        return new MetricsSummaryDto
        {
            TotalOperations = dbSummary.TotalOperations + _totalOperations,
            SuccessfulOperations = dbSummary.SuccessfulOperations + _successfulOperations,
            IdempotentBlocks = dbSummary.IdempotentBlocks + _idempotentBlocks,
            AverageResponseTime = CalculateCurrentAverageResponseTime(),
            SuccessRate = CalculateSuccessRate(dbSummary),
            ErrorCount = dbSummary.ErrorCount + _errorCount,
            LastUpdated = DateTime.UtcNow,
            SystemHealth = CalculateSystemHealth(),
            ThroughputPerMinute = CalculateThroughput(),
            PeakResponseTime = GetPeakResponseTime(),
            MinResponseTime = GetMinResponseTime()
        };
    }

    public void RecordOperation(string operationType, double responseTimeMs, bool wasSuccessful, bool wasIdempotent = false)
    {
        try
        {
            // Record response time
            _responseTimeHistory.Enqueue(responseTimeMs);
            
            // Keep only last 1000 response times for memory efficiency
            while (_responseTimeHistory.Count > 1000)
            {
                _responseTimeHistory.TryDequeue(out _);
            }

            // Update counters
            Interlocked.Increment(ref _totalOperations);
            
            if (wasSuccessful)
            {
                Interlocked.Increment(ref _successfulOperations);
            }
            else
            {
                Interlocked.Increment(ref _errorCount);
            }

            if (wasIdempotent)
            {
                Interlocked.Increment(ref _idempotentBlocks);
            }

            // Update operation-specific counters
            _operationCounters.AddOrUpdate(operationType, 1, (key, value) => value + 1);
            _lastOperationTimes[operationType] = DateTime.UtcNow;

            // Recalculate average response time
            RecalculateAverageResponseTime();

            _logger.LogDebug("Recorded operation: {OperationType}, ResponseTime: {ResponseTime}ms, Success: {Success}, Idempotent: {Idempotent}",
                operationType, responseTimeMs, wasSuccessful, wasIdempotent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording operation metrics");
        }
    }

    public void ResetMetrics()
    {
        _responseTimeHistory.Clear();
        _operationCounters.Clear();
        _lastOperationTimes.Clear();
        
        _totalOperations = 0;
        _successfulOperations = 0;
        _idempotentBlocks = 0;
        _errorCount = 0;
        _averageResponseTime = 0.0;
        _lastResetTime = DateTime.UtcNow;
        
        _logger.LogInformation("Metrics reset at {ResetTime}", _lastResetTime);
    }

    public Dictionary<string, object> GetRealTimeMetrics()
    {
        return new Dictionary<string, object>
        {
            ["currentResponseTime"] = GetCurrentResponseTime(),
            ["operationsPerSecond"] = CalculateOperationsPerSecond(),
            ["systemLoad"] = CalculateSystemLoad(),
            ["memoryUsage"] = GC.GetTotalMemory(false) / (1024 * 1024), // MB
            ["activeConnections"] = _operationCounters.Count,
            ["uptime"] = DateTime.UtcNow - _lastResetTime,
            ["healthStatus"] = CalculateSystemHealth()
        };
    }

    private double CalculateCurrentAverageResponseTime()
    {
        if (_responseTimeHistory.IsEmpty)
            return 0.0;

        var times = _responseTimeHistory.ToArray();
        return times.Length > 0 ? times.Average() : 0.0;
    }

    private double CalculateSuccessRate(MetricsSummaryDto dbSummary)
    {
        var totalOps = dbSummary.TotalOperations + _totalOperations;
        var successOps = dbSummary.SuccessfulOperations + _successfulOperations;
        
        return totalOps > 0 ? (double)successOps / totalOps * 100 : 100.0;
    }

    private string CalculateSystemHealth()
    {
        var successRate = CalculateSuccessRate(new MetricsSummaryDto());
        var avgResponseTime = _averageResponseTime;
        
        if (successRate >= 99 && avgResponseTime < 100)
            return "Excellent";
        else if (successRate >= 95 && avgResponseTime < 500)
            return "Good";
        else if (successRate >= 90 && avgResponseTime < 1000)
            return "Fair";
        else
            return "Poor";
    }

    private double CalculateThroughput()
    {
        var timeSpan = DateTime.UtcNow - _lastResetTime;
        return timeSpan.TotalMinutes > 0 ? _totalOperations / timeSpan.TotalMinutes : 0;
    }

    private double GetPeakResponseTime()
    {
        return _responseTimeHistory.IsEmpty ? 0.0 : _responseTimeHistory.Max();
    }

    private double GetMinResponseTime()
    {
        return _responseTimeHistory.IsEmpty ? 0.0 : _responseTimeHistory.Min();
    }

    private double GetCurrentResponseTime()
    {
        return _responseTimeHistory.IsEmpty ? 0.0 : _responseTimeHistory.Last();
    }

    private double CalculateOperationsPerSecond()
    {
        var timeSpan = DateTime.UtcNow - _lastResetTime;
        return timeSpan.TotalSeconds > 0 ? _totalOperations / timeSpan.TotalSeconds : 0;
    }

    private double CalculateSystemLoad()
    {
        // Simple load calculation based on recent activity
        var recentOperations = _operationCounters.Values.Sum();
        return Math.Min(recentOperations / 100.0, 1.0) * 100; // Normalize to percentage
    }

    private void RecalculateAverageResponseTime()
    {
        if (!_responseTimeHistory.IsEmpty)
        {
            var times = _responseTimeHistory.ToArray();
            _averageResponseTime = times.Length > 0 ? times.Average() : 0.0;
        }
    }
}
