namespace Sirh.Modules.Identity.Application.DTOs;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string OtpCode, string NewPassword);
