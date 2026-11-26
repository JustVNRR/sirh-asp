using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.DTOs;

namespace Sirh.Modules.InternalLife.Application.Abstractions;

public interface ICommonDocumentService
{
    Task<Result<PagedResult<CommonDocumentDto>>> GetPagedAsync(
        CommonDocumentQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default);

    Task<Result<CommonDocumentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<DownloadedCommonFileDto>> OpenFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Result<CommonDocumentDto>> CreateAsync(
        CreateCommonDocumentCommand command,
        CancellationToken cancellationToken = default);

    Task<Result<CommonDocumentDto>> UpdateAsync(
        Guid id,
        UpdateCommonDocumentCommand command,
        CancellationToken cancellationToken = default);

    Task<Result> ArchiveAsync(
        Guid id,
        ArchiveCommonDocumentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
