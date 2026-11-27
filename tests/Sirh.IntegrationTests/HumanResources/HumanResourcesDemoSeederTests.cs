using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.HumanResources;

public sealed class HumanResourcesDemoSeederTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string ManagerEmail = "manager.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";

    [Fact]
    public async Task SeedDemoData_Should_Create_Linked_Employees_Hierarchy_And_Positions()
    {
        using var baseFactory = new ApiWebApplicationFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HumanResources:SeedDemoData"] = "true"
                })));

        using var adminClient = factory.CreateClient();
        var loginResp = await adminClient.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(AdminEmail, DemoPassword));
        loginResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = (await loginResp.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var me = await adminClient.GetFromJsonAsync<CurrentUserProfileResponse>("/api/v1/auth/me");
        me.ShouldNotBeNull();
        me.EmployeeId.ShouldNotBeNull();

        var employees = await adminClient.GetFromJsonAsync<PagedResult<EmployeeSummaryDto>>("/api/v1/employees?page=1&pageSize=20");
        employees.ShouldNotBeNull();
        employees.TotalCount.ShouldBe(3);
        employees.Items.ShouldAllBe(e => e.IsActive && e.CurrentlyInPosition);

        var adminEmp = employees.Items.Single(e => e.Email == AdminEmail);
        var managerEmp = employees.Items.Single(e => e.Email == ManagerEmail);
        var standardEmp = employees.Items.Single(e => e.Email == EmployeeEmail);

        adminEmp.Id.ShouldBe(me.EmployeeId.Value);
        managerEmp.DirectManagerId.ShouldBe(adminEmp.Id);
        standardEmp.DirectManagerId.ShouldBe(managerEmp.Id);
    }
}
