using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Constants;

namespace Sirh.Modules.InternalLife.Presentation;

internal static class OnboardingQuizEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingQuizEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/onboarding-quizzes")
            .WithTags("Onboarding Quizzes");

        group.MapGet("/", async (
                Guid? companyId,
                bool? published,
                string? search,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(companyId, published, search, pagedRequest, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesRead)
            .Produces<PagedResult<OnboardingQuizSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesRead)
            .Produces<OnboardingQuizDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateOnboardingQuizRequest request,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/onboarding-quizzes/{result.Value.Id}", result.Value)
                    : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesWrite)
            .Produces<OnboardingQuizDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateOnboardingQuizRequest request,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.UpdateAsync(id, request, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesWrite)
            .Produces<OnboardingQuizDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/publish", async (
                Guid id,
                PublishOnboardingQuizRequest request,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.SetPublishedAsync(id, request, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesWrite)
            .Produces<OnboardingQuizDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/attempts", async (
                Guid id,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.StartAttemptAsync(id, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/onboarding-quizzes/{id}/attempts", result.Value)
                    : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesTake)
            .Produces<StartedQuizAttemptDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/attempts/{attemptId:guid}/submit", async (
                Guid attemptId,
                SubmitQuizAttemptRequest request,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.SubmitAttemptAsync(attemptId, request, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesTake)
            .Produces<QuizAttemptResultDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}/attempts", async (
                Guid id,
                Guid? employeeId,
                bool? passed,
                IOnboardingQuizService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetAttemptsAsync(id, employeeId, passed, pagedRequest, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.QuizzesRead)
            .Produces<PagedResult<QuizAttemptSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
