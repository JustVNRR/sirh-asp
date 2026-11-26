using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sirh.Modules.Health.Infrastructure.Persistence;

public sealed class HealthDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HealthDbContext>
{
    public HealthDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HealthDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=SirhDesignTime;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", HealthDbContext.Schema));

        return new HealthDbContext(optionsBuilder.Options);
    }
}
