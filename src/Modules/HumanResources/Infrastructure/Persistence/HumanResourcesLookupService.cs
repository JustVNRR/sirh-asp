using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;

namespace Sirh.Modules.HumanResources.Infrastructure.Persistence;

public sealed class HumanResourcesLookupService(
    HumanResourcesDbContext dbContext,
    IDataProtectorService dataProtector) : IHumanResourcesLookup
{
    public async Task<PositionContractContextDto?> GetPositionContractContextAsync(
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var row = await dbContext.Positions
            .AsNoTracking()
            .Include(p => p.Employee)
            .Where(p => p.Id == positionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var decryptedNationalId = string.IsNullOrWhiteSpace(row.Employee.NationalIdEncrypted)
            ? null
            : dataProtector.DecryptString(row.Employee.NationalIdEncrypted);

        return new PositionContractContextDto(
            row.Id,
            row.Slug,
            row.EmployeeId,
            row.ManagerId,
            row.Employee.CompanyId,
            row.LocationId,
            row.JobTitle,
            row.StartAt,
            row.EndAt,
            row.ContractLabel,
            row.Coefficient,
            row.Rate,
            row.Supervisor,
            row.TimeContractType,
            row.TimeContractAmount,
            row.HierarchicalPosition,
            row.Employee.FirstName,
            row.Employee.LastName,
            row.Employee.Honorific,
            row.Employee.Email,
            row.Employee.BornAt,
            row.Employee.BirthPlace,
            row.Employee.AddressStreet,
            row.Employee.AddressPostalCode,
            row.Employee.AddressCity,
            decryptedNationalId);
    }

    public async Task<bool> IsManagerOfEmployeeAsync(
        Guid managerEmployeeId,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (managerEmployeeId == Guid.Empty || employeeId == Guid.Empty)
        {
            return false;
        }

        var isDirectManager = await dbContext.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeId && e.DirectManagerId == managerEmployeeId, cancellationToken);

        if (isDirectManager)
        {
            return true;
        }

        return await dbContext.Positions
            .AsNoTracking()
            .AnyAsync(p => p.EmployeeId == employeeId && p.ManagerId == managerEmployeeId, cancellationToken);
    }

    public Task<bool> EmployeeExistsAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        dbContext.Employees
            .AsNoTracking()
            .AnyAsync(e => e.Id == employeeId, cancellationToken);
}
