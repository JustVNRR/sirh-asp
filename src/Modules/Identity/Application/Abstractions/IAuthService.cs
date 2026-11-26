using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.DTOs;

namespace Sirh.Modules.Identity.Application.Abstractions;

public interface IAuthService
{
    Task<Result<AuthTokensResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result<AuthTokensResponse>> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<Result> LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> LogoutAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
