using FluentValidation;
using Sirh.BuildingBlocks.Domain.Validation;
using Sirh.Modules.Organization.Application.DTOs;

namespace Sirh.Modules.Organization.Application.Validators;

internal static class OrganizationValidationRules
{
    public static IRuleBuilderOptions<T, string> ValidRequiredSiret<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("SIRET is required.")
            .Must(SiretValidator.IsValid)
            .WithMessage("SIRET must be a valid 14-digit French SIRET number.");

    public static IRuleBuilderOptions<T, string?> ValidOptionalSiret<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(siret => string.IsNullOrWhiteSpace(siret) || SiretValidator.IsValid(siret))
            .WithMessage("SIRET must be a valid 14-digit French SIRET number when provided.");

    public static IRuleBuilderOptions<T, string?> ValidOptionalHttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(url => string.IsNullOrWhiteSpace(url) ||
                         (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
                          (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("LogoUrl must be a valid http or https URL.");

    public static IRuleBuilderOptions<T, string> ValidHexColorCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("ColorCode is required.")
            .Matches(@"^#[0-9A-Fa-f]{6}$")
            .WithMessage("ColorCode must be a valid #RRGGBB hexadecimal color.");
}

public sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Siret).ValidRequiredSiret();
        RuleFor(x => x.CollectiveAgreement).MaximumLength(150);
        RuleFor(x => x.LegalForm).MaximumLength(80);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.Register).MaximumLength(120);
        RuleFor(x => x.Capital).MaximumLength(100);
        RuleFor(x => x.Owner).MaximumLength(150);
        RuleFor(x => x.LogoUrl).MaximumLength(500).ValidOptionalHttpUrl();
    }
}

public sealed class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(250);
        RuleFor(x => x.Siret).ValidRequiredSiret();
        RuleFor(x => x.CollectiveAgreement).MaximumLength(150);
        RuleFor(x => x.LegalForm).MaximumLength(80);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.City).MaximumLength(100);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.Register).MaximumLength(120);
        RuleFor(x => x.Capital).MaximumLength(100);
        RuleFor(x => x.Owner).MaximumLength(150);
        RuleFor(x => x.LogoUrl).MaximumLength(500).ValidOptionalHttpUrl();
    }
}

public sealed class CreateLocationRequestValidator : AbstractValidator<CreateLocationRequest>
{
    public CreateLocationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(250);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ColorCode).ValidHexColorCode();
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.DepartmentCode).MaximumLength(10);
        RuleFor(x => x.Siret).ValidOptionalSiret();
    }
}

public sealed class UpdateLocationRequestValidator : AbstractValidator<UpdateLocationRequest>
{
    public UpdateLocationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(250);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ColorCode).ValidHexColorCode();
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.DepartmentCode).MaximumLength(10);
        RuleFor(x => x.Siret).ValidOptionalSiret();
    }
}

public sealed class CreateContractTypeRequestValidator : AbstractValidator<CreateContractTypeRequest>
{
    public CreateContractTypeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("ContractType code can only contain letters, digits, underscores, and hyphens.");

        RuleFor(x => x.Label).NotEmpty().MaximumLength(150);
    }
}

public sealed class UpdateContractTypeRequestValidator : AbstractValidator<UpdateContractTypeRequest>
{
    public UpdateContractTypeRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(50)
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("ContractType code can only contain letters, digits, underscores, and hyphens.");

        RuleFor(x => x.Label).NotEmpty().MaximumLength(150);
    }
}
