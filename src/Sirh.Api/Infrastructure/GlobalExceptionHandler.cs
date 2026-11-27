using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Sirh.Api.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        IHostEnvironment environment,
        ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _environment = environment;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, errors) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                SanitizeForLog(httpContext.Request.Method),
                SanitizeForLog(httpContext.Request.Path.Value));
        }
        else
        {
            _logger.LogWarning("Handled exception ({StatusCode}) on {Method} {Path}: {Message}",
                statusCode,
                SanitizeForLog(httpContext.Request.Method),
                SanitizeForLog(httpContext.Request.Path.Value),
                SanitizeForLog(exception.Message));
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }

    private (int StatusCode, string Title, string Detail, IDictionary<string, string[]>? Errors) MapException(
        Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                "One or more validation errors occurred.",
                validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray())
            ),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "The resource was modified or deleted by another request. Refresh and retry.",
                null
            ),
            UnauthorizedAccessException unauthorizedEx => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                unauthorizedEx.Message,
                null
            ),
            FileNotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                notFoundEx.Message,
                null
            ),
            ArgumentException or InvalidOperationException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                exception.Message,
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred. Please contact support with the traceId.",
                null
            )
        };
    }

    private static string SanitizeForLog(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace("\r", string.Empty, StringComparison.Ordinal)
                .Replace("\n", string.Empty, StringComparison.Ordinal);
}
