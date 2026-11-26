using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Infrastructure.Storage;

namespace Sirh.BuildingBlocks.Infrastructure.Health;

public sealed class FileStorageHealthCheck : IHealthCheck
{
    private readonly FileStorageOptions _options;

    public FileStorageHealthCheck(IOptions<FileStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var root = Path.GetFullPath(_options.LocalRootPath);
            Directory.CreateDirectory(root);

            var probePath = Path.Combine(root, $".health-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);

            return Task.FromResult(HealthCheckResult.Healthy($"Local storage ready at '{root}'."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Local storage health check failed.", ex));
        }
    }
}
