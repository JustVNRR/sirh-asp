using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Identity.Domain.Entities;
using Xunit;

namespace Sirh.IntegrationTests.Identity;

public sealed class ProfileAndRolesEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [Fact]
    public async Task GetMe_Without_Auth_Should_Return_401()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_As_Authenticated_Employee_Should_Return_Roles_Priority_And_Permissions()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, EmployeeEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await client.GetAsync("/api/v1/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<CurrentUserProfileResponse>();
        profile.ShouldNotBeNull();
        profile.Email.ShouldBe(EmployeeEmail);
        profile.IsActive.ShouldBeTrue();
        profile.MaxRolePriority.ShouldBe(2);
        profile.UnreadNotificationsCount.ShouldBe(0);
        profile.Roles.Select(r => r.Code).ShouldContain(IdentityRoles.Employee);
        profile.Permissions.ShouldContain(IdentityPermissions.EmployeesReadSelf);
        profile.Permissions.ShouldNotContain(IdentityPermissions.RolesManagePermissions);
    }

    [Fact]
    public async Task Role_Management_Endpoints_Should_Return_403_For_Employee()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, EmployeeEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        (await client.GetAsync("/api/v1/roles")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/permissions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PutAsJsonAsync(
                $"/api/v1/roles/{Guid.CreateVersion7()}/permissions",
                new UpdateRolePermissionsRequest([IdentityPermissions.EmployeesReadSelf])))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_Should_List_Roles_And_Permissions_And_Update_NonSuperAdmin_Role()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var rolesResponse = await client.GetAsync("/api/v1/roles");
        rolesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var roles = await rolesResponse.Content.ReadFromJsonAsync<List<RoleSummaryDto>>();
        roles.ShouldNotBeNull();
        roles.Count.ShouldBeGreaterThanOrEqualTo(IdentityRoles.All.Count);

        var permissionsResponse = await client.GetAsync("/api/v1/permissions");
        permissionsResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var permissions = await permissionsResponse.Content.ReadFromJsonAsync<List<PermissionDto>>();
        permissions.ShouldNotBeNull();
        permissions.Count.ShouldBe(IdentityPermissions.All.Count);

        var employeeRole = roles.Single(r => r.Code == IdentityRoles.Employee);

        var getRolePermissions = await client.GetAsync($"/api/v1/roles/{employeeRole.Id}/permissions");
        getRolePermissions.StatusCode.ShouldBe(HttpStatusCode.OK);
        var initialRolePermissions = await getRolePermissions.Content.ReadFromJsonAsync<RolePermissionsResponse>();
        initialRolePermissions.ShouldNotBeNull();
        initialRolePermissions.Code.ShouldBe(IdentityRoles.Employee);

        var updatedCodes = new[]
        {
            IdentityPermissions.EmployeesReadSelf,
            IdentityPermissions.CommonDocsRead
        };

        var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/roles/{employeeRole.Id}/permissions",
            new UpdateRolePermissionsRequest(updatedCodes));
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updatedBody = await updateResponse.Content.ReadFromJsonAsync<RolePermissionsResponse>();
        updatedBody.ShouldNotBeNull();
        updatedBody.Permissions.Select(p => p.Code).ShouldBe(updatedCodes.OrderBy(c => c).ToArray());

        var verifyAfterUpdate = await client.GetFromJsonAsync<RolePermissionsResponse>(
            $"/api/v1/roles/{employeeRole.Id}/permissions");
        verifyAfterUpdate.ShouldNotBeNull();
        verifyAfterUpdate.Permissions.Select(p => p.Code).ShouldBe(updatedCodes.OrderBy(c => c).ToArray());

        var invalidUpdate = await client.PutAsJsonAsync(
            $"/api/v1/roles/{employeeRole.Id}/permissions",
            new UpdateRolePermissionsRequest(["does:not:exist"]));
        invalidUpdate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SuperAdmin_Role_Modification_Should_Be_Forbidden_For_Admin_And_Allowed_For_SuperAdmin()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        const string superAdminEmail = "superadmin.rh@nova-conseil.fr";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var superAdminRoleName = IdentityRoles.All.Single(r => r.Code == IdentityRoles.SuperAdmin).Name;
            var superUser = new ApplicationUser
            {
                UserName = superAdminEmail,
                Email = superAdminEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            (await userManager.CreateAsync(superUser, DemoPassword)).Succeeded.ShouldBeTrue();
            (await userManager.AddToRoleAsync(superUser, superAdminRoleName)).Succeeded.ShouldBeTrue();
        }

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var roles = await client.GetFromJsonAsync<List<RoleSummaryDto>>("/api/v1/roles");
        roles.ShouldNotBeNull();
        var superAdminRole = roles.Single(r => r.Code == IdentityRoles.SuperAdmin);

        var currentSuperPerms = await client.GetFromJsonAsync<RolePermissionsResponse>(
            $"/api/v1/roles/{superAdminRole.Id}/permissions");
        currentSuperPerms.ShouldNotBeNull();

        var forbiddenUpdate = await client.PutAsJsonAsync(
            $"/api/v1/roles/{superAdminRole.Id}/permissions",
            new UpdateRolePermissionsRequest([IdentityPermissions.RolesRead]));
        forbiddenUpdate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var superTokens = await LoginAsync(client, superAdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", superTokens.AccessToken);

        var allowedUpdate = await client.PutAsJsonAsync(
            $"/api/v1/roles/{superAdminRole.Id}/permissions",
            new UpdateRolePermissionsRequest(currentSuperPerms.Permissions.Select(p => p.Code).ToArray()));
        allowedUpdate.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<AuthTokensResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<AuthTokensResponse>();
        tokens.ShouldNotBeNull();
        return tokens;
    }
}
