using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;

namespace Sirh.Modules.HumanResources.Presentation;

public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapHumanResourcesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/employees")
            .WithTags("Employees");

        group.MapGet("/", async (
                Guid? companyId,
                Guid? managerId,
                string? department,
                bool? active,
                bool? inPosition,
                string? role,
                string? search,
                IEmployeeService employeeService,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var query = new EmployeeQuery(companyId, managerId, department, active, inPosition, role, search);
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await employeeService.GetPagedAsync(query, pagedRequest, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<PagedResult<EmployeeSummaryDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IEmployeeService employeeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await employeeService.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<EmployeeDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateEmployeeRequest request,
                IEmployeeService employeeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await employeeService.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/employees/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HrPermissions.EmployeesWrite)
            .Produces<EmployeeDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateEmployeeRequest request,
                IEmployeeService employeeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await employeeService.UpdateAsync(id, request, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(HrPermissions.EmployeesWrite)
            .Produces<EmployeeDetailDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (
                Guid id,
                IEmployeeService employeeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await employeeService.DeactivateAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HrPermissions.EmployeesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", async (
                Guid id,
                IEmployeeService employeeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await employeeService.ActivateAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HrPermissions.EmployeesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapEmergencyContactAndPositionEndpoints();
        endpoints.MapEmployeeDocumentEndpoints();

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
