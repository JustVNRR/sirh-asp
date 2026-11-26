using FluentValidation;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;

namespace Sirh.Modules.HumanResources.Application.Validators;

public sealed class CreateEmergencyContactRequestValidator : AbstractValidator<CreateEmergencyContactRequest>
{
    public CreateEmergencyContactRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PriorityOrder).InclusiveBetween(1, 2);
        RuleFor(x => x.MobilePhone).MaximumLength(30);
        RuleFor(x => x.HomePhone).MaximumLength(30);
        RuleFor(x => x.WorkPhone).MaximumLength(30);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.MobilePhone) ||
                       !string.IsNullOrWhiteSpace(x.HomePhone) ||
                       !string.IsNullOrWhiteSpace(x.WorkPhone))
            .WithMessage("At least one phone number (mobile, home, or work) is required.");
    }
}

public sealed class UpdateEmergencyContactRequestValidator : AbstractValidator<UpdateEmergencyContactRequest>
{
    public UpdateEmergencyContactRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Relationship).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PriorityOrder).InclusiveBetween(1, 2);
        RuleFor(x => x.MobilePhone).MaximumLength(30);
        RuleFor(x => x.HomePhone).MaximumLength(30);
        RuleFor(x => x.WorkPhone).MaximumLength(30);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.MobilePhone) ||
                       !string.IsNullOrWhiteSpace(x.HomePhone) ||
                       !string.IsNullOrWhiteSpace(x.WorkPhone))
            .WithMessage("At least one phone number (mobile, home, or work) is required.");
    }
}

public sealed class CreatePositionRequestValidator : AbstractValidator<CreatePositionRequest>
{
    public CreatePositionRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ManagerId)
            .Must((req, managerId) => !managerId.HasValue || managerId.Value != req.EmployeeId)
            .WithMessage("An employee cannot be their own manager.");
        RuleFor(x => x.EndAt)
            .Must((req, endAt) => !endAt.HasValue || endAt.Value >= req.StartAt)
            .WithMessage("EndAt must be on or after StartAt.");
        RuleFor(x => x.Rate)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Rate.HasValue);
        RuleFor(x => x.TimeContractAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TimeContractAmount.HasValue);
        RuleFor(x => x.TimeContractType)
            .Must(t => string.IsNullOrWhiteSpace(t) ||
                       HrPermissions.TimeContractTypes.Contains(t.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"TimeContractType must be one of: {string.Join(", ", HrPermissions.TimeContractTypes)}.");
        RuleFor(x => x.ContractLabel).MaximumLength(150);
        RuleFor(x => x.Coefficient).MaximumLength(50);
        RuleFor(x => x.Supervisor).MaximumLength(150);
        RuleFor(x => x.HierarchicalPosition).MaximumLength(120);
    }
}

public sealed class UpdatePositionRequestValidator : AbstractValidator<UpdatePositionRequest>
{
    public UpdatePositionRequestValidator()
    {
        RuleFor(x => x.JobTitle).NotEmpty().MaximumLength(150);
        RuleFor(x => x.EndAt)
            .Must((req, endAt) => !endAt.HasValue || endAt.Value >= req.StartAt)
            .WithMessage("EndAt must be on or after StartAt.");
        RuleFor(x => x.Rate)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Rate.HasValue);
        RuleFor(x => x.TimeContractAmount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TimeContractAmount.HasValue);
        RuleFor(x => x.TimeContractType)
            .Must(t => string.IsNullOrWhiteSpace(t) ||
                       HrPermissions.TimeContractTypes.Contains(t.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage($"TimeContractType must be one of: {string.Join(", ", HrPermissions.TimeContractTypes)}.");
        RuleFor(x => x.ContractLabel).MaximumLength(150);
        RuleFor(x => x.Coefficient).MaximumLength(50);
        RuleFor(x => x.Supervisor).MaximumLength(150);
        RuleFor(x => x.HierarchicalPosition).MaximumLength(120);
    }
}
