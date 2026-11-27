using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Sirh.Modules.Contracts.Infrastructure.Persistence;
using Sirh.Modules.Health.Infrastructure.Persistence;
using Sirh.Modules.HumanResources.Infrastructure.Persistence;
using Sirh.Modules.Identity.Infrastructure.Persistence;
using Sirh.Modules.InternalLife.Infrastructure.Persistence;
using Sirh.Modules.Organization.Application.Abstractions;
using Sirh.Modules.Organization.Infrastructure.Persistence;

namespace Sirh.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly string _storagePath = Path.Combine(
        Path.GetTempPath(),
        $"sirh-api-it-{Guid.NewGuid():N}");

    private readonly string _inMemoryDbName = $"sirh-it-{Guid.NewGuid():N}";
    private readonly string _orgInMemoryDbName = $"sirh-org-it-{Guid.NewGuid():N}";
    private readonly string _hrInMemoryDbName = $"sirh-hr-it-{Guid.NewGuid():N}";
    private readonly string _contractsInMemoryDbName = $"sirh-contracts-it-{Guid.NewGuid():N}";
    private readonly string _healthInMemoryDbName = $"sirh-health-it-{Guid.NewGuid():N}";
    private readonly string _internalLifeInMemoryDbName = $"sirh-internallife-it-{Guid.NewGuid():N}";
    private readonly int _authPermitLimit;

    internal string StoragePath => _storagePath;
    internal InMemoryEmailService EmailService { get; } = new();
    internal HashSet<Guid> LocationReferences { get; } = [];
    internal HashSet<Guid> ContractTypeReferences { get; } = [];
    internal HashSet<Guid> PositionReferences { get; } = [];

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
                ["Organization:AutoMigrate"] = "false",
                ["Organization:SeedOnStartup"] = "true",
                ["Organization:SeedDemoData"] = "true",
                ["HumanResources:AutoMigrate"] = "false",
                ["HumanResources:SeedDemoData"] = "false",
                ["Contracts:AutoMigrate"] = "false",
                ["Health:AutoMigrate"] = "false",
                ["Health:Reminders:Enabled"] = "false",
                ["InternalLife:AutoMigrate"] = "false",
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

            services.RemoveAll<IDbContextOptionsConfiguration<OrganizationDbContext>>();
            services.RemoveAll<DbContextOptions<OrganizationDbContext>>();
            services.RemoveAll<OrganizationDbContext>();

            services.AddDbContext<OrganizationDbContext>((sp, options) =>
                options.UseInMemoryDatabase(_orgInMemoryDbName)
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

            services.RemoveAll<IDbContextOptionsConfiguration<HumanResourcesDbContext>>();
            services.RemoveAll<DbContextOptions<HumanResourcesDbContext>>();
            services.RemoveAll<HumanResourcesDbContext>();

            services.AddDbContext<HumanResourcesDbContext>((sp, options) =>
                options.UseInMemoryDatabase(_hrInMemoryDbName)
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

            services.RemoveAll<IDbContextOptionsConfiguration<ContractsDbContext>>();
            services.RemoveAll<DbContextOptions<ContractsDbContext>>();
            services.RemoveAll<ContractsDbContext>();

            services.AddDbContext<ContractsDbContext>((sp, options) =>
                options.UseInMemoryDatabase(_contractsInMemoryDbName)
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

            services.RemoveAll<IDbContextOptionsConfiguration<HealthDbContext>>();
            services.RemoveAll<DbContextOptions<HealthDbContext>>();
            services.RemoveAll<HealthDbContext>();

            services.AddDbContext<HealthDbContext>((sp, options) =>
                options.UseInMemoryDatabase(_healthInMemoryDbName)
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

            services.RemoveAll<IDbContextOptionsConfiguration<InternalLifeDbContext>>();
            services.RemoveAll<DbContextOptions<InternalLifeDbContext>>();
            services.RemoveAll<InternalLifeDbContext>();

            services.AddDbContext<InternalLifeDbContext>((sp, options) =>
                options.UseInMemoryDatabase(_internalLifeInMemoryDbName)
                    .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(EmailService);
            services.AddSingleton<ILocationReferenceChecker>(new StubReferenceChecker(this));
            services.AddSingleton<IContractTypeReferenceChecker>(new StubReferenceChecker(this));
            services.AddSingleton<IPositionReferenceChecker>(new StubReferenceChecker(this));
        });
    }

    private sealed class StubReferenceChecker(ApiWebApplicationFactory factory)
        : ILocationReferenceChecker, IContractTypeReferenceChecker, IPositionReferenceChecker
    {
        public Task<bool> IsLocationReferencedAsync(Guid locationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(factory.LocationReferences.Contains(locationId));

        public Task<bool> IsContractTypeReferencedAsync(Guid contractTypeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(factory.ContractTypeReferences.Contains(contractTypeId));

        public Task<bool> IsPositionReferencedAsync(Guid positionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(factory.PositionReferences.Contains(positionId));
    }

    internal sealed class InMemoryEmailService : IEmailService
    {
        private readonly ConcurrentQueue<EmailMessage> _messages = new();

        public IReadOnlyList<EmailMessage> SentMessages => _messages.ToArray();

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            _messages.Enqueue(message);
            return Task.CompletedTask;
        }
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
