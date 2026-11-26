using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.DTOs;

namespace Sirh.Modules.Identity.Application.Abstractions;

public interface IPasswordService
{
    Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);
}
