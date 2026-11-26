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

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly IdentityDbContext _dbContext;

    public PasswordResetTokenRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PasswordResetToken?> GetLatestByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        _dbContext.PasswordResetTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<PasswordResetToken?> GetLatestUsableByUserIdAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default) =>
        _dbContext.PasswordResetTokens
            .Where(t => t.UserId == userId
                        && t.UsedAtUtc == null
                        && t.InvalidatedAtUtc == null
                        && t.FailedAttempts < PasswordResetToken.MaxFailedAttempts
                        && t.ExpiresAtUtc > utcNow)
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        await _dbContext.PasswordResetTokens.AddAsync(token, cancellationToken);
    }

    public async Task InvalidateActiveForUserAsync(
        Guid userId,
        DateTime invalidatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAtUtc == null && t.InvalidatedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Invalidate(invalidatedAtUtc);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}

public sealed class RolePermissionRepository : IRolePermissionRepository
{
    private readonly IdentityDbContext _dbContext;

    public RolePermissionRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ApplicationRole>> GetAllRolesWithPermissionsAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ApplicationRole>> GetRolesByNamesOrCodesAsync(
        IReadOnlyCollection<string> roleIdentifiers,
        CancellationToken cancellationToken = default)
    {
        if (roleIdentifiers.Count == 0)
        {
            return [];
        }

        var upperIdentifiers = roleIdentifiers
            .Select(r => r.ToUpperInvariant())
            .Distinct()
            .ToArray();

        return await _dbContext.Roles
            .AsNoTracking()
            .Where(r => (r.NormalizedName != null && upperIdentifiers.Contains(r.NormalizedName))
                        || upperIdentifiers.Contains(r.Code.ToUpper()))
            .ToListAsync(cancellationToken);
    }

    public Task<ApplicationRole?> GetRoleWithPermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetAllPermissionsAsync(
        CancellationToken cancellationToken = default) =>
        await _dbContext.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Permission>> GetPermissionsByCodesAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        if (codes.Count == 0)
        {
            return [];
        }

        var normalizedCodes = codes
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = await _dbContext.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return permissions
            .Where(p => normalizedCodes.Contains(p.Code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    public async Task ReplaceRolePermissionsAsync(
        Guid roleId,
        IReadOnlyCollection<Guid> permissionIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        _dbContext.RolePermissions.RemoveRange(existing);

        var replacements = permissionIds
            .Distinct()
            .Select(permissionId => new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });

        await _dbContext.RolePermissions.AddRangeAsync(replacements, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
