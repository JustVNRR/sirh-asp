using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.Services;
using Sirh.Modules.InternalLife.Infrastructure.Persistence;

namespace Sirh.Modules.InternalLife.Infrastructure;

public static class InternalLifeModuleExtensions
{
    public static IServiceCollection AddInternalLifeModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        if (configureDbContext is not null)
        {
            services.AddDbContext<InternalLifeDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<InternalLifeDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", InternalLifeDbContext.Schema))
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            });
        }

        services.AddScoped<ICommonDocumentRepository, CommonDocumentRepository>();
        services.AddScoped<IStaffRepresentativeRepository, StaffRepresentativeRepository>();
        services.AddScoped<IRseIdeaRepository, RseIdeaRepository>();
        services.AddScoped<ICommonDocumentService, CommonDocumentService>();
        services.AddScoped<IStaffRepresentativeService, StaffRepresentativeService>();
        services.AddScoped<IRseIdeaService, RseIdeaService>();
        services.AddScoped<IContractTypeReferenceChecker, CommonDocumentContractTypeReferenceChecker>();
        services.AddScoped<ILocationReferenceChecker, RseIdeaLocationReferenceChecker>();

        return services;
    }
}
