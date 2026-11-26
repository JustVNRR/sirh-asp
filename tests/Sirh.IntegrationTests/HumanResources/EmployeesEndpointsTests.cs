using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Infrastructure.Persistence;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.HumanResources;

public sealed class EmployeesEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string EmployeePassword = "NewEmployee123!";

    [Fact]
    public async Task Hr_Creates_Employee_With_AutoEmail_EncryptedNationalId_Login_And_AntiEscalation()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var hrEmail = await CreateUserWithRoleAsync(factory, "hr.team@nova-conseil.fr", IdentityRoles.Hr);
        var hrTokens = await LoginAsync(client, hrEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hrTokens.AccessToken);

        var companyId = await GetDemoCompanyIdAsync(client);

        var aliceResponse = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                "Mme",
                "Alice",
                "Durand",
                EmployeePassword,
                ["Employe"],
                BornAt: new DateOnly(1992, 5, 14),
                BirthPlace: "Paris",
                BirthDepartmentCode: "75",
                NationalId: "2920575123456",
                PersonalEmail: "alice.perso@example.com",
                Address: new EmployeeAddressDto("10 Rue de Rivoli", "75001", "Paris", null),
                Departments: ["DEPT_HR"]));

        aliceResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var alice = await aliceResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>();
        alice.ShouldNotBeNull();
        alice.Slug.ShouldBe("alice-durand");
        alice.Email.ShouldBe("adurand@nova-conseil.fr");
        alice.NationalId.ShouldBe("2920575123456");
        alice.Roles.ShouldBe([IdentityRoles.Employee]);

        // Verify NationalId is stored encrypted in HumanResourcesDbContext
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var hrDb = scope.ServiceProvider.GetRequiredService<HumanResourcesDbContext>();
            var crypto = scope.ServiceProvider.GetRequiredService<IDataProtectorService>();
            var entity = await hrDb.Employees.SingleAsync(e => e.Id == alice.Id);
            entity.NationalIdEncrypted.ShouldNotBeNullOrWhiteSpace();
            entity.NationalIdEncrypted.ShouldNotBe("2920575123456");
            crypto.DecryptString(entity.NationalIdEncrypted).ShouldBe("2920575123456");
        }

        // Verify email collision appends numeric suffix
        var albertResponse = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                "M",
                "Albert",
                "Durand",
                EmployeePassword,
                [IdentityRoles.Employee]));
        albertResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var albert = await albertResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>();
        albert.ShouldNotBeNull();
        albert.Email.ShouldBe("adurand2@nova-conseil.fr");

        // HR cannot assign Admin or SuperAdmin
        var escalateAdmin = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                "M",
                "Eve",
                "Escalator",
                EmployeePassword,
                [IdentityRoles.Admin]));
        escalateAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var escalateSuperAdmin = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                "M",
                "Root",
                "Attempt",
                EmployeePassword,
                [IdentityRoles.SuperAdmin]));
        escalateSuperAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Alice can authenticate immediately and see her EmployeeId in /auth/me
        client.DefaultRequestHeaders.Authorization = null;
        var aliceTokens = await LoginAsync(client, "adurand@nova-conseil.fr", EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceTokens.AccessToken);
        var me = await client.GetFromJsonAsync<CurrentUserProfileResponse>("/api/v1/auth/me");
        me.ShouldNotBeNull();
        me.EmployeeId.ShouldBe(alice.Id);
    }

    [Fact]
    public async Task GetAndUpdate_Employees_Should_Enforce_AntiIdor_ManagerMasking_And_RowVersion_Conflict()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var companyId = await GetDemoCompanyIdAsync(client);

        var manager = await CreateEmployeeViaApiAsync(
            client,
            companyId,
            null,
            "M",
            "Marc",
            "Chef",
            [IdentityRoles.Manager],
            nationalId: "1800175000111",
            departments: ["DEPT_SALES"]);

        var reportB = await CreateEmployeeViaApiAsync(
            client,
            companyId,
            manager.Id,
            "Mme",
            "Beatrice",
            "Equipe",
            [IdentityRoles.Employee],
            nationalId: "2910269000222",
            personalEmail: "beatrice.perso@example.com",
            departments: ["DEPT_SALES"]);

        var employeeA = await CreateEmployeeViaApiAsync(
            client,
            companyId,
            null,
            "M",
            "Arthur",
            "Autre",
            [IdentityRoles.Employee],
            nationalId: "1880333000333",
            departments: ["DEPT_PAY"]);

        // Employee A cannot view Employee B or list employees, but can view self
        var aTokens = await LoginAsync(client, employeeA.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aTokens.AccessToken);

        (await client.GetAsync($"/api/v1/employees/{reportB.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/employees")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var aSelfResponse = await client.GetAsync($"/api/v1/employees/{employeeA.Id}");
        aSelfResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var aSelf = await aSelfResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>();
        aSelf.ShouldNotBeNull();
        aSelf.NationalId.ShouldBe("1880333000333");

        // Direct Manager sees report B with NationalId & personal info masked, and cannot view Employee A
        var mTokens = await LoginAsync(client, manager.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mTokens.AccessToken);

        var managerViewBResponse = await client.GetAsync($"/api/v1/employees/{reportB.Id}");
        managerViewBResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var managerViewB = await managerViewBResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>();
        managerViewB.ShouldNotBeNull();
        managerViewB.NationalId.ShouldBeNull();
        managerViewB.PersonalEmail.ShouldBeNull();
        managerViewB.Email.ShouldBe(reportB.Email);

        (await client.GetAsync($"/api/v1/employees/{employeeA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var managerTeam = await client.GetFromJsonAsync<PagedResult<EmployeeSummaryDto>>("/api/v1/employees");
        managerTeam.ShouldNotBeNull();
        managerTeam.Items.Select(e => e.Id).ShouldBe([reportB.Id]);

        // Admin filters by department + role and updates with optimistic concurrency check
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var payRoleFiltered = await client.GetFromJsonAsync<PagedResult<EmployeeSummaryDto>>(
            "/api/v1/employees?department=DEPT_SALES&role=MANAGER");
        payRoleFiltered.ShouldNotBeNull();
        payRoleFiltered.Items.Select(e => e.Id).ShouldBe([manager.Id]);

        var updateOk = await client.PutAsJsonAsync(
            $"/api/v1/employees/{reportB.Id}",
            new UpdateEmployeeRequest(
                companyId,
                manager.Id,
                "Mme",
                "Beatrice",
                "Nouvelle",
                [IdentityRoles.Employee],
                NationalId: null,
                Departments: ["DEPT_SALES", "DEPT_HR"],
                RowVersion: reportB.RowVersion));
        updateOk.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedB = await updateOk.Content.ReadFromJsonAsync<EmployeeDetailDto>();
        updatedB.ShouldNotBeNull();
        updatedB.Slug.ShouldBe(reportB.Slug);
        updatedB.Email.ShouldBe("bnouvelle@nova-conseil.fr");
        updatedB.NationalId.ShouldBe("2910269000222");
        updatedB.RowVersion.ShouldNotBe(reportB.RowVersion);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/v1/employees/{reportB.Id}",
            new UpdateEmployeeRequest(
                companyId,
                manager.Id,
                "Mme",
                "Beatrice",
                "Stale",
                RowVersion: reportB.RowVersion));
        staleUpdate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deactivate_And_Activate_Employee_Should_Revoke_Tokens_And_Block_Hr_On_Admin()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        var companyId = await GetDemoCompanyIdAsync(client);

        var regularEmp = await CreateEmployeeViaApiAsync(
            client,
            companyId,
            null,
            "M",
            "Claire",
            "Sortante",
            [IdentityRoles.Employee]);

        var privilegedAdminEmp = await CreateEmployeeViaApiAsync(
            client,
            companyId,
            null,
            "M",
            "Luc",
            "AdminTarget",
            [IdentityRoles.Admin]);

        client.DefaultRequestHeaders.Authorization = null;
        var empTokens = await LoginAsync(client, regularEmp.Email, EmployeePassword);

        var hrEmail = await CreateUserWithRoleAsync(factory, "hr.ops@nova-conseil.fr", IdentityRoles.Hr);
        var hrTokens = await LoginAsync(client, hrEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hrTokens.AccessToken);

        // HR cannot deactivate an Admin account
        var deactivateAdmin = await client.PostAsync($"/api/v1/employees/{privilegedAdminEmp.Id}/deactivate", null);
        deactivateAdmin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // HR deactivates regular employee -> revokes refresh token & blocks login
        var deactivateOk = await client.PostAsync($"/api/v1/employees/{regularEmp.Id}/deactivate", null);
        deactivateOk.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refreshAfterDeactivate = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshTokenRequest(empTokens.RefreshToken));
        refreshAfterDeactivate.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var loginAfterDeactivate = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(regularEmp.Email, EmployeePassword));
        loginAfterDeactivate.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var persistedAfterDeactivate = await client.GetFromJsonAsync<EmployeeDetailDto>($"/api/v1/employees/{regularEmp.Id}");
        persistedAfterDeactivate.ShouldNotBeNull();
        persistedAfterDeactivate.IsActive.ShouldBeFalse();

        // Reactivate -> login works again
        var activateOk = await client.PostAsync($"/api/v1/employees/{regularEmp.Id}/activate", null);
        activateOk.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loginAfterActivate = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(regularEmp.Email, EmployeePassword));
        loginAfterActivate.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<Guid> GetDemoCompanyIdAsync(HttpClient client)
    {
        var companies = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies");
        companies.ShouldNotBeNull();
        return companies.Items.First().Id;
    }

    private static async Task<EmployeeDetailDto> CreateEmployeeViaApiAsync(
        HttpClient client,
        Guid companyId,
        Guid? managerId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyCollection<string> roleCodes,
        string? nationalId = null,
        string? personalEmail = null,
        IReadOnlyCollection<string>? departments = null)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                managerId,
                honorific,
                firstName,
                lastName,
                EmployeePassword,
                roleCodes,
                NationalId: nationalId,
                PersonalEmail: personalEmail,
                Departments: departments));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<EmployeeDetailDto>();
        created.ShouldNotBeNull();
        return created;
    }

    private static async Task<string> CreateUserWithRoleAsync(
        ApiWebApplicationFactory factory,
        string email,
        string roleCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleName = IdentityRoles.All.Single(r => r.Code == roleCode).Name;
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        (await userManager.CreateAsync(user, DemoPassword)).Succeeded.ShouldBeTrue();
        (await userManager.AddToRoleAsync(user, roleName)).Succeeded.ShouldBeTrue();
        return email;
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
