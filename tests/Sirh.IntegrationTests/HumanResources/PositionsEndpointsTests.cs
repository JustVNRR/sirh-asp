using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.HumanResources;

public sealed class PositionsEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string EmployeePassword = "NewEmployee123!";

    [Fact]
    public async Task Positions_Lifecycle_CurrentCalculation_Abac_And_CrossModule_Location_409_Should_Work()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var companyId = (await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;

        var location = await (await client.PostAsJsonAsync(
            "/api/v1/locations",
            new CreateLocationRequest("Site Bordeaux", "1 Cours de l'Intendance", "33000", "Bordeaux", null, "33", null, "#EA580C")))
            .Content.ReadFromJsonAsync<LocationDto>();
        location.ShouldNotBeNull();

        var manager = await CreateEmployeeAsync(client, companyId, "Chef", "Poste", [IdentityRoles.Manager]);
        var employee = await CreateEmployeeAsync(client, companyId, "Julie", "Poste", [IdentityRoles.Employee]);
        var outsider = await CreateEmployeeAsync(client, companyId, "Pierre", "Externe", [IdentityRoles.Employee]);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Create 1st position
        var pos1Response = await client.PostAsJsonAsync(
            "/api/v1/positions",
            new CreatePositionRequest(
                employee.Id,
                manager.Id,
                location.Id,
                "Consultante Junior",
                today.AddDays(-60),
                EndAt: null,
                ContractLabel: "CDI",
                Coefficient: "115",
                Rate: 3200m,
                TimeContractType: "Hours",
                TimeContractAmount: 35m,
                HierarchicalPosition: "Cadre",
                Departments: ["DEPT_HR"]));
        pos1Response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var pos1 = await pos1Response.Content.ReadFromJsonAsync<PositionDto>();
        pos1.ShouldNotBeNull();
        pos1.Slug.ShouldBe($"poste-{employee.Slug}");
        pos1.IsCurrent.ShouldBeTrue();

        // Employee CurrentlyInPosition and DirectManagerId are synced
        var updatedEmp = await client.GetFromJsonAsync<EmployeeDetailDto>($"/api/v1/employees/{employee.Id}");
        updatedEmp.ShouldNotBeNull();
        updatedEmp.CurrentlyInPosition.ShouldBeTrue();
        updatedEmp.DirectManagerId.ShouldBe(manager.Id);

        // Real cross-module check: deleting location used by Position must return 409 Conflict
        var deleteLocationInUse = await client.DeleteAsync($"/api/v1/locations/{location.Id}");
        deleteLocationInUse.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Create 2nd newer active position -> suffix -2 and becomes IsCurrent
        var pos2Response = await client.PostAsJsonAsync(
            "/api/v1/positions",
            new CreatePositionRequest(
                employee.Id,
                manager.Id,
                location.Id,
                "Consultante Confirmée",
                today.AddDays(-5),
                TimeContractType: "Days",
                TimeContractAmount: 218m));
        pos2Response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var pos2 = await pos2Response.Content.ReadFromJsonAsync<PositionDto>();
        pos2.ShouldNotBeNull();
        pos2.Slug.ShouldBe($"poste-{employee.Slug}-2");
        pos2.IsCurrent.ShouldBeTrue();

        // Validation failure: EndAt < StartAt and ManagerId == EmployeeId -> 400
        (await client.PostAsJsonAsync(
                "/api/v1/positions",
                new CreatePositionRequest(employee.Id, manager.Id, null, "Bad Dates", today, today.AddDays(-1))))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync(
                "/api/v1/positions",
                new CreatePositionRequest(employee.Id, employee.Id, null, "Self Manager", today)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // ABAC on GET /api/v1/employees/{id}/positions
        var empTokens = await LoginAsync(client, employee.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empTokens.AccessToken);
        var selfPositions = await client.GetFromJsonAsync<List<PositionDto>>(
            $"/api/v1/employees/{employee.Id}/positions");
        selfPositions.ShouldNotBeNull();
        selfPositions.Count.ShouldBe(2);
        selfPositions.Single(p => p.IsCurrent).Id.ShouldBe(pos2.Id);

        var mgrTokens = await LoginAsync(client, manager.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mgrTokens.AccessToken);
        (await client.GetAsync($"/api/v1/employees/{employee.Id}/positions")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var outsiderTokens = await LoginAsync(client, outsider.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", outsiderTokens.AccessToken);
        (await client.GetAsync($"/api/v1/employees/{employee.Id}/positions"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Delete with IPositionReferenceChecker -> 409 then 204 when unmarked
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        factory.PositionReferences.Add(pos2.Id);
        (await client.DeleteAsync($"/api/v1/positions/{pos2.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        factory.PositionReferences.Clear();
        (await client.DeleteAsync($"/api/v1/positions/{pos2.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/v1/positions/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Now location has no positions referencing it -> DELETE succeeds!
        (await client.DeleteAsync($"/api/v1/locations/{location.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient client,
        Guid companyId,
        string firstName,
        string lastName,
        IReadOnlyCollection<string> roles)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                "Mme",
                firstName,
                lastName,
                EmployeePassword,
                roles));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }

    private static async Task<AuthTokensResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
    }
}
