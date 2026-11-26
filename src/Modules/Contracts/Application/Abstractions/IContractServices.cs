using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Constants;

namespace Sirh.Modules.Contracts.Application.Abstractions;

public interface IContractModelService
{
    IReadOnlyList<TemplateVariableDto> GetVariables();

    Task<Result<PagedResult<ContractModelDto>>> GetPagedAsync(
        bool? isActive,
        Guid? contractTypeId,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<ContractModelDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<ContractModelDto>> CreateAsync(
        CreateContractModelRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<ContractModelDto>> UpdateAsync(
        Guid id,
        UpdateContractModelRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<PreviewPdfResult>> PreviewAsync(
        Guid modelId,
        PreviewContractRequest request,
        CancellationToken cancellationToken = default);
}

public interface IContractGenerationService
{
    Task<Result<ContractDto>> GenerateAsync(
        GenerateContractRequest request,
        CancellationToken cancellationToken = default);
}
