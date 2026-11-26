using System.Globalization;
using System.Net.Mime;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Constants;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Presentation;

public static class MedicalVisitEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MapMedicalVisits(endpoints);
        endpoints.MapNotificationEndpoints();

        return endpoints;
    }

    private static void MapMedicalVisits(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/medical-visits")
            .WithTags("Medical Visits");

        group.MapGet("/", async (
                Guid? employeeId,
                MedicalVisitType? visitType,
                DateTime? fromDate,
                DateTime? toDate,
                bool? carriedOut,
                IMedicalVisitService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20) =>
            {
                var query = new MedicalVisitQuery(employeeId, visitType, fromDate, toDate, carriedOut);
                var pagedRequest = new PagedRequest { Page = page, PageSize = pageSize };
                var result = await service.GetPagedAsync(query, pagedRequest, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<PagedResult<MedicalVisitDto>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IMedicalVisitService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.GetByIdAsync(id, cancellationToken);
                return ToHttpResult(result, httpContext);
            })
            .RequireAuthorization()
            .Produces<MedicalVisitDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/convocation-file", async (
                Guid id,
                IMedicalVisitService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenConvocationFileAsync(id, cancellationToken);
                return ToFileResult(result, download, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/report-file", async (
                Guid id,
                IMedicalVisitService service,
                HttpContext httpContext,
                CancellationToken cancellationToken,
                bool download = false) =>
            {
                var result = await service.OpenReportFileAsync(id, cancellationToken);
                return ToFileResult(result, download, httpContext);
            })
            .RequireAuthorization()
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                HttpContext httpContext,
                IMedicalVisitService service,
                CancellationToken cancellationToken) =>
            {
                if (!httpContext.Request.HasFormContentType)
                {
                    return ToProblem(
                        Error.Validation("Request.InvalidContentType", "Expected multipart/form-data payload."),
                        httpContext);
                }

                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                if (!TryParseRequiredGuid(form["employeeId"].FirstOrDefault(), "employeeId", out var employeeId, out var error) ||
                    !TryParseVisitType(form["visitType"].FirstOrDefault(), out var visitType, out error) ||
                    !TryParseRequiredUtcDateTime(form["visitDateTimeUtc"].FirstOrDefault(), out var visitDateTimeUtc, out error) ||
                    !TryParseOptionalDate(form["validityDate"].FirstOrDefault(), "validityDate", out var validityDate, out error) ||
                    !TryParseOptionalBool(form["carriedOut"].FirstOrDefault(), "carriedOut", defaultValue: false, out var carriedOut, out error))
                {
                    return ToProblem(error!, httpContext);
                }

                var convocationFormFile = form.Files.GetFile("convocationFile");
                if (convocationFormFile is null)
                {
                    return ToProblem(
                        Error.Validation("File.ConvocationRequired", "Convocation file ('convocationFile') is required."),
                        httpContext);
                }

                var reportFormFile = form.Files.GetFile("reportFile");

                await using var convocationStream = convocationFormFile.OpenReadStream();
                await using var reportStream = reportFormFile?.OpenReadStream();

                var command = new CreateMedicalVisitCommand(
                    employeeId,
                    visitType,
                    visitDateTimeUtc,
                    validityDate,
                    form["medicalCenter"].FirstOrDefault(),
                    carriedOut,
                    form["comments"].FirstOrDefault(),
                    new UploadedHealthFileInput(convocationStream, convocationFormFile.FileName, convocationFormFile.ContentType),
                    reportFormFile is not null && reportStream is not null
                        ? new UploadedHealthFileInput(reportStream, reportFormFile.FileName, reportFormFile.ContentType)
                        : null);

                var result = await service.CreateAsync(command, cancellationToken);
                return result.IsSuccess
                    ? Results.Created($"/api/v1/medical-visits/{result.Value.Id}", result.Value)
                    : ToProblem(result.Error, httpContext);
            })
            .DisableAntiforgery()
            .RequireAuthorization(HealthPermissions.MedicalVisitsWrite)
            .Produces<MedicalVisitDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id,
                HttpContext httpContext,
                IMedicalVisitService service,
                CancellationToken cancellationToken) =>
            {
                UpdateMedicalVisitCommand command;
                Stream? convocationStream = null;
                Stream? reportStream = null;

                try
                {
                    if (httpContext.Request.HasFormContentType)
                    {
                        var form = await httpContext.Request.ReadFormAsync(cancellationToken);
                        if (!TryParseVisitType(form["visitType"].FirstOrDefault(), out var visitType, out var error) ||
                            !TryParseRequiredUtcDateTime(form["visitDateTimeUtc"].FirstOrDefault(), out var visitDateTimeUtc, out error) ||
                            !TryParseOptionalDate(form["validityDate"].FirstOrDefault(), "validityDate", out var validityDate, out error) ||
                            !TryParseOptionalBool(form["carriedOut"].FirstOrDefault(), "carriedOut", defaultValue: false, out var carriedOut, out error))
                        {
                            return ToProblem(error!, httpContext);
                        }

                        var convocationFormFile = form.Files.GetFile("convocationFile");
                        UploadedHealthFileInput? convocationInput = null;
                        if (convocationFormFile is not null)
                        {
                            convocationStream = convocationFormFile.OpenReadStream();
                            convocationInput = new UploadedHealthFileInput(
                                convocationStream,
                                convocationFormFile.FileName,
                                convocationFormFile.ContentType);
                        }

                        var reportFormFile = form.Files.GetFile("reportFile");
                        UploadedHealthFileInput? reportInput = null;
                        if (reportFormFile is not null)
                        {
                            reportStream = reportFormFile.OpenReadStream();
                            reportInput = new UploadedHealthFileInput(
                                reportStream,
                                reportFormFile.FileName,
                                reportFormFile.ContentType);
                        }

                        command = new UpdateMedicalVisitCommand(
                            visitType,
                            visitDateTimeUtc,
                            validityDate,
                            form["medicalCenter"].FirstOrDefault(),
                            carriedOut,
                            form["comments"].FirstOrDefault(),
                            convocationInput,
                            reportInput,
                            form["rowVersion"].FirstOrDefault());
                    }
                    else
                    {
                        var json = await httpContext.Request.ReadFromJsonAsync<UpdateMedicalVisitJsonRequest>(cancellationToken);
                        if (json is null)
                        {
                            return ToProblem(
                                Error.Validation("Request.InvalidBody", "Request payload is required."),
                                httpContext);
                        }

                        command = new UpdateMedicalVisitCommand(
                            json.VisitType,
                            json.VisitDateTimeUtc,
                            json.ValidityDate,
                            json.MedicalCenter,
                            json.CarriedOut,
                            json.Comments,
                            ConvocationFile: null,
                            ReportFile: null,
                            json.RowVersion);
                    }

                    var result = await service.UpdateAsync(id, command, cancellationToken);
                    return ToHttpResult(result, httpContext);
                }
                finally
                {
                    if (convocationStream is not null)
                    {
                        await convocationStream.DisposeAsync();
                    }

                    if (reportStream is not null)
                    {
                        await reportStream.DisposeAsync();
                    }
                }
            })
            .DisableAntiforgery()
            .RequireAuthorization(HealthPermissions.MedicalVisitsWrite)
            .Produces<MedicalVisitDto>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (
                Guid id,
                IMedicalVisitService service,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await service.DeleteAsync(id, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error, httpContext);
            })
            .RequireAuthorization(HealthPermissions.MedicalVisitsWrite)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static IResult ToFileResult(
        Result<DownloadedHealthFileDto> result,
        bool download,
        HttpContext httpContext)
    {
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

    private static bool TryParseRequiredGuid(
        string? raw,
        string fieldName,
        out Guid value,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw.Trim(), out value) || value == Guid.Empty)
        {
            value = Guid.Empty;
            error = Error.Validation($"Form.{fieldName}", $"Field '{fieldName}' must be a valid non-empty GUID.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseVisitType(
        string? raw,
        out MedicalVisitType visitType,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            visitType = default;
            error = Error.Validation("Form.visitType", "Field 'visitType' is required.");
            return false;
        }

        if (!Enum.TryParse<MedicalVisitType>(raw.Trim(), ignoreCase: true, out visitType) ||
            !Enum.IsDefined(visitType))
        {
            error = Error.Validation("Form.visitType", $"Invalid medical visit type '{raw}'.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseRequiredUtcDateTime(
        string? raw,
        out DateTime value,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw) ||
            !DateTime.TryParse(
                raw.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out value))
        {
            value = default;
            error = Error.Validation("Form.visitDateTimeUtc", "Field 'visitDateTimeUtc' must be a valid ISO-8601 UTC date-time.");
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

    private static bool TryParseOptionalBool(
        string? raw,
        string fieldName,
        bool defaultValue,
        out bool value,
        out Error? error)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = defaultValue;
            error = null;
            return true;
        }

        if (!bool.TryParse(raw.Trim(), out value))
        {
            value = defaultValue;
            error = Error.Validation($"Form.{fieldName}", $"Field '{fieldName}' must be a boolean ('true' or 'false').");
            return false;
        }

        error = null;
        return true;
    }

    private sealed record UpdateMedicalVisitJsonRequest(
        MedicalVisitType VisitType,
        DateTime VisitDateTimeUtc,
        DateOnly? ValidityDate,
        string? MedicalCenter,
        bool CarriedOut,
        string? Comments,
        string? RowVersion);
}
