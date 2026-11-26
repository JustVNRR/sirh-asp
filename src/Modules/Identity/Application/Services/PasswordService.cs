using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Application.Services;

public sealed class PasswordService : IPasswordService
{
    public const int OtpTtlMinutes = 15;
    public const int ResendCooldownSeconds = 60;

    private static readonly Error UserNotFoundOrInactiveError =
        Error.Unauthorized("Auth.UserNotFound", "Authenticated user was not found or is inactive.");

    private static readonly Error InvalidCurrentPasswordError =
        Error.Validation("Auth.InvalidCurrentPassword", "The current password is incorrect.");

    private static readonly Error InvalidOrExpiredOtpError =
        Error.Validation("Auth.InvalidOtp", "The reset code is invalid or has expired.");

    private static readonly Error OtpInvalidatedError =
        Error.Validation("Auth.OtpInvalidated", "Too many invalid attempts. Please request a new reset code.");

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailService _emailService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<ChangePasswordRequest> _changeValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(
        UserManager<ApplicationUser> userManager,
        IPasswordResetTokenRepository tokenRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IEmailService emailService,
        IDateTimeProvider dateTimeProvider,
        IValidator<ChangePasswordRequest> changeValidator,
        IValidator<ForgotPasswordRequest> forgotValidator,
        IValidator<ResetPasswordRequest> resetValidator,
        ILogger<PasswordService> logger)
    {
        _userManager = userManager;
        _tokenRepository = tokenRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailService = emailService;
        _dateTimeProvider = dateTimeProvider;
        _changeValidator = changeValidator;
        _forgotValidator = forgotValidator;
        _resetValidator = resetValidator;
        _logger = logger;
    }

    public async Task<Result> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        await _changeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return Result.Failure(UserNotFoundOrInactiveError);
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => string.Equals(e.Code, "PasswordMismatch", StringComparison.Ordinal)))
            {
                return Result.Failure(InvalidCurrentPasswordError);
            }

            return Result.Failure(MapIdentityPasswordErrors(result));
        }

        var utcNow = _dateTimeProvider.UtcNow;
        user.UpdatedAtUtc = utcNow;
        await _userManager.UpdateSecurityStampAsync(user);
        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, utcNow, "Password changed", cancellationToken);
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} changed password and all refresh tokens were revoked.", user.Id);
        return Result.Success();
    }

    public async Task<Result> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await _forgotValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
        {
            return Result.Success();
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var latestToken = await _tokenRepository.GetLatestByUserIdAsync(user.Id, cancellationToken);
        if (latestToken is not null && utcNow - latestToken.CreatedAtUtc < TimeSpan.FromSeconds(ResendCooldownSeconds))
        {
            _logger.LogInformation(
                "Password reset cooldown active for user {UserId}. Skipping new OTP emission.",
                user.Id);
            return Result.Success();
        }

        await _tokenRepository.InvalidateActiveForUserAsync(user.Id, utcNow, cancellationToken);

        var otpCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var otpHash = _jwtTokenGenerator.HashToken(otpCode);

        var token = new PasswordResetToken(
            user.Id,
            otpHash,
            utcNow,
            utcNow.AddMinutes(OtpTtlMinutes),
            ipAddress);

        await _tokenRepository.AddAsync(token, cancellationToken);
        await _tokenRepository.SaveChangesAsync(cancellationToken);

        var encodedEmail = WebUtility.HtmlEncode(user.Email);
        var encodedOtp = WebUtility.HtmlEncode(otpCode);

        var emailMessage = new EmailMessage(
            user.Email,
            "SIRH - Code de réinitialisation de mot de passe",
            $"""
            <p>Bonjour,</p>
            <p>Vous avez demandé la réinitialisation du mot de passe pour le compte <strong>{encodedEmail}</strong>.</p>
            <p>Votre code de vérification à usage unique est : <strong style="font-size:18px;letter-spacing:2px;">{encodedOtp}</strong></p>
            <p>Ce code expire dans {OtpTtlMinutes} minutes. Si vous n'êtes pas à l'origine de cette demande, ignorez ce message.</p>
            """,
            $"Votre code de reinitialisation SIRH est : {otpCode} (valable {OtpTtlMinutes} minutes).");

        await _emailService.SendAsync(emailMessage, cancellationToken);

        _logger.LogInformation("Issued password reset OTP for user {UserId}.", user.Id);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        await _resetValidator.ValidateAndThrowAsync(request, cancellationToken);

        var normalizedEmail = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user is null || !user.IsActive)
        {
            return Result.Failure(InvalidOrExpiredOtpError);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var token = await _tokenRepository.GetLatestUsableByUserIdAsync(user.Id, utcNow, cancellationToken);
        if (token is null || !token.IsUsable(utcNow))
        {
            return Result.Failure(InvalidOrExpiredOtpError);
        }

        var providedHash = _jwtTokenGenerator.HashToken(request.OtpCode);
        if (!ConstantTimeEquals(token.OtpHash, providedHash))
        {
            token.RegisterFailedAttempt(utcNow);
            await _tokenRepository.SaveChangesAsync(cancellationToken);

            if (token.InvalidatedAtUtc.HasValue)
            {
                _logger.LogWarning(
                    "Password reset OTP {TokenId} invalidated for user {UserId} after {Attempts} failed attempts.",
                    token.Id,
                    user.Id,
                    token.FailedAttempts);
                return Result.Failure(OtpInvalidatedError);
            }

            return Result.Failure(InvalidOrExpiredOtpError);
        }

        var identityResetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, identityResetToken, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            return Result.Failure(MapIdentityPasswordErrors(resetResult));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        user.UpdatedAtUtc = utcNow;
        await _userManager.UpdateAsync(user);

        token.MarkUsed(utcNow);
        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, utcNow, "Password reset via OTP", cancellationToken);
        await _tokenRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} completed OTP password reset and all sessions were revoked.", user.Id);
        return Result.Success();
    }

    private static bool ConstantTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
               && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static Error MapIdentityPasswordErrors(IdentityResult result)
    {
        var description = string.Join(" ", result.Errors.Select(e => e.Description));
        return Error.Validation("Auth.PasswordPolicyViolation", description);
    }
}
