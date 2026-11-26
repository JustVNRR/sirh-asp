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
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Enums;
using Sirh.Modules.Health.Infrastructure.Persistence;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Health;

public sealed class MedicalVisitsEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string UserPassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly byte[] MinimalPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static readonly byte[] ReportPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Report true >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static readonly byte[] FakeExecutableBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    [Fact]
    public async Task MedicalVisits_Should_Require_Convocation_Enforce_MedicalSecrecy_And_Manage_Encrypted_Files()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;

        var manager = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Clara", "ManagerSante", [IdentityRoles.Manager]);
        var empA = await CreateEmployeeAsync(adminClient, companyId, manager.Id, "M", "Hugo", "SanteA", [IdentityRoles.Employee]);
        var empB = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Ines", "SanteB", [IdentityRoles.Employee]);

        // 1. Missing convocationFile returns 400 Bad Request
        using (var noConvocation = new MultipartFormDataContent())
        {
            noConvocation.Add(new StringContent(empA.Id.ToString()), "employeeId");
            noConvocation.Add(new StringContent("Hiring"), "visitType");
            noConvocation.Add(new StringContent("2026-12-10T09:30:00Z"), "visitDateTimeUtc");
            var reportPart = new ByteArrayContent(ReportPdfBytes);
            reportPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            noConvocation.Add(reportPart, "reportFile", "rapport.pdf");

            (await adminClient.PostAsync("/api/v1/medical-visits", noConvocation)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. Magic-bytes check on convocationFile rejects executable renamed .pdf (400 Bad Request)
        using (var fakeForm = BuildCreateForm(
                   empA.Id,
                   "Hiring",
                   "2026-12-10T09:30:00Z",
                   "2028-12-10",
                   "Centre Santé Paris 9",
                   "false",
                   "Visite embauche",
                   FakeExecutableBytes,
                   "virus.pdf"))
        {
            (await adminClient.PostAsync("/api/v1/medical-visits", fakeForm)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 3. Valid creation with PDF convocation returns 201 Created, encrypted on disk
        MedicalVisitDto created;
        using (var validForm = BuildCreateForm(
                   empA.Id,
                   "Hiring",
                   "2026-12-10T09:30:00Z",
                   "2028-12-10",
                   "Centre Santé Paris 9",
                   "false",
                   "Convocation visite d'embauche",
                   MinimalPdfBytes,
                   "convocation-hugo.pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/medical-visits", validForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            created = (await resp.Content.ReadFromJsonAsync<MedicalVisitDto>(ApiJsonOptions))!;
        }

        created.EmployeeId.ShouldBe(empA.Id);
        created.VisitType.ShouldBe(MedicalVisitType.Hiring);
        created.CarriedOut.ShouldBeFalse();
        created.HasReport.ShouldBeFalse();
        created.ConvocationFileName.ShouldBe("convocation-hugo.pdf");

        string convocationFileKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
            var entity = await db.MedicalVisits.SingleAsync(v => v.Id == created.Id);
            convocationFileKey = entity.ConvocationFileKey;
            var diskBytes = await File.ReadAllBytesAsync(FindStoredFilePath(factory.StoragePath, convocationFileKey));
            Encoding.ASCII.GetString(diskBytes.AsSpan(0, 5)).ShouldNotBe("%PDF-");
        }

        // Missing report returns 404 NotFound
        (await adminClient.GetAsync($"/api/v1/medical-visits/{created.Id}/report-file")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // 4. Employee A (Self) can read list, detail and download convocation (inline & attachment)
        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);
        var listA = await empAClient.GetFromJsonAsync<PagedResult<MedicalVisitDto>>("/api/v1/medical-visits", ApiJsonOptions);
        listA.ShouldNotBeNull();
        listA.TotalCount.ShouldBe(1);
        listA.Items.Single().Id.ShouldBe(created.Id);

        (await empAClient.GetAsync($"/api/v1/medical-visits/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var inlineConv = await empAClient.GetAsync($"/api/v1/medical-visits/{created.Id}/convocation-file");
        inlineConv.StatusCode.ShouldBe(HttpStatusCode.OK);
        inlineConv.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        inlineConv.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        Encoding.ASCII.GetString((await inlineConv.Content.ReadAsByteArrayAsync()).AsSpan(0, 5)).ShouldBe("%PDF-");

        var attachmentConv = await empAClient.GetAsync($"/api/v1/medical-visits/{created.Id}/convocation-file?download=true");
        attachmentConv.StatusCode.ShouldBe(HttpStatusCode.OK);
        attachmentConv.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

        // 5. Medical secrecy: Employee B AND Direct Manager both receive 403 Forbidden
        using var empBClient = await CreateAuthenticatedClientAsync(factory, empB.Email, UserPassword);
        (await empBClient.GetAsync($"/api/v1/medical-visits?employeeId={empA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.GetAsync($"/api/v1/medical-visits/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.GetAsync($"/api/v1/medical-visits/{created.Id}/convocation-file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var mgrClient = await CreateAuthenticatedClientAsync(factory, manager.Email, UserPassword);
        (await mgrClient.GetAsync($"/api/v1/medical-visits?employeeId={empA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mgrClient.GetAsync($"/api/v1/medical-visits/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mgrClient.GetAsync($"/api/v1/medical-visits/{created.Id}/convocation-file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mgrClient.GetAsync($"/api/v1/medical-visits/{created.Id}/report-file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. HR updates visit attaching report PDF and carriedOut = true; stale RowVersion returns 409 Conflict
        MedicalVisitDto updated;
        using (var updateForm = new MultipartFormDataContent())
        {
            updateForm.Add(new StringContent("Hiring"), "visitType");
            updateForm.Add(new StringContent("2026-12-10T09:30:00Z"), "visitDateTimeUtc");
            updateForm.Add(new StringContent("2028-12-10"), "validityDate");
            updateForm.Add(new StringContent("Centre Santé Paris 9"), "medicalCenter");
            updateForm.Add(new StringContent("true"), "carriedOut");
            updateForm.Add(new StringContent("Apte sans réserve"), "comments");
            updateForm.Add(new StringContent(created.RowVersion), "rowVersion");
            var reportPart = new ByteArrayContent(ReportPdfBytes);
            reportPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            updateForm.Add(reportPart, "reportFile", "attestation-suivi-hugo.pdf");

            var putResp = await adminClient.PutAsync($"/api/v1/medical-visits/{created.Id}", updateForm);
            putResp.StatusCode.ShouldBe(HttpStatusCode.OK);
            updated = (await putResp.Content.ReadFromJsonAsync<MedicalVisitDto>(ApiJsonOptions))!;
        }

        updated.CarriedOut.ShouldBeTrue();
        updated.HasReport.ShouldBeTrue();
        updated.ReportFileName.ShouldBe("attestation-suivi-hugo.pdf");
        updated.RowVersion.ShouldNotBe(created.RowVersion);

        // Stale RowVersion conflict (409)
        var stalePut = await adminClient.PutAsJsonAsync(
            $"/api/v1/medical-visits/{created.Id}",
            new
            {
                visitType = "FollowUp",
                visitDateTimeUtc = "2026-12-10T09:30:00Z",
                carriedOut = true,
                rowVersion = created.RowVersion
            });
        stalePut.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Employee A downloads decrypted report PDF
        var reportResp = await empAClient.GetAsync($"/api/v1/medical-visits/{created.Id}/report-file");
        reportResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        Encoding.ASCII.GetString((await reportResp.Content.ReadAsByteArrayAsync()).AsSpan(0, 5)).ShouldBe("%PDF-");

        // 7. Delete cleans DB row and both encrypted files on disk
        string reportFileKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
            var entity = await db.MedicalVisits.SingleAsync(v => v.Id == created.Id);
            reportFileKey = entity.ReportFileKey!;
            FindStoredFilePath(factory.StoragePath, reportFileKey).ShouldNotBeNullOrWhiteSpace();
        }

        (await adminClient.DeleteAsync($"/api/v1/medical-visits/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HealthDbContext>();
            (await db.MedicalVisits.CountAsync()).ShouldBe(0);
            Directory.GetFiles(factory.StoragePath, Path.GetFileName(convocationFileKey), SearchOption.AllDirectories).ShouldBeEmpty();
            Directory.GetFiles(factory.StoragePath, Path.GetFileName(reportFileKey), SearchOption.AllDirectories).ShouldBeEmpty();
        }
    }

    private static MultipartFormDataContent BuildCreateForm(
        Guid employeeId,
        string visitType,
        string visitDateTimeUtc,
        string validityDate,
        string medicalCenter,
        string carriedOut,
        string comments,
        byte[] convocationBytes,
        string convocationFileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(employeeId.ToString()), "employeeId");
        form.Add(new StringContent(visitType), "visitType");
        form.Add(new StringContent(visitDateTimeUtc), "visitDateTimeUtc");
        form.Add(new StringContent(validityDate), "validityDate");
        form.Add(new StringContent(medicalCenter), "medicalCenter");
        form.Add(new StringContent(carriedOut), "carriedOut");
        form.Add(new StringContent(comments), "comments");
        var convPart = new ByteArrayContent(convocationBytes);
        convPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(convPart, "convocationFile", convocationFileName);
        return form;
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
        Guid? directManagerId,
        string honorific,
        string firstName,
        string lastName,
        IReadOnlyList<string> roles)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                directManagerId,
                honorific,
                firstName,
                lastName,
                UserPassword,
                roles));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;
    }
}
