using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.Options;
using Sirh.Modules.Identity.Application.Services;
using Sirh.Modules.Identity.Application.Validators;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Identity.Infrastructure.Persistence;
using Sirh.Modules.Identity.Infrastructure.Security;

namespace Sirh.Modules.Identity.Infrastructure;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.SigningKey) &&
                     o.SigningKey.Length >= JwtOptions.MinSigningKeyLength,
                $"Jwt:SigningKey must be configured and at least {JwtOptions.MinSigningKeyLength} characters long.")
            .ValidateOnStart();

        if (configureDbContext is not null)
        {
            services.AddDbContext<IdentityDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<IdentityDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema));
            });
        }

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        var authorizationBuilder = services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

        foreach (var permissionCode in IdentityPermissions.All.Select(permission => permission.Code))
        {
            authorizationBuilder.AddPolicy(permissionCode, policy =>
                policy.RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permissionCode)));
        }

        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();
        services.AddScoped<IIdentityUserProvisioningService, IdentityUserProvisioningService>();
        services.AddScoped<IdentityDbSeeder>();

        return services;
    }

    public static async Task SeedIdentityAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IdentityDbSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }

    private sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions)
        : IConfigureNamedOptions<JwtBearerOptions>
    {
        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
            {
                return;
            }

            var jwt = jwtOptions.Value;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));

            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(15),
                NameClaimType = "sub",
                RoleClaimType = "role"
            };
        }

        public void Configure(JwtBearerOptions options) =>
            Configure(JwtBearerDefaults.AuthenticationScheme, options);
    }
}
