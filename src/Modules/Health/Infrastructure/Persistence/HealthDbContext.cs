using Microsoft.EntityFrameworkCore;
using Sirh.Modules.Health.Domain.Entities;

namespace Sirh.Modules.Health.Infrastructure.Persistence;

public sealed class HealthDbContext : DbContext
{
    public const string Schema = "health";

    public HealthDbContext(DbContextOptions<HealthDbContext> options) : base(options)
    {
    }

    public DbSet<MedicalVisit> MedicalVisits => Set<MedicalVisit>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        var isInMemory = string.Equals(
            Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal);

        modelBuilder.Entity<MedicalVisit>(b =>
        {
            b.ToTable("medical_visits");
            b.HasKey(v => v.Id);

            b.Property(v => v.VisitType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(v => v.MedicalCenter).HasMaxLength(200);
            b.Property(v => v.Comments).HasMaxLength(1000);

            b.Property(v => v.ConvocationFileKey).HasMaxLength(512).IsRequired();
            b.Property(v => v.ConvocationFileName).HasMaxLength(256).IsRequired();
            b.Property(v => v.ConvocationContentType).HasMaxLength(128).IsRequired();

            b.Property(v => v.ReportFileKey).HasMaxLength(512);
            b.Property(v => v.ReportFileName).HasMaxLength(256);
            b.Property(v => v.ReportContentType).HasMaxLength(128);

            b.Property(v => v.CreatedBy).HasMaxLength(256);
            b.Property(v => v.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(v => v.RowVersion).IsRowVersion();
            }

            b.HasIndex(v => new { v.EmployeeId, v.VisitDateTimeUtc });
            b.HasIndex(v => new { v.CarriedOut, v.ReminderSentAtUtc, v.VisitDateTimeUtc });
        });

        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("notifications");
            b.HasKey(n => n.Id);

            b.Property(n => n.Category)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(n => n.ReferenceEntity).HasMaxLength(64);
            b.Property(n => n.Subject).HasMaxLength(200).IsRequired();
            b.Property(n => n.Content).HasMaxLength(2000).IsRequired();
            b.Property(n => n.CreatedBy).HasMaxLength(256);
            b.Property(n => n.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(n => n.RowVersion).IsRowVersion();
            }

            b.HasIndex(n => new { n.RecipientEmployeeId, n.IsRead, n.CreatedAtUtc });
            b.HasIndex(n => new { n.RecipientEmployeeId, n.Category });
        });
    }
}
