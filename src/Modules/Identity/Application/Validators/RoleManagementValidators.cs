using FluentValidation;
using Sirh.Modules.Identity.Application.DTOs;

namespace Sirh.Modules.Identity.Application.Validators;

public sealed class UpdateRolePermissionsRequestValidator : AbstractValidator<UpdateRolePermissionsRequest>
{
    public UpdateRolePermissionsRequestValidator()
    {
        RuleFor(x => x.PermissionCodes)
            .NotNull().WithMessage("PermissionCodes is required.");

        RuleForEach(x => x.PermissionCodes)
            .NotEmpty().WithMessage("Permission code cannot be empty.");
    }
}
