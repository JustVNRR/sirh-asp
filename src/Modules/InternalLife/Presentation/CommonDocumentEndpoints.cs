using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Presentation;

public static class CommonDocumentEndpoints
{
    public static IEndpointRouteBuilder MapInternalLifeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapCommonDocuments(endpoints);

        return endpoints;
    }

    private static void MapCommonDocuments(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/common-documents")
            .WithTags("Common Documents");

        group.MapGet("/", async (
                Guid? companyId,
                CommonDocumentCategory? category,
                bool? isWelcomeKit,
                Guid? contractTypeId,
                bool? archived,
                string? search,
                ICommonDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var query = new CommonDocumentQuery(companyId, category, isWelcomeKit, contractTypeId, archived, search);
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(query, pagedRequest, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<PagedResult<CommonDocumentDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                ICommonDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<CommonDocumentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/file", async (
                Guid id,
                ICommonDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenFileAsync(id, cancellationToken);
                if (result.IsFailure)
                {
                    return ToProblem(result.Error, httpContext);
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
                ICommonDocumentService service,
                CancellationToken cancellationToken) =>
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    return ToProblem(
                        Error.Validation("Request.InvalidContentType", "Expected multipart/form-data payload."),
                        httpContext);
                }

                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                if (!TryParseOptionalGuid(form["companyId"].FirstOrDefault(), "companyId", out var companyId, out var error) ||
                    !TryParseOptionalGuid(form["contractTypeId"].FirstOrDefault(), "contractTypeId", out var contractTypeId, out error) ||
                    !TryParseCategory(form["category"].FirstOrDefault(), out var category, out error) ||
                    !TryParseOptionalBool(form["isWelcomeKit"].FirstOrDefault(), "isWelcomeKit", out var isWelcomeKit, out error) ||
                    !TryParseOptionalDate(form["fileDate"].FirstOrDefault(), "fileDate", out var fileDate, out error))
                {
                    return ToProblem(error!, httpContext);
                }

                var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (formFile is null)
                {
                    return ToProblem(
                        Error.Validation("File.Required", "A file ('file') is required."),
                        httpContext);
                }

                await using var fileStream = formFile.OpenReadStream();
                var command = new CreateCommonDocumentCommand(
                    companyId,
                    contractTypeId,
                    category,
                    isWelcomeKit,
                    form["title"].FirstOrDefault() ?? string.Empty,
                    form["description"].FirstOrDefault(),
                    fileDate,
                    new UploadedCommonFileInput(fileStream, formFile.FileName, formFile.ContentType));

                var result = await service.CreateAsync(command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/common-documents/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .DisableAntiforgery()
            .RequireAuthorization()
            .Produces<CommonDocumentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                ICommonDocumentService service,
                CancellationToken cancellationToken) =>
            {
                UpdateCommonDocumentCommand command;
                Stream? fileStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        if (!TryParseOptionalGuid(form["companyId"].FirstOrDefault(), "companyId", out var companyId, out var error) ||
                            !TryParseOptionalGuid(form["contractTypeId"].FirstOrDefault(), "contractTypeId", out var contractTypeId, out error) ||
                            !TryParseCategory(form["category"].FirstOrDefault(), out var category, out error) ||
                            !TryParseOptionalBool(form["isWelcomeKit"].FirstOrDefault(), "isWelcomeKit", out var isWelcomeKit, out error) ||
                            !TryParseOptionalDate(form["fileDate"].FirstOrDefault(), "fileDate", out var fileDate, out error))
                        {
                            return ToProblem(error!, httpContext);
                        }

                        var formFile = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                        UploadedCommonFileInput? fileInput = null;
                        if (formFile is not null)
                        {
                            fileStream = formFile.OpenReadStream();
                            fileInput = new UploadedCommonFileInput(fileStream, formFile.FileName, formFile.ContentType);
                        }

                        command = new UpdateCommonDocumentCommand(
                            companyId,
                            contractTypeId,
                            category,
                            isWelcomeKit,
                            form["title"].FirstOrDefault() ?? string.Empty,
                            form["description"].FirstOrDefault(),
                            fileDate,
                            fileInput,
                            form["rowVersion"].FirstOrDefault());
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<UpdateCommonDocumentJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new UpdateCommonDocumentCommand(
                            json.CompanyId,
                            json.ContractTypeId,
                            json.Category,
                            json.IsWelcomeKit,
                            json.Title,
                            json.Description,
                            json.FileDate,
                            File: null,
                            json.RowVersion);
                    }

                    var result = await service.UpdateAsync(id, command, cancellationToken);
                    return ToHttpResult(result, httpContext);
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
            .Produces<CommonDocumentDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id:guid}/archive", async (
                Guid id,
                ArchiveCommonDocumentRequest request,
                ICommonDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.ArchiveAsync(id, request, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                ICommonDocumentService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
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

    private static bool TryParseCategory(
        string? raw,
        out CommonDocumentCategory category,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            category = default;
            error = Error.Validation("Form.category", "Field 'category' is required.");
            return false;
        }

        if (!Enum.TryParse<CommonDocumentCategory>(raw.Trim(), ignoreCase: true, out category) ||
            !Enum.IsDefined(category))
        {
            error = Error.Validation("Form.category", $"Invalid document category '{raw}'.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseOptionalBool(
        string? raw,
        string fieldName,
        out bool? value,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            error = null;
            return true;
        }

        if (!bool.TryParse(raw.Trim(), out var parsed))
        {
            value = null;
            error = Error.Validation($"Form.{fieldName}", $"Field '{fieldName}' must be a boolean ('true' or 'false').");
            return false;
        }

        value = parsed;
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

    private sealed record UpdateCommonDocumentJsonRequest(
        Guid? CompanyId,
        Guid? ContractTypeId,
        CommonDocumentCategory Category,
        bool? IsWelcomeKit,
        string Title,
        string? Description,
        DateOnly? FileDate,
        string? RowVersion);
}
