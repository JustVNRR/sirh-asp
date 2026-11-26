namespace Sirh.BuildingBlocks.Application.Contracts;

public interface ILocationReferenceChecker
{
    Task<bool> IsLocationReferencedAsync(Guid locationId, CancellationToken cancellationToken = default);
}

public interface IContractTypeReferenceChecker
{
    Task<bool> IsContractTypeReferencedAsync(Guid contractTypeId, CancellationToken cancellationToken = default);
}

public interface IPositionReferenceChecker
{
    Task<bool> IsPositionReferencedAsync(Guid positionId, CancellationToken cancellationToken = default);
}
