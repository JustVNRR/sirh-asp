using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Identity.Domain.Entities;

public sealed class PasswordResetToken : Entity
{
    public const int MaxFailedAttempts = 5;

    private PasswordResetToken()
    {
    }

    public PasswordResetToken(
        Guid userId,
        string otpHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        string? requestedByIp)
    {
        UserId = userId;
        OtpHash = otpHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        RequestedByIp = requestedByIp;
    }

    public Guid UserId { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    public string OtpHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public string? RequestedByIp { get; private set; }

    public int FailedAttempts { get; private set; }
    public DateTime? UsedAtUtc { get; private set; }
    public DateTime? InvalidatedAtUtc { get; private set; }

    public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresAtUtc;

    public bool IsUsable(DateTime utcNow) =>
        !UsedAtUtc.HasValue
        && !InvalidatedAtUtc.HasValue
        && FailedAttempts < MaxFailedAttempts
        && !IsExpired(utcNow);

    public void RegisterFailedAttempt(DateTime utcNow)
    {
        FailedAttempts++;
        if (FailedAttempts >= MaxFailedAttempts && !InvalidatedAtUtc.HasValue)
        {
            InvalidatedAtUtc = utcNow;
        }
    }

    public void MarkUsed(DateTime utcNow)
    {
        if (!UsedAtUtc.HasValue)
        {
            UsedAtUtc = utcNow;
        }
    }

    public void Invalidate(DateTime utcNow)
    {
        if (!UsedAtUtc.HasValue && !InvalidatedAtUtc.HasValue)
        {
            InvalidatedAtUtc = utcNow;
        }
    }
}
