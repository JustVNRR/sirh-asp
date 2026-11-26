using Microsoft.AspNetCore.Mvc;
using Sirh.BuildingBlocks.Domain.Results;

namespace Sirh.Api.Infrastructure;

public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result, HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return MapError(result.Error, httpContext);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, HttpContext? httpContext = null)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return MapError(result.Error, httpContext);
    }

    private static IResult MapError(Error error, HttpContext? httpContext)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Locked => StatusCodes.Status423Locked,
            _ => StatusCodes.Status500InternalServerError
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = error.Code,
            Detail = error.Message,
            Instance = httpContext?.Request.Path
        };

        if (httpContext is not null)
        {
            problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        }

        return Results.Problem(problem);
    }
}
