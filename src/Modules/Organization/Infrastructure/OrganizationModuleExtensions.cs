using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Application.Services;
using Sirh.Modules.Organization.Application.Validators;
using Sirh.Modules.Organization.Infrastructure.Persistence;

namespace Sirh.Modules.Organization.Infrastructure;

public static class OrganizationModuleExtensions
{
    public static IServiceCollection AddOrganizationModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        if (configureDbContext is not null)
        {
            services.AddDbContext<OrganizationDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<OrganizationDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", OrganizationDbContext.Schema))
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            });
        }

        services.AddValidatorsFromAssemblyContaining<CreateCompanyRequestValidator>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<IContractTypeRepository, ContractTypeRepository>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IContractTypeService, ContractTypeService>();
        services.AddScoped<OrganizationDbSeeder>();

        return services;
    }

    public static async Task SeedOrganizationAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<OrganizationDbSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
