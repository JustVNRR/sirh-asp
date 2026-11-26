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
    public DbSet<StaffRepresentative> StaffRepresentatives => Set<StaffRepresentative>();
    public DbSet<RseIdea> RseIdeas => Set<RseIdea>();

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

        builder.Entity<StaffRepresentative>(b =>
        {
            b.ToTable("staff_representatives");
            b.HasKey(r => r.Id);

            b.Property(r => r.FirstName).HasMaxLength(100).IsRequired();
            b.Property(r => r.LastName).HasMaxLength(100).IsRequired();
            b.Property(r => r.Email).HasMaxLength(256).IsRequired();
            b.Property(r => r.RepresentativeRole)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(r => r.College)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(r => r.CreatedBy).HasMaxLength(256);
            b.Property(r => r.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(r => r.RowVersion).IsRowVersion();
            }

            b.HasIndex(r => new { r.CompanyId, r.Email }).IsUnique();
            b.HasIndex(r => new { r.CompanyId, r.IsHarassmentAdvisor });
        });

        builder.Entity<RseIdea>(b =>
        {
            b.ToTable("rse_ideas");
            b.HasKey(i => i.Id);

            b.Property(i => i.Title).HasMaxLength(200).IsRequired();
            b.Property(i => i.IdeaContent).HasMaxLength(4000).IsRequired();
            b.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(i => i.RhComment).HasMaxLength(2000);
            b.Property(i => i.AttachmentFileKey).HasMaxLength(512);
            b.Property(i => i.AttachmentFileName).HasMaxLength(256);
            b.Property(i => i.AttachmentContentType).HasMaxLength(128);
            b.Property(i => i.CreatedBy).HasMaxLength(256);
            b.Property(i => i.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(i => i.RowVersion).IsRowVersion();
            }

            b.HasIndex(i => new { i.AuthorEmployeeId, i.Status });
            b.HasIndex(i => new { i.LocationId, i.Status });
        });
    }
}
