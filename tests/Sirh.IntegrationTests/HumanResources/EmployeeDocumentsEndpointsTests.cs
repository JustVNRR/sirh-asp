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
using Sirh.Modules.HumanResources.Domain.Enums;
using Sirh.Modules.HumanResources.Infrastructure.Persistence;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.HumanResources;

public sealed class EmployeeDocumentsEndpointsTests
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

    private static readonly byte[] UpdatedPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Version /1.7 >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static readonly byte[] MinimalPngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    private static readonly byte[] FakeExecutableBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    [Fact]
    public async Task EmployeeDocuments_Should_Enforce_MagicBytes_SensitiveExclusion_Abac_Update_And_Delete()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;

        var manager1 = await CreateEmployeeAsync(adminClient, companyId, null, "M", "Marc", "ManagerUn", [IdentityRoles.Manager]);
        var manager2 = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Sophie", "ManagerDeux", [IdentityRoles.Manager]);
        var empA = await CreateEmployeeAsync(adminClient, companyId, manager1.Id, "Mme", "Julie", "DossierA", [IdentityRoles.Employee]);
        var empB = await CreateEmployeeAsync(adminClient, companyId, null, "M", "Thomas", "DossierB", [IdentityRoles.Employee]);

        // 1. Magic-bytes check & category mismatch check
        using (var fakeForm = BuildDocumentForm(empA.Id, null, "Cv", "Fake", FakeExecutableBytes, "virus.pdf", "application/pdf"))
        {
            (await adminClient.PostAsync("/api/v1/employee-documents", fakeForm)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var mismatchForm = BuildDocumentForm(empA.Id, "Disciplinary", "Cv", "Mismatch", MinimalPdfBytes, "cv.pdf", "application/pdf"))
        {
            (await adminClient.PostAsync("/api/v1/employee-documents", mismatchForm)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. HR uploads PersonalFile (CV PDF + VitalCard PNG) and Disciplinary (Warning PDF) for Employee A
        EmployeeDocumentDto cvDoc;
        using (var cvForm = BuildDocumentForm(empA.Id, "PersonalFile", "Cv", "CV initial", MinimalPdfBytes, "cv-julie.pdf", "application/pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/employee-documents", cvForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            cvDoc = (await resp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        }

        EmployeeDocumentDto vitalDoc;
        using (var vitalForm = BuildDocumentForm(empA.Id, null, "VitalCard", "Carte vitale", MinimalPngBytes, "vitale.png", "image/png"))
        {
            var resp = await adminClient.PostAsync("/api/v1/employee-documents", vitalForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            vitalDoc = (await resp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        }
        vitalDoc.Category.ShouldBe(EmployeeDocumentCategory.PersonalFile);
        vitalDoc.ContentType.ShouldBe("image/png");

        EmployeeDocumentDto warningDoc;
        using (var warnForm = BuildDocumentForm(empA.Id, null, "Warning", "Avertissement disciplinaire", MinimalPdfBytes, "avertissement.pdf", "application/pdf"))
        {
            var resp = await adminClient.PostAsync("/api/v1/employee-documents", warnForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            warningDoc = (await resp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        }
        warningDoc.Category.ShouldBe(EmployeeDocumentCategory.Disciplinary);

        // Verify AES-256-GCM encryption on disk
        string originalCvFileKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HumanResourcesDbContext>();
            var cvEntity = await db.EmployeeDocuments.SingleAsync(d => d.Id == cvDoc.Id);
            originalCvFileKey = cvEntity.FileKey;
            var diskBytes = await File.ReadAllBytesAsync(FindStoredFilePath(factory.StoragePath, originalCvFileKey));
            Encoding.ASCII.GetString(diskBytes.AsSpan(0, 5)).ShouldNotBe("%PDF-");
        }

        // 3. Employee A (Self): sees Cv & VitalCard, Warning is excluded and directly forbidden (403)
        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);
        var listA = await empAClient.GetFromJsonAsync<PagedResult<EmployeeDocumentDto>>("/api/v1/employee-documents", ApiJsonOptions);
        listA.ShouldNotBeNull();
        listA.TotalCount.ShouldBe(2);
        listA.Items.Select(i => i.Id).ShouldNotContain(warningDoc.Id);

        (await empAClient.GetAsync("/api/v1/employee-documents?category=Disciplinary")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empAClient.GetAsync($"/api/v1/employee-documents/{warningDoc.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empAClient.GetAsync($"/api/v1/employee-documents/{warningDoc.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var inlineFile = await empAClient.GetAsync($"/api/v1/employee-documents/{cvDoc.Id}/file");
        inlineFile.StatusCode.ShouldBe(HttpStatusCode.OK);
        inlineFile.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        inlineFile.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        Encoding.ASCII.GetString((await inlineFile.Content.ReadAsByteArrayAsync()).AsSpan(0, 5)).ShouldBe("%PDF-");

        var attachmentFile = await empAClient.GetAsync($"/api/v1/employee-documents/{cvDoc.Id}/file?download=true");
        attachmentFile.StatusCode.ShouldBe(HttpStatusCode.OK);
        attachmentFile.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

        // Self-service upload: allowed for own PersonalFile, forbidden for Disciplinary or another employee
        EmployeeDocumentDto ibanDoc;
        using (var ibanForm = BuildDocumentForm(null, null, "Iban", "Mon RIB", MinimalPdfBytes, "rib.pdf", "application/pdf"))
        {
            var resp = await empAClient.PostAsync("/api/v1/employee-documents", ibanForm);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            ibanDoc = (await resp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        }
        ibanDoc.EmployeeId.ShouldBe(empA.Id);

        using (var selfWarnForm = BuildDocumentForm(empA.Id, null, "Warning", "Interdit", MinimalPdfBytes, "w.pdf", "application/pdf"))
        {
            (await empAClient.PostAsync("/api/v1/employee-documents", selfWarnForm)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        using (var otherEmpForm = BuildDocumentForm(empB.Id, null, "Iban", "Interdit", MinimalPdfBytes, "rib-b.pdf", "application/pdf"))
        {
            (await empAClient.PostAsync("/api/v1/employee-documents", otherEmpForm)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }

        // 4. Direct Manager M1: can view A's non-sensitive docs, excluded/403 for Warning
        using var mgr1Client = await CreateAuthenticatedClientAsync(factory, manager1.Email, UserPassword);
        var mgrList = await mgr1Client.GetFromJsonAsync<PagedResult<EmployeeDocumentDto>>(
            $"/api/v1/employee-documents?employeeId={empA.Id}",
            ApiJsonOptions);
        mgrList.ShouldNotBeNull();
        mgrList.TotalCount.ShouldBe(3);
        mgrList.Items.Select(i => i.Id).ShouldNotContain(warningDoc.Id);
        (await mgr1Client.GetAsync($"/api/v1/employee-documents/{warningDoc.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mgr1Client.GetAsync($"/api/v1/employee-documents/{warningDoc.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 5. Manager M2 (other team) & Employee B get 403 Forbidden
        using var mgr2Client = await CreateAuthenticatedClientAsync(factory, manager2.Email, UserPassword);
        (await mgr2Client.GetAsync($"/api/v1/employee-documents?employeeId={empA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mgr2Client.GetAsync($"/api/v1/employee-documents/{cvDoc.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var empBClient = await CreateAuthenticatedClientAsync(factory, empB.Email, UserPassword);
        (await empBClient.GetAsync($"/api/v1/employee-documents?employeeId={empA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.GetAsync($"/api/v1/employee-documents/{cvDoc.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. HR can access sensitive documents, filter by comma-separated documentTypes, update (with RowVersion 409) and delete
        var hrSensitiveList = await adminClient.GetFromJsonAsync<PagedResult<EmployeeDocumentDto>>(
            $"/api/v1/employee-documents?employeeId={empA.Id}&category=Disciplinary",
            ApiJsonOptions);
        hrSensitiveList.ShouldNotBeNull();
        hrSensitiveList.Items.Select(i => i.Id).ShouldBe([warningDoc.Id]);
        (await adminClient.GetAsync($"/api/v1/employee-documents/{warningDoc.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var filteredTypes = await adminClient.GetFromJsonAsync<PagedResult<EmployeeDocumentDto>>(
            $"/api/v1/employee-documents?employeeId={empA.Id}&documentTypes=Cv,Iban",
            ApiJsonOptions);
        filteredTypes.ShouldNotBeNull();
        filteredTypes.TotalCount.ShouldBe(2);

        var stalePut = await adminClient.PutAsJsonAsync(
            $"/api/v1/employee-documents/{cvDoc.Id}",
            new
            {
                documentType = "Cv",
                commentary = "Stale",
                rowVersion = Convert.ToBase64String([1, 2, 3, 4])
            });
        stalePut.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using (var replaceForm = new MultipartFormDataContent())
        {
            replaceForm.Add(new StringContent("Cv"), "documentType");
            replaceForm.Add(new StringContent("CV mis à jour"), "commentary");
            replaceForm.Add(new StringContent(cvDoc.RowVersion), "rowVersion");
            var pdfContent = new ByteArrayContent(UpdatedPdfBytes);
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            replaceForm.Add(pdfContent, "file", "cv-julie-v2.pdf");

            var replaceResp = await adminClient.PutAsync($"/api/v1/employee-documents/{cvDoc.Id}", replaceForm);
            replaceResp.StatusCode.ShouldBe(HttpStatusCode.OK);
            var updated = (await replaceResp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
            updated.FileName.ShouldBe("cv-julie-v2.pdf");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HumanResourcesDbContext>();
            var entity = await db.EmployeeDocuments.SingleAsync(d => d.Id == cvDoc.Id);
            entity.FileKey.ShouldNotBe(originalCvFileKey);
            Directory.GetFiles(factory.StoragePath, Path.GetFileName(originalCvFileKey), SearchOption.AllDirectories)
                .ShouldBeEmpty();
            FindStoredFilePath(factory.StoragePath, entity.FileKey).ShouldNotBeNullOrWhiteSpace();

            (await adminClient.DeleteAsync($"/api/v1/employee-documents/{cvDoc.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await db.EmployeeDocuments.AnyAsync(d => d.Id == cvDoc.Id)).ShouldBeFalse();
            Directory.GetFiles(factory.StoragePath, Path.GetFileName(entity.FileKey), SearchOption.AllDirectories)
                .ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task EmployeeDocuments_Should_Validate_And_Persist_Rqth_And_ResidentPermit_Details()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var emp = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Nadia", "Conformite", [IdentityRoles.Employee]);

        // 1. Validation failures when required specialized fields are missing
        using (var rqthMissingExpiry = BuildDocumentForm(
                   emp.Id, null, "RqthCertificate", "Sans échéance", MinimalPdfBytes, "rqth.pdf", "application/pdf"))
        {
            (await adminClient.PostAsync("/api/v1/employee-documents", rqthMissingExpiry)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var permitMissingExpiry = BuildDocumentForm(
                   emp.Id, null, "ResidentPermit", "Sans date", MinimalPdfBytes, "titre.pdf", "application/pdf",
                   permitNumber: "75AB98765"))
        {
            (await adminClient.PostAsync("/api/v1/employee-documents", permitMissingExpiry)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var permitMissingNumber = BuildDocumentForm(
                   emp.Id, null, "ResidentPermit", "Sans numéro", MinimalPdfBytes, "titre.pdf", "application/pdf",
                   expiryDate: "2028-01-15"))
        {
            (await adminClient.PostAsync("/api/v1/employee-documents", permitMissingNumber)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. Valid RQTH creation + GET detail
        EmployeeDocumentDto rqthDoc;
        using (var validRqth = BuildDocumentForm(
                   emp.Id, "PersonalFile", "RqthCertificate", "RQTH validée", MinimalPdfBytes, "rqth-nadia.pdf", "application/pdf",
                   expiryDate: "2027-06-30",
                   workplaceAccommodation: "Poste assis-debout ergonomique"))
        {
            var resp = await adminClient.PostAsync("/api/v1/employee-documents", validRqth);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            rqthDoc = (await resp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        }

        var rqthFetched = await adminClient.GetFromJsonAsync<EmployeeDocumentDto>(
            $"/api/v1/employee-documents/{rqthDoc.Id}",
            ApiJsonOptions);
        rqthFetched.ShouldNotBeNull();
        rqthFetched.RqthDetails.ShouldNotBeNull();
        rqthFetched.RqthDetails!.ExpiryDate.ShouldBe(new DateOnly(2027, 6, 30));
        rqthFetched.RqthDetails.WorkplaceAccommodation.ShouldBe("Poste assis-debout ergonomique");
        rqthFetched.ResidentPermitDetails.ShouldBeNull();

        // 3. Valid ResidentPermit creation + JSON PUT update + RowVersion 409
        EmployeeDocumentDto permitDoc;
        using (var validPermit = BuildDocumentForm(
                   emp.Id, "PersonalFile", "ResidentPermit", "Titre de séjour", MinimalPdfBytes, "titre-nadia.pdf", "application/pdf",
                   expiryDate: "2028-01-15",
                   permitNumber: "99AA12345"))
        {
            var resp = await adminClient.PostAsync("/api/v1/employee-documents", validPermit);
            resp.StatusCode.ShouldBe(HttpStatusCode.Created);
            permitDoc = (await resp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        }

        var permitFetched = await adminClient.GetFromJsonAsync<EmployeeDocumentDto>(
            $"/api/v1/employee-documents/{permitDoc.Id}",
            ApiJsonOptions);
        permitFetched.ShouldNotBeNull();
        permitFetched.ResidentPermitDetails.ShouldNotBeNull();
        permitFetched.ResidentPermitDetails!.PermitNumber.ShouldBe("99AA12345");
        permitFetched.ResidentPermitDetails.ExpiryDate.ShouldBe(new DateOnly(2028, 1, 15));

        var updateResp = await adminClient.PutAsJsonAsync(
            $"/api/v1/employee-documents/{permitDoc.Id}",
            new
            {
                documentType = "ResidentPermit",
                commentary = "Renouvellement validé",
                expiryDate = "2030-01-15",
                permitNumber = "99BB54321",
                rowVersion = permitFetched.RowVersion
            });
        updateResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updatedPermit = (await updateResp.Content.ReadFromJsonAsync<EmployeeDocumentDto>(ApiJsonOptions))!;
        updatedPermit.RowVersion.ShouldNotBe(permitFetched.RowVersion);
        updatedPermit.ResidentPermitDetails!.PermitNumber.ShouldBe("99BB54321");
        updatedPermit.ResidentPermitDetails.ExpiryDate.ShouldBe(new DateOnly(2030, 1, 15));

        var staleResp = await adminClient.PutAsJsonAsync(
            $"/api/v1/employee-documents/{permitDoc.Id}",
            new
            {
                documentType = "ResidentPermit",
                expiryDate = "2031-01-15",
                permitNumber = "99CC00000",
                rowVersion = permitFetched.RowVersion
            });
        staleResp.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    private static MultipartFormDataContent BuildDocumentForm(
        Guid? employeeId,
        string? category,
        string documentType,
        string? commentary,
        byte[] bytes,
        string fileName,
        string contentType,
        string? expiryDate = null,
        string? workplaceAccommodation = null,
        string? permitNumber = null)
    {
        var form = new MultipartFormDataContent();
        if (employeeId.HasValue)
        {
            form.Add(new StringContent(employeeId.Value.ToString()), "employeeId");
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            form.Add(new StringContent(category), "category");
        }

        form.Add(new StringContent(documentType), "documentType");
        if (!string.IsNullOrWhiteSpace(commentary))
        {
            form.Add(new StringContent(commentary), "commentary");
        }

        if (!string.IsNullOrWhiteSpace(expiryDate))
        {
            form.Add(new StringContent(expiryDate), "expiryDate");
        }

        if (!string.IsNullOrWhiteSpace(workplaceAccommodation))
        {
            form.Add(new StringContent(workplaceAccommodation), "workplaceAccommodation");
        }

        if (!string.IsNullOrWhiteSpace(permitNumber))
        {
            form.Add(new StringContent(permitNumber), "permitNumber");
        }

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
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
