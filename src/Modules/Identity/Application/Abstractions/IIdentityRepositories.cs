using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(
        string familyId,
        DateTime revokedAtUtc,
        string reason,
        CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(
        Guid userId,
        DateTime revokedAtUtc,
        string reason,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPermissionRepository
{
    Task<IReadOnlyCollection<string>> GetPermissionsForRolesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default);
}

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<ApplicationRole>> GetAllRolesWithPermissionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationRole>> GetRolesByNamesOrCodesAsync(
        IReadOnlyCollection<string> roleIdentifiers,
        CancellationToken cancellationToken = default);

    Task<ApplicationRole?> GetRoleWithPermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Permission>> GetPermissionsByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default);

    Task ReplaceRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
