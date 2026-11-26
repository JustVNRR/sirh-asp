using Microsoft.EntityFrameworkCore;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Infrastructure.Persistence;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public RefreshTokenRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _dbContext.RefreshTokens.FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public async Task RevokeFamilyAsync(
        string familyId,
        DateTime revokedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(revokedAtUtc, reason);
        }
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        DateTime revokedAtUtc,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.Revoke(revokedAtUtc, reason);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class PermissionRepository : IPermissionRepository
{
    private readonly IdentityDbContext _dbContext;

    public PermissionRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<string>> GetPermissionsForRolesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        if (roleNames.Count == 0)
        {
            return [];
        }

        var normalizedNames = roleNames
            .Select(r => r.ToUpperInvariant())
            .Distinct()
            .ToArray();

        return await _dbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.Role.NormalizedName != null && normalizedNames.Contains(rp.Role.NormalizedName))
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToArrayAsync(cancellationToken);
    }
}
