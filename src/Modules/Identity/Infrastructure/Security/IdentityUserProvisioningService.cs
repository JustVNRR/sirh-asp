using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Identity.Infrastructure.Persistence;

namespace Sirh.Modules.Identity.Infrastructure.Security;

public sealed class IdentityUserProvisioningService(
    UserManager<ApplicationUser> userManager,
    IdentityDbContext dbContext,
    IRefreshTokenRepository refreshTokenRepository,
    IDateTimeProvider dateTimeProvider) : IIdentityUserProvisioningService
{
    public Task<bool> EmailExistsAsync(
        string email,
        Guid? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToUpperInvariant();
        return dbContext.Users.AnyAsync(
            u => u.NormalizedEmail == normalized && (!excludeUserId.HasValue || u.Id != excludeUserId.Value),
            cancellationToken);
    }

    public async Task<Result<IReadOnlyList<string>>> ResolveRoleCodesAsync(
        IReadOnlyCollection<string> roleInputs,
        CancellationToken cancellationToken = default)
    {
        if (roleInputs is null || roleInputs.Count == 0)
        {
            return Error.Validation("Roles.Empty", "At least one role is required.");
        }

        var allRoles = await dbContext.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var resolvedCodes = new List<string>(roleInputs.Count);
        var unknown = new List<string>();

        foreach (var rawInput in roleInputs)
        {
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                unknown.Add(rawInput ?? string.Empty);
                continue;
            }

            var trimmed = rawInput.Trim();
            var match = allRoles.FirstOrDefault(r =>
                string.Equals(r.Code, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name, trimmed, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                unknown.Add(trimmed);
            }
            else if (!resolvedCodes.Contains(match.Code, StringComparer.OrdinalIgnoreCase))
            {
                resolvedCodes.Add(match.Code);
            }
        }

        if (unknown.Count > 0)
        {
            return Error.Validation("Roles.InvalidRole", $"Unknown role(s): {string.Join(", ", unknown)}.");
        }

        return resolvedCodes.ToArray();
    }

    public async Task<Result<ProvisionedUserDto>> CreateUserAsync(
        string email,
        string password,
        Guid employeeId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken = default)
    {
        var resolvedRoles = await ResolveRolesByCodesAsync(roleCodes, cancellationToken);
        if (resolvedRoles.IsFailure)
        {
            return resolvedRoles.Error;
        }

        var normalizedEmail = email.Trim();
        var utcNow = dateTimeProvider.UtcNow;

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = true,
            IsActive = true,
            EmployeeId = employeeId,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return MapIdentityErrors("Identity.UserCreationFailed", createResult);
        }

        var roleNames = resolvedRoles.Value.Select(r => r.Name!).ToArray();
        var roleResult = await userManager.AddToRolesAsync(user, roleNames);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return MapIdentityErrors("Identity.RoleAssignmentFailed", roleResult);
        }

        var canonicalCodes = resolvedRoles.Value.Select(r => r.Code).OrderBy(c => c).ToArray();
        return new ProvisionedUserDto(user.Id, canonicalCodes);
    }

    public async Task<Result<IReadOnlyList<string>>> UpdateUserAsync(
        Guid userId,
        string newEmail,
        IReadOnlyCollection<string>? roleCodes,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Error.NotFound("Identity.UserNotFound", $"User '{userId}' was not found.");
        }

        var trimmedEmail = newEmail.Trim();
        if (!string.Equals(user.Email, trimmedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var setEmailResult = await userManager.SetEmailAsync(user, trimmedEmail);
            if (!setEmailResult.Succeeded)
            {
                return MapIdentityErrors("Identity.EmailUpdateFailed", setEmailResult);
            }

            var setUserNameResult = await userManager.SetUserNameAsync(user, trimmedEmail);
            if (!setUserNameResult.Succeeded)
            {
                return MapIdentityErrors("Identity.UserNameUpdateFailed", setUserNameResult);
            }
        }

        if (roleCodes is not null)
        {
            var resolvedRoles = await ResolveRolesByCodesAsync(roleCodes, cancellationToken);
            if (resolvedRoles.IsFailure)
            {
                return resolvedRoles.Error;
            }

            var desiredRoleNames = resolvedRoles.Value.Select(r => r.Name!).ToArray();
            var currentRoleNames = await userManager.GetRolesAsync(user);

            var toRemove = currentRoleNames.Except(desiredRoleNames, StringComparer.OrdinalIgnoreCase).ToArray();
            var toAdd = desiredRoleNames.Except(currentRoleNames, StringComparer.OrdinalIgnoreCase).ToArray();

            if (toRemove.Length > 0)
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removeResult.Succeeded)
                {
                    return MapIdentityErrors("Identity.RoleUpdateFailed", removeResult);
                }
            }

            if (toAdd.Length > 0)
            {
                var addResult = await userManager.AddToRolesAsync(user, toAdd);
                if (!addResult.Succeeded)
                {
                    return MapIdentityErrors("Identity.RoleUpdateFailed", addResult);
                }
            }
        }

        user.UpdatedAtUtc = dateTimeProvider.UtcNow;
        await userManager.UpdateSecurityStampAsync(user);
        await userManager.UpdateAsync(user);

        var updatedMap = await GetRoleCodesByUserIdsAsync([user.Id], cancellationToken);
        return updatedMap.TryGetValue(user.Id, out var codes) ? Result.Success(codes) : Result.Success<IReadOnlyList<string>>([]);
    }

    public async Task<Result> DeactivateUserAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Identity.UserNotFound", $"User '{userId}' was not found."));
        }

        var utcNow = dateTimeProvider.UtcNow;
        user.IsActive = false;
        user.UpdatedAtUtc = utcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Failure(MapIdentityErrors("Identity.UserUpdateFailed", updateResult));
        }

        await userManager.UpdateSecurityStampAsync(user);
        await refreshTokenRepository.RevokeAllForUserAsync(userId, utcNow, reason, cancellationToken);
        await refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ActivateUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Failure(Error.NotFound("Identity.UserNotFound", $"User '{userId}' was not found."));
        }

        user.IsActive = true;
        user.UpdatedAtUtc = dateTimeProvider.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Failure(MapIdentityErrors("Identity.UserUpdateFailed", updateResult));
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);

        return Result.Success();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetRoleCodesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var distinctIds = userIds.Distinct().ToArray();
        var entries = await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => distinctIds.Contains(ur.UserId))
            .Join(
                dbContext.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, r.Code })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(e => e.UserId)
            .ToDictionary(
                g => g.Key,
                IReadOnlyList<string> (g) => g.Select(x => x.Code).Distinct().OrderBy(c => c).ToArray());
    }

    public async Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(
        string roleInput,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(roleInput))
        {
            return [];
        }

        var trimmed = roleInput.Trim();
        var allRoles = await dbContext.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var role = allRoles.FirstOrDefault(r =>
            string.Equals(r.Code, trimmed, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (role is null)
        {
            return [];
        }

        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == role.Id)
            .Select(ur => ur.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<Guid?> GetUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var user = await userManager.FindByEmailAsync(email.Trim());
        return user?.Id;
    }

    public async Task LinkEmployeeAsync(
        Guid userId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.EmployeeId == employeeId)
        {
            return;
        }

        user.EmployeeId = employeeId;
        user.UpdatedAtUtc = dateTimeProvider.UtcNow;
        await userManager.UpdateAsync(user);
        await userManager.UpdateSecurityStampAsync(user);
    }

    private async Task<Result<IReadOnlyList<ApplicationRole>>> ResolveRolesByCodesAsync(
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken)
    {
        if (roleCodes.Count == 0)
        {
            return Error.Validation("Roles.Empty", "At least one role is required.");
        }

        var allRoles = await dbContext.Roles.AsNoTracking().ToListAsync(cancellationToken);
        var matched = new List<ApplicationRole>(roleCodes.Count);
        var unknown = new List<string>();

        foreach (var code in roleCodes)
        {
            var role = allRoles.FirstOrDefault(r =>
                (string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(r.Name, code, StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(r.Name));

            if (role is null)
            {
                unknown.Add(code);
            }
            else if (matched.All(m => m.Id != role.Id))
            {
                matched.Add(role);
            }
        }

        if (unknown.Count > 0)
        {
            return Error.Validation("Roles.InvalidRole", $"Unknown role(s): {string.Join(", ", unknown)}.");
        }

        return matched;
    }

    private static Error MapIdentityErrors(string code, IdentityResult result) =>
        Error.Validation(code, string.Join(" ", result.Errors.Select(e => e.Description)));
}
