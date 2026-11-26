using Microsoft.EntityFrameworkCore;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Infrastructure.Persistence;

public sealed class MedicalVisitRepository(HealthDbContext dbContext) : IMedicalVisitRepository
{
    public Task<MedicalVisit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.MedicalVisits.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<PagedResult<MedicalVisit>> GetPagedAsync(
        Guid? employeeId,
        MedicalVisitType? visitType,
        DateTime? fromDateUtc,
        DateTime? toDateUtc,
        bool? carriedOut,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MedicalVisits.AsNoTracking();

        if (employeeId.HasValue)
        {
            query = query.Where(v => v.EmployeeId == employeeId.Value);
        }

        if (visitType.HasValue)
        {
            query = query.Where(v => v.VisitType == visitType.Value);
        }

        if (fromDateUtc.HasValue)
        {
            query = query.Where(v => v.VisitDateTimeUtc >= fromDateUtc.Value);
        }

        if (toDateUtc.HasValue)
        {
            query = query.Where(v => v.VisitDateTimeUtc <= toDateUtc.Value);
        }

        if (carriedOut.HasValue)
        {
            query = query.Where(v => v.CarriedOut == carriedOut.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.VisitDateTimeUtc)
            .Skip(pagedRequest.Skip)
            .Take(pagedRequest.NormalizedPageSize)
            .ToArrayAsync(cancellationToken);

        return PagedResult<MedicalVisit>.Create(
            items,
            totalCount,
            pagedRequest.NormalizedPage,
            pagedRequest.NormalizedPageSize);
    }

    public async Task<IReadOnlyList<MedicalVisit>> GetVisitsDueForReminderAsync(
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.MedicalVisits
            .Where(v =>
                !v.CarriedOut &&
                v.ReminderSentAtUtc == null &&
                v.VisitDateTimeUtc >= windowStartUtc &&
                v.VisitDateTimeUtc <= windowEndUtc)
            .OrderBy(v => v.VisitDateTimeUtc)
            .ToArrayAsync(cancellationToken);

    public async Task AddAsync(MedicalVisit visit, CancellationToken cancellationToken = default) =>
        await dbContext.MedicalVisits.AddAsync(visit, cancellationToken);

    public void Remove(MedicalVisit visit) => dbContext.MedicalVisits.Remove(visit);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
