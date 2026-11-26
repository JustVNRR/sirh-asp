using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Application.DTOs;
using Sirh.Modules.Organization.Domain.Constants;

namespace Sirh.Modules.Organization.Presentation;

public static class OrganizationEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapCompanies(endpoints);
        MapLocations(endpoints);
        MapContractTypes(endpoints);
        MapReferentials(endpoints);

        return endpoints;
    }

    private static void MapCompanies(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/companies")
            .WithTags("Companies");

        group.MapGet("/", async (
                bool? active,
                string? search,
                ICompanyService companyService,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await companyService.GetPagedAsync(active, search, pagedRequest, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.CompaniesRead)
            .Produces<PagedResult<CompanyDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ICompanyService companyService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await companyService.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.CompaniesRead)
            .Produces<CompanyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateCompanyRequest request,
                ICompanyService companyService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await companyService.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/companies/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.CompaniesWrite)
            .Produces<CompanyDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateCompanyRequest request,
                ICompanyService companyService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await companyService.UpdateAsync(id, request, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.CompaniesWrite)
            .Produces<CompanyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", async (
                Guid id,
                UpdateCompanyStatusRequest request,
                ICompanyService companyService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await companyService.UpdateStatusAsync(id, request, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.CompaniesWrite)
            .Produces<CompanyDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapLocations(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/locations")
            .WithTags("Locations");

        group.MapGet("/", async (
                bool? published,
                Guid? companyId,
                string? search,
                ILocationService locationService,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await locationService.GetPagedAsync(
                    published,
                    companyId,
                    search,
                    pagedRequest,
                    cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.LocationsRead)
            .Produces<PagedResult<LocationDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ILocationService locationService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await locationService.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.LocationsRead)
            .Produces<LocationDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateLocationRequest request,
                ILocationService locationService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await locationService.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/locations/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.LocationsWrite)
            .Produces<LocationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateLocationRequest request,
                ILocationService locationService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await locationService.UpdateAsync(id, request, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.LocationsWrite)
            .Produces<LocationDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ILocationService locationService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await locationService.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.LocationsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapContractTypes(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/contract-types")
            .WithTags("Contract Types");

        group.MapGet("/", async (
                bool? active,
                string? search,
                IContractTypeService contractTypeService,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await contractTypeService.GetPagedAsync(active, search, pagedRequest, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.ContractTypesRead)
            .Produces<PagedResult<ContractTypeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IContractTypeService contractTypeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await contractTypeService.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.ContractTypesRead)
            .Produces<ContractTypeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateContractTypeRequest request,
                IContractTypeService contractTypeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await contractTypeService.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/contract-types/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.ContractTypesWrite)
            .Produces<ContractTypeDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateContractTypeRequest request,
                IContractTypeService contractTypeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await contractTypeService.UpdateAsync(id, request, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.ContractTypesWrite)
            .Produces<ContractTypeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IContractTypeService contractTypeService,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await contractTypeService.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(OrganizationPermissions.ContractTypesWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapReferentials(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/referentials/departments", () =>
            {
                var items = DepartmentReferential.All
                    .Select(d => new DepartmentDto(d.Code, d.Label))
                    .ToArray();
                return Results.Ok(items);
            })
            .WithTags("Referentials")
            .RequireAuthorization()
            .Produces<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static IResult ToHttpResult<T>(Result<T> result, HttpContext httpContext) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error, httpContext);

    private static IResult ToProblem(Error error, HttpContext httpContext)
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
