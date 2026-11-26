using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sirh.Modules.Contracts.Infrastructure.Persistence;

public sealed class ContractsDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ContractsDbContext>
{
    public ContractsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ContractsDbContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=SirhDesignTime;Trusted_Connection=True;",
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ContractsDbContext.Schema));

        return new ContractsDbContext(optionsBuilder.Options);
    }
}
