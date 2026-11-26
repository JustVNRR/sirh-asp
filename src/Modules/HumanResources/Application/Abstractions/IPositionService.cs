using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.DTOs;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IPositionService
{
    Task<Result<IReadOnlyList<PositionDto>>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<PositionDto>> CreateAsync(
        CreatePositionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PositionDto>> UpdateAsync(
        Guid id,
        UpdatePositionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
