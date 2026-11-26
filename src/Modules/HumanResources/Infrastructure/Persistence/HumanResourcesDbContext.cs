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
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<RqthDetail> RqthDetails => Set<RqthDetail>();
    public DbSet<ResidentPermitDetail> ResidentPermitDetails => Set<ResidentPermitDetail>();

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

        builder.Entity<EmergencyContact>(b =>
        {
            b.ToTable("emergency_contacts");
            b.HasKey(c => c.Id);

            b.Property(c => c.FullNameEncrypted).HasMaxLength(512).IsRequired();
            b.Property(c => c.RelationshipEncrypted).HasMaxLength(512).IsRequired();
            b.Property(c => c.MobilePhoneEncrypted).HasMaxLength(512);
            b.Property(c => c.HomePhoneEncrypted).HasMaxLength(512);
            b.Property(c => c.WorkPhoneEncrypted).HasMaxLength(512);
            b.Property(c => c.CreatedBy).HasMaxLength(256);
            b.Property(c => c.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(c => c.RowVersion).IsRowVersion();
            }

            b.HasIndex(c => c.EmployeeId);

            b.HasOne(c => c.Employee)
                .WithMany(e => e.EmergencyContacts)
                .HasForeignKey(c => c.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Position>(b =>
        {
            b.ToTable("positions");
            b.HasKey(p => p.Id);

            b.Property(p => p.Slug).HasMaxLength(120).IsRequired();
            b.Property(p => p.JobTitle).HasMaxLength(150).IsRequired();
            b.Property(p => p.ContractLabel).HasMaxLength(150);
            b.Property(p => p.Coefficient).HasMaxLength(50);
            b.Property(p => p.Rate).HasPrecision(18, 2);
            b.Property(p => p.Supervisor).HasMaxLength(150);
            b.Property(p => p.TimeContractType).HasMaxLength(20);
            b.Property(p => p.TimeContractAmount).HasPrecision(10, 2);
            b.Property(p => p.HierarchicalPosition).HasMaxLength(120);
            b.Property(p => p.DepartmentsCsv).HasMaxLength(500).IsRequired();
            b.Property(p => p.CreatedBy).HasMaxLength(256);
            b.Property(p => p.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(p => p.RowVersion).IsRowVersion();
            }

            b.HasIndex(p => p.Slug).IsUnique();
            b.HasIndex(p => p.EmployeeId);
            b.HasIndex(p => p.LocationId);

            b.HasOne(p => p.Employee)
                .WithMany(e => e.Positions)
                .HasForeignKey(p => p.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(p => p.Manager)
                .WithMany()
                .HasForeignKey(p => p.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<EmployeeDocument>(b =>
        {
            b.ToTable("employee_documents");
            b.HasKey(d => d.Id);

            b.Property(d => d.Category)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            b.Property(d => d.DocumentType)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();
            b.Property(d => d.FileKey).HasMaxLength(512).IsRequired();
            b.Property(d => d.FileName).HasMaxLength(256).IsRequired();
            b.Property(d => d.ContentType).HasMaxLength(128).IsRequired();
            b.Property(d => d.Commentary).HasMaxLength(1000);
            b.Property(d => d.CreatedBy).HasMaxLength(256);
            b.Property(d => d.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(d => d.RowVersion).IsRowVersion();
            }

            b.HasIndex(d => new { d.EmployeeId, d.Category });
            b.HasIndex(d => d.DocumentType);

            b.HasOne(d => d.Employee)
                .WithMany(e => e.Documents)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RqthDetail>(b =>
        {
            b.ToTable("rqth_details");
            b.HasKey(r => r.DocumentId);

            b.Property(r => r.WorkplaceAccommodation).HasMaxLength(1000);
            b.HasIndex(r => r.ExpiryDate);

            b.HasOne(r => r.Document)
                .WithOne(d => d.RqthDetail)
                .HasForeignKey<RqthDetail>(r => r.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResidentPermitDetail>(b =>
        {
            b.ToTable("resident_permit_details");
            b.HasKey(p => p.DocumentId);

            b.Property(p => p.PermitNumber).HasMaxLength(100).IsRequired();
            b.HasIndex(p => p.ExpiryDate);

            b.HasOne(p => p.Document)
                .WithOne(d => d.ResidentPermitDetail)
                .HasForeignKey<ResidentPermitDetail>(p => p.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
