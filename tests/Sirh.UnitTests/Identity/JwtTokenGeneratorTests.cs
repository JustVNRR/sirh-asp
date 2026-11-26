using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.Modules.Identity.Application.Options;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Identity.Infrastructure.Security;
using Xunit;

namespace Sirh.UnitTests.Identity;

public sealed class JwtTokenGeneratorTests
{
    private const string SigningKey = "UnitTestJwtSigningKeyAtLeast32CharactersLong!";

    private static JwtTokenGenerator CreateGenerator(DateTime? now = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(now ?? new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc));

        var options = Options.Create(new JwtOptions
        {
            SigningKey = SigningKey,
            Issuer = "Sirh.Api",
            Audience = "Sirh.Clients",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });

        return new JwtTokenGenerator(options, clock);
    }

    [Fact]
    public void GenerateAccessToken_Should_Include_Expected_Claims_And_15Min_Expiry()
    {
        var now = new DateTime(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);
        var sut = CreateGenerator(now);
        var employeeId = Guid.CreateVersion7();
        var user = new ApplicationUser
        {
            Email = "alice.durand@nova-conseil.fr",
            EmployeeId = employeeId
        };

        var (token, expiresAt) = sut.GenerateAccessToken(
            user,
            ["RH", "Admin"],
            ["employees:read:all", "employees:write"]);

        expiresAt.ShouldBe(now.AddMinutes(15));

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            ValidateIssuer = true,
            ValidIssuer = "Sirh.Api",
            ValidateAudience = true,
            ValidAudience = "Sirh.Clients",
            ValidateLifetime = false
        }, out _);

        principal.FindFirst("sub")?.Value.ShouldBe(user.Id.ToString());
        principal.FindFirst("email")?.Value.ShouldBe("alice.durand@nova-conseil.fr");
        principal.FindFirst("employee_id")?.Value.ShouldBe(employeeId.ToString());
        principal.FindAll("role").Select(c => c.Value).ShouldBe(["RH", "Admin"], ignoreOrder: true);
        principal.FindAll("permission").Select(c => c.Value)
            .ShouldBe(["employees:read:all", "employees:write"], ignoreOrder: true);
    }

    [Fact]
    public void GenerateOpaqueRefreshToken_And_HashToken_Should_Be_Unique_And_Deterministic()
    {
        var sut = CreateGenerator();

        var token1 = sut.GenerateOpaqueRefreshToken();
        var token2 = sut.GenerateOpaqueRefreshToken();

        token1.ShouldNotBeNullOrWhiteSpace();
        token1.ShouldNotBe(token2);

        var hash1a = sut.HashToken(token1);
        var hash1b = sut.HashToken(token1);
        hash1a.Length.ShouldBe(64);
        hash1a.ShouldBe(hash1b);
        hash1a.ShouldNotBe(sut.HashToken(token2));
    }
}
