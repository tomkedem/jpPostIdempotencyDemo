namespace PostalIdempotencyDemo.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationIdHeaderName = "X-Correlation-ID";

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrGenerateCorrelationId(context);
        
        // Add to response headers
        if (!context.Response.Headers.ContainsKey(CorrelationIdHeaderName))
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
        }
        
        // Add to HttpContext for use in controllers/services
        context.Items["CorrelationId"] = correlationId;
        
        await _next(context);
    }

    private static string GetOrGenerateCorrelationId(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();
        
        if (string.IsNullOrEmpty(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }
        
        return correlationId;
    }
}
