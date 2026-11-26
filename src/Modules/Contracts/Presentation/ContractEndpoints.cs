using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Constants;

namespace Sirh.Modules.Contracts.Presentation;

public static class ContractEndpoints
{
    public static IEndpointRouteBuilder MapContractsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapContractModels(endpoints);
        MapContractGeneration(endpoints);
        endpoints.MapContractManagementAndJobSheetEndpoints();

        return endpoints;
    }

    private static void MapContractModels(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/contract-models")
            .WithTags("Contract Models");

        group.MapGet("/variables", (IContractModelService service) =>
                Results.Ok(service.GetVariables()))
            .RequireAuthorization(ContractPermissions.ContractModelsRead)
            .Produces<IReadOnlyList<TemplateVariableDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/", async (
                bool? active,
                Guid? contractTypeId,
                string? search,
                IContractModelService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(active, contractTypeId, search, pagedRequest, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractModelsRead)
            .Produces<PagedResult<ContractModelDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IContractModelService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractModelsRead)
            .Produces<ContractModelDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateContractModelRequest request,
                IContractModelService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/contract-models/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractModelsWrite)
            .Produces<ContractModelDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateContractModelRequest request,
                IContractModelService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.UpdateAsync(id, request, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractModelsWrite)
            .Produces<ContractModelDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IContractModelService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractModelsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/preview", async (
                Guid id,
                PreviewContractRequest request,
                IContractModelService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.PreviewAsync(id, request, cancellationToken);
                return result.IsSuccess
                    ? Results.File(result.Value.Content, "application/pdf", result.Value.FileName)
                    : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractModelsRead)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapContractGeneration(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/contracts/generate", async (
                GenerateContractRequest request,
                IContractGenerationService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GenerateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/contracts/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .WithTags("Contracts")
            .RequireAuthorization(ContractPermissions.ContractsWrite)
            .Produces<ContractDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
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
            ErrorType.UnprocessableEntity => StatusCodes.Status422UnprocessableEntity,
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
