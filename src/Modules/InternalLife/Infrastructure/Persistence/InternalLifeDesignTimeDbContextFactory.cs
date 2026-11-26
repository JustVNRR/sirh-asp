using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class InternalLifeDesignTimeDbContextFactory : IDesignTimeDbContextFactory<InternalLifeDbContext>
{
    public InternalLifeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<InternalLifeDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=localhost;Database=SirhDesignTime;Trusted_Connection=True;TrustServerCertificate=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", InternalLifeDbContext.Schema));

        return new InternalLifeDbContext(optionsBuilder.Options);
    }
}
