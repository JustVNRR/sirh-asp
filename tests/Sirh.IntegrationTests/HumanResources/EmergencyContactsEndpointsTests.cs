using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Infrastructure.Persistence;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.HumanResources;

public sealed class EmergencyContactsEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string EmployeePassword = "NewEmployee123!";

    [Fact]
    public async Task Employee_EmergencyContacts_Should_Be_Encrypted_Limited_To_Two_And_Protected_By_Abac()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var companies = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies");
        var companyId = companies!.Items.First().Id;

        var alice = await CreateEmployeeAsync(client, companyId, "Alice", "Contact");
        var bob = await CreateEmployeeAsync(client, companyId, "Bob", "Autre");

        // Login as Alice and add 2 contacts
        var aliceTokens = await LoginAsync(client, alice.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aliceTokens.AccessToken);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/v1/employees/{alice.Id}/emergency-contacts",
            new CreateEmergencyContactRequest("Paul Contact", "Conjoint", "0611223344", PriorityOrder: 1));
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var first = await firstResponse.Content.ReadFromJsonAsync<EmergencyContactDto>();
        first.ShouldNotBeNull();
        first.FullName.ShouldBe("Paul Contact");

        var secondResponse = await client.PostAsJsonAsync(
            $"/api/v1/employees/{alice.Id}/emergency-contacts",
            new CreateEmergencyContactRequest("Jeanne Contact", "Mère", HomePhone: "0144556677", PriorityOrder: 2));
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // 3rd emergency contact must fail with 422
        var thirdResponse = await client.PostAsJsonAsync(
            $"/api/v1/employees/{alice.Id}/emergency-contacts",
            new CreateEmergencyContactRequest("Troisieme Contact", "Frère", WorkPhone: "0199887766", PriorityOrder: 2));
        thirdResponse.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        // Verify stored encrypted in DB and decrypted on GET
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var hrDb = scope.ServiceProvider.GetRequiredService<HumanResourcesDbContext>();
            var crypto = scope.ServiceProvider.GetRequiredService<IDataProtectorService>();
            var stored = await hrDb.EmergencyContacts.SingleAsync(c => c.Id == first.Id);
            stored.FullNameEncrypted.ShouldNotBe("Paul Contact");
            stored.RelationshipEncrypted.ShouldNotBe("Conjoint");
            stored.MobilePhoneEncrypted.ShouldNotBe("0611223344");
            crypto.DecryptString(stored.FullNameEncrypted).ShouldBe("Paul Contact");
        }

        var list = await client.GetFromJsonAsync<List<EmergencyContactDto>>(
            $"/api/v1/employees/{alice.Id}/emergency-contacts");
        list.ShouldNotBeNull();
        list.Count.ShouldBe(2);

        // Concurrency & valid update
        var updateOk = await client.PutAsJsonAsync(
            $"/api/v1/emergency-contacts/{first.Id}",
            new UpdateEmergencyContactRequest(
                "Paul Contact Modifié",
                "Époux",
                "0699887766",
                PriorityOrder: 1,
                RowVersion: first.RowVersion));
        updateOk.StatusCode.ShouldBe(HttpStatusCode.OK);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/v1/emergency-contacts/{first.Id}",
            new UpdateEmergencyContactRequest(
                "Stale",
                "Époux",
                "0699887766",
                PriorityOrder: 1,
                RowVersion: first.RowVersion));
        staleUpdate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Bob (unrelated employee) receives 403 Forbidden
        var bobTokens = await LoginAsync(client, bob.Email, EmployeePassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bobTokens.AccessToken);

        (await client.GetAsync($"/api/v1/employees/{alice.Id}/emergency-contacts"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsJsonAsync(
                $"/api/v1/employees/{alice.Id}/emergency-contacts",
                new CreateEmergencyContactRequest("Hack", "Inconnu", "0600000000")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Admin/HR can read & delete Alice's contact
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        var deleteOk = await client.DeleteAsync($"/api/v1/emergency-contacts/{first.Id}");
        deleteOk.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient client,
        Guid companyId,
        string firstName,
        string lastName)
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
                [IdentityRoles.Employee]));
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
