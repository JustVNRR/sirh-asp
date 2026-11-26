using Microsoft.EntityFrameworkCore;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext : DbContext
{
    public const string Schema = "organization";

    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ContractType> ContractTypes => Set<ContractType>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        var isInMemory = string.Equals(
            Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        builder.Entity<Company>(b =>
        {
            b.ToTable("companies");
            b.HasKey(c => c.Id);

            b.Property(c => c.Name).HasMaxLength(150).IsRequired();
            b.Property(c => c.Slug).HasMaxLength(120).IsRequired();
            b.Property(c => c.LegalForm).HasMaxLength(80);
            b.Property(c => c.Address).HasMaxLength(250).IsRequired();
            b.Property(c => c.PostalCode).HasMaxLength(20);
            b.Property(c => c.City).HasMaxLength(100);
            b.Property(c => c.Country).HasMaxLength(100).IsRequired();
            b.Property(c => c.Siret).HasMaxLength(14).IsRequired();
            b.Property(c => c.Register).HasMaxLength(120);
            b.Property(c => c.Capital).HasMaxLength(100);
            b.Property(c => c.CollectiveAgreement).HasMaxLength(150).IsRequired();
            b.Property(c => c.Owner).HasMaxLength(150);
            b.Property(c => c.LogoUrl).HasMaxLength(500);
            b.Property(c => c.CreatedBy).HasMaxLength(256);
            b.Property(c => c.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(c => c.RowVersion).IsRowVersion();
            }

            b.HasIndex(c => c.Name).IsUnique();
            b.HasIndex(c => c.Slug).IsUnique();
            b.HasIndex(c => c.Siret).IsUnique();
        });

        builder.Entity<Location>(b =>
        {
            b.ToTable("locations");
            b.HasKey(l => l.Id);

            b.Property(l => l.Slug).HasMaxLength(120).IsRequired();
            b.Property(l => l.Name).HasMaxLength(150).IsRequired();
            b.Property(l => l.Address).HasMaxLength(250).IsRequired();
            b.Property(l => l.PostalCode).HasMaxLength(20);
            b.Property(l => l.City).HasMaxLength(100).IsRequired();
            b.Property(l => l.Country).HasMaxLength(100).IsRequired();
            b.Property(l => l.DepartmentCode).HasMaxLength(10);
            b.Property(l => l.Siret).HasMaxLength(14);
            b.Property(l => l.ColorCode).HasMaxLength(7).IsRequired();
            b.Property(l => l.CreatedBy).HasMaxLength(256);
            b.Property(l => l.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(l => l.RowVersion).IsRowVersion();
            }

            b.HasIndex(l => l.Slug).IsUnique();

            b.HasOne(l => l.Company)
                .WithMany(c => c.Locations)
                .HasForeignKey(l => l.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ContractType>(b =>
        {
            b.ToTable("contract_types");
            b.HasKey(ct => ct.Id);

            b.Property(ct => ct.Code).HasMaxLength(50).IsRequired();
            b.Property(ct => ct.Label).HasMaxLength(150).IsRequired();
            b.Property(ct => ct.CreatedBy).HasMaxLength(256);
            b.Property(ct => ct.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(ct => ct.RowVersion).IsRowVersion();
            }

            b.HasIndex(ct => ct.Code).IsUnique();
        });
    }
}
