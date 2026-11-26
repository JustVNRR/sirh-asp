using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.Modules.Contracts.Application.Abstractions;
using Sirh.Modules.Contracts.Application.Services;
using Sirh.Modules.Contracts.Application.Validators;
using Sirh.Modules.Contracts.Infrastructure.Documents;
using Sirh.Modules.Contracts.Infrastructure.Persistence;

namespace Sirh.Modules.Contracts.Infrastructure;

public static class ContractsModuleExtensions
{
    public static IServiceCollection AddContractsModule(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IServiceProvider, DbContextOptionsBuilder>? configureDbContext = null)
    {
        if (configureDbContext is not null)
        {
            services.AddDbContext<ContractsDbContext>(configureDbContext);
        }
        else
        {
            services.AddDbContext<ContractsDbContext>((sp, options) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

                options.UseSqlServer(connectionString, sql =>
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", ContractsDbContext.Schema))
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
            });
        }

        services.AddValidatorsFromAssemblyContaining<CreateContractModelRequestValidator>();
        services.AddSingleton<IContractTemplateEngine, ScribanContractTemplateEngine>();
        services.AddSingleton<IPdfContractRenderer, QuestPdfContractRenderer>();
        services.AddScoped<IContractModelRepository, ContractModelRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IContractModelService, ContractModelService>();
        services.AddScoped<IContractGenerationService, ContractGenerationService>();
        services.AddScoped<IPositionReferenceChecker, ContractPositionReferenceChecker>();
        services.AddScoped<IContractTypeReferenceChecker, ContractTypeUsageChecker>();

        return services;
    }
}
