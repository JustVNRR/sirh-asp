using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sirh.Modules.Organization.Domain.Entities;

namespace Sirh.Modules.Organization.Infrastructure.Persistence;

public sealed class OrganizationDbSeeder(
    OrganizationDbContext dbContext,
    IConfiguration configuration,
    ILogger<OrganizationDbSeeder> logger)
{
    private static readonly (string Code, string Label)[] SystemContractTypes =
    [
        ("CDI", "Contrat à Durée Indéterminée"),
        ("CDD", "Contrat à Durée Déterminée"),
        ("APPRENTISSAGE", "Contrat d'Apprentissage"),
        ("CONTRAT_PRO", "Contrat de Professionnalisation")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedSystemContractTypesAsync(cancellationToken);

        if (configuration.GetValue<bool>("Organization:SeedDemoData"))
        {
            await SeedDemoOrganizationDataAsync(cancellationToken);
        }
    }

    private async Task SeedSystemContractTypesAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.ContractTypes
            .Select(ct => ct.Code)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, cancellationToken);

        var toAdd = SystemContractTypes
            .Where(t => !existingCodes.Contains(t.Code))
            .Select(t => new ContractType(t.Code, t.Label, isSystem: true, isActive: true))
            .ToList();

        if (toAdd.Count > 0)
        {
            await dbContext.ContractTypes.AddRangeAsync(toAdd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} system contract types.", toAdd.Count);
        }
    }

    private async Task SeedDemoOrganizationDataAsync(CancellationToken cancellationToken)
    {
        const string demoSiret = "73282932000074";
        var company = await dbContext.Companies
            .FirstOrDefaultAsync(c => c.Siret == demoSiret, cancellationToken);

        if (company is null)
        {
            company = new Company(
                name: "Nova Conseil",
                slug: "nova-conseil",
                legalForm: "SAS",
                address: "15 Rue de la République",
                postalCode: "75001",
                city: "Paris",
                country: "France",
                siret: demoSiret,
                register: "RCS Paris B 732 829 320",
                capital: "50 000 EUR",
                collectiveAgreement: Company.DefaultCollectiveAgreement,
                owner: "Marie Laurent",
                logoUrl: null);

            await dbContext.Companies.AddAsync(company, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo company {CompanyName} ({Siret}).", company.Name, company.Siret);
        }

        const string demoLocationSlug = "siege-paris";
        if (!await dbContext.Locations.AnyAsync(l => l.Slug == demoLocationSlug, cancellationToken))
        {
            var location = new Location(
                slug: demoLocationSlug,
                name: "Siège Paris",
                address: "15 Rue de la République",
                postalCode: "75001",
                city: "Paris",
                country: "France",
                departmentCode: "DEPT_HR",
                siret: demoSiret,
                colorCode: "#2563EB",
                isPublished: true,
                companyId: company.Id);

            await dbContext.Locations.AddAsync(location, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo location {LocationName}.", location.Name);
        }
    }
}
