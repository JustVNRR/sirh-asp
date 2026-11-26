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
using Sirh.Modules.Contracts.Domain.Constants;
using Sirh.Modules.Contracts.Domain.Enums;
using Sirh.Modules.Contracts.Infrastructure.Persistence;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.Identity.Application.DTOs;
using Sirh.Modules.Identity.Domain.Constants;
using Sirh.Modules.Organization.Application.DTOs;
using Xunit;

namespace Sirh.IntegrationTests.Contracts;

public sealed class ContractModelsAndGenerationEndpointsTests
{
    private const string AdminEmail = "admin.rh@nova-conseil.fr";
    private const string EmployeeEmail = "employe.rh@nova-conseil.fr";
    private const string DemoPassword = "SirhDemo123!";
    private const string EmployeePassword = "NewEmployee123!";

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task ContractModels_Should_Expose_Variables_Validate_Scriban_Syntax_And_Enforce_Permissions()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var empTokens = await LoginAsync(client, EmployeeEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", empTokens.AccessToken);
        (await client.GetAsync("/api/v1/contract-models/variables")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var variables = await client.GetFromJsonAsync<List<TemplateVariableDto>>("/api/v1/contract-models/variables");
        variables.ShouldNotBeNull();
        variables.Select(v => v.Key).ShouldContain("employee.full_name");
        variables.Select(v => v.Key).ShouldContain("company.siret");
        variables.Select(v => v.Key).ShouldContain("position.job_title");

        // Invalid Scriban syntax returns 400 Bad Request
        var invalidCreate = await client.PostAsJsonAsync(
            "/api/v1/contract-models",
            new CreateContractModelRequest(
                "Modèle Cassé",
                null,
                null,
                "Bonjour {{ if employee.first_name }} sans end"));
        invalidCreate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Valid model CRUD + RowVersion 409 check
        var validCreate = await client.PostAsJsonAsync(
            "/api/v1/contract-models",
            new CreateContractModelRequest(
                "Modèle Standard Syntec",
                "CDI cadre",
                null,
                "# Contrat\n\nEntre {{ company.name }} et {{ employee.full_name }}.",
                ["periode_essai"]));
        validCreate.StatusCode.ShouldBe(HttpStatusCode.Created);
        var model = await validCreate.Content.ReadFromJsonAsync<ContractModelDto>();
        model.ShouldNotBeNull();
        model.CustomVariables.ShouldBe(["periode_essai"]);

        var staleUpdate = await client.PutAsJsonAsync(
            $"/api/v1/contract-models/{model.Id}",
            new UpdateContractModelRequest(
                "Modèle Standard Syntec v2",
                null,
                null,
                "# Updated\n\n{{ employee.full_name }}",
                ["periode_essai"],
                true,
                Convert.ToBase64String([1, 2, 3, 4])));
        staleUpdate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        (await client.DeleteAsync($"/api/v1/contract-models/{model.Id}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Preview_Should_Stream_Pdf_Without_Persisting_And_Generate_Should_Encrypt_And_Protect_Position()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var adminTokens = await LoginAsync(client, AdminEmail, DemoPassword);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var companyId = (await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/v1/companies"))!.Items.First().Id;
        var locationId = (await client.GetFromJsonAsync<PagedResult<LocationDto>>("/api/v1/locations"))!.Items.First().Id;
        var contractTypes = await client.GetFromJsonAsync<PagedResult<ContractTypeDto>>("/api/v1/contract-types");
        var cdiId = contractTypes!.Items.Single(ct => ct.Code == "CDI").Id;

        var empResponse = await client.PostAsJsonAsync(
            "/api/v1/employees",
            new CreateEmployeeRequest(
                companyId,
                null,
                "Mme",
                "Camille",
                "Contrat",
                EmployeePassword,
                [IdentityRoles.Employee]));
        empResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var employee = (await empResponse.Content.ReadFromJsonAsync<EmployeeDetailDto>())!;

        var posResponse = await client.PostAsJsonAsync(
            "/api/v1/positions",
            new CreatePositionRequest(
                employee.Id,
                null,
                locationId,
                "Architecte Logiciel",
                new DateOnly(2026, 2, 1),
                Rate: 5200m,
                TimeContractType: "Days",
                TimeContractAmount: 218m,
                HierarchicalPosition: "Cadre"));
        posResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var position = (await posResponse.Content.ReadFromJsonAsync<PositionDto>())!;

        var modelResponse = await client.PostAsJsonAsync(
            "/api/v1/contract-models",
            new CreateContractModelRequest(
                "CDI Cadre Syntec",
                "Contrat standard",
                cdiId,
                """
                # CONTRAT DE TRAVAIL À DURÉE INDÉTERMINÉE

                Entre **{{ company.name }}** (SIRET {{ company.siret }}) et **{{ employee.full_name }}**.

                ## Poste occupé
                - Fonction : {{ position.job_title }} ({{ position.location_name }})
                - Date d'effet : {{ position.start_at }}
                {{ if position.rate > 0 }}- Rémunération mensuelle : {{ position.rate }} EUR{{ end }}
                - Clause spécifique : {{ custom.clause_specifique }}
                """));
        modelResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var model = (await modelResponse.Content.ReadFromJsonAsync<ContractModelDto>())!;

        // 1. Preview returns valid PDF stream without persisting anything
        var previewResponse = await client.PostAsJsonAsync(
            $"/api/v1/contract-models/{model.Id}/preview",
            new PreviewContractRequest(
                position.Id,
                new Dictionary<string, string> { ["clause_specifique"] = "Astreinte mensuelle" }));
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        previewResponse.Content.Headers.ContentType?.MediaType.ShouldBe("application/pdf");
        var previewBytes = await previewResponse.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(previewBytes.AsSpan(0, 5)).ShouldBe("%PDF-");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contractsDb = scope.ServiceProvider.GetRequiredService<ContractsDbContext>();
            (await contractsDb.Contracts.CountAsync()).ShouldBe(0);
        }

        // 2. Generate persists Contract + stores encrypted PDF via IFileStorageService
        var generateResponse = await client.PostAsJsonAsync(
            "/api/v1/contracts/generate",
            new GenerateContractRequest(
                model.Id,
                position.Id,
                cdiId,
                new Dictionary<string, string> { ["clause_specifique"] = "Clause de mobilité IDF" },
                "Généré pour signature"));
        generateResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var generated = await generateResponse.Content.ReadFromJsonAsync<ContractDto>(ApiJsonOptions);
        generated.ShouldNotBeNull();
        generated.PositionId.ShouldBe(position.Id);
        generated.EmployeeId.ShouldBe(employee.Id);
        generated.Status.ShouldBe(ContractStatus.Draft);
        generated.ContentType.ShouldBe("application/pdf");
        generated.FileSizeBytes.ShouldBeGreaterThan(500);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var contractsDb = scope.ServiceProvider.GetRequiredService<ContractsDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            var entity = await contractsDb.Contracts.SingleAsync(c => c.Id == generated.Id);
            var diskPath = Directory.GetFiles(
                    factory.StoragePath,
                    Path.GetFileName(entity.FileKey),
                    SearchOption.AllDirectories)
                .Single();
            var encryptedOnDisk = await File.ReadAllBytesAsync(diskPath);
            Encoding.ASCII.GetString(encryptedOnDisk.AsSpan(0, 5)).ShouldNotBe("%PDF-");

            await using var decryptedStream = await storage.OpenReadAsync(
                StorageScope.EmployeeSensitive,
                entity.FileKey);
            using var ms = new MemoryStream();
            await decryptedStream.CopyToAsync(ms);
            var decryptedBytes = ms.ToArray();
            Encoding.ASCII.GetString(decryptedBytes.AsSpan(0, 5)).ShouldBe("%PDF-");
        }

        // 3. Only 1 contract per position -> 2nd generate returns 409 Conflict
        var secondGenerate = await client.PostAsJsonAsync(
            "/api/v1/contracts/generate",
            new GenerateContractRequest(model.Id, position.Id, cdiId));
        secondGenerate.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // 4. Real cross-module protection: deleting Position with generated Contract returns 409 Conflict
        var deletePosition = await client.DeleteAsync($"/api/v1/positions/{position.Id}");
        deletePosition.StatusCode.ShouldBe(HttpStatusCode.Conflict);
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
