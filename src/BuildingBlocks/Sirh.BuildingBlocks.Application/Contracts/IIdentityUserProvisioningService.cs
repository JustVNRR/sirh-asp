using Sirh.BuildingBlocks.Domain.Results;

namespace Sirh.BuildingBlocks.Application.Contracts;

public sealed record ProvisionedUserDto(Guid UserId, IReadOnlyList<string> RoleCodes);

public interface IIdentityUserProvisioningService
{
    Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> ResolveRoleCodesAsync(
        IReadOnlyCollection<string> roleInputs,
        CancellationToken cancellationToken = default);

    Task<Result<ProvisionedUserDto>> CreateUserAsync(
        string email,
        string password,
        Guid employeeId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<string>>> UpdateUserAsync(
        Guid userId,
        string newEmail,
        IReadOnlyCollection<string>? roleCodes,
        CancellationToken cancellationToken = default);

    Task<Result> DeactivateUserAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<Result> ActivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRoleCodesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(
        string roleInput,
        CancellationToken cancellationToken = default);
}
