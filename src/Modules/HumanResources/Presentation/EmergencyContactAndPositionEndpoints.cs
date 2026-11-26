using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;

namespace Sirh.Modules.HumanResources.Presentation;

public static class EmergencyContactAndPositionEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyContactAndPositionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapEmergencyContacts(endpoints);
        MapPositions(endpoints);

        return endpoints;
    }

    private static void MapEmergencyContacts(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/employees/{employeeId:guid}/emergency-contacts", async (
                Guid employeeId,
                IEmergencyContactService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByEmployeeIdAsync(employeeId, cancellationToken);
                return EmployeeEndpoints.ToHttpResult(result, httpContext);
            })
            .WithTags("Emergency Contacts")
            .RequireAuthorization()
            .Produces<IReadOnlyList<EmergencyContactDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/v1/employees/{employeeId:guid}/emergency-contacts", async (
                Guid employeeId,
                CreateEmergencyContactRequest request,
                IEmergencyContactService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(employeeId, request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created(
                        $"/api/v1/employees/{employeeId}/emergency-contacts/{result.Value.Id}",
                        result.Value)
                    : EmployeeEndpoints.ToProblem(result.Error, httpContext);
            })
            .WithTags("Emergency Contacts")
            .RequireAuthorization()
            .Produces<EmergencyContactDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var group = endpoints.MapGroup("/api/v1/emergency-contacts")
            .WithTags("Emergency Contacts");

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateEmergencyContactRequest request,
                IEmergencyContactService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.UpdateAsync(id, request, cancellationToken);
                return EmployeeEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<EmergencyContactDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IEmergencyContactService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : EmployeeEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapPositions(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/employees/{employeeId:guid}/positions", async (
                Guid employeeId,
                IPositionService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByEmployeeIdAsync(employeeId, cancellationToken);
                return EmployeeEndpoints.ToHttpResult(result, httpContext);
            })
            .WithTags("Positions")
            .RequireAuthorization()
            .Produces<IReadOnlyList<PositionDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var group = endpoints.MapGroup("/api/v1/positions")
            .WithTags("Positions");

        group.MapPost("/", async (
                CreatePositionRequest request,
                IPositionService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/positions/{result.Value.Id}", result.Value)
                    : EmployeeEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HrPermissions.PositionsWrite)
            .Produces<PositionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdatePositionRequest request,
                IPositionService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.UpdateAsync(id, request, cancellationToken);
                return EmployeeEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(HrPermissions.PositionsWrite)
            .Produces<PositionDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IPositionService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : EmployeeEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HrPermissions.PositionsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
