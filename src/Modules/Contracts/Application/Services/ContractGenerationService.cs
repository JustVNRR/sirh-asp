using FluentValidation;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.BuildingBlocks.Domain.Text;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Entities;
using Sirh.Modules.Contracts.Domain.Enums;

namespace Sirh.Modules.Contracts.Application.Services;

public sealed class ContractGenerationService(
    IContractRepository contractRepository,
    IContractModelRepository modelRepository,
    IOrganizationLookup organizationLookup,
    IHumanResourcesLookup humanResourcesLookup,
    IContractTemplateEngine templateEngine,
    IPdfContractRenderer pdfRenderer,
    IFileStorageService fileStorageService,
    IDateTimeProvider dateTimeProvider,
    IValidator<GenerateContractRequest> validator) : IContractGenerationService
{
    public async Task<Result<ContractDto>> GenerateAsync(
        GenerateContractRequest request,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        if (await contractRepository.ExistsByPositionIdAsync(request.PositionId, cancellationToken))
        {
            return Error.Conflict(
                "Contracts.PositionAlreadyHasContract",
                $"Position '{request.PositionId}' already has an associated contract.");
        }

        var model = await modelRepository.GetByIdAsync(request.ContractModelId, cancellationToken);
        if (model is null)
        {
            return Error.NotFound(
                "ContractModels.NotFound",
                $"Contract model '{request.ContractModelId}' was not found.");
        }

        if (!model.IsActive)
        {
            return Error.Validation(
                "ContractModels.Inactive",
                $"Contract model '{model.Title}' is inactive and cannot be used to generate a contract.");
        }

        if (!await organizationLookup.ContractTypeExistsAsync(request.ContractTypeId, cancellationToken))
        {
            return Error.NotFound(
                "ContractTypes.NotFound",
                $"Contract type '{request.ContractTypeId}' was not found.");
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

        var fileName = $"contrat-{SlugGenerator.Generate(model.Title)}-{contextResult.Value.PositionContext.PositionSlug}.pdf";
        var subFolder = $"employees/{contextResult.Value.PositionContext.EmployeeId}/contracts";

        await using var pdfStream = new MemoryStream(pdfBytes, writable: false);
        var stored = await fileStorageService.SaveAsync(
            StorageScope.EmployeeSensitive,
            subFolder,
            fileName,
            "application/pdf",
            pdfStream,
            cancellationToken);

        var contract = new Contract(
            request.PositionId,
            contextResult.Value.PositionContext.EmployeeId,
            request.ContractTypeId,
            model.Id,
            ContractStatus.Draft,
            signedAt: null,
            stored.FileKey,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            dateTimeProvider.TodayUtc,
            request.Commentary);

        await contractRepository.AddAsync(contract, cancellationToken);
        await contractRepository.SaveChangesAsync(cancellationToken);

        return new ContractDto(
            contract.Id,
            contract.PositionId,
            contract.EmployeeId,
            contract.ContractTypeId,
            contract.ContractModelId,
            contract.Status,
            contract.SignedAt,
            contract.FileName,
            contract.ContentType,
            contract.FileSizeBytes,
            contract.FileDate,
            contract.Commentary,
            contract.CreatedAtUtc,
            contract.UpdatedAtUtc,
            Convert.ToBase64String(contract.RowVersion));
    }
}
