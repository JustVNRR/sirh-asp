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

    public async Task<IReadOnlyDictionary<Guid, EmployeeContactDto>> GetEmployeeContactsByIdsAsync(
        IReadOnlyCollection<Guid> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<Guid, EmployeeContactDto>();
        }

        var rows = await dbContext.Employees
            .AsNoTracking()
            .Where(e => employeeIds.Contains(e.Id))
            .Select(e => new EmployeeContactDto(
                e.Id,
                e.UserId,
                e.FirstName,
                e.LastName,
                e.Email,
                e.IsActive))
            .ToArrayAsync(cancellationToken);

        return rows.ToDictionary(r => r.EmployeeId);
    }

    public async Task<IReadOnlyList<EmployeeContactDto>> GetActiveEmployeesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Employees
            .AsNoTracking()
            .Where(e => e.IsActive && userIds.Contains(e.UserId))
            .Select(e => new EmployeeContactDto(
                e.Id,
                e.UserId,
                e.FirstName,
                e.LastName,
                e.Email,
                e.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpiringDocumentAlertDto>> GetExpiringDocumentsAsync(
        DateOnly todayUtc,
        int maxDaysAhead,
        CancellationToken cancellationToken = default)
    {
        var maxDate = todayUtc.AddDays(maxDaysAhead);

        var rqthAlerts = await dbContext.RqthDetails
            .AsNoTracking()
            .Include(r => r.Document)
            .ThenInclude(d => d.Employee)
            .Where(r =>
                r.Document.Employee.IsActive &&
                r.ExpiryDate >= todayUtc &&
                r.ExpiryDate <= maxDate &&
                (r.ExpiryReminder30SentAtUtc == null || r.ExpiryReminder7SentAtUtc == null))
            .Select(r => new ExpiringDocumentAlertDto(
                r.DocumentId,
                r.Document.EmployeeId,
                r.Document.Employee.FirstName,
                r.Document.Employee.LastName,
                r.Document.Employee.Email,
                "RqthCertificate",
                r.ExpiryDate,
                null,
                r.ExpiryReminder30SentAtUtc,
                r.ExpiryReminder7SentAtUtc))
            .ToArrayAsync(cancellationToken);

        var permitAlerts = await dbContext.ResidentPermitDetails
            .AsNoTracking()
            .Include(p => p.Document)
            .ThenInclude(d => d.Employee)
            .Where(p =>
                p.Document.Employee.IsActive &&
                p.ExpiryDate >= todayUtc &&
                p.ExpiryDate <= maxDate &&
                (p.ExpiryReminder30SentAtUtc == null || p.ExpiryReminder7SentAtUtc == null))
            .Select(p => new ExpiringDocumentAlertDto(
                p.DocumentId,
                p.Document.EmployeeId,
                p.Document.Employee.FirstName,
                p.Document.Employee.LastName,
                p.Document.Employee.Email,
                "ResidentPermit",
                p.ExpiryDate,
                p.PermitNumber,
                p.ExpiryReminder30SentAtUtc,
                p.ExpiryReminder7SentAtUtc))
            .ToArrayAsync(cancellationToken);

        return [..rqthAlerts, ..permitAlerts];
    }

    public async Task MarkDocumentExpiryReminderSentAsync(
        Guid documentId,
        string documentType,
        int thresholdDays,
        DateTime sentAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(documentType, "RqthCertificate", StringComparison.OrdinalIgnoreCase))
        {
            var rqth = await dbContext.RqthDetails.FirstOrDefaultAsync(r => r.DocumentId == documentId, cancellationToken);
            if (rqth is not null)
            {
                rqth.MarkExpiryReminderSent(thresholdDays, sentAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (string.Equals(documentType, "ResidentPermit", StringComparison.OrdinalIgnoreCase))
        {
            var permit = await dbContext.ResidentPermitDetails.FirstOrDefaultAsync(p => p.DocumentId == documentId, cancellationToken);
            if (permit is not null)
            {
                permit.MarkExpiryReminderSent(thresholdDays, sentAtUtc);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }

    public Task<Guid?> GetEmployeeCompanyIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default) =>
        dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => (Guid?)e.CompanyId)
            .FirstOrDefaultAsync(cancellationToken);
}
