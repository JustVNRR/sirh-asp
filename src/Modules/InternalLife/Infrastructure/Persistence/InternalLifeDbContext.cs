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
    public DbSet<OnboardingQuiz> OnboardingQuizzes => Set<OnboardingQuiz>();
    public DbSet<QuizQuestion> QuizQuestions => Set<QuizQuestion>();
    public DbSet<QuizChoice> QuizChoices => Set<QuizChoice>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<QuizAttemptAnswer> QuizAttemptAnswers => Set<QuizAttemptAnswer>();

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

        builder.Entity<OnboardingQuiz>(b =>
        {
            b.ToTable("onboarding_quizzes");
            b.HasKey(q => q.Id);

            b.Property(q => q.Title).HasMaxLength(200).IsRequired();
            b.Property(q => q.Description).HasMaxLength(2000);
            b.Property(q => q.CreatedBy).HasMaxLength(256);
            b.Property(q => q.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(q => q.RowVersion).IsRowVersion();
            }

            b.HasIndex(q => new { q.CompanyId, q.IsPublished });
        });

        builder.Entity<QuizQuestion>(b =>
        {
            b.ToTable("quiz_questions");
            b.HasKey(q => q.Id);

            b.Property(q => q.QuestionText).HasMaxLength(1000).IsRequired();
            b.Property(q => q.QuestionType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(q => q.Explanation).HasMaxLength(2000);

            b.HasIndex(q => new { q.QuizId, q.OrderIndex });

            b.HasOne(q => q.Quiz)
                .WithMany(z => z.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuizChoice>(b =>
        {
            b.ToTable("quiz_choices");
            b.HasKey(c => c.Id);

            b.Property(c => c.ChoiceText).HasMaxLength(500).IsRequired();

            b.HasIndex(c => new { c.QuestionId, c.OrderIndex });

            b.HasOne(c => c.Question)
                .WithMany(q => q.Choices)
                .HasForeignKey(c => c.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuizAttempt>(b =>
        {
            b.ToTable("quiz_attempts");
            b.HasKey(a => a.Id);

            b.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            b.Property(a => a.CreatedBy).HasMaxLength(256);
            b.Property(a => a.UpdatedBy).HasMaxLength(256);

            if (!isInMemory)
            {
                b.Property(a => a.RowVersion).IsRowVersion();
            }

            b.HasIndex(a => new { a.QuizId, a.EmployeeId });

            b.HasOne(a => a.Quiz)
                .WithMany(q => q.Attempts)
                .HasForeignKey(a => a.QuizId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<QuizAttemptAnswer>(b =>
        {
            b.ToTable("quiz_attempt_answers");
            b.HasKey(a => a.Id);

            b.Property(a => a.SelectedChoiceIdsJson).HasMaxLength(2000).IsRequired();

            b.HasIndex(a => new { a.AttemptId, a.QuestionId });

            b.HasOne(a => a.Attempt)
                .WithMany(t => t.Answers)
                .HasForeignKey(a => a.AttemptId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
