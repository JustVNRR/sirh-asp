using FluentValidation;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;

namespace Sirh.Modules.HumanResources.Application.Validators;

internal static class EmployeeValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidHonorific<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(h => !string.IsNullOrWhiteSpace(h) &&
                       HrPermissions.HonorificValues.Contains(h.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Honorific must be one of: {string.Join(", ", HrPermissions.HonorificValues)}.");

    public static IRuleBuilderOptions<T, string> ValidPasswordPolicy<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters long.")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character.");
}

public sealed class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Honorific).ValidHonorific();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).ValidPasswordPolicy();
        RuleFor(x => x.RoleCodes).NotNull().NotEmpty().WithMessage("At least one role is required.");
        RuleForEach(x => x.RoleCodes).NotEmpty();

        RuleFor(x => x.BirthPlace).MaximumLength(120);
        RuleFor(x => x.BirthDepartmentCode).MaximumLength(10);
        RuleFor(x => x.NationalId).MaximumLength(30);
        RuleFor(x => x.Landline).MaximumLength(30);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.PersonalLandline).MaximumLength(30);
        RuleFor(x => x.PersonalMobile).MaximumLength(30);
        RuleFor(x => x.PersonalEmail)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));
    }
}

public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Honorific).ValidHonorific();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);

        RuleFor(x => x.RoleCodes)
            .Must(roles => roles is null || roles.Count > 0)
            .WithMessage("RoleCodes cannot be empty when supplied.");
        RuleForEach(x => x.RoleCodes).NotEmpty().When(x => x.RoleCodes is not null);

        RuleFor(x => x.BirthPlace).MaximumLength(120);
        RuleFor(x => x.BirthDepartmentCode).MaximumLength(10);
        RuleFor(x => x.NationalId).MaximumLength(30);
        RuleFor(x => x.Landline).MaximumLength(30);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.PersonalLandline).MaximumLength(30);
        RuleFor(x => x.PersonalMobile).MaximumLength(30);
        RuleFor(x => x.PersonalEmail)
            .EmailAddress()
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));
    }
}
