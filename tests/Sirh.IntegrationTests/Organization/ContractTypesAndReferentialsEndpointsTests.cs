using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Organization;

public sealed class ContractTypesAndReferentialsEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [Fact]
    public async Task System_ContractTypes_Should_Be_Seeded_And_Protected_With_403()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var paged = await client.GetFromJsonAsync<PagedResult<ContractTypeDto>>("/api/v1/contract-types");
        paged.ShouldNotBeNull();
        paged.Items.Select(ct => ct.Code).ShouldContain("CDI");
        paged.Items.Select(ct => ct.Code).ShouldContain("CDD");
        paged.Items.Select(ct => ct.Code).ShouldContain("APPRENTISSAGE");
        paged.Items.Select(ct => ct.Code).ShouldContain("CONTRAT_PRO");

        var cdi = paged.Items.Single(ct => ct.Code == "CDI");
        cdi.IsSystem.ShouldBeTrue();

        var deleteSystem = await client.DeleteAsync($"/api/v1/contract-types/{cdi.Id}");
        deleteSystem.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var renameSystemCode = await client.PutAsJsonAsync(
            $"/api/v1/contract-types/{cdi.Id}",
            new UpdateContractTypeRequest("CDI_RENAMED", cdi.Label, true, cdi.RowVersion));
        renameSystemCode.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var updateSystemLabel = await client.PutAsJsonAsync(
            $"/api/v1/contract-types/{cdi.Id}",
            new UpdateContractTypeRequest("CDI", "CDI Temps Plein", true, cdi.RowVersion));
        updateSystemLabel.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_Should_Manage_Custom_ContractTypes_And_Return_409_When_In_Use()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var createResponse = await client.PostAsJsonAsync(
            "/api/v1/contract-types",
            new CreateContractTypeRequest("stage_long", "Stage de fin d'études"));
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await createResponse.Content.ReadFromJsonAsync<ContractTypeDto>();
        created.ShouldNotBeNull();
        created.Code.ShouldBe("STAGE_LONG");
        created.IsSystem.ShouldBeFalse();

        factory.ContractTypeReferences.Add(created.Id);
        var conflictDelete = await client.DeleteAsync($"/api/v1/contract-types/{created.Id}");
        conflictDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        factory.ContractTypeReferences.Clear();
        var okDelete = await client.DeleteAsync($"/api/v1/contract-types/{created.Id}");
        okDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Referentials_Departments_Should_Require_Auth_And_Return_Official_List()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var anonResponse = await client.GetAsync("/api/v1/referentials/departments");
        anonResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var empTokens = await LoginAsync(client, EmployeeEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empTokens.AccessToken);

        var authResponse = await client.GetAsync("/api/v1/referentials/departments");
        authResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var departments = await authResponse.Content.ReadFromJsonAsync<List<DepartmentDto>>();
        departments.ShouldNotBeNull();
        departments.Select(d => d.Code).ShouldContain("DEPT_HR");
        departments.Select(d => d.Code).ShouldContain("DEPT_SALES");
        departments.Select(d => d.Code).ShouldContain("DEPT_PAY");
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
