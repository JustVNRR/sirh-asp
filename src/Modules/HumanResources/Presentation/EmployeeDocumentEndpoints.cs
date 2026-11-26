using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Enums;

namespace Sirh.Modules.HumanResources.Presentation;

internal static class EmployeeDocumentEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/employee-documents")
            .WithTags("Employee Documents");

        group.MapGet("/", async (
                Guid? employeeId,
                EmployeeDocumentCategory? category,
                string? documentTypes,
                string? search,
                IEmployeeDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                if (!TryParseDocumentTypes(documentTypes, out var parsedTypes, out var parseError))
                {
                    return EmployeeEndpoints.ToProblem(parseError!, httpContext);
                }

                var query = new EmployeeDocumentQuery(employeeId, category, parsedTypes, search);
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(query, pagedRequest, cancellationToken);
                return EmployeeEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<PagedResult<EmployeeDocumentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IEmployeeDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return EmployeeEndpoints.ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<EmployeeDocumentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/file", async (
                Guid id,
                IEmployeeDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenFileAsync(id, cancellationToken);
                if (result.IsFailure)
                {
                    return EmployeeEndpoints.ToProblem(result.Error, httpContext);
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
                IEmployeeDocumentService service,
                CancellationToken cancellationToken) =>
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    return EmployeeEndpoints.ToProblem(
                        Error.Validation("Request.InvalidContentType", "Expected multipart/form-data payload."),
                        httpContext);
                }

                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                if (!TryParseOptionalGuid(form["employeeId"].FirstOrDefault(), "employeeId", out var employeeId, out var error) ||
                    !TryParseOptionalCategory(form["category"].FirstOrDefault(), out var category, out error) ||
                    !TryParseDocumentType(form["documentType"].FirstOrDefault(), out var documentType, out error) ||
                    !TryParseOptionalDate(form["fileDate"].FirstOrDefault(), "fileDate", out var fileDate, out error) ||
                    !TryParseOptionalDate(form["expiryDate"].FirstOrDefault(), "expiryDate", out var expiryDate, out error))
                {
                    return EmployeeEndpoints.ToProblem(error!, httpContext);
                }

                var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (formFile is null)
                {
                    return EmployeeEndpoints.ToProblem(
                        Error.Validation("File.Required", "A file ('file') is required."),
                        httpContext);
                }

                await using var fileStream = formFile.OpenReadStream();
                var command = new CreateEmployeeDocumentCommand(
                    employeeId,
                    category,
                    documentType,
                    fileDate,
                    form["commentary"].FirstOrDefault(),
                    expiryDate,
                    form["workplaceAccommodation"].FirstOrDefault(),
                    form["permitNumber"].FirstOrDefault(),
                    new UploadedDocumentFileInput(fileStream, formFile.FileName, formFile.ContentType));

                var result = await service.CreateAsync(command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/employee-documents/{result.Value.Id}", result.Value)
                    : EmployeeEndpoints.ToProblem(result.Error, httpContext);
            })
            .DisableAntiforgery()
            .RequireAuthorization()
            .Produces<EmployeeDocumentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                IEmployeeDocumentService service,
                CancellationToken cancellationToken) =>
            {
                UpdateEmployeeDocumentCommand command;
                Stream? fileStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        if (!TryParseOptionalCategory(form["category"].FirstOrDefault(), out var category, out var error) ||
                            !TryParseDocumentType(form["documentType"].FirstOrDefault(), out var documentType, out error) ||
                            !TryParseOptionalDate(form["fileDate"].FirstOrDefault(), "fileDate", out var fileDate, out error) ||
                            !TryParseOptionalDate(form["expiryDate"].FirstOrDefault(), "expiryDate", out var expiryDate, out error))
                        {
                            return EmployeeEndpoints.ToProblem(error!, httpContext);
                        }

                        var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                        UploadedDocumentFileInput? fileInput = null;
                        if (formFile is not null)
                        {
                            fileStream = formFile.OpenReadStream();
                            fileInput = new UploadedDocumentFileInput(fileStream, formFile.FileName, formFile.ContentType);
                        }

                        command = new UpdateEmployeeDocumentCommand(
                            category,
                            documentType,
                            fileDate,
                            form["commentary"].FirstOrDefault(),
                            expiryDate,
                            form["workplaceAccommodation"].FirstOrDefault(),
                            form["permitNumber"].FirstOrDefault(),
                            fileInput,
                            form["rowVersion"].FirstOrDefault());
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<UpdateEmployeeDocumentJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return EmployeeEndpoints.ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new UpdateEmployeeDocumentCommand(
                            json.Category,
                            json.DocumentType,
                            json.FileDate,
                            json.Commentary,
                            json.ExpiryDate,
                            json.WorkplaceAccommodation,
                            json.PermitNumber,
                            File: null,
                            json.RowVersion);
                    }

                    var result = await service.UpdateAsync(id, command, cancellationToken);
                    return EmployeeEndpoints.ToHttpResult(result, httpContext);
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
            .RequireAuthorization()
            .Produces<EmployeeDocumentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IEmployeeDocumentService service,
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

        return endpoints;
    }

    private static bool TryParseDocumentTypes(
        string? raw,
        out IReadOnlyCollection<EmployeeDocumentType>? parsedTypes,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            parsedTypes = null;
            error = null;
            return true;
        }

        var list = new List<EmployeeDocumentType>();
        var tokens = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (!Enum.TryParse<EmployeeDocumentType>(token, ignoreCase: true, out var value) ||
                !Enum.IsDefined(value))
            {
                parsedTypes = null;
                error = Error.Validation("Query.documentTypes", $"Invalid document type '{token}'.");
                return false;
            }

            list.Add(value);
        }

        parsedTypes = list;
        error = null;
        return true;
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

    private static bool TryParseOptionalCategory(
        string? raw,
        out EmployeeDocumentCategory? category,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            category = null;
            error = null;
            return true;
        }

        if (!Enum.TryParse<EmployeeDocumentCategory>(raw.Trim(), ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
        {
            category = null;
            error = Error.Validation("Form.category", $"Invalid document category '{raw}'.");
            return false;
        }

        category = parsed;
        error = null;
        return true;
    }

    private static bool TryParseDocumentType(
        string? raw,
        out EmployeeDocumentType documentType,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            documentType = default;
            error = Error.Validation("Form.documentType", "Field 'documentType' is required.");
            return false;
        }

        if (!Enum.TryParse<EmployeeDocumentType>(raw.Trim(), ignoreCase: true, out documentType) ||
            !Enum.IsDefined(documentType))
        {
            error = Error.Validation("Form.documentType", $"Invalid document type '{raw}'.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseOptionalDate(
        string? raw,
        string fieldName,
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
            error = Error.Validation($"Form.{fieldName}", $"Field '{fieldName}' must be a valid ISO date (YYYY-MM-DD).");
            return false;
        }

        date = parsed;
        error = null;
        return true;
    }

    private sealed record UpdateEmployeeDocumentJsonRequest(
        EmployeeDocumentCategory? Category,
        EmployeeDocumentType DocumentType,
        DateOnly? FileDate,
        string? Commentary,
        DateOnly? ExpiryDate,
        string? WorkplaceAccommodation,
        string? PermitNumber,
        string? RowVersion);
}
