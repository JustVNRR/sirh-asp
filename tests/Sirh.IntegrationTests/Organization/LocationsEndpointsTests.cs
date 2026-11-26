using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Organization;

public sealed class LocationsEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [Fact]
    public async Task Admin_Should_Create_Locations_With_Unique_Auto_Slug_And_Filter_Results()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var companies = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies");
        companies.ShouldNotBeNull();
        var companyId = companies.Items.First().Id;

        var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/locations",
            new CreateLocationRequest(
                "Agence Lyon Part-Dieu",
                "10 Boulevard Vivier-Merle",
                "69003",
                "Lyon",
                null,
                "69",
                "44306184100047",
                "#1d4ed8",
                IsPublished: true,
                CompanyId: companyId));

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var first = await firstResponse.Content.ReadFromJsonAsync<LocationDto>();
        first.ShouldNotBeNull();
        first.Slug.ShouldBe("agence-lyon-part-dieu");
        first.ColorCode.ShouldBe("#1D4ED8");

        var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/locations",
            new CreateLocationRequest(
                "Agence Lyon Part-Dieu",
                "12 Boulevard Vivier-Merle",
                "69003",
                "Lyon",
                null,
                "69",
                null,
                "#0F766E",
                IsPublished: false,
                CompanyId: companyId));

        secondResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var second = await secondResponse.Content.ReadFromJsonAsync<LocationDto>();
        second.ShouldNotBeNull();
        second.Slug.ShouldBe("agence-lyon-part-dieu-2");

        var publishedOnly = await client.GetFromJsonAsync<PagedResult<LocationDto>>(
            $"/api/v1/locations?published=true&companyId={companyId}&search=Part-Dieu");
        publishedOnly.ShouldNotBeNull();
        publishedOnly.Items.Select(l => l.Id).ShouldBe([first.Id]);
    }

    [Fact]
    public async Task Delete_Location_Should_Return_409_When_Referenced_And_204_Otherwise()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var tokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var created = await (await client.PostAsJsonAsync(
            "/api/v1/locations",
            new CreateLocationRequest(
                "Campus Nantes",
                "5 Rue Crébillon",
                "44000",
                "Nantes",
                "France",
                "44",
                null,
                "#9333EA",
                IsPublished: true))).Content.ReadFromJsonAsync<LocationDto>();
        created.ShouldNotBeNull();

        factory.LocationReferences.Add(created.Id);
        var conflictDelete = await client.DeleteAsync($"/api/v1/locations/{created.Id}");
        conflictDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        factory.LocationReferences.Clear();
        var okDelete = await client.DeleteAsync($"/api/v1/locations/{created.Id}");
        okDelete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterDelete = await client.GetAsync($"/api/v1/locations/{created.Id}");
        afterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Employee_Should_Receive_403_On_Location_Write()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var empTokens = await LoginAsync(client, EmployeeEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empTokens.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/locations",
            new CreateLocationRequest("Site Test", "1 Rue Test", null, "Paris", null, null, null, "#000000"));
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
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
