using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;

namespace Sirh.Modules.Identity.Presentation;

public static class AuthEndpoints
{
    public const string AuthRateLimitPolicy = "AuthRateLimit";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        group.MapPost("/login", async (
                LoginRequest request,
                IAuthService authService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var result = await authService.LoginAsync(request, ip, userAgent, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status423Locked)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (
                RefreshTokenRequest request,
                IAuthService authService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var result = await authService.RefreshAsync(request, ip, userAgent, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/logout", async (
                LogoutRequest request,
                ICurrentUser currentUser,
                IAuthService authService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
                {
                    return Results.Unauthorized();
                }

                var result = await authService.LogoutAsync(currentUser.UserId.Value, request, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout-all", async (
                ICurrentUser currentUser,
                IAuthService authService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
                {
                    return Results.Unauthorized();
                }

                var result = await authService.LogoutAllAsync(currentUser.UserId.Value, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (
                ICurrentUser currentUser,
                IRoleManagementService roleManagementService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
                {
                    return Results.Unauthorized();
                }

                var result = await roleManagementService.GetCurrentUserProfileAsync(
                    currentUser.UserId.Value,
                    cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<CurrentUserProfileResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/password/change", async (
                ChangePasswordRequest request,
                ICurrentUser currentUser,
                IPasswordService passwordService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                if (!currentUser.IsAuthenticated || !currentUser.UserId.HasValue)
                {
                    return Results.Unauthorized();
                }

                var result = await passwordService.ChangePasswordAsync(
                    currentUser.UserId.Value,
                    request,
                    cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password/forgot", async (
                ForgotPasswordRequest request,
                IPasswordService passwordService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString();
                var result = await passwordService.ForgotPasswordAsync(request, ip, cancellationToken);
                return result.IsSuccess ? Results.Accepted() : ToProblem(result.Error, httpContext);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/password/reset", async (
                ResetPasswordRequest request,
                IPasswordService passwordService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await passwordService.ResetPasswordAsync(request, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .AllowAnonymous()
            .RequireRateLimiting(AuthRateLimitPolicy)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        endpoints.MapRoleEndpoints();

        return endpoints;
    }

    internal static IResult ToHttpResult<T>(Result<T> result, HttpContext httpContext) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error, httpContext);

    internal static IResult ToProblem(Error error, HttpContext httpContext)
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
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        return Results.Problem(problem);
    }
}
