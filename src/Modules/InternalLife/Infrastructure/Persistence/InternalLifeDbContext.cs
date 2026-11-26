using Microsoft.EntityFrameworkCore;
using Sirh.Modules.InternalLife.Domain.Entities;

namespace Sirh.Modules.InternalLife.Infrastructure.Persistence;

public sealed class InternalLifeDbContext : DbContext
{
    public const string Schema = "internal_life";

    public InternalLifeDbContext(DbContextOptions<InternalLifeDbContext> options) : base(options)
    {
    }

    public DbSet<CommonDocument> CommonDocuments => Set<CommonDocument>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        var isInMemory = string.Equals(
            Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        builder.Entity<CommonDocument>(b =>
        {
            b.ToTable("common_documents");
            b.HasKey(d => d.Id);

            b.Property(d => d.Category)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            b.Property(d => d.Title).HasMaxLength(200).IsRequired();
            b.Property(d => d.Description).HasMaxLength(1000);
            b.Property(d => d.FileKey).HasMaxLength(512).IsRequired();
            b.Property(d => d.FileName).HasMaxLength(256).IsRequired();
            b.Property(d => d.ContentType).HasMaxLength(128).IsRequired();
            b.Property(d => d.CreatedBy).HasMaxLength(256);
            b.Property(d => d.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(d => d.RowVersion).IsRowVersion();
            }

            b.HasIndex(d => new { d.CompanyId, d.IsWelcomeKit, d.IsArchived });
            b.HasIndex(d => new { d.Category, d.IsArchived });
            b.HasIndex(d => d.ContractTypeId);
        });
    }
}
