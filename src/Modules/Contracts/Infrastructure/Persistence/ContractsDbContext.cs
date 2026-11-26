using Microsoft.EntityFrameworkCore;
using Sirh.Modules.Contracts.Domain.Entities;

namespace Sirh.Modules.Contracts.Infrastructure.Persistence;

public sealed class ContractsDbContext : DbContext
{
    public const string Schema = "contracts";

    public ContractsDbContext(DbContextOptions<ContractsDbContext> options) : base(options)
    {
    }

    public DbSet<ContractModel> ContractModels => Set<ContractModel>();
    public DbSet<Contract> Contracts => Set<Contract>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        var isInMemory = string.Equals(
            Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        builder.Entity<ContractModel>(b =>
        {
            b.ToTable("contract_models");
            b.HasKey(m => m.Id);

            b.Property(m => m.Title).HasMaxLength(150).IsRequired();
            b.Property(m => m.Description).HasMaxLength(500);
            b.Property(m => m.TemplateContent).IsRequired();
            b.Property(m => m.CustomVariablesJson).HasMaxLength(2000);
            b.Property(m => m.CreatedBy).HasMaxLength(256);
            b.Property(m => m.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(m => m.RowVersion).IsRowVersion();
            }

            b.HasIndex(m => m.Title).IsUnique();
            b.HasIndex(m => m.ContractTypeId);
        });

        builder.Entity<Contract>(b =>
        {
            b.ToTable("contracts");
            b.HasKey(c => c.Id);

            b.Property(c => c.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(c => c.FileKey).HasMaxLength(512).IsRequired();
            b.Property(c => c.FileName).HasMaxLength(256).IsRequired();
            b.Property(c => c.ContentType).HasMaxLength(128).IsRequired();
            b.Property(c => c.Commentary).HasMaxLength(1000);
            b.Property(c => c.CreatedBy).HasMaxLength(256);
            b.Property(c => c.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(c => c.RowVersion).IsRowVersion();
            }

            b.HasIndex(c => c.PositionId).IsUnique();
            b.HasIndex(c => c.EmployeeId);
            b.HasIndex(c => c.ContractTypeId);

            b.HasOne(c => c.ContractModel)
                .WithMany(m => m.Contracts)
                .HasForeignKey(c => c.ContractModelId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
