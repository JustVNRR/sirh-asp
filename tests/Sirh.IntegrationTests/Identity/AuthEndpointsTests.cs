using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.Modules.Identity.Application.Abstractions;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Identity.Infrastructure.Persistence;
using Xunit;

namespace Sirh.IntegrationTests.Identity;

public sealed class AuthEndpointsTests
{
    private const string DemoEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [Fact]
    public async Task Login_With_Valid_Credentials_Should_Return_Tokens_And_Store_Only_Sha256_Hash()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DemoEmail, DemoPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>();
        tokens.ShouldNotBeNull();
        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        tokens.TokenType.ShouldBe("Bearer");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var jwtGen = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var expectedHash = jwtGen.HashToken(tokens.RefreshToken);
        var storedToken = await db.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == expectedHash);
        storedToken.ShouldNotBeNull();
        storedToken.TokenHash.ShouldNotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Login_Should_Return_401_For_Wrong_Password_Unknown_Email_And_Inactive_User()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var wrongPass = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DemoEmail, "WrongPassword99!"));
        wrongPass.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var unknownUser = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("nobody@nova-conseil.fr", DemoPassword));
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var employee = await userManager.FindByEmailAsync("employe.rh@nova-conseil.fr");
            employee.ShouldNotBeNull();
            employee.IsActive = false;
            await userManager.UpdateAsync(employee);
        }

        var inactiveResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("employe.rh@nova-conseil.fr", DemoPassword));
        inactiveResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Should_Lock_Account_With_423_After_Five_Failed_Attempts()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var i = 0; i < 4; i++)
        {
            var attempt = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest("manager.rh@nova-conseil.fr", "WrongPassword99!"));
            attempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var fifthAttempt = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("manager.rh@nova-conseil.fr", "WrongPassword99!"));
        fifthAttempt.StatusCode.ShouldBe(HttpStatusCode.Locked);

        var validAfterLockout = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("manager.rh@nova-conseil.fr", DemoPassword));
        validAfterLockout.StatusCode.ShouldBe(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Refresh_Should_Rotate_Token_And_Revoke_Entire_Family_On_Reuse()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DemoEmail, DemoPassword));
        var initialTokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        initialTokens.ShouldNotBeNull();

        var refresh1Response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(initialTokens.RefreshToken));
        refresh1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotatedTokens = await refresh1Response.Content.ReadFromJsonAsync<AuthTokensResponse>();
        rotatedTokens.ShouldNotBeNull();
        rotatedTokens.RefreshToken.ShouldNotBe(initialTokens.RefreshToken);

        // Reuse the already-rotated initial refresh token -> theft detection
        var reuseResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(initialTokens.RefreshToken));
        reuseResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The valid rotated token in the same family must now also be revoked
        var compromisedFamilyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(rotatedTokens.RefreshToken));
        compromisedFamilyResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_Should_Require_Authentication_And_Revoke_Refresh_Tokens()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var anonymousLogout = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest("abc"));
        anonymousLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DemoEmail, DemoPassword));
        var tokens = await loginResponse.Content.ReadFromJsonAsync<AuthTokensResponse>();
        tokens.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var logoutResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new LogoutRequest(tokens.RefreshToken));
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        client.DefaultRequestHeaders.Authorization = null;
        var refreshAfterLogout = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(tokens.RefreshToken));
        refreshAfterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_Endpoints_Should_Enforce_Rate_Limiting_With_429_ProblemDetails()
    {
        using var factory = ApiWebApplicationFactory.WithAuthPermitLimit(3);
        using var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var resp = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new LoginRequest(DemoEmail, "WrongPassword99!"));
            resp.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var rateLimited = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DemoEmail, DemoPassword));

        rateLimited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rateLimited.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var body = await rateLimited.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("status").GetInt32().ShouldBe(429);
    }
}
