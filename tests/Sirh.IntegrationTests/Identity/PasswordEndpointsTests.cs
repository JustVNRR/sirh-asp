using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Sirh.IntegrationTests.Identity;

public sealed partial class PasswordEndpointsTests
{
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex OtpRegex();

    [Fact]
    public async Task ChangePassword_With_Valid_Credentials_Should_Return_204_Revoke_Tokens_And_Update_Password()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, DemoPassword));
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialTokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        initialTokens.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", initialTokens.AccessToken);

        const string newPassword = "UpdatedPassword123!";
        var changeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/change",
            new ChangePasswordRequest(DemoPassword, newPassword));
        changeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;

        var oldRefreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(initialTokens.RefreshToken));
        oldRefreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var oldLoginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, DemoPassword));
        oldLoginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var newLoginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, newPassword));
        newLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_With_Invalid_Current_Or_Weak_Or_Same_Password_Should_Return_400()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, DemoPassword));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        tokens.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var wrongCurrent = await client.PostAsJsonAsync(
            "/api/v1/auth/password/change",
            new ChangePasswordRequest("WrongCurrent123!", "ValidNewPass123!"));
        wrongCurrent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        wrongCurrent.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var weakNew = await client.PostAsJsonAsync(
            "/api/v1/auth/password/change",
            new ChangePasswordRequest(DemoPassword, "weak"));
        weakNew.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var samePassword = await client.PostAsJsonAsync(
            "/api/v1/auth/password/change",
            new ChangePasswordRequest(DemoPassword, DemoPassword));
        samePassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForgotPassword_Should_Return_202_Protect_Email_Existence_Store_Hashed_Otp_And_Enforce_Cooldown()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var unknownResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot",
            new ForgotPasswordRequest("unknown.user@nova-conseil.fr"));
        unknownResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.EmailService.SentMessages.ShouldBeEmpty();

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot",
            new ForgotPasswordRequest(EmployeeEmail));
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.EmailService.SentMessages.Count.ShouldBe(1);

        var otp = ExtractOtp(factory.EmailService.SentMessages[0].PlainTextBody!);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var jwtGen = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();
            var stored = await db.PasswordResetTokens.SingleOrDefaultAsync();
            stored.ShouldNotBeNull();
            stored.OtpHash.ShouldBe(jwtGen.HashToken(otp));
            stored.OtpHash.ShouldNotBe(otp);
        }

        // Immediate retry within 60s cooldown returns 202 without sending another email
        var cooldownResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot",
            new ForgotPasswordRequest(EmployeeEmail));
        cooldownResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        factory.EmailService.SentMessages.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ResetPassword_With_Valid_Otp_Should_Reset_Unlock_Account_And_Revoke_Tokens()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var initialLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, DemoPassword));
        var initialTokens = await initialLogin.Content.ReadFromJsonAsync<AuthTokensResponse>();
        initialTokens.ShouldNotBeNull();

        // Trigger account lockout via 5 bad password attempts
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(EmployeeEmail, "WrongPassword99!"));
        }

        var lockedLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, DemoPassword));
        lockedLogin.StatusCode.ShouldBe(HttpStatusCode.Locked);

        var forgotResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot",
            new ForgotPasswordRequest(EmployeeEmail));
        forgotResponse.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        var otp = ExtractOtp(factory.EmailService.SentMessages.Single().PlainTextBody!);
        const string newPassword = "ResetStrongPass123!";

        var resetResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset",
            new ResetPasswordRequest(EmployeeEmail, otp, newPassword));
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // OTP cannot be reused
        var reuseResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset",
            new ResetPasswordRequest(EmployeeEmail, otp, "AnotherStrong123!"));
        reuseResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Existing refresh tokens were revoked
        var refreshResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(initialTokens.RefreshToken));
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Account is unlocked and new password works
        var unlockedLogin = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(EmployeeEmail, newPassword));
        unlockedLogin.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_Should_Invalidate_Otp_After_Five_Failed_Attempts()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(
            "/api/v1/auth/password/forgot",
            new ForgotPasswordRequest(EmployeeEmail));

        var realOtp = ExtractOtp(factory.EmailService.SentMessages.Single().PlainTextBody!);
        var wrongOtp = realOtp == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var attempt = await client.PostAsJsonAsync(
                "/api/v1/auth/password/reset",
                new ResetPasswordRequest(EmployeeEmail, wrongOtp, "StrongNewPass123!"));
            attempt.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // Now even the valid OTP fails because the token was invalidated after 5 failures
        var validAfterLockout = await client.PostAsJsonAsync(
            "/api/v1/auth/password/reset",
            new ResetPasswordRequest(EmployeeEmail, realOtp, "StrongNewPass123!"));
        validAfterLockout.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string ExtractOtp(string body)
    {
        var match = OtpRegex().Match(body);
        match.Success.ShouldBeTrue($"Expected a 6-digit OTP in email body: {body}");
        return match.Groups[1].Value;
    }
}
