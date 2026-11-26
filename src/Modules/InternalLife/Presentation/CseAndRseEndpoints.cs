using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Constants;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Presentation;

internal static class CseAndRseEndpoints
{
    public static IEndpointRouteBuilder MapCseAndRseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapStaffRepresentatives(endpoints);
        MapRseIdeas(endpoints);

        return endpoints;
    }

    private static void MapStaffRepresentatives(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/staff-representatives")
            .WithTags("Staff Representatives");

        group.MapGet("/", async (
                Guid? companyId,
                StaffRepresentativeRole? role,
                bool? harassmentAdvisor,
                string? search,
                IStaffRepresentativeService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(
                    companyId,
                    role,
                    harassmentAdvisor,
                    search,
                    pagedRequest,
                    cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.StaffRepsRead)
            .Produces<PagedResult<StaffRepresentativeDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IStaffRepresentativeService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.StaffRepsRead)
            .Produces<StaffRepresentativeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                CreateStaffRepresentativeRequest request,
                IStaffRepresentativeService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/staff-representatives/{result.Value.Id}", result.Value)
                    : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.StaffRepsWrite)
            .Produces<StaffRepresentativeDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                UpdateStaffRepresentativeRequest request,
                IStaffRepresentativeService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.UpdateAsync(id, request, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.StaffRepsWrite)
            .Produces<StaffRepresentativeDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IStaffRepresentativeService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.StaffRepsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapRseIdeas(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/rse-ideas")
            .WithTags("RSE Ideas");

        group.MapGet("/", async (
                Guid? locationId,
                RseIdeaStatus? status,
                Guid? authorId,
                bool? mineOnly,
                string? search,
                IRseIdeaService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var query = new RseIdeaQuery(locationId, status, authorId, mineOnly, search);
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(query, pagedRequest, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<PagedResult<RseIdeaDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IRseIdeaService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<RseIdeaDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/file", async (
                Guid id,
                IRseIdeaService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenAttachmentAsync(id, cancellationToken);
                if (result.IsFailure)
                {
                    return CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
                }

                var contentDisposition = new ContentDisposition
                {
                    Inline = !download,
                    FileName = result.Value.FileName
                };

                httpContext.Response.Headers.ContentDisposition = contentDisposition.ToString();
                return Results.Stream(result.Value.Content, result.Value.ContentType);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                HttpContext httpContext,
                IRseIdeaService service,
                CancellationToken cancellationToken) =>
            {
                CreateRseIdeaCommand command;
                Stream? fileStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        if (!TryParseOptionalGuid(form["locationId"].FirstOrDefault(), "locationId", out var locationId, out var error))
                        {
                            return CommonDocumentEndpoints.ToProblem(error!, httpContext);
                        }

                        var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                        UploadedCommonFileInput? attachment = null;
                        if (formFile is not null)
                        {
                            fileStream = formFile.OpenReadStream();
                            attachment = new UploadedCommonFileInput(fileStream, formFile.FileName, formFile.ContentType);
                        }

                        command = new CreateRseIdeaCommand(
                            locationId,
                            form["title"].FirstOrDefault() ?? string.Empty,
                            form["ideaContent"].FirstOrDefault() ?? string.Empty,
                            attachment);
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<CreateRseIdeaJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return CommonDocumentEndpoints.ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new CreateRseIdeaCommand(
                            json.LocationId,
                            json.Title,
                            json.IdeaContent,
                            Attachment: null);
                    }

                    var result = await service.CreateAsync(command, cancellationToken);
                    return result.IsSuccess
                        ? Results.Created($"/api/v1/rse-ideas/{result.Value.Id}", result.Value)
                        : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
                }
                finally
                {
                    if (fileStream is not null)
                    {
                        await fileStream.DisposeAsync();
                    }
                }
            })
            .DisableAntiforgery()
            .RequireAuthorization(InternalLifePermissions.RseWrite)
            .Produces<RseIdeaDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                IRseIdeaService service,
                CancellationToken cancellationToken) =>
            {
                UpdateRseIdeaCommand command;
                Stream? fileStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        if (!TryParseOptionalGuid(form["locationId"].FirstOrDefault(), "locationId", out var locationId, out var error))
                        {
                            return CommonDocumentEndpoints.ToProblem(error!, httpContext);
                        }

                        var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                        UploadedCommonFileInput? attachment = null;
                        if (formFile is not null)
                        {
                            fileStream = formFile.OpenReadStream();
                            attachment = new UploadedCommonFileInput(fileStream, formFile.FileName, formFile.ContentType);
                        }

                        command = new UpdateRseIdeaCommand(
                            locationId,
                            form["title"].FirstOrDefault() ?? string.Empty,
                            form["ideaContent"].FirstOrDefault() ?? string.Empty,
                            attachment,
                            form["rowVersion"].FirstOrDefault());
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<UpdateRseIdeaJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return CommonDocumentEndpoints.ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new UpdateRseIdeaCommand(
                            json.LocationId,
                            json.Title,
                            json.IdeaContent,
                            Attachment: null,
                            json.RowVersion);
                    }

                    var result = await service.UpdateAsync(id, command, cancellationToken);
                    return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
                }
                finally
                {
                    if (fileStream is not null)
                    {
                        await fileStream.DisposeAsync();
                    }
                }
            })
            .DisableAntiforgery()
            .RequireAuthorization(InternalLifePermissions.RseWrite)
            .Produces<RseIdeaDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/status", async (
                Guid id,
                TreatRseIdeaRequest request,
                IRseIdeaService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.TreatAsync(id, request, cancellationToken);
                return CommonDocumentEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization(InternalLifePermissions.RseTreat)
            .Produces<RseIdeaDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IRseIdeaService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : CommonDocumentEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static bool TryParseOptionalGuid(
        string? raw,
        string fieldName,
        out Guid? value,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            error = null;
            return true;
        }

        if (!Guid.TryParse(raw.Trim(), out var parsed) || parsed == Guid.Empty)
        {
            value = null;
            error = Error.Validation($"Form.{fieldName}", $"Field '{fieldName}' must be a valid non-empty GUID.");
            return false;
        }

        value = parsed;
        error = null;
        return true;
    }

    private sealed record CreateRseIdeaJsonRequest(
        Guid? LocationId,
        string Title,
        string IdeaContent);

    private sealed record UpdateRseIdeaJsonRequest(
        Guid? LocationId,
        string Title,
        string IdeaContent,
        string? RowVersion);
}
