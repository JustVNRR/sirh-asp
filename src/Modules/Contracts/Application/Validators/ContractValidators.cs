using FluentValidation;
using Sirh.Modules.Contracts.Application.DTOs;

namespace Sirh.Modules.Contracts.Application.Validators;

public sealed class CreateContractModelRequestValidator : AbstractValidator<CreateContractModelRequest>
{
    public CreateContractModelRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.TemplateContent).NotEmpty();
    }
}

public sealed class UpdateContractModelRequestValidator : AbstractValidator<UpdateContractModelRequest>
{
    public UpdateContractModelRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.TemplateContent).NotEmpty();
    }
}

public sealed class PreviewContractRequestValidator : AbstractValidator<PreviewContractRequest>
{
    public PreviewContractRequestValidator()
    {
        RuleFor(x => x.PositionId).NotEmpty();
    }
}

public sealed class GenerateContractRequestValidator : AbstractValidator<GenerateContractRequest>
{
    public GenerateContractRequestValidator()
    {
        RuleFor(x => x.ContractModelId).NotEmpty();
        RuleFor(x => x.PositionId).NotEmpty();
        RuleFor(x => x.ContractTypeId).NotEmpty();
        RuleFor(x => x.Commentary).MaximumLength(1000);
    }
}
