using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class HumanResourcesDbSeeder(
    HumanResourcesDbContext dbContext,
    IOrganizationLookup organizationLookup,
    IIdentityUserProvisioningService identityProvisioning,
    IConfiguration configuration,
    ILogger<HumanResourcesDbSeeder> logger)
{
    private const string DemoCompanySlug = "nova-conseil";
    private const string DemoLocationSlug = "siege-paris";

    private sealed record DemoEmployeeSpec(
        string Email,
        string Slug,
        string Honorific,
        string FirstName,
        string LastName,
        string DepartmentCode,
        string JobTitle,
        string TimeContractType,
        decimal TimeContractAmount,
        string? ManagerEmail);

    private static readonly DemoEmployeeSpec[] DemoEmployees =
    [
        new(
            Email: "admin.rh@nova-conseil.fr",
            Slug: "marie-laurent",
            Honorific: "Mme",
            FirstName: "Marie",
            LastName: "Laurent",
            DepartmentCode: "DEPT_HR",
            JobTitle: "Directrice des Ressources Humaines",
            TimeContractType: "Hours",
            TimeContractAmount: 35m,
            ManagerEmail: null),
        new(
            Email: "manager.rh@nova-conseil.fr",
            Slug: "julien-moreau",
            Honorific: "M",
            FirstName: "Julien",
            LastName: "Moreau",
            DepartmentCode: "DEPT_IT",
            JobTitle: "Engineering Manager",
            TimeContractType: "Days",
            TimeContractAmount: 218m,
            ManagerEmail: "admin.rh@nova-conseil.fr"),
        new(
            Email: "employe.rh@nova-conseil.fr",
            Slug: "camille-dubois",
            Honorific: "Mme",
            FirstName: "Camille",
            LastName: "Dubois",
            DepartmentCode: "DEPT_IT",
            JobTitle: "Ingénieure Logiciel .NET",
            TimeContractType: "Hours",
            TimeContractAmount: 35m,
            ManagerEmail: "manager.rh@nova-conseil.fr")
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("HumanResources:SeedDemoData"))
        {
            return;
        }

        var companyId = await organizationLookup.GetCompanyIdBySlugAsync(DemoCompanySlug, cancellationToken);
        if (!companyId.HasValue)
        {
            return;
        }

        var locationId = await organizationLookup.GetLocationIdBySlugAsync(DemoLocationSlug, cancellationToken);
        var seededEmployeeIdsByEmail = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var createdCount = 0;

        foreach (var spec in DemoEmployees)
        {
            var userId = await identityProvisioning.GetUserIdByEmailAsync(spec.Email, cancellationToken);
            if (!userId.HasValue)
            {
                continue;
            }

            var existing = await dbContext.Employees
                .FirstOrDefaultAsync(
                    e => e.UserId == userId.Value || e.Email == spec.Email,
                    cancellationToken);

            if (existing is not null)
            {
                seededEmployeeIdsByEmail[spec.Email] = existing.Id;
                await identityProvisioning.LinkEmployeeAsync(userId.Value, existing.Id, cancellationToken);
                continue;
            }

            Guid? directManagerId = spec.ManagerEmail is not null &&
                                    seededEmployeeIdsByEmail.TryGetValue(spec.ManagerEmail, out var resolvedManagerId)
                ? resolvedManagerId
                : null;

            var employee = new Employee(
                Guid.CreateVersion7(),
                userId.Value,
                companyId.Value,
                directManagerId,
                spec.Slug,
                spec.Honorific,
                spec.FirstName,
                spec.LastName,
                bornAt: null,
                birthPlace: null,
                birthDepartmentCode: null,
                nationalIdEncrypted: null,
                email: spec.Email,
                landline: null,
                mobile: null,
                personalEmail: null,
                personalLandline: null,
                personalMobile: null,
                addressStreet: null,
                addressPostalCode: null,
                addressCity: null,
                addressAdditionalInfo: null,
                departments: [spec.DepartmentCode]);

            employee.SetCurrentlyInPosition(true);

            var position = new Position(
                slug: $"poste-{spec.Slug}",
                employeeId: employee.Id,
                managerId: directManagerId,
                locationId: locationId,
                jobTitle: spec.JobTitle,
                startAt: new DateOnly(2025, 1, 6),
                endAt: null,
                contractLabel: "CDI",
                coefficient: null,
                rate: null,
                supervisor: null,
                timeContractType: spec.TimeContractType,
                timeContractAmount: spec.TimeContractAmount,
                hierarchicalPosition: null,
                departments: [spec.DepartmentCode]);

            await dbContext.Employees.AddAsync(employee, cancellationToken);
            await dbContext.Positions.AddAsync(position, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            await identityProvisioning.LinkEmployeeAsync(userId.Value, employee.Id, cancellationToken);
            seededEmployeeIdsByEmail[spec.Email] = employee.Id;
            createdCount++;
        }

        if (createdCount > 0)
        {
            logger.LogInformation("Seeded {Count} demo employees.", createdCount);
        }
    }
}
