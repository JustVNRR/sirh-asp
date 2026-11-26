using System.Text.Json;
using FluentValidation;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.BuildingBlocks.Domain.Text;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Constants;
using Sirh.Modules.Contracts.Domain.Entities;

namespace Sirh.Modules.Contracts.Application.Services;

public sealed class ContractModelService(
    IContractModelRepository modelRepository,
    IOrganizationLookup organizationLookup,
    IHumanResourcesLookup humanResourcesLookup,
    IContractTemplateEngine templateEngine,
    IPdfContractRenderer pdfRenderer,
    IValidator<CreateContractModelRequest> createValidator,
    IValidator<UpdateContractModelRequest> updateValidator,
    IValidator<PreviewContractRequest> previewValidator) : IContractModelService
{
    public IReadOnlyList<TemplateVariableDto> GetVariables() => ContractTemplateVariables.All;

    public async Task<Result<PagedResult<ContractModelDto>>> GetPagedAsync(
        bool? isActive,
        Guid? contractTypeId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var paged = await modelRepository.GetPagedAsync(
            isActive,
            contractTypeId,
            search,
            pagedRequest,
            cancellationToken);

        var items = paged.Items.Select(ToDto).ToArray();
        return PagedResult<ContractModelDto>.Create(items, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<Result<ContractModelDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var model = await modelRepository.GetByIdAsync(id, cancellationToken);
        return model is null
            ? Error.NotFound("ContractModels.NotFound", $"Contract model '{id}' was not found.")
            : ToDto(model);
    }

    public async Task<Result<ContractModelDto>> CreateAsync(
        CreateContractModelRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var syntax = templateEngine.ValidateSyntax(request.TemplateContent);
        if (!syntax.IsValid)
        {
            return Error.Validation(
                "ContractModels.InvalidTemplateSyntax",
                $"Template Scriban syntax error: {string.Join(" | ", syntax.Errors)}");
        }

        if (request.ContractTypeId.HasValue &&
            !await organizationLookup.ContractTypeExistsAsync(request.ContractTypeId.Value, cancellationToken))
        {
            return Error.NotFound(
                "ContractTypes.NotFound",
                $"Contract type '{request.ContractTypeId.Value}' was not found.");
        }

        var title = request.Title.Trim();
        if (await modelRepository.ExistsByTitleAsync(title, null, cancellationToken))
        {
            return Error.Conflict(
                "ContractModels.DuplicateTitle",
                $"A contract model titled '{title}' already exists.");
        }

        var customVarsJson = SerializeCustomVariables(request.CustomVariables);
        var model = new ContractModel(
            title,
            request.Description,
            request.ContractTypeId,
            request.TemplateContent,
            customVarsJson,
            request.IsActive);

        await modelRepository.AddAsync(model, cancellationToken);
        await modelRepository.SaveChangesAsync(cancellationToken);

        return ToDto(model);
    }

    public async Task<Result<ContractModelDto>> UpdateAsync(
        Guid id,
        UpdateContractModelRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var model = await modelRepository.GetByIdAsync(id, cancellationToken);
        if (model is null)
        {
            return Error.NotFound("ContractModels.NotFound", $"Contract model '{id}' was not found.");
        }

        var concurrency = ContractRenderContextBuilder.ValidateRowVersion(request.RowVersion, model.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        var syntax = templateEngine.ValidateSyntax(request.TemplateContent);
        if (!syntax.IsValid)
        {
            return Error.Validation(
                "ContractModels.InvalidTemplateSyntax",
                $"Template Scriban syntax error: {string.Join(" | ", syntax.Errors)}");
        }

        if (request.ContractTypeId.HasValue &&
            !await organizationLookup.ContractTypeExistsAsync(request.ContractTypeId.Value, cancellationToken))
        {
            return Error.NotFound(
                "ContractTypes.NotFound",
                $"Contract type '{request.ContractTypeId.Value}' was not found.");
        }

        var title = request.Title.Trim();
        if (await modelRepository.ExistsByTitleAsync(title, id, cancellationToken))
        {
            return Error.Conflict(
                "ContractModels.DuplicateTitle",
                $"A contract model titled '{title}' already exists.");
        }

        var customVarsJson = SerializeCustomVariables(request.CustomVariables);
        model.Update(
            title,
            request.Description,
            request.ContractTypeId,
            request.TemplateContent,
            customVarsJson,
            request.IsActive);

        await modelRepository.SaveChangesAsync(cancellationToken);
        return ToDto(model);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var model = await modelRepository.GetByIdAsync(id, cancellationToken);
        if (model is null)
        {
            return Result.Failure(Error.NotFound("ContractModels.NotFound", $"Contract model '{id}' was not found."));
        }

        modelRepository.Remove(model);
        await modelRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PreviewPdfResult>> PreviewAsync(
        Guid modelId,
        PreviewContractRequest request,
        CancellationToken cancellationToken = default)
    {
        await previewValidator.ValidateAndThrowAsync(request, cancellationToken);

        var model = await modelRepository.GetByIdAsync(modelId, cancellationToken);
        if (model is null)
        {
            return Error.NotFound("ContractModels.NotFound", $"Contract model '{modelId}' was not found.");
        }

        var contextResult = await ContractRenderContextBuilder.BuildAsync(
            request.PositionId,
            request.CustomVariables,
            humanResourcesLookup,
            organizationLookup,
            cancellationToken);
        if (contextResult.IsFailure)
        {
            return contextResult.Error;
        }

        var renderedResult = templateEngine.Render(model.TemplateContent, contextResult.Value.RenderModel);
        if (renderedResult.IsFailure)
        {
            return renderedResult.Error;
        }

        var pdfBytes = pdfRenderer.RenderPdf(
            model.Title,
            contextResult.Value.Company.Name,
            renderedResult.Value);

        var fileName = $"preview-{SlugGenerator.Generate(model.Title)}-{contextResult.Value.PositionContext.PositionSlug}.pdf";
        return new PreviewPdfResult(pdfBytes, fileName);
    }

    private static string? SerializeCustomVariables(IReadOnlyList<string>? customVariables)
    {
        if (customVariables is null || customVariables.Count == 0)
        {
            return null;
        }

        var cleaned = customVariables
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return cleaned.Length == 0 ? null : JsonSerializer.Serialize(cleaned);
    }

    private static IReadOnlyList<string> DeserializeCustomVariables(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }

    private static ContractModelDto ToDto(ContractModel model) =>
        new(
            model.Id,
            model.Title,
            model.Description,
            model.ContractTypeId,
            model.TemplateContent,
            DeserializeCustomVariables(model.CustomVariablesJson),
            model.IsActive,
            model.CreatedAtUtc,
            model.UpdatedAtUtc,
            Convert.ToBase64String(model.RowVersion));
}
