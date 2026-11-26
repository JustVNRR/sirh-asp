using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.DTOs;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface IRseIdeaService
{
    Task<Result<PagedResult<RseIdeaDto>>> GetPagedAsync(
        RseIdeaQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<RseIdeaDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DownloadedCommonFileDto>> OpenAttachmentAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<RseIdeaDto>> CreateAsync(
        CreateRseIdeaCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<RseIdeaDto>> UpdateAsync(
        Guid id,
        UpdateRseIdeaCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<RseIdeaDto>> TreatAsync(
        Guid id,
        TreatRseIdeaRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
