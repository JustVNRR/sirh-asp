using Shouldly;
using Sirh.Modules.Identity.Domain.Entities;
using Xunit;

namespace Sirh.UnitTests.Identity;

public sealed class RefreshTokenTests
{
    [Fact]
    public void RefreshToken_Should_Track_Active_Expired_And_Revoked_States()
    {
        var now = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
        var token = new RefreshToken(
            Guid.CreateVersion7(),
            "hash-abc",
            "family-1",
            now,
            now.AddDays(7),
            "127.0.0.1",
            "xunit");

        token.IsRevoked.ShouldBeFalse();
        token.IsExpired(now.AddDays(1)).ShouldBeFalse();
        token.IsActive(now.AddDays(1)).ShouldBeTrue();
        token.IsExpired(now.AddDays(8)).ShouldBeTrue();
        token.IsActive(now.AddDays(8)).ShouldBeFalse();

        token.Revoke(now.AddHours(1), "Rotated", "hash-def");

        token.IsRevoked.ShouldBeTrue();
        token.IsActive(now.AddHours(2)).ShouldBeFalse();
        token.RevokedReason.ShouldBe("Rotated");
        token.ReplacedByTokenHash.ShouldBe("hash-def");
    }
}
