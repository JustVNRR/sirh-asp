using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbSeeder
{
    private readonly IdentityDbContext _dbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentityDbSeeder> _logger;

    public IdentityDbSeeder(
        IdentityDbContext dbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IDateTimeProvider dateTimeProvider,
        IConfiguration configuration,
        ILogger<IdentityDbSeeder> logger)
    {
        _dbContext = dbContext;
        _roleManager = roleManager;
        _userManager = userManager;
        _dateTimeProvider = dateTimeProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedPermissionsAsync(cancellationToken);
        await SeedRolesAndMappingsAsync(cancellationToken);

        if (_configuration.GetValue<bool>("Identity:SeedDemoUsers"))
        {
            await SeedDemoUsersAsync(cancellationToken);
        }
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _dbContext.Permissions
            .Select(p => p.Code)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var toAdd = IdentityPermissions.All
            .Where(p => !existingCodes.Contains(p.Code))
            .Select(p => new Permission(p.Code, p.Module, p.Description))
            .ToList();

        if (toAdd.Count > 0)
        {
            await _dbContext.Permissions.AddRangeAsync(toAdd, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SeedRolesAndMappingsAsync(CancellationToken cancellationToken)
    {
        var permissionsByCode = await _dbContext.Permissions
            .ToDictionaryAsync(p => p.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var roleDef in IdentityRoles.All)
        {
            var role = await _roleManager.Roles
                .FirstOrDefaultAsync(r => r.Code == roleDef.Code, cancellationToken);

            if (role is null)
            {
                role = new ApplicationRole
                {
                    Name = roleDef.Name,
                    Code = roleDef.Code,
                    Priority = roleDef.Priority,
                    Description = roleDef.Description
                };

                var createResult = await _roleManager.CreateAsync(role);
                if (!createResult.Succeeded)
                {
                    var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to seed role '{roleDef.Code}': {errors}");
                }
            }

            if (!IdentityPermissions.DefaultRoleMappings.TryGetValue(roleDef.Code, out var desiredCodes))
            {
                continue;
            }

            var existingPermissionIds = await _dbContext.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Select(rp => rp.PermissionId)
                .ToHashSetAsync(cancellationToken);

            var missingLinks = desiredCodes
                .Where(code => permissionsByCode.ContainsKey(code))
                .Select(code => permissionsByCode[code].Id)
                .Where(permissionId => !existingPermissionIds.Contains(permissionId))
                .Select(permissionId => new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId
                })
                .ToList();

            if (missingLinks.Count > 0)
            {
                await _dbContext.RolePermissions.AddRangeAsync(missingLinks, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    private async Task SeedDemoUsersAsync(CancellationToken cancellationToken)
    {
        var now = _dateTimeProvider.UtcNow;
        var demoUsers = new (string Email, string Password, string[] RoleCodes)[]
        {
            ("admin.rh@nova-conseil.fr", "SirhDemo123!", [IdentityRoles.Admin, IdentityRoles.Hr]),
            ("manager.rh@nova-conseil.fr", "SirhDemo123!", [IdentityRoles.Manager]),
            ("employe.rh@nova-conseil.fr", "SirhDemo123!", [IdentityRoles.Employee])
        };

        foreach (var (email, password, roleCodes) in demoUsers)
        {
            var existing = await _userManager.FindByEmailAsync(email);
            if (existing is not null)
            {
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed demo user '{email}': {errors}");
            }

            var roleNames = await _dbContext.Roles
                .Where(r => roleCodes.Contains(r.Code) && r.Name != null)
                .Select(r => r.Name!)
                .ToListAsync(cancellationToken);

            await _userManager.AddToRolesAsync(user, roleNames);
            _logger.LogInformation("Seeded demo user {Email} with roles {Roles}", email, string.Join(", ", roleNames));
        }
    }
}
