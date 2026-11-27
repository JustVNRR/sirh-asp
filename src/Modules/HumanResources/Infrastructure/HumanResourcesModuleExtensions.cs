using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.Services;
using Sirh.Modules.HumanResources.Application.Validators;
using Sirh.Modules.HumanResources.Infrastructure.Persistence;

namespace Sirh.Modules.HumanResources.Infrastructure;

public static class HumanResourcesModuleExtensions
{
    public static IServiceCollection AddHumanResourcesModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        if (configureDbContext is not null)
        {
            services.AddDbContext<HumanResourcesDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<HumanResourcesDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", HumanResourcesDbContext.Schema))
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            });
        }

        services.AddValidatorsFromAssemblyContaining<CreateEmployeeRequestValidator>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<IEmergencyContactRepository, EmergencyContactRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IEmployeeDocumentRepository, EmployeeDocumentRepository>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmergencyContactService, EmergencyContactService>();
        services.AddScoped<IPositionService, PositionService>();
        services.AddScoped<IEmployeeDocumentService, EmployeeDocumentService>();
        services.AddScoped<ILocationReferenceChecker, PositionLocationReferenceChecker>();
        services.AddScoped<IHumanResourcesLookup, HumanResourcesLookupService>();
        services.AddScoped<HumanResourcesDbSeeder>();

        return services;
    }

    public static async Task SeedHumanResourcesAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<HumanResourcesDbSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
