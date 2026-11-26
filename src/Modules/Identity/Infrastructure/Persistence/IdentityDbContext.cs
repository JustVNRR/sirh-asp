using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public const string Schema = "identity";

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema(Schema);

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.IsActive).HasDefaultValue(true);
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("roles");
            b.Property(r => r.Code).HasMaxLength(64).IsRequired();
            b.Property(r => r.Description).HasMaxLength(256);
            b.HasIndex(r => r.Code).IsUnique();
        });

        builder.Entity<IdentityUserRole<Guid>>(b => b.ToTable("user_roles"));
        builder.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("user_claims"));
        builder.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("user_logins"));
        builder.Entity<IdentityUserToken<Guid>>(b => b.ToTable("user_tokens"));
        builder.Entity<IdentityRoleClaim<Guid>>(b => b.ToTable("role_claims"));

        builder.Entity<Permission>(b =>
        {
            b.ToTable("permissions");
            b.HasKey(p => p.Id);
            b.Property(p => p.Code).HasMaxLength(128).IsRequired();
            b.Property(p => p.Module).HasMaxLength(64).IsRequired();
            b.Property(p => p.Description).HasMaxLength(256).IsRequired();
            b.HasIndex(p => p.Code).IsUnique();
        });

        builder.Entity<RolePermission>(b =>
        {
            b.ToTable("role_permissions");
            b.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            b.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(rt => rt.Id);
            b.Property(rt => rt.TokenHash).HasMaxLength(128).IsRequired();
            b.Property(rt => rt.FamilyId).HasMaxLength(64).IsRequired();
            b.Property(rt => rt.CreatedByIp).HasMaxLength(64);
            b.Property(rt => rt.UserAgent).HasMaxLength(512);
            b.Property(rt => rt.RevokedReason).HasMaxLength(256);
            b.Property(rt => rt.ReplacedByTokenHash).HasMaxLength(128);

            b.HasIndex(rt => rt.TokenHash).IsUnique();
            b.HasIndex(rt => rt.FamilyId);
            b.HasIndex(rt => rt.UserId);

            b.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PasswordResetToken>(b =>
        {
            b.ToTable("password_reset_tokens");
            b.HasKey(t => t.Id);

            b.Property(t => t.OtpHash).HasMaxLength(128).IsRequired();
            b.Property(t => t.RequestedByIp).HasMaxLength(64);

            b.HasIndex(t => new { t.UserId, t.CreatedAtUtc });

            b.HasOne(t => t.User)
                .WithMany(u => u.PasswordResetTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
