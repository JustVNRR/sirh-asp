using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.BuildingBlocks.Infrastructure.Email;
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
        RegisterFileStorageProvider(services, configuration);

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.TryAddSingleton<IEmailService, SmtpEmailService>();

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver")
            .AddCheck<FileStorageHealthCheck>("storage");

        return services;
    }

    private static void RegisterFileStorageProvider(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Storage:Provider") ?? FileStorageOptions.LocalProvider;

        if (provider.Equals(FileStorageOptions.LocalProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            return;
        }

        if (provider.Equals(FileStorageOptions.AzureBlobProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.TryAddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<FileStorageOptions>>().Value;

                if (!string.IsNullOrWhiteSpace(options.AzureConnectionString))
                {
                    return new BlobServiceClient(options.AzureConnectionString);
                }

                if (!string.IsNullOrWhiteSpace(options.AzureServiceUri) &&
                    Uri.TryCreate(options.AzureServiceUri, UriKind.Absolute, out var serviceUri))
                {
                    return new BlobServiceClient(serviceUri, new DefaultAzureCredential());
                }

                throw new InvalidOperationException(
                    "Storage:Provider is set to 'AzureBlob' but neither 'Storage:AzureConnectionString' nor a valid 'Storage:AzureServiceUri' is configured.");
            });

            services.AddSingleton<IFileStorageService, AzureBlobFileStorageService>();
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported storage provider '{provider}'. Supported values are '{FileStorageOptions.LocalProvider}' and '{FileStorageOptions.AzureBlobProvider}'.");
    }
}
