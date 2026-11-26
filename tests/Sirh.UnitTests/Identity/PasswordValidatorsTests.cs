using Shouldly;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Application.Validators;
using Xunit;

namespace Sirh.UnitTests.Identity;

public sealed class PasswordValidatorsTests
{
    [Fact]
    public void ChangePasswordValidator_Should_Accept_Strong_Different_Password()
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest("OldPass123!", "NewStrong123!"));
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Short1!", "Short1!")]
    [InlineData("OldPass123!", "OldPass123!")]
    [InlineData("OldPass123!", "lowercase123!")]
    [InlineData("OldPass123!", "UPPERCASE123!")]
    [InlineData("OldPass123!", "NoDigitsHere!")]
    [InlineData("OldPass123!", "NoSpecialChar123")]
    public void ChangePasswordValidator_Should_Reject_Weak_Or_Identical_Password(string current, string next)
    {
        var validator = new ChangePasswordRequestValidator();
        var result = validator.Validate(new ChangePasswordRequest(current, next));
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ForgotPasswordValidator_Should_Validate_Email()
    {
        var validator = new ForgotPasswordRequestValidator();
        validator.Validate(new ForgotPasswordRequest("user@nova-conseil.fr")).IsValid.ShouldBeTrue();
        validator.Validate(new ForgotPasswordRequest("not-an-email")).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("user@nova-conseil.fr", "123456", "NewStrong123!", true)]
    [InlineData("user@nova-conseil.fr", "12345", "NewStrong123!", false)]
    [InlineData("user@nova-conseil.fr", "abcdef", "NewStrong123!", false)]
    [InlineData("user@nova-conseil.fr", "123456", "weak", false)]
    public void ResetPasswordValidator_Should_Validate_Otp_And_Password(
        string email,
        string otp,
        string newPassword,
        bool expectedValid)
    {
        var validator = new ResetPasswordRequestValidator();
        var result = validator.Validate(new ResetPasswordRequest(email, otp, newPassword));
        result.IsValid.ShouldBe(expectedValid);
    }
}
