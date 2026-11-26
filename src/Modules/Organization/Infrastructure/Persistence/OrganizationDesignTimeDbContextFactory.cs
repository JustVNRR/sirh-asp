using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sirh.Modules.Organization.Infrastructure.Persistence;

public sealed class OrganizationDesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=SirhDesignTime;Trusted_Connection=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", OrganizationDbContext.Schema));

        return new OrganizationDbContext(optionsBuilder.Options);
    }
}
