using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.HumanResources.Domain.Entities;

public sealed class Position : AuditableEntity
{
    private Position()
    {
    }

    public Position(
        string slug,
        Guid employeeId,
        Guid? managerId,
        Guid? locationId,
        string jobTitle,
        DateOnly startAt,
        DateOnly? endAt,
        string? contractLabel,
        string? coefficient,
        decimal? rate,
        string? supervisor,
        string? timeContractType,
        decimal? timeContractAmount,
        string? hierarchicalPosition,
        IEnumerable<string>? departments)
    {
        Slug = slug.Trim();
        EmployeeId = employeeId;
        ApplyMutableFields(
            managerId,
            locationId,
            jobTitle,
            startAt,
            endAt,
            contractLabel,
            coefficient,
            rate,
            supervisor,
            timeContractType,
            timeContractAmount,
            hierarchicalPosition,
            departments);
    }

    public string Slug { get; private set; } = string.Empty;
    public Guid EmployeeId { get; private set; }
    public Employee Employee { get; private set; } = null!;

    public Guid? ManagerId { get; private set; }
    public Employee? Manager { get; init; }

    public Guid? LocationId { get; private set; }
    public string JobTitle { get; private set; } = string.Empty;
    public DateOnly StartAt { get; private set; }
    public DateOnly? EndAt { get; private set; }

    public string? ContractLabel { get; private set; }
    public string? Coefficient { get; private set; }
    public decimal? Rate { get; private set; }
    public string? Supervisor { get; private set; }
    public string? TimeContractType { get; private set; }
    public decimal? TimeContractAmount { get; private set; }
    public string? HierarchicalPosition { get; private set; }
    public string DepartmentsCsv { get; private set; } = string.Empty;

    public bool IsActiveOn(DateOnly date) =>
        StartAt <= date && (!EndAt.HasValue || EndAt.Value >= date);

    public IReadOnlyList<string> GetDepartments() =>
        string.IsNullOrWhiteSpace(DepartmentsCsv)
            ? []
            : DepartmentsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Update(
        Guid? managerId,
        Guid? locationId,
        string jobTitle,
        DateOnly startAt,
        DateOnly? endAt,
        string? contractLabel,
        string? coefficient,
        decimal? rate,
        string? supervisor,
        string? timeContractType,
        decimal? timeContractAmount,
        string? hierarchicalPosition,
        IEnumerable<string>? departments) =>
        ApplyMutableFields(
            managerId,
            locationId,
            jobTitle,
            startAt,
            endAt,
            contractLabel,
            coefficient,
            rate,
            supervisor,
            timeContractType,
            timeContractAmount,
            hierarchicalPosition,
            departments);

    private void ApplyMutableFields(
        Guid? managerId,
        Guid? locationId,
        string jobTitle,
        DateOnly startAt,
        DateOnly? endAt,
        string? contractLabel,
        string? coefficient,
        decimal? rate,
        string? supervisor,
        string? timeContractType,
        decimal? timeContractAmount,
        string? hierarchicalPosition,
        IEnumerable<string>? departments)
    {
        ManagerId = managerId;
        LocationId = locationId;
        JobTitle = jobTitle.Trim();
        StartAt = startAt;
        EndAt = endAt;
        ContractLabel = NormalizeOptional(contractLabel);
        Coefficient = NormalizeOptional(coefficient);
        Rate = rate;
        Supervisor = NormalizeOptional(supervisor);
        TimeContractType = NormalizeTimeContractType(timeContractType);
        TimeContractAmount = timeContractAmount;
        HierarchicalPosition = NormalizeOptional(hierarchicalPosition);
        DepartmentsCsv = departments is null
            ? string.Empty
            : string.Join(
                ',',
                departments
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Select(d => d.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(d => d));
    }

    private static string? NormalizeTimeContractType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "Hours", StringComparison.OrdinalIgnoreCase))
        {
            return "Hours";
        }

        return string.Equals(trimmed, "Days", StringComparison.OrdinalIgnoreCase) ? "Days" : trimmed;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
