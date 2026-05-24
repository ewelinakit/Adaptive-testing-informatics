using System.Text.Json;
using TestSystem.Services.Common.Exceptions;

namespace TestSystem.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var path = context.Request.Path;
        var method = context.Request.Method;
        int statusCode;
        object? errors = null;
        string message;

        switch (exception)
        {
            case ValidationException ve:
                statusCode = 400;
                message = ve.Message;
                errors = ve.Errors;
                _logger.LogWarning("Validation error on {Method} {Path}: {Message} | Errors: {Errors}",
                    method, path, ve.Message, JsonSerializer.Serialize(ve.Errors));
                break;
            case BadRequestException bre:
                statusCode = 400;
                message = bre.Message;
                _logger.LogWarning("Bad request on {Method} {Path}: {Message}",
                    method, path, bre.Message);
                break;
            case NotFoundException nfe:
                statusCode = 404;
                message = nfe.Message;
                _logger.LogWarning("Not found on {Method} {Path}: {Message}",
                    method, path, nfe.Message);
                break;
            case ForbiddenException fe:
                statusCode = 403;
                message = fe.Message;
                _logger.LogWarning("Forbidden on {Method} {Path}: {Message}",
                    method, path, fe.Message);
                break;
            default:
                statusCode = 500;
                message = "Внутрішня помилка сервера";
                _logger.LogError(exception, "Unhandled exception on {Method} {Path}", method, path);
                break;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new { error = message, errors };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
