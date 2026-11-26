using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sirh.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=SirhDesignTime;Trusted_Connection=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema));

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
