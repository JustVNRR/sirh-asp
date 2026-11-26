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
using Sirh.BuildingBlocks.Application.Storage;
using Sirh.Modules.Contracts.Application.DTOs;
using Sirh.Modules.Contracts.Domain.Enums;
using Sirh.Modules.Contracts.Infrastructure.Persistence;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Contracts;

public sealed class ContractsAndJobSheetsEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string UserPassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly byte[] MinimalValidPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static readonly byte[] UpdatedValidPdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Version /1.7 >>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    private static readonly byte[] FakeExecutableBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

    [Fact]
    public async Task Contracts_Should_Support_Import_PdfValidation_Abac_Download_StatusUpdate_Concurrency_And_Delete()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var (companyId, locationId, cdiId) = await GetOrganizationRefsAsync(adminClient);

        var empA = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Alice", "ContratA", [IdentityRoles.Employee]);
        var empB = await CreateEmployeeAsync(adminClient, companyId, null, "M", "Bob", "ContratB", [IdentityRoles.Employee]);
        var manager = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Claire", "ManagerC", [IdentityRoles.Manager]);

        var posA = await CreatePositionAsync(adminClient, empA.Id, manager.Id, locationId, "Consultante SIRH");

        // 1. Magic-bytes check: executable renamed to .pdf must fail with 400
        using (var fakeForm = BuildContractForm(posA.Id, cdiId, "PendingSignature", "Fake PDF", FakeExecutableBytes, "virus.pdf"))
        {
            var fakeResponse = await adminClient.PostAsync("/api/v1/contracts", fakeForm);
            fakeResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. Valid PDF import returns 201 Created and encrypts on disk
        ContractDto imported;
        using (var validForm = BuildContractForm(posA.Id, cdiId, "PendingSignature", "Importé pour signature", MinimalValidPdfBytes, "cdi-alice.pdf"))
        {
            var importResponse = await adminClient.PostAsync("/api/v1/contracts", validForm);
            importResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
            imported = (await importResponse.Content.ReadFromJsonAsync<ContractDto>(ApiJsonOptions))!;
        }

        imported.PositionId.ShouldBe(posA.Id);
        imported.EmployeeId.ShouldBe(empA.Id);
        imported.Status.ShouldBe(ContractStatus.PendingSignature);
        imported.FileName.ShouldBe("cdi-alice.pdf");
        imported.ContentType.ShouldBe("application/pdf");

        string firstFileKey;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContractsDbContext>();
            var entity = await db.Contracts.SingleAsync(c => c.Id == imported.Id);
            firstFileKey = entity.FileKey;
            var diskPath = FindStoredFilePath(factory.StoragePath, firstFileKey);
            var diskBytes = await File.ReadAllBytesAsync(diskPath);
            Encoding.ASCII.GetString(diskBytes.AsSpan(0, 5)).ShouldNotBe("%PDF-");
        }

        // 3. Second contract on same position returns 409 Conflict
        using (var dupForm = BuildContractForm(posA.Id, cdiId, "Draft", "Duplicate", MinimalValidPdfBytes, "dup.pdf"))
        {
            var dupResponse = await adminClient.PostAsync("/api/v1/contracts", dupForm);
            dupResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        // 4. ABAC: Employee A (Self) can list, read by position, and download inline or attachment
        using var empAClient = await CreateAuthenticatedClientAsync(factory, empA.Email, UserPassword);
        var listA = await empAClient.GetFromJsonAsync<PagedResult<ContractDto>>("/api/v1/contracts", ApiJsonOptions);
        listA.ShouldNotBeNull();
        listA.TotalCount.ShouldBe(1);
        listA.Items.Single().Id.ShouldBe(imported.Id);

        var byPosA = await empAClient.GetAsync($"/api/v1/contracts/by-position/{posA.Id}");
        byPosA.StatusCode.ShouldBe(HttpStatusCode.OK);

        var inlineFile = await empAClient.GetAsync($"/api/v1/contracts/{imported.Id}/file");
        inlineFile.StatusCode.ShouldBe(HttpStatusCode.OK);
        inlineFile.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        inlineFile.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");
        var inlineBytes = await inlineFile.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(inlineBytes.AsSpan(0, 5)).ShouldBe("%PDF-");

        var attachmentFile = await empAClient.GetAsync($"/api/v1/contracts/{imported.Id}/file?download=true");
        attachmentFile.StatusCode.ShouldBe(HttpStatusCode.OK);
        attachmentFile.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

        // 5. ABAC: Unrelated Employee B and Manager C cannot read Employee A's contract
        using var empBClient = await CreateAuthenticatedClientAsync(factory, empB.Email, UserPassword);
        (await empBClient.GetAsync($"/api/v1/contracts/by-position/{posA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.GetAsync($"/api/v1/contracts/{imported.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await empBClient.GetAsync($"/api/v1/contracts?employeeId={empA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var managerClient = await CreateAuthenticatedClientAsync(factory, manager.Email, UserPassword);
        (await managerClient.GetAsync($"/api/v1/contracts/by-position/{posA.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await managerClient.GetAsync($"/api/v1/contracts/{imported.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 6. Status update to Signed (JSON) + RowVersion concurrency conflict (409) + multipart PDF replacement
        var signResponse = await adminClient.PutAsJsonAsync(
            $"/api/v1/contracts/{imported.Id}",
            new
            {
                contractTypeId = cdiId,
                status = "Signed",
                signedAt = "2026-03-01",
                commentary = "Contrat signé",
                rowVersion = imported.RowVersion
            });
        signResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var signed = (await signResponse.Content.ReadFromJsonAsync<ContractDto>(ApiJsonOptions))!;
        signed.Status.ShouldBe(ContractStatus.Signed);
        signed.SignedAt.ShouldBe(new DateOnly(2026, 3, 1));
        signed.RowVersion.ShouldNotBe(imported.RowVersion);

        var staleUpdate = await adminClient.PutAsJsonAsync(
            $"/api/v1/contracts/{imported.Id}",
            new
            {
                contractTypeId = cdiId,
                status = "Archived",
                signedAt = "2026-03-01",
                commentary = "Stale",
                rowVersion = imported.RowVersion
            });
        staleUpdate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using (var replaceForm = new MultipartFormDataContent())
        {
            replaceForm.Add(new StringContent(cdiId.ToString()), "contractTypeId");
            replaceForm.Add(new StringContent("Signed"), "status");
            replaceForm.Add(new StringContent("2026-03-01"), "signedAt");
            replaceForm.Add(new StringContent("Version signée finale"), "commentary");
            replaceForm.Add(new StringContent(signed.RowVersion), "rowVersion");
            var pdfContent = new ByteArrayContent(UpdatedValidPdfBytes);
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            replaceForm.Add(pdfContent, "file", "cdi-alice-signed.pdf");

            var replaceResponse = await adminClient.PutAsync($"/api/v1/contracts/{imported.Id}", replaceForm);
            replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var replaced = (await replaceResponse.Content.ReadFromJsonAsync<ContractDto>(ApiJsonOptions))!;
            replaced.FileName.ShouldBe("cdi-alice-signed.pdf");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContractsDbContext>();
            var entity = await db.Contracts.SingleAsync(c => c.Id == imported.Id);
            entity.FileKey.ShouldNotBe(firstFileKey);
            Directory.GetFiles(factory.StoragePath, Path.GetFileName(firstFileKey), SearchOption.AllDirectories)
                .ShouldBeEmpty();
            FindStoredFilePath(factory.StoragePath, entity.FileKey).ShouldNotBeNullOrWhiteSpace();
        }

        // 7. Delete removes DB record and encrypted file on disk
        (await adminClient.DeleteAsync($"/api/v1/contracts/{imported.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.GetAsync($"/api/v1/contracts/by-position/{posA.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ContractsDbContext>();
            (await db.Contracts.CountAsync()).ShouldBe(0);
            Directory.GetFiles(factory.StoragePath, "*.enc", SearchOption.AllDirectories).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task JobSheets_Should_Enforce_UniquePerPosition_AbacForManager_And_ProtectPositionDeletion()
    {
        using var factory = new ApiWebApplicationFactory();
        using var adminClient = await CreateAuthenticatedClientAsync(factory, AdminEmail, DemoPassword);

        var (companyId, locationId, _) = await GetOrganizationRefsAsync(adminClient);

        var manager1 = await CreateEmployeeAsync(adminClient, companyId, null, "M", "Direct", "ManagerOne", [IdentityRoles.Manager]);
        var manager2 = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Autre", "ManagerTwo", [IdentityRoles.Manager]);
        var emp1 = await CreateEmployeeAsync(adminClient, companyId, manager1.Id, "M", "Lucas", "EquipeUn", [IdentityRoles.Employee]);
        var emp2 = await CreateEmployeeAsync(adminClient, companyId, null, "Mme", "Nadia", "EquipeDeux", [IdentityRoles.Employee]);

        var pos1 = await CreatePositionAsync(adminClient, emp1.Id, manager1.Id, locationId, "Ingénieur DevSecOps");

        // 1. Invalid non-PDF upload rejected with 400
        using (var badForm = BuildJobSheetForm(pos1.Id, "Bad file", FakeExecutableBytes, "not-a-pdf.pdf"))
        {
            var badResponse = await adminClient.PostAsync("/api/v1/job-sheets", badForm);
            badResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // 2. Valid PDF creation returns 201 Created and encrypts on disk
        JobSheetDto created;
        using (var form = BuildJobSheetForm(pos1.Id, "Fiche initiale", MinimalValidPdfBytes, "fiche-devsecops.pdf"))
        {
            var createResponse = await adminClient.PostAsync("/api/v1/job-sheets", form);
            createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
            created = (await createResponse.Content.ReadFromJsonAsync<JobSheetDto>(ApiJsonOptions))!;
        }

        created.PositionId.ShouldBe(pos1.Id);
        created.EmployeeId.ShouldBe(emp1.Id);
        created.FileName.ShouldBe("fiche-devsecops.pdf");

        // 3. Duplicate JobSheet for same position returns 409 Conflict
        using (var dupForm = BuildJobSheetForm(pos1.Id, "Duplicate", MinimalValidPdfBytes, "fiche-dup.pdf"))
        {
            var dupResponse = await adminClient.PostAsync("/api/v1/job-sheets", dupForm);
            dupResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        }

        // 4. ABAC: Self (emp1) and Direct Manager (manager1) -> 200 OK; Manager2 and Employee2 -> 403 Forbidden
        using var emp1Client = await CreateAuthenticatedClientAsync(factory, emp1.Email, UserPassword);
        (await emp1Client.GetAsync($"/api/v1/job-sheets/by-position/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var selfFile = await emp1Client.GetAsync($"/api/v1/job-sheets/{created.Id}/file");
        selfFile.StatusCode.ShouldBe(HttpStatusCode.OK);
        selfFile.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("inline");

        using var mgr1Client = await CreateAuthenticatedClientAsync(factory, manager1.Email, UserPassword);
        (await mgr1Client.GetAsync($"/api/v1/job-sheets/by-position/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var mgrFile = await mgr1Client.GetAsync($"/api/v1/job-sheets/{created.Id}/file?download=true");
        mgrFile.StatusCode.ShouldBe(HttpStatusCode.OK);
        mgrFile.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");

        using var mgr2Client = await CreateAuthenticatedClientAsync(factory, manager2.Email, UserPassword);
        (await mgr2Client.GetAsync($"/api/v1/job-sheets/by-position/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await mgr2Client.GetAsync($"/api/v1/job-sheets/{created.Id}/file")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var emp2Client = await CreateAuthenticatedClientAsync(factory, emp2.Email, UserPassword);
        (await emp2Client.GetAsync($"/api/v1/job-sheets/by-position/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // 5. Cross-module protection: deleting Position with active JobSheet returns 409 Conflict
        (await adminClient.DeleteAsync($"/api/v1/positions/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 6. Update with stale RowVersion returns 409, valid RowVersion updates metadata & file
        var stalePut = await adminClient.PutAsJsonAsync(
            $"/api/v1/job-sheets/{created.Id}",
            new { commentary = "Stale", rowVersion = Convert.ToBase64String([9, 9, 9]) });
        stalePut.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using (var updateForm = new MultipartFormDataContent())
        {
            updateForm.Add(new StringContent("Fiche révisée"), "commentary");
            updateForm.Add(new StringContent(created.RowVersion), "rowVersion");
            var pdfContent = new ByteArrayContent(UpdatedValidPdfBytes);
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            updateForm.Add(pdfContent, "file", "fiche-devsecops-v2.pdf");

            var updateResponse = await adminClient.PutAsync($"/api/v1/job-sheets/{created.Id}", updateForm);
            updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
            var updated = (await updateResponse.Content.ReadFromJsonAsync<JobSheetDto>(ApiJsonOptions))!;
            updated.Commentary.ShouldBe("Fiche révisée");
            updated.FileName.ShouldBe("fiche-devsecops-v2.pdf");
        }

        // 7. Delete JobSheet, then deleting Position succeeds (204 NoContent)
        (await adminClient.DeleteAsync($"/api/v1/job-sheets/{created.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await adminClient.DeleteAsync($"/api/v1/positions/{pos1.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static MultipartFormDataContent BuildContractForm(
        Guid positionId,
        Guid contractTypeId,
        string status,
        string commentary,
        byte[] bytes,
        string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(positionId.ToString()), "positionId");
        form.Add(new StringContent(contractTypeId.ToString()), "contractTypeId");
        form.Add(new StringContent(status), "status");
        form.Add(new StringContent(commentary), "commentary");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", fileName);
        return form;
    }

    private static MultipartFormDataContent BuildJobSheetForm(
        Guid positionId,
        string commentary,
        byte[] bytes,
        string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(positionId.ToString()), "positionId");
        form.Add(new StringContent(commentary), "commentary");
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
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

    private static async Task<(Guid CompanyId, Guid LocationId, Guid CdiId)> GetOrganizationRefsAsync(HttpClient adminClient)
    {
        var companyId = (await adminClient.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var locationId = (await adminClient.GetFromJsonAsync<PagedResult<LocationDto>>("/api/v1/locations"))!.Items.First().Id;
        var cdiId = (await adminClient.GetFromJsonAsync<PagedResult<ContractTypeDto>>("/api/v1/contract-types"))!
            .Items.Single(ct => ct.Code == "CDI").Id;
        return (companyId, locationId, cdiId);
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

    private static async Task<PositionDto> CreatePositionAsync(
        HttpClient adminClient,
        Guid employeeId,
        Guid? managerId,
        Guid locationId,
        string jobTitle)
    {
        var response = await adminClient.PostAsJsonAsync(
            "/api/v1/positions",
            new CreatePositionRequest(
                employeeId,
                managerId,
                locationId,
                jobTitle,
                new DateOnly(2026, 3, 1)));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PositionDto>())!;
    }
}
