using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class HumanResourcesDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HumanResourcesDbContext>
{
    public HumanResourcesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HumanResourcesDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=SirhDesignTime;Trusted_Connection=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", HumanResourcesDbContext.Schema));

        return new HumanResourcesDbContext(optionsBuilder.Options);
    }
}
