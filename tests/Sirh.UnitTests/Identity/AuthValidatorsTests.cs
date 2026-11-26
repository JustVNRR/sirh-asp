using Shouldly;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Application.Validators;
using Xunit;

namespace Sirh.UnitTests.Identity;

public sealed class AuthValidatorsTests
{
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly RefreshTokenRequestValidator _refreshValidator = new();

    [Fact]
    public async Task LoginValidator_Should_Reject_Invalid_Email_And_Empty_Password()
    {
        var result = await _loginValidator.ValidateAsync(new LoginRequest("not-an-email", ""));

        result.IsValid.ShouldBeFalse();
        result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task LoginValidator_Should_Accept_Valid_Credentials()
    {
        var result = await _loginValidator.ValidateAsync(new LoginRequest("admin.rh@nova-conseil.fr", "SirhDemo123!"));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshValidator_Should_Reject_Empty_Token()
    {
        var result = await _refreshValidator.ValidateAsync(new RefreshTokenRequest("  "));

        result.IsValid.ShouldBeFalse();
    }
}
