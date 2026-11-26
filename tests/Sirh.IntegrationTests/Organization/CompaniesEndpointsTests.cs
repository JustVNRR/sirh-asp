using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Identity.Domain.Entities;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Organization;

public sealed class CompaniesEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [Fact]
    public async Task Admin_Should_Create_Company_With_Default_Syntec_While_Employee_Can_Read_Only()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/companies",
            new CreateCompanyRequest(
                "Horizon Tech",
                "SAS",
                "42 Avenue Kléber",
                "75116",
                "Paris",
                null,
                "44306184100047",
                "RCS Paris 443 061 841",
                "100 000 EUR",
                CollectiveAgreement: null,
                Owner: "Jean Martin",
                LogoUrl: null));

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CompanyDto>();
        created.ShouldNotBeNull();
        created.Name.ShouldBe("Horizon Tech");
        created.Slug.ShouldBe("horizon-tech");
        created.CollectiveAgreement.ShouldBe("Syntec");
        created.Country.ShouldBe("France");
        created.IsActive.ShouldBeTrue();
        created.RowVersion.ShouldNotBeNullOrWhiteSpace();

        var empTokens = await LoginAsync(client, EmployeeEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empTokens.AccessToken);

        var listResponse = await client.GetAsync("/api/v1/companies?active=true&search=Horizon");
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paged = await listResponse.Content.ReadFromJsonAsync<PagedResult<CompanyDto>>();
        paged.ShouldNotBeNull();
        paged.Items.Select(c => c.Id).ShouldContain(created.Id);

        var forbiddenCreate = await client.PostAsJsonAsync(
            "/api/v1/companies",
            new CreateCompanyRequest(
                "Unauthorized Corp",
                null,
                "1 Rue Test",
                null,
                null,
                null,
                "55203253400646",
                null,
                null,
                null,
                null,
                null));
        forbiddenCreate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_Company_Name_Or_Siret_Should_Return_409()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var duplicateName = await client.PostAsJsonAsync(
            "/api/v1/companies",
            new CreateCompanyRequest(
                "Nova Conseil",
                null,
                "1 Rue Test",
                null,
                null,
                null,
                "44306184100047",
                null,
                null,
                null,
                null,
                null));
        duplicateName.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var duplicateSiret = await client.PostAsJsonAsync(
            "/api/v1/companies",
            new CreateCompanyRequest(
                "Autre Nom",
                null,
                "1 Rue Test",
                null,
                null,
                null,
                "73282932000074",
                null,
                null,
                null,
                null,
                null));
        duplicateSiret.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_Company_With_Stale_RowVersion_Should_Return_409_And_Status_Toggle_Should_Work()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var created = await (await client.PostAsJsonAsync(
            "/api/v1/companies",
            new CreateCompanyRequest(
                "Beta Conseil",
                "SARL",
                "12 Rue Royale",
                "69001",
                "Lyon",
                "France",
                "55203253400646",
                null,
                null,
                "Métallurgie",
                null,
                null))).Content.ReadFromJsonAsync<CompanyDto>();
        created.ShouldNotBeNull();

        var firstUpdate = await client.PutAsJsonAsync(
            $"/api/v1/companies/{created.Id}",
            new UpdateCompanyRequest(
                "Beta Conseil Updated",
                "SAS",
                "12 Rue Royale",
                "69001",
                "Lyon",
                "France",
                "55203253400646",
                null,
                null,
                "Métallurgie",
                null,
                null,
                true,
                created.RowVersion));
        firstUpdate.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedDto = await firstUpdate.Content.ReadFromJsonAsync<CompanyDto>();
        updatedDto.ShouldNotBeNull();
        updatedDto.RowVersion.ShouldNotBe(created.RowVersion);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/v1/companies/{created.Id}",
            new UpdateCompanyRequest(
                "Stale Edit",
                "SAS",
                "12 Rue Royale",
                "69001",
                "Lyon",
                "France",
                "55203253400646",
                null,
                null,
                "Métallurgie",
                null,
                null,
                true,
                created.RowVersion));
        staleUpdate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var statusToggle = await client.PatchAsJsonAsync(
            $"/api/v1/companies/{created.Id}/status",
            new UpdateCompanyStatusRequest(false, updatedDto.RowVersion));
        statusToggle.StatusCode.ShouldBe(HttpStatusCode.OK);
        var toggled = await statusToggle.Content.ReadFromJsonAsync<CompanyDto>();
        toggled.ShouldNotBeNull();
        toggled.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Hr_Should_Receive_403_On_Company_Write()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        const string hrEmail = "only.hr@nova-conseil.fr";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var hrRoleName = IdentityRoles.All.Single(r => r.Code == IdentityRoles.Hr).Name;
            var hrUser = new ApplicationUser
            {
                UserName = hrEmail,
                Email = hrEmail,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            (await userManager.CreateAsync(hrUser, DemoPassword)).Succeeded.ShouldBeTrue();
            (await userManager.AddToRoleAsync(hrUser, hrRoleName)).Succeeded.ShouldBeTrue();
        }

        var hrTokens = await LoginAsync(client, hrEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hrTokens.AccessToken);

        var postResponse = await client.PostAsJsonAsync(
            "/api/v1/companies",
            new CreateCompanyRequest(
                "HR Blocked Corp",
                null,
                "1 Rue Test",
                null,
                null,
                null,
                "44306184100047",
                null,
                null,
                null,
                null,
                null));
        postResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
