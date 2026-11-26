using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Enums;
using Sirh.Modules.InternalLife.Infrastructure.Persistence;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.InternalLife;

public sealed class StaffRepresentativesAndRseEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string UserPassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly byte[] MinimalPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    [Fact]
    public async Task StaffRepresentatives_Should_Allow_Employee_Read_And_Restrict_Writes_To_Cse_And_Hr()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var cseEmp = await CreateEmployeeAsync(adminClient, companyId, "Mme", "Amelie", "ElueTitulaire", [IdentityRoles.Cse]);

        using var empClient = await CreateAuthenticatedClientAsync(factory, EmployeeEmail, DemoPassword);
        using var cseClient = await CreateAuthenticatedClientAsync(factory, cseEmp.Email, UserPassword);

        // 1. Standard employee can read (200), but receives 403 Forbidden on POST / PUT / DELETE
        (await empClient.GetAsync("/api/v1/staff-representatives")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var empPost = await empClient.PostAsJsonAsync(
            "/api/v1/staff-representatives",
            new CreateStaffRepresentativeRequest(
                companyId,
                null,
                "Test",
                "User",
                "test.cse@nova-conseil.fr",
                StaffRepresentativeRole.TitularDelegate,
                ElectoralCollege.Executives,
                false));
        empPost.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 2. CSE member creates representative (201) and duplicate email in same company returns 409
        var createResp = await cseClient.PostAsJsonAsync(
            "/api/v1/staff-representatives",
            new CreateStaffRepresentativeRequest(
                companyId,
                cseEmp.Id,
                "Amelie",
                "ElueTitulaire",
                "amelie.cse@nova-conseil.fr",
                StaffRepresentativeRole.TitularDelegate,
                ElectoralCollege.Executives,
                IsHarassmentAdvisor: true));
        createResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var rep = (await createResp.Content.ReadFromJsonAsync<StaffRepresentativeDto>(ApiJsonOptions))!;
        rep.IsHarassmentAdvisor.ShouldBeTrue();

        var dupEmailResp = await cseClient.PostAsJsonAsync(
            "/api/v1/staff-representatives",
            new CreateStaffRepresentativeRequest(
                companyId,
                null,
                "Autre",
                "Elu",
                "AMELIE.CSE@nova-conseil.fr",
                StaffRepresentativeRole.AlternateDelegate,
                ElectoralCollege.Executives,
                IsHarassmentAdvisor: false));
        dupEmailResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 3. Employee filters by companyId and harassmentAdvisor = true
        var advisors = await empClient.GetFromJsonAsync<PagedResult<StaffRepresentativeDto>>(
            $"/api/v1/staff-representatives?companyId={companyId}&harassmentAdvisor=true",
            ApiJsonOptions);
        advisors.ShouldNotBeNull();
        advisors.TotalCount.ShouldBe(1);
        advisors.Items.Single().Id.ShouldBe(rep.Id);

        (await empClient.PutAsJsonAsync(
                $"/api/v1/staff-representatives/{rep.Id}",
                new UpdateStaffRepresentativeRequest(
                    companyId,
                    cseEmp.Id,
                    "Amelie",
                    "Hack",
                    "amelie.cse@nova-conseil.fr",
                    StaffRepresentativeRole.TitularDelegate,
                    ElectoralCollege.Executives,
                    true,
                    rep.RowVersion)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 4. CSE updates with RowVersion (stale -> 409, valid -> 200) and deletes (204)
        var stalePut = await cseClient.PutAsJsonAsync(
            $"/api/v1/staff-representatives/{rep.Id}",
            new UpdateStaffRepresentativeRequest(
                companyId,
                cseEmp.Id,
                "Amelie",
                "Stale",
                "amelie.cse@nova-conseil.fr",
                StaffRepresentativeRole.TitularDelegate,
                ElectoralCollege.Executives,
                true,
                Convert.ToBase64String([9, 9, 9])));
        stalePut.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var validPut = await cseClient.PutAsJsonAsync(
            $"/api/v1/staff-representatives/{rep.Id}",
            new UpdateStaffRepresentativeRequest(
                companyId,
                cseEmp.Id,
                "Amelie",
                "ElueMiseAJour",
                "amelie.cse@nova-conseil.fr",
                StaffRepresentativeRole.UnionDelegate,
                ElectoralCollege.Executives,
                true,
                rep.RowVersion));
        validPut.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await validPut.Content.ReadFromJsonAsync<StaffRepresentativeDto>(ApiJsonOptions))!;
        updated.RepresentativeRole.ShouldBe(StaffRepresentativeRole.UnionDelegate);

        (await empClient.DeleteAsync($"/api/v1/staff-representatives/{rep.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await cseClient.DeleteAsync($"/api/v1/staff-representatives/{rep.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RseIdeas_Should_Encrypt_Attachment_Isolate_Authors_And_Lock_Edits_After_Hr_Treatment()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var locationId = (await adminClient.GetFromJsonAsync<PagedResult<LocationDto>>("/api/v1/locations"))!.Items.First().Id;

        var empA = await CreateEmployeeAsync(adminClient, companyId, "Mme", "Lea", "IdeeA", [IdentityRoles.Employee]);
        var empB = await CreateEmployeeAsync(adminClient, companyId, "M", "Paul", "IdeeB", [IdentityRoles.Employee]);

        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);
        using var empBClient = await CreateAuthenticatedClientAsync(factory, empB.Email, UserPassword);

        // 1. Employee A submits an RSE idea with PDF attachment -> 201 Created, encrypted on disk
        RseIdeaDto idea;
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new StringContent(locationId.ToString()), "locationId");
            form.Add(new StringContent("Panneaux solaires parking"), "title");
            form.Add(new StringContent("Installer des ombrières photovoltaïques sur le site."), "ideaContent");
            var pdfPart = new ByteArrayContent(MinimalPdfBytes);
            pdfPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(pdfPart, "file", "etude-solaire.pdf");

            var resp = await empAClient.PostAsync("/api/v1/rse-ideas", form);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            idea = (await resp.Content.ReadFromJsonAsync<RseIdeaDto>(ApiJsonOptions))!;
        }

        idea.AuthorEmployeeId.ShouldBe(empA.Id);
        idea.Status.ShouldBe(RseIdeaStatus.Submitted);
        idea.HasAttachment.ShouldBeTrue();

        string attachmentKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InternalLifeDbContext>();
            var entity = await db.RseIdeas.SingleAsync(i => i.Id == idea.Id);
            attachmentKey = entity.AttachmentFileKey!;
            var diskBytes = await File.ReadAllBytesAsync(FindStoredFilePath(factory.StoragePath, attachmentKey));
            Encoding.ASCII.GetString(diskBytes.AsSpan(0, 5)).ShouldNotBe("%PDF-");
        }

        // 2. Author downloads attachment & edits while Submitted
        var fileResp = await empAClient.GetAsync($"/api/v1/rse-ideas/{idea.Id}/file");
        fileResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        fileResp.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        Encoding.ASCII.GetString((await fileResp.Content.ReadAsByteArrayAsync()).AsSpan(0, 5)).ShouldBe("%PDF-");

        var editResp = await empAClient.PutAsJsonAsync(
            $"/api/v1/rse-ideas/{idea.Id}",
            new
            {
                locationId,
                title = "Ombrières solaires & bornes VE",
                ideaContent = "Coupler les ombrières avec des bornes de recharge.",
                rowVersion = idea.RowVersion
            });
        editResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        // 3. Employee B cannot view, download, edit, or delete A's idea (403)
        (await empBClient.GetAsync($"/api/v1/rse-ideas/{idea.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.GetAsync($"/api/v1/rse-ideas/{idea.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.PutAsJsonAsync(
                $"/api/v1/rse-ideas/{idea.Id}",
                new { locationId, title = "Hijack", ideaContent = "Hijack" }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.DeleteAsync($"/api/v1/rse-ideas/{idea.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 4. Employee A cannot treat own idea (403 Forbidden)
        (await empAClient.PatchAsJsonAsync(
                $"/api/v1/rse-ideas/{idea.Id}/status",
                new TreatRseIdeaRequest(RseIdeaStatus.Treated, "Auto-validation")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 5. HR lists submitted ideas and marks idea as Treated
        var hrList = await adminClient.GetFromJsonAsync<PagedResult<RseIdeaDto>>(
            "/api/v1/rse-ideas?status=Submitted",
            ApiJsonOptions);
        hrList.ShouldNotBeNull();
        hrList.Items.Select(i => i.Id).ShouldContain(idea.Id);

        var treatResp = await adminClient.PatchAsJsonAsync(
            $"/api/v1/rse-ideas/{idea.Id}/status",
            new TreatRseIdeaRequest(RseIdeaStatus.Treated, "Retenue pour le budget Q2"));
        treatResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var treated = (await treatResp.Content.ReadFromJsonAsync<RseIdeaDto>(ApiJsonOptions))!;
        treated.Status.ShouldBe(RseIdeaStatus.Treated);
        treated.RhComment.ShouldBe("Retenue pour le budget Q2");
        treated.TreatedAtUtc.ShouldNotBeNull();

        // 6. Once Treated, Employee A gets 409 Conflict when trying to edit or delete
        (await empAClient.PutAsJsonAsync(
                $"/api/v1/rse-ideas/{idea.Id}",
                new { locationId, title = "Modif tardive", ideaContent = "Refusée" }))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await empAClient.DeleteAsync($"/api/v1/rse-ideas/{idea.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 7. HR can delete treated idea and clean encrypted attachment on disk
        (await adminClient.DeleteAsync($"/api/v1/rse-ideas/{idea.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InternalLifeDbContext>();
            (await db.RseIdeas.CountAsync()).ShouldBe(0);
            Directory.GetFiles(factory.StoragePath, Path.GetFileName(attachmentKey), SearchOption.AllDirectories)
                .ShouldBeEmpty();
        }
    }

    private static string FindStoredFilePath(string storageRoot, string fileKey) =>
        Directory.GetFiles(storageRoot, Path.GetFileName(fileKey), SearchOption.AllDirectories).Single();

    private static async Task<HttpClient> CreateAuthenticatedClientAsync(
        ApiWebApplicationFactory factory,
        string email,
        string password)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, password));
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
                UserPassword,
                roles));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }
}
