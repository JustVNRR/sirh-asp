using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Sirh.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        $"sirh-api-it-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["HealthChecks:SkipDatabase"] = "true",
                ["Storage:LocalRootPath"] = _storagePath,
                ["Encryption:MasterKey"] = "IntegrationTestMasterKeyWithAtLeast32Chars!"
            });
        });
    }

    public new void Dispose()
    {
        base.Dispose();
        if (Directory.Exists(_storagePath))
        {
            Directory.Delete(_storagePath, recursive: true);
        }
    }
}
