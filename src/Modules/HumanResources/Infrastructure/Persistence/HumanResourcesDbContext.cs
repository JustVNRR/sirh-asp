using Microsoft.EntityFrameworkCore;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class HumanResourcesDbContext : DbContext
{
    public const string Schema = "hr";

    public HumanResourcesDbContext(DbContextOptions<HumanResourcesDbContext> options) : base(options)
    {
    }

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        var isInMemory = string.Equals(
            Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        builder.Entity<Employee>(b =>
        {
            b.ToTable("employees");
            b.HasKey(e => e.Id);

            b.Property(e => e.Slug).HasMaxLength(120).IsRequired();
            b.Property(e => e.Honorific).HasMaxLength(16).IsRequired();
            b.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
            b.Property(e => e.LastName).HasMaxLength(100).IsRequired();
            b.Property(e => e.BirthPlace).HasMaxLength(120);
            b.Property(e => e.BirthDepartmentCode).HasMaxLength(10);
            b.Property(e => e.NationalIdEncrypted).HasMaxLength(512);
            b.Property(e => e.Email).HasMaxLength(256).IsRequired();
            b.Property(e => e.Landline).HasMaxLength(30);
            b.Property(e => e.Mobile).HasMaxLength(30);
            b.Property(e => e.PersonalEmail).HasMaxLength(256);
            b.Property(e => e.PersonalLandline).HasMaxLength(30);
            b.Property(e => e.PersonalMobile).HasMaxLength(30);
            b.Property(e => e.AddressStreet).HasMaxLength(250);
            b.Property(e => e.AddressPostalCode).HasMaxLength(20);
            b.Property(e => e.AddressCity).HasMaxLength(100);
            b.Property(e => e.AddressAdditionalInfo).HasMaxLength(200);
            b.Property(e => e.DepartmentsCsv).HasMaxLength(500).IsRequired();
            b.Property(e => e.CreatedBy).HasMaxLength(256);
            b.Property(e => e.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(e => e.RowVersion).IsRowVersion();
            }

            b.HasIndex(e => e.UserId).IsUnique();
            b.HasIndex(e => e.Slug).IsUnique();
            b.HasIndex(e => e.Email).IsUnique();
            b.HasIndex(e => e.CompanyId);
            b.HasIndex(e => e.DirectManagerId);

            b.HasOne(e => e.DirectManager)
                .WithMany()
                .HasForeignKey(e => e.DirectManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
