using System.Diagnostics;

namespace TestSystem.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        await _next(context);
        sw.Stop();

        var statusCode = context.Response.StatusCode;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (statusCode >= 400)
        {
            _logger.LogWarning("{Method} {Path} → {StatusCode} ({Duration}ms) | User: {UserId}",
                method, path, statusCode, sw.ElapsedMilliseconds, userId ?? "anonymous");
        }
        else
        {
            _logger.LogInformation("{Method} {Path} → {StatusCode} ({Duration}ms) | User: {UserId}",
                method, path, statusCode, sw.ElapsedMilliseconds, userId ?? "anonymous");
        }
    }
}
