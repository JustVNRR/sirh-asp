using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Constants;
using Sirh.Modules.Contracts.Domain.Enums;

namespace Sirh.Modules.Contracts.Presentation;

internal static class ContractManagementAndJobSheetEndpoints
{
    public static IEndpointRouteBuilder MapContractManagementAndJobSheetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapContracts(endpoints);
        MapJobSheets(endpoints);

        return endpoints;
    }

    private static void MapContracts(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/contracts")
            .WithTags("Contracts");

        group.MapGet("/", async (
                Guid? employeeId,
                ContractStatus? status,
                Guid? contractTypeId,
                IContractManagementService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(employeeId, status, contractTypeId, pagedRequest, cancellationToken);
                return ContractEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<PagedResult<ContractDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/by-position/{positionId:guid}", async (
                Guid positionId,
                IContractManagementService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByPositionIdAsync(positionId, cancellationToken);
                return ContractEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<ContractDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/file", async (
                Guid id,
                IContractManagementService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenFileAsync(id, cancellationToken);
                return ToFileResult(result, download, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                HttpContext httpContext,
                IContractManagementService service,
                CancellationToken cancellationToken) =>
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    return ContractEndpoints.ToProblem(
                        Error.Validation("Request.InvalidContentType", "Expected multipart/form-data payload."),
                        httpContext);
                }

                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                if (!TryParseGuidField(form, "positionId", out var positionId, out var error) ||
                    !TryParseGuidField(form, "contractTypeId", out var contractTypeId, out error) ||
                    !TryParseContractStatus(form["status"].FirstOrDefault(), ContractStatus.Draft, out var status, out error) ||
                    !TryParseOptionalDate(form["signedAt"].FirstOrDefault(), out var signedAt, out error))
                {
                    return ContractEndpoints.ToProblem(error!, httpContext);
                }

                var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (formFile is null)
                {
                    return ContractEndpoints.ToProblem(
                        Error.Validation("File.Required", "A PDF file ('file') is required."),
                        httpContext);
                }

                await using var fileStream = formFile.OpenReadStream();
                var command = new ImportContractCommand(
                    positionId,
                    contractTypeId,
                    status,
                    signedAt,
                    form["commentary"].FirstOrDefault(),
                    new UploadedPdfInput(fileStream, formFile.FileName, formFile.ContentType));

                var result = await service.ImportAsync(command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/contracts/{result.Value.Id}", result.Value)
                    : ContractEndpoints.ToProblem(result.Error, httpContext);
            })
            .DisableAntiforgery()
            .RequireAuthorization(ContractPermissions.ContractsWrite)
            .Produces<ContractDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                IContractManagementService service,
                CancellationToken cancellationToken) =>
            {
                UpdateContractCommand command;
                Stream? fileStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        if (!TryParseGuidField(form, "contractTypeId", out var contractTypeId, out var error) ||
                            !TryParseContractStatus(form["status"].FirstOrDefault(), ContractStatus.Draft, out var status, out error, required: true) ||
                            !TryParseOptionalDate(form["signedAt"].FirstOrDefault(), out var signedAt, out error))
                        {
                            return ContractEndpoints.ToProblem(error!, httpContext);
                        }

                        var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                        UploadedPdfInput? fileInput = null;
                        if (formFile is not null)
                        {
                            fileStream = formFile.OpenReadStream();
                            fileInput = new UploadedPdfInput(fileStream, formFile.FileName, formFile.ContentType);
                        }

                        command = new UpdateContractCommand(
                            contractTypeId,
                            status,
                            signedAt,
                            form["commentary"].FirstOrDefault(),
                            fileInput,
                            form["rowVersion"].FirstOrDefault());
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<UpdateContractJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return ContractEndpoints.ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new UpdateContractCommand(
                            json.ContractTypeId,
                            json.Status,
                            json.SignedAt,
                            json.Commentary,
                            File: null,
                            json.RowVersion);
                    }

                    var result = await service.UpdateAsync(id, command, cancellationToken);
                    return ContractEndpoints.ToHttpResult(result, httpContext);
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
            .RequireAuthorization(ContractPermissions.ContractsWrite)
            .Produces<ContractDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IContractManagementService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ContractEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(ContractPermissions.ContractsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapJobSheets(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/job-sheets")
            .WithTags("Job Sheets");

        group.MapGet("/by-position/{positionId:guid}", async (
                Guid positionId,
                IJobSheetService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByPositionIdAsync(positionId, cancellationToken);
                return ContractEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<JobSheetDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/file", async (
                Guid id,
                IJobSheetService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenFileAsync(id, cancellationToken);
                return ToFileResult(result, download, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                HttpContext httpContext,
                IJobSheetService service,
                CancellationToken cancellationToken) =>
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    return ContractEndpoints.ToProblem(
                        Error.Validation("Request.InvalidContentType", "Expected multipart/form-data payload."),
                        httpContext);
                }

                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                if (!TryParseGuidField(form, "positionId", out var positionId, out var error))
                {
                    return ContractEndpoints.ToProblem(error!, httpContext);
                }

                var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (formFile is null)
                {
                    return ContractEndpoints.ToProblem(
                        Error.Validation("File.Required", "A PDF file ('file') is required."),
                        httpContext);
                }

                await using var fileStream = formFile.OpenReadStream();
                var command = new CreateJobSheetCommand(
                    positionId,
                    form["commentary"].FirstOrDefault(),
                    new UploadedPdfInput(fileStream, formFile.FileName, formFile.ContentType));

                var result = await service.CreateAsync(command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/job-sheets/{result.Value.Id}", result.Value)
                    : ContractEndpoints.ToProblem(result.Error, httpContext);
            })
            .DisableAntiforgery()
            .RequireAuthorization(ContractPermissions.JobSheetsWrite)
            .Produces<JobSheetDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                IJobSheetService service,
                CancellationToken cancellationToken) =>
            {
                UpdateJobSheetCommand command;
                Stream? fileStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                        UploadedPdfInput? fileInput = null;
                        if (formFile is not null)
                        {
                            fileStream = formFile.OpenReadStream();
                            fileInput = new UploadedPdfInput(fileStream, formFile.FileName, formFile.ContentType);
                        }

                        command = new UpdateJobSheetCommand(
                            form["commentary"].FirstOrDefault(),
                            fileInput,
                            form["rowVersion"].FirstOrDefault());
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<UpdateJobSheetJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return ContractEndpoints.ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new UpdateJobSheetCommand(
                            json.Commentary,
                            File: null,
                            json.RowVersion);
                    }

                    var result = await service.UpdateAsync(id, command, cancellationToken);
                    return ContractEndpoints.ToHttpResult(result, httpContext);
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
            .RequireAuthorization(ContractPermissions.JobSheetsWrite)
            .Produces<JobSheetDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IJobSheetService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ContractEndpoints.ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(ContractPermissions.JobSheetsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static IResult ToFileResult(
        Result<DownloadedFileDto> result,
        bool download,
        HttpContext httpContext)
    {
        if (result.IsFailure)
        {
            return ContractEndpoints.ToProblem(result.Error, httpContext);
        }

        var contentDisposition = new ContentDisposition
        {
            Inline = !download,
            FileName = result.Value.FileName
        };

        httpContext.Response.Headers.ContentDisposition = contentDisposition.ToString();
        return Results.Stream(result.Value.Content, result.Value.ContentType);
    }

    private static bool TryParseGuidField(
        IFormCollection form,
        string fieldName,
        out Guid value,
        out Error? error)
    {
        var raw = form[fieldName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out value) || value == Guid.Empty)
        {
            value = Guid.Empty;
            error = Error.Validation($"Form.{fieldName}", $"Field '{fieldName}' must be a valid non-empty GUID.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseContractStatus(
        string? raw,
        ContractStatus defaultStatus,
        out ContractStatus status,
        out Error? error,
        bool required = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (required)
            {
                status = default;
                error = Error.Validation("Form.status", "Field 'status' is required.");
                return false;
            }

            status = defaultStatus;
            error = null;
            return true;
        }

        if (!Enum.TryParse<ContractStatus>(raw.Trim(), ignoreCase: true, out status) ||
            !Enum.IsDefined(status))
        {
            error = Error.Validation("Form.status", $"Invalid contract status '{raw}'.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseOptionalDate(
        string? raw,
        out DateOnly? date,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            date = null;
            error = null;
            return true;
        }

        if (!DateOnly.TryParse(raw.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = null;
            error = Error.Validation("Form.signedAt", "Field 'signedAt' must be a valid ISO date (YYYY-MM-DD).");
            return false;
        }

        date = parsed;
        error = null;
        return true;
    }

    private sealed record UpdateContractJsonRequest(
        Guid ContractTypeId,
        ContractStatus Status,
        DateOnly? SignedAt,
        string? Commentary,
        string? RowVersion);

    private sealed record UpdateJobSheetJsonRequest(
        string? Commentary,
        string? RowVersion);
}
