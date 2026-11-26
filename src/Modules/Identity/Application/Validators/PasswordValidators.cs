using FluentValidation;
using Sirh.Modules.Identity.Application.DTOs;

namespace Sirh.Modules.Identity.Application.Validators;

internal static class PasswordValidatorRules
{
    public const int MinimumLength = 10;

    public static IRuleBuilderOptions<T, string> ApplyStrongPasswordRules<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(MinimumLength).WithMessage($"Password must be at least {MinimumLength} characters long.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .ApplyStrongPasswordRules()
            .NotEqual(x => x.CurrentPassword).WithMessage("New password must be different from current password.");
    }
}

public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email format is invalid.");

        RuleFor(x => x.OtpCode)
            .NotEmpty().WithMessage("OTP code is required.")
            .Matches(@"^\d{6}$").WithMessage("OTP code must be a 6-digit numeric code.");

        RuleFor(x => x.NewPassword)
            .ApplyStrongPasswordRules();
    }
}
