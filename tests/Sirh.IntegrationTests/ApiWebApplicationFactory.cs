using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sirh.Modules.Identity.Infrastructure.Persistence;

namespace Sirh.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        $"sirh-api-it-{Guid.NewGuid():N}");

    private readonly string _inMemoryDbName = $"sirh-it-{Guid.NewGuid():N}";
    private readonly int _authPermitLimit;

    public ApiWebApplicationFactory() : this(100)
    {
    }

    private ApiWebApplicationFactory(int authPermitLimit)
    {
        _authPermitLimit = authPermitLimit;
    }

    internal static ApiWebApplicationFactory WithAuthPermitLimit(int authPermitLimit) =>
        new(authPermitLimit);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=InMemoryTest;",
                ["HealthChecks:SkipDatabase"] = "true",
                ["Storage:LocalRootPath"] = _storagePath,
                ["Encryption:MasterKey"] = "IntegrationTestMasterKeyWithAtLeast32Chars!",
                ["Jwt:SigningKey"] = "IntegrationTestJwtSigningKeyAtLeast32Chars!",
                ["Jwt:Issuer"] = "Sirh.Api",
                ["Jwt:Audience"] = "Sirh.Clients",
                ["Identity:AutoMigrate"] = "false",
                ["Identity:SeedOnStartup"] = "true",
                ["Identity:SeedDemoUsers"] = "true",
                ["RateLimiting:AuthPermitLimit"] = _authPermitLimit.ToString(),
                ["RateLimiting:AuthWindowSeconds"] = "60"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<IdentityDbContext>>();
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            services.RemoveAll<IdentityDbContext>();

            services.AddDbContext<IdentityDbContext>(options =>
                options.UseInMemoryDatabase(_inMemoryDbName));
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
