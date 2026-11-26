using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Infrastructure.Health;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.BuildingBlocks.Infrastructure.Security;
using Sirh.BuildingBlocks.Infrastructure.Storage;
using Sirh.BuildingBlocks.Infrastructure.Time;

namespace Sirh.BuildingBlocks.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBuildingBlocksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<AuditableEntityInterceptor>();

        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.AddSingleton<IDataProtectorService, AesGcmCryptoService>();

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileValidator, MagicBytesFileValidator>();
        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver")
            .AddCheck<FileStorageHealthCheck>("storage");

        return services;
    }
}
