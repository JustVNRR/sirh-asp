using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Infrastructure.Storage;

namespace Sirh.BuildingBlocks.Infrastructure.Health;

public sealed class FileStorageHealthCheck : IHealthCheck
{
    private readonly FileStorageOptions _options;
    private readonly IServiceProvider _serviceProvider;

    public FileStorageHealthCheck(
        IOptions<FileStorageOptions> options,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        _options = options.Value;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_options.Provider.Equals(FileStorageOptions.AzureBlobProvider, StringComparison.OrdinalIgnoreCase))
        {
            return await CheckAzureBlobHealthAsync(cancellationToken);
        }

        return CheckLocalHealth();
    }

    private async Task<HealthCheckResult> CheckAzureBlobHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var blobServiceClient = _serviceProvider.GetRequiredService<BlobServiceClient>();
            await blobServiceClient.GetAccountInfoAsync(cancellationToken);

            return HealthCheckResult.Healthy(
                $"Azure Blob storage ready (containers '{_options.EmployeeSensitiveContainer}', '{_options.CommonPublicContainer}').");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Azure Blob storage health check failed.", ex);
        }
    }

    private HealthCheckResult CheckLocalHealth()
    {
        try
        {
            var root = Path.GetFullPath(_options.LocalRootPath);
            Directory.CreateDirectory(root);

            var probePath = Path.Combine(root, $".health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);

            return HealthCheckResult.Healthy($"Local storage ready at '{root}'.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Local storage health check failed.", ex);
        }
    }
}
