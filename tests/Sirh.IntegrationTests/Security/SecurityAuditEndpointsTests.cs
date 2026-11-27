using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Enums;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Security;

public sealed class SecurityAuditEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string EmployeePassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly byte[] MinimalPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    public static IEnumerable<object[]> ProtectedEndpointsMatrix()
    {
        var sampleId = Guid.NewGuid();

        // Identity
        yield return ["GET", "/api/v1/auth/me"];
        yield return ["POST", "/api/v1/auth/logout"];
        yield return ["POST", "/api/v1/auth/logout-all"];
        yield return ["POST", "/api/v1/auth/password/change"];
        yield return ["GET", "/api/v1/roles"];
        yield return ["GET", $"/api/v1/roles/{sampleId}/permissions"];
        yield return ["GET", "/api/v1/permissions"];

        // Organization
        yield return ["GET", "/api/v1/companies"];
        yield return ["POST", "/api/v1/companies"];
        yield return ["GET", "/api/v1/locations"];
        yield return ["POST", "/api/v1/locations"];
        yield return ["GET", "/api/v1/contract-types"];
        yield return ["GET", "/api/v1/referentials/departments"];

        // HumanResources
        yield return ["GET", "/api/v1/employees"];
        yield return ["POST", "/api/v1/employees"];
        yield return ["GET", $"/api/v1/employees/{sampleId}/emergency-contacts"];
        yield return ["GET", $"/api/v1/employees/{sampleId}/positions"];
        yield return ["GET", "/api/v1/employee-documents"];

        // Contracts
        yield return ["GET", "/api/v1/contract-models"];
        yield return ["GET", "/api/v1/contract-models/variables"];
        yield return ["POST", "/api/v1/contracts/generate"];
        yield return ["GET", "/api/v1/contracts"];
        yield return ["GET", $"/api/v1/job-sheets/by-position/{sampleId}"];

        // Health
        yield return ["GET", "/api/v1/medical-visits"];
        yield return ["POST", "/api/v1/medical-visits"];
        yield return ["GET", "/api/v1/notifications"];
        yield return ["GET", "/api/v1/notifications/unread-count"];

        // InternalLife
        yield return ["GET", "/api/v1/common-documents"];
        yield return ["GET", "/api/v1/staff-representatives"];
        yield return ["GET", "/api/v1/rse-ideas"];
        yield return ["POST", "/api/v1/rse-ideas"];
        yield return ["GET", "/api/v1/onboarding-quizzes"];
        yield return ["POST", "/api/v1/onboarding-quizzes"];
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpointsMatrix))]
    public async Task Anonymous_Calls_To_Protected_Endpoints_Should_Return_401_Unauthorized(
        string method,
        string path)
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FallbackPolicy_Should_Require_Authenticated_User_And_Guard_Every_Endpoint()
    {
        using var factory = new ApiWebApplicationFactory();
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallbackPolicy = await policyProvider.GetFallbackPolicyAsync();

        fallbackPolicy.ShouldNotBeNull();
        fallbackPolicy.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().ShouldNotBeEmpty();

        // Anonymous request to an unattributed/unmatched route is blocked with 401 by FallbackPolicy
        // before routing resolves 404 for authenticated callers.
        using var anonymousClient = factory.CreateClient();
        var anonymousResponse = await anonymousClient.GetAsync("/api/v1/_security-audit-probe");
        anonymousResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var authClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);
        var authResponse = await authClient.GetAsync("/api/v1/_security-audit-probe");
        authResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Audit every mapped RouteEndpoint in the application: only explicit public routes may allow anonymous access.
        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var routeEndpoints = endpointDataSource.Endpoints.OfType<RouteEndpoint>().ToList();
        routeEndpoints.ShouldNotBeEmpty();

        var allowedAnonymousPrefixes = new[]
        {
            "/health",
            "/openapi",
            "/scalar",
            "/api/v1/auth/login",
            "/api/v1/auth/refresh",
            "/api/v1/auth/password/forgot",
            "/api/v1/auth/password/reset"
        };

        foreach (var endpoint in routeEndpoints)
        {
            var routePattern = endpoint.RoutePattern.RawText ?? string.Empty;
            var hasAllowAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

            if (hasAllowAnonymous)
            {
                allowedAnonymousPrefixes.Any(prefix =>
                        routePattern.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    .ShouldBeTrue($"Unexpected [AllowAnonymous] endpoint: '{routePattern}'.");
            }
            else
            {
                // Protected either explicitly (.RequireAuthorization) or by FallbackPolicy.
                (endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null || fallbackPolicy is not null)
                    .ShouldBeTrue();
            }
        }
    }

    [Fact]
    public async Task SecurityHeaders_Should_Be_Present_On_Success_And_Error_Responses()
    {
        using var factory = new ApiWebApplicationFactory();
        using var anonClient = factory.CreateClient();
        using var authClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var healthResponse = await anonClient.GetAsync("/health");
        healthResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertSecurityHeaders(healthResponse);

        var unauthorizedResponse = await anonClient.GetAsync("/api/v1/auth/me");
        unauthorizedResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        AssertSecurityHeaders(unauthorizedResponse);

        var authorizedResponse = await authClient.GetAsync("/api/v1/auth/me");
        authorizedResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertSecurityHeaders(authorizedResponse);
    }

    [Fact]
    public async Task Standard_Employee_Should_Receive_403_Across_Privileged_And_CrossEmployee_Resources()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!
            .Items.First().Id;

        var alice = await CreateEmployeeAsync(adminClient, companyId, "Mme", "Alice", "AuditOne", [IdentityRoles.Employee]);
        var bob = await CreateEmployeeAsync(adminClient, companyId, "M", "Bob", "AuditTwo", [IdentityRoles.Employee]);

        using var bobClient = await CreateAuthenticatedClientAsync(factory, bob.Email, EmployeePassword);
        using var aliceClient = await CreateAuthenticatedClientAsync(factory, alice.Email, EmployeePassword);

        // Create sensitive resources owned by Bob for anti-IDOR checks
        var contactResp = await bobClient.PostAsJsonAsync(
            $"/api/v1/employees/{bob.Id}/emergency-contacts",
            new CreateEmergencyContactRequest("Marc Audit", "Père", "0612345678", PriorityOrder: 1));
        contactResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobContact = (await contactResp.Content.ReadFromJsonAsync<EmergencyContactDto>())!;

        using var bobDocForm = BuildPdfMultipart(
            ("employeeId", bob.Id.ToString()),
            ("category", "PersonalFile"),
            ("documentType", "Cv"),
            ("fileDate", "2026-11-27"));
        var bobDocResp = await adminClient.PostAsync("/api/v1/employee-documents", bobDocForm);
        bobDocResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobDoc = (await bobDocResp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;

        using var visitForm = new MultipartFormDataContent();
        visitForm.Add(new StringContent(bob.Id.ToString()), "employeeId");
        visitForm.Add(new StringContent("Hiring"), "visitType");
        visitForm.Add(new StringContent(DateTime.UtcNow.AddDays(14).ToString("O")), "visitDateTimeUtc");
        visitForm.Add(new StringContent("2028-11-27"), "validityDate");
        visitForm.Add(new StringContent("Centre Santé Paris 9"), "medicalCenter");
        visitForm.Add(new StringContent("false"), "carriedOut");
        var convocationContent = new ByteArrayContent(MinimalPdfBytes);
        convocationContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        visitForm.Add(convocationContent, "convocationFile", "convocation.pdf");
        var visitResp = await adminClient.PostAsync("/api/v1/medical-visits", visitForm);
        visitResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobVisit = (await visitResp.Content.ReadFromJsonAsync<MedicalVisitDto>(ApiJsonOptions))!;

        var bobIdeaResp = await bobClient.PostAsJsonAsync(
            "/api/v1/rse-ideas",
            new { locationId = (Guid?)null, title = "Idée confidentielle Bob", ideaContent = "Installer des bornes vélos." });
        bobIdeaResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobIdea = (await bobIdeaResp.Content.ReadFromJsonAsync<RseIdeaDto>(ApiJsonOptions))!;

        // 1. Identity module privilege escalation guards (403)
        (await aliceClient.GetAsync("/api/v1/roles")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync("/api/v1/permissions")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 2. Organization module privilege guards (403)
        (await aliceClient.PostAsJsonAsync("/api/v1/companies", new { name = "Rogue" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PostAsJsonAsync("/api/v1/locations", new { name = "Rogue" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PostAsJsonAsync("/api/v1/contract-types", new { code = "ROGUE", name = "Rogue" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 3. HumanResources anti-IDOR & admin guards (403)
        (await aliceClient.PostAsJsonAsync("/api/v1/employees", new { firstName = "X" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/employees/{bob.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PutAsJsonAsync($"/api/v1/employees/{bob.Id}", new { firstName = "Hacked" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PostAsync($"/api/v1/employees/{bob.Id}/deactivate", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/employees/{bob.Id}/emergency-contacts"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.DeleteAsync($"/api/v1/emergency-contacts/{bobContact.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/employee-documents?employeeId={bob.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/employee-documents/{bobDoc.Id}/file"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 4. Contracts module privilege guards (403)
        (await aliceClient.GetAsync("/api/v1/contract-models")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PostAsJsonAsync("/api/v1/contract-models", new { title = "Forbidden" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PostAsJsonAsync("/api/v1/contracts/generate", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 5. Health module anti-IDOR & medical secrecy guards (403)
        (await aliceClient.PostAsJsonAsync("/api/v1/medical-visits", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/medical-visits/{bobVisit.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/medical-visits/{bobVisit.Id}/convocation-file"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. InternalLife module anti-IDOR & HR/CSE guards (403)
        using (var forbiddenCommonDocForm = BuildPdfMultipart(
                   ("companyId", companyId.ToString()),
                   ("category", "CompanyAgreements"),
                   ("isWelcomeKit", "false"),
                   ("title", "Forbidden Agreement")))
        {
            (await aliceClient.PostAsync("/api/v1/common-documents", forbiddenCommonDocForm))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        (await aliceClient.PostAsJsonAsync("/api/v1/staff-representatives", new { }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.GetAsync($"/api/v1/rse-ideas/{bobIdea.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PatchAsJsonAsync($"/api/v1/rse-ideas/{bobIdea.Id}/status", new { status = RseIdeaStatus.Treated }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await aliceClient.PostAsJsonAsync("/api/v1/onboarding-quizzes", new { title = "Forbidden" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response)
    {
        GetHeader(response, "X-Content-Type-Options").ShouldBe("nosniff");
        GetHeader(response, "X-Frame-Options").ShouldBe("DENY");
        GetHeader(response, "Referrer-Policy").ShouldBe("no-referrer");
        GetHeader(response, "Permissions-Policy").ShouldContain("camera=()");
        GetHeader(response, "X-Permitted-Cross-Domain-Policies").ShouldBe("none");
        GetHeader(response, "Cross-Origin-Resource-Policy").ShouldBe("same-origin");
        GetHeader(response, "Content-Security-Policy").ShouldContain("frame-ancestors 'none'");
    }

    private static string GetHeader(HttpResponseMessage response, string headerName)
    {
        response.Headers.TryGetValues(headerName, out var values).ShouldBeTrue($"Missing header '{headerName}'.");
        return string.Join(", ", values);
    }

    private static MultipartFormDataContent BuildPdfMultipart(params (string Key, string Value)[] fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            form.Add(new StringContent(value), key);
        }

        var fileContent = new ByteArrayContent(MinimalPdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "audit-doc.pdf");
        return form;
    }

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string email,
        string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }

    private static async Task<EmployeeDetailDto> CreateEmployeeAsync(
        HttpClient adminClient,
        Guid companyId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyList<string> roles)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                honorific,
                firstName,
                lastName,
                EmployeePassword,
                roles));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }
}
