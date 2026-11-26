using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Application.Options;
using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Application.Services;

public sealed class AuthService : IAuthService
{
    private static readonly Error InvalidCredentialsError =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

    private static readonly Error AccountLockedError =
        Error.Locked("Auth.AccountLocked", "Account is temporarily locked due to too many failed login attempts.");

    private static readonly Error InvalidRefreshTokenError =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtOptions _jwtOptions;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IRefreshTokenRepository refreshTokenRepository,
        IPermissionRepository permissionRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IOptions<JwtOptions> jwtOptions,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _refreshTokenRepository = refreshTokenRepository;
        _permissionRepository = permissionRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _jwtOptions = jwtOptions.Value;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _logger = logger;
    }

    public async Task<Result<AuthTokensResponse>> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !user.IsActive)
        {
            return InvalidCredentialsError;
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return AccountLockedError;
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                _logger.LogWarning("User {UserId} locked out after repeated failed logins.", user.Id);
                return AccountLockedError;
            }

            return InvalidCredentialsError;
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var familyId = Guid.NewGuid().ToString("N");
        var tokens = await IssueTokensAsync(user, familyId, ipAddress, userAgent, cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return tokens;
    }

    public async Task<Result<AuthTokensResponse>> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        await _refreshValidator.ValidateAndThrowAsync(request, cancellationToken);

        var tokenHash = _jwtTokenGenerator.HashToken(request.RefreshToken.Trim());
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existingToken is null)
        {
            return InvalidRefreshTokenError;
        }

        var now = _dateTimeProvider.UtcNow;

        if (existingToken.IsRevoked)
        {
            _logger.LogWarning(
                "Refresh token reuse detected for user {UserId}, family {FamilyId}. Revoking entire family.",
                existingToken.UserId,
                existingToken.FamilyId);

            await _refreshTokenRepository.RevokeFamilyAsync(
                existingToken.FamilyId,
                now,
                "Token reuse detected",
                cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

            return InvalidRefreshTokenError;
        }

        if (existingToken.IsExpired(now))
        {
            existingToken.Revoke(now, "Expired");
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
            return InvalidRefreshTokenError;
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user is null || !user.IsActive || await _userManager.IsLockedOutAsync(user))
        {
            await _refreshTokenRepository.RevokeFamilyAsync(
                existingToken.FamilyId,
                now,
                "User inactive or locked",
                cancellationToken);
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
            return InvalidRefreshTokenError;
        }

        var newTokens = await IssueTokensAsync(
            user,
            existingToken.FamilyId,
            ipAddress,
            userAgent,
            cancellationToken);

        var newTokenHash = _jwtTokenGenerator.HashToken(newTokens.RefreshToken);
        existingToken.Revoke(now, "Rotated", newTokenHash);

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        return newTokens;
    }

    public async Task<Result> LogoutAsync(
        Guid userId,
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Success();
        }

        var tokenHash = _jwtTokenGenerator.HashToken(request.RefreshToken.Trim());
        var existingToken = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (existingToken is not null && existingToken.UserId == userId && !existingToken.IsRevoked)
        {
            existingToken.Revoke(_dateTimeProvider.UtcNow, "User logout");
            await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result> LogoutAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(
            userId,
            _dateTimeProvider.UtcNow,
            "Logout all sessions",
            cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(
        ApplicationUser user,
        string familyId,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var roles = (await _userManager.GetRolesAsync(user)).ToArray();
        var permissions = await _permissionRepository.GetPermissionsForRolesAsync(roles, cancellationToken);

        var (accessToken, accessExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(user, roles, permissions);

        var rawRefreshToken = _jwtTokenGenerator.GenerateOpaqueRefreshToken();
        var refreshHash = _jwtTokenGenerator.HashToken(rawRefreshToken);
        var now = _dateTimeProvider.UtcNow;
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        var refreshTokenEntity = new RefreshToken(
            user.Id,
            refreshHash,
            familyId,
            now,
            refreshExpiresAt,
            ipAddress,
            userAgent);

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);

        return new AuthTokensResponse(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            refreshExpiresAt);
    }
}
