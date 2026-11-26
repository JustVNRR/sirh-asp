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

public sealed class CommonDocumentsEndpointsTests
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

    private static readonly byte[] UpdatedPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Updated true >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static readonly byte[] FakeExecutableBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    [Fact]
    public async Task CommonDocuments_And_WelcomeKits_Should_Filter_Stream_And_Enforce_Cse_And_ContractTypeGuard()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var contractTypes = (await adminClient.GetFromJsonAsync<PagedResult<ContractTypeDto>>("/api/v1/contract-types"))!.Items;
        var cdiId = contractTypes.Single(ct => ct.Code == "CDI").Id;
        var apprenticeshipId = contractTypes.Single(ct => ct.Code == "APPRENTISSAGE").Id;

        var cseMember = await CreateEmployeeAsync(adminClient, companyId, "Mme", "Nora", "ElueCse", [IdentityRoles.Cse]);

        // 1. Magic-bytes check rejects fake .pdf (400 Bad Request)
        using (var fakeForm = BuildForm(companyId, null, "CompanyAgreements", "false", "Accord Fake", FakeExecutableBytes, "fake.pdf"))
        {
            (await adminClient.PostAsync("/api/v1/common-documents", fakeForm)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. HR publishes Welcome Kits ("Mutuelle CDI" targeted, "Livret" universal, "Alternance" other type) + Company Agreement
        CommonDocumentDto mutualCdiKit;
        using (var form = BuildForm(companyId, cdiId, "WelcomeKitMutual", "true", "Mutuelle CDI", MinimalPdfBytes, "mutuelle-cdi.pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/common-documents", form);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            mutualCdiKit = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }

        CommonDocumentDto universalBooklet;
        using (var form = BuildForm(companyId, null, "WelcomeKitBooklet", "true", "Livret d'accueil", MinimalPdfBytes, "livret.pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/common-documents", form);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            universalBooklet = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }

        CommonDocumentDto apprenticeshipKit;
        using (var form = BuildForm(companyId, apprenticeshipId, "WelcomeKitForesight", "true", "Guide Alternance", MinimalPdfBytes, "alternance.pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/common-documents", form);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            apprenticeshipKit = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }

        CommonDocumentDto agreementDoc;
        using (var form = BuildForm(companyId, null, "CompanyAgreements", "false", "Accord Télétravail", MinimalPdfBytes, "teletravail.pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/common-documents", form);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            agreementDoc = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }

        // Verify StorageScope.CommonPublic writes unencrypted PDF on disk
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InternalLifeDbContext>();
            var entity = await db.CommonDocuments.SingleAsync(d => d.Id == mutualCdiKit.Id);
            var diskPath = FindStoredFilePath(factory.StoragePath, entity.FileKey);
            var diskBytes = await File.ReadAllBytesAsync(diskPath);
            Encoding.ASCII.GetString(diskBytes.AsSpan(0, 5)).ShouldBe("%PDF-");
        }

        // 3. Employee filters welcome kits for CDI -> receives BOTH targeted "Mutuelle CDI" AND universal "Livret d'accueil"
        using var empClient = await CreateAuthenticatedClientAsync(factory, EmployeeEmail, DemoPassword);
        var cdiKits = await empClient.GetFromJsonAsync<PagedResult<CommonDocumentDto>>(
            $"/api/v1/common-documents?isWelcomeKit=true&contractTypeId={cdiId}",
            ApiJsonOptions);
        cdiKits.ShouldNotBeNull();
        cdiKits.TotalCount.ShouldBe(2);
        cdiKits.Items.Select(i => i.Id).ShouldContain(mutualCdiKit.Id);
        cdiKits.Items.Select(i => i.Id).ShouldContain(universalBooklet.Id);
        cdiKits.Items.Select(i => i.Id).ShouldNotContain(apprenticeshipKit.Id);

        // Employee can stream inline & attachment, but cannot write/archive/delete
        var inlineResp = await empClient.GetAsync($"/api/v1/common-documents/{mutualCdiKit.Id}/file");
        inlineResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        inlineResp.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        inlineResp.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        Encoding.ASCII.GetString((await inlineResp.Content.ReadAsByteArrayAsync()).AsSpan(0, 5)).ShouldBe("%PDF-");

        var downloadResp = await empClient.GetAsync($"/api/v1/common-documents/{mutualCdiKit.Id}/file?download=true");
        downloadResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        downloadResp.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

        (await empClient.PatchAsJsonAsync(
                $"/api/v1/common-documents/{agreementDoc.Id}/archive",
                new ArchiveCommonDocumentRequest(true)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empClient.DeleteAsync($"/api/v1/common-documents/{agreementDoc.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 4. CSE member can CRUD & archive CSE categories, but gets 403 Forbidden on CompanyAgreements
        using var cseClient = await CreateAuthenticatedClientAsync(factory, cseMember.Email, UserPassword);

        using (var forbiddenAgreement = BuildForm(companyId, null, "CompanyAgreements", "false", "Accord interdit CSE", MinimalPdfBytes, "accord.pdf"))
        {
            (await cseClient.PostAsync("/api/v1/common-documents", forbiddenAgreement)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        (await cseClient.PatchAsJsonAsync(
                $"/api/v1/common-documents/{agreementDoc.Id}/archive",
                new ArchiveCommonDocumentRequest(true)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        CommonDocumentDto csePv;
        using (var cseForm = BuildForm(companyId, null, "CsePv", "false", "PV CSE Novembre", MinimalPdfBytes, "pv-nov.pdf"))
        {
            var resp = await cseClient.PostAsync("/api/v1/common-documents", cseForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            csePv = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }

        // Stale RowVersion check (409)
        var stalePut = await cseClient.PutAsJsonAsync(
            $"/api/v1/common-documents/{csePv.Id}",
            new
            {
                companyId,
                category = "CsePv",
                title = "PV Stale",
                rowVersion = Convert.ToBase64String([1, 2, 3, 4])
            });
        stalePut.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // Valid multipart update replaces file and increments RowVersion
        CommonDocumentDto updatedCsePv;
        using (var updateForm = new MultipartFormDataContent())
        {
            updateForm.Add(new StringContent(companyId.ToString()), "companyId");
            updateForm.Add(new StringContent("CsePv"), "category");
            updateForm.Add(new StringContent("PV CSE Novembre (Approuvé)"), "title");
            updateForm.Add(new StringContent(csePv.RowVersion), "rowVersion");
            var pdfPart = new ByteArrayContent(UpdatedPdfBytes);
            pdfPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            updateForm.Add(pdfPart, "file", "pv-nov-approuve.pdf");

            var resp = await cseClient.PutAsync($"/api/v1/common-documents/{csePv.Id}", updateForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.OK);
            updatedCsePv = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }
        updatedCsePv.FileName.ShouldBe("pv-nov-approuve.pdf");
        updatedCsePv.RowVersion.ShouldNotBe(csePv.RowVersion);

        // Archive & filter by archived=true
        var archiveResp = await cseClient.PatchAsJsonAsync(
            $"/api/v1/common-documents/{csePv.Id}/archive",
            new ArchiveCommonDocumentRequest(true, updatedCsePv.RowVersion));
        archiveResp.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var archivedList = await empClient.GetFromJsonAsync<PagedResult<CommonDocumentDto>>(
            "/api/v1/common-documents?archived=true",
            ApiJsonOptions);
        archivedList.ShouldNotBeNull();
        archivedList.Items.Select(i => i.Id).ShouldBe([csePv.Id]);

        (await cseClient.DeleteAsync($"/api/v1/common-documents/{csePv.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // 5. Cross-module ContractType guard: deleting a custom ContractType used by a Welcome Kit returns 409, then 204 after deletion
        var customTypeResp = await adminClient.PostAsJsonAsync(
            "/api/v1/contract-types",
            new CreateContractTypeRequest("VIE", "Volontariat International en Entreprise"));
        customTypeResp.StatusCode.ShouldBe(HttpStatusCode.Created);
        var customType = (await customTypeResp.Content.ReadFromJsonAsync<ContractTypeDto>())!;

        CommonDocumentDto vieKit;
        using (var form = BuildForm(companyId, customType.Id, "WelcomeKitMobility", "true", "Kit Mobilité VIE", MinimalPdfBytes, "kit-vie.pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/common-documents", form);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            vieKit = (await resp.Content.ReadFromJsonAsync<CommonDocumentDto>(ApiJsonOptions))!;
        }

        (await adminClient.DeleteAsync($"/api/v1/contract-types/{customType.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await adminClient.DeleteAsync($"/api/v1/common-documents/{vieKit.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/contract-types/{customType.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static MultipartFormDataContent BuildForm(
        Guid? companyId,
        Guid? contractTypeId,
        string category,
        string isWelcomeKit,
        string title,
        byte[] fileBytes,
        string fileName)
    {
        var form = new MultipartFormDataContent();
        if (companyId.HasValue)
        {
            form.Add(new StringContent(companyId.Value.ToString()), "companyId");
        }

        if (contractTypeId.HasValue)
        {
            form.Add(new StringContent(contractTypeId.Value.ToString()), "contractTypeId");
        }

        form.Add(new StringContent(category), "category");
        form.Add(new StringContent(isWelcomeKit), "isWelcomeKit");
        form.Add(new StringContent(title), "title");

        var part = new ByteArrayContent(fileBytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(part, "file", fileName);
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
