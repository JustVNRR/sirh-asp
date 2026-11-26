using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.Services;
using Sirh.Modules.Health.Infrastructure.Persistence;

namespace Sirh.Modules.Health.Infrastructure;

public static class HealthModuleExtensions
{
    public static IServiceCollection AddHealthModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        if (configureDbContext is not null)
        {
            services.AddDbContext<HealthDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<HealthDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", HealthDbContext.Schema))
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            });
        }

        services.AddScoped<IMedicalVisitRepository, MedicalVisitRepository>();
        services.AddScoped<IMedicalVisitService, MedicalVisitService>();

        return services;
    }
}
