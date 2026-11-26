using Shouldly;
using Sirh.Modules.Identity.Domain.Entities;
using Xunit;

namespace Sirh.UnitTests.Identity;

public sealed class PasswordResetTokenTests
{
    [Fact]
    public void NewToken_Should_Be_Usable_Until_Expiry()
    {
        var now = new DateTime(2026, 11, 26, 20, 0, 0, DateTimeKind.Utc);
        var token = new PasswordResetToken(
            Guid.CreateVersion7(),
            "hash",
            now,
            now.AddMinutes(15),
            "127.0.0.1");

        token.IsExpired(now.AddMinutes(14)).ShouldBeFalse();
        token.IsUsable(now.AddMinutes(14)).ShouldBeTrue();
        token.IsExpired(now.AddMinutes(15)).ShouldBeTrue();
        token.IsUsable(now.AddMinutes(15)).ShouldBeFalse();
    }

    [Fact]
    public void RegisterFailedAttempt_Should_Invalidate_On_Fifth_Failure()
    {
        var now = new DateTime(2026, 11, 26, 20, 0, 0, DateTimeKind.Utc);
        var token = new PasswordResetToken(
            Guid.CreateVersion7(),
            "hash",
            now,
            now.AddMinutes(15),
            null);

        for (var i = 0; i < 4; i++)
        {
            token.RegisterFailedAttempt(now.AddSeconds(i + 1));
            token.InvalidatedAtUtc.ShouldBeNull();
            token.IsUsable(now.AddSeconds(i + 1)).ShouldBeTrue();
        }

        token.RegisterFailedAttempt(now.AddSeconds(5));
        token.FailedAttempts.ShouldBe(PasswordResetToken.MaxFailedAttempts);
        token.InvalidatedAtUtc.ShouldNotBeNull();
        token.IsUsable(now.AddSeconds(5)).ShouldBeFalse();
    }

    [Fact]
    public void MarkUsed_Should_Make_Token_Unusable()
    {
        var now = new DateTime(2026, 11, 26, 20, 0, 0, DateTimeKind.Utc);
        var token = new PasswordResetToken(
            Guid.CreateVersion7(),
            "hash",
            now,
            now.AddMinutes(15),
            null);

        token.MarkUsed(now.AddMinutes(1));
        token.UsedAtUtc.ShouldNotBeNull();
        token.IsUsable(now.AddMinutes(2)).ShouldBeFalse();
    }
}
