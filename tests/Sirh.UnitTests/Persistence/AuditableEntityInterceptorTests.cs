using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.BuildingBlocks.Infrastructure.Persistence;
using Xunit;

namespace Sirh.UnitTests.Persistence;

public sealed class AuditableEntityInterceptorTests
{
    private sealed class DummyAuditableEntity : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DummyDbContext(DbContextOptions<DummyDbContext> options) : DbContext(options)
    {
        public DbSet<DummyAuditableEntity> Items => Set<DummyAuditableEntity>();
    }

    [Fact]
    public async Task SavingChangesAsync_Should_Populate_Audit_Fields_On_Add_And_Update()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        var createdAt = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2026, 11, 26, 12, 30, 0, DateTimeKind.Utc);
        clock.UtcNow.Returns(createdAt);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.Email.Returns("admin.rh@nova-conseil.fr");

        var interceptor = new AuditableEntityInterceptor(clock, currentUser);
        var options = new DbContextOptionsBuilder<DummyDbContext>()
            .UseInMemoryDatabase($"audit-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;

        await using var db = new DummyDbContext(options);

        var entity = new DummyAuditableEntity { Name = "Initial" };
        db.Items.Add(entity);
        await db.SaveChangesAsync();

        entity.CreatedAtUtc.ShouldBe(createdAt);
        entity.CreatedBy.ShouldBe("admin.rh@nova-conseil.fr");
        entity.UpdatedAtUtc.ShouldBe(createdAt);
        entity.UpdatedBy.ShouldBe("admin.rh@nova-conseil.fr");

        clock.UtcNow.Returns(updatedAt);
        currentUser.Email.Returns("manager.rh@nova-conseil.fr");
        entity.Name = "Updated";
        await db.SaveChangesAsync();

        entity.CreatedAtUtc.ShouldBe(createdAt);
        entity.CreatedBy.ShouldBe("admin.rh@nova-conseil.fr");
        entity.UpdatedAtUtc.ShouldBe(updatedAt);
        entity.UpdatedBy.ShouldBe("manager.rh@nova-conseil.fr");
    }
}
