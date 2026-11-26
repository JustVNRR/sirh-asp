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
