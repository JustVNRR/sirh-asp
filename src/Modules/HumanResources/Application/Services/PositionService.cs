using FluentValidation;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Application.Services;

public sealed class PositionService(
    IPositionRepository positionRepository,
    IEmployeeRepository employeeRepository,
    IOrganizationLookup organizationLookup,
    IEnumerable<IPositionReferenceChecker> referenceCheckers,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreatePositionRequest> createValidator,
    IValidator<UpdatePositionRequest> updateValidator) : IPositionService
{
    public async Task<Result<IReadOnlyList<PositionDto>>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{employeeId}' was not found.");
        }

        var canRead = await CanReadPositionsAsync(employee, cancellationToken);
        if (!canRead)
        {
            return Error.Forbidden(
                "Positions.ReadForbidden",
                "You do not have permission to view positions for this employee.");
        }

        var positions = await positionRepository.GetByEmployeeIdAsync(employeeId, cancellationToken);
        var currentPositionId = DetermineCurrentPosition(positions, dateTimeProvider.TodayUtc)?.Id;

        IReadOnlyList<PositionDto> dtos = positions
            .OrderByDescending(p => p.StartAt)
            .ThenByDescending(p => p.CreatedAtUtc)
            .Select(p => ToDto(p, p.Id == currentPositionId))
            .ToArray();

        return Result.Success(dtos);
    }

    public async Task<Result<PositionDto>> CreateAsync(
        CreatePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var employee = await employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{request.EmployeeId}' was not found.");
        }

        if (request.ManagerId.HasValue &&
            !await employeeRepository.ExistsAsync(request.ManagerId.Value, cancellationToken))
        {
            return Error.NotFound("Employees.ManagerNotFound", $"Manager '{request.ManagerId.Value}' was not found.");
        }

        if (request.LocationId.HasValue &&
            !await organizationLookup.LocationExistsAsync(request.LocationId.Value, cancellationToken))
        {
            return Error.NotFound("Locations.NotFound", $"Location '{request.LocationId.Value}' was not found.");
        }

        var slug = await GenerateUniquePositionSlugAsync(employee.Slug, cancellationToken);
        var position = new Position(
            slug,
            request.EmployeeId,
            request.ManagerId,
            request.LocationId,
            request.JobTitle,
            request.StartAt,
            request.EndAt,
            request.ContractLabel,
            request.Coefficient,
            request.Rate,
            request.Supervisor,
            request.TimeContractType,
            request.TimeContractAmount,
            request.HierarchicalPosition,
            request.Departments);

        await positionRepository.AddAsync(position, cancellationToken);
        await positionRepository.SaveChangesAsync(cancellationToken);

        var currentPositionId = await SyncEmployeePositionStateAsync(employee, cancellationToken);
        return ToDto(position, position.Id == currentPositionId);
    }

    public async Task<Result<PositionDto>> UpdateAsync(
        Guid id,
        UpdatePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var position = await positionRepository.GetByIdAsync(id, cancellationToken);
        if (position is null)
        {
            return Error.NotFound("Positions.NotFound", $"Position '{id}' was not found.");
        }

        var concurrency = HrConcurrencyHelper.ValidateRowVersion(request.RowVersion, position.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        if (request.ManagerId == position.EmployeeId)
        {
            return Error.Validation("Positions.SelfManager", "An employee cannot be their own manager.");
        }

        if (request.ManagerId.HasValue &&
            !await employeeRepository.ExistsAsync(request.ManagerId.Value, cancellationToken))
        {
            return Error.NotFound("Employees.ManagerNotFound", $"Manager '{request.ManagerId.Value}' was not found.");
        }

        if (request.LocationId.HasValue &&
            !await organizationLookup.LocationExistsAsync(request.LocationId.Value, cancellationToken))
        {
            return Error.NotFound("Locations.NotFound", $"Location '{request.LocationId.Value}' was not found.");
        }

        position.Update(
            request.ManagerId,
            request.LocationId,
            request.JobTitle,
            request.StartAt,
            request.EndAt,
            request.ContractLabel,
            request.Coefficient,
            request.Rate,
            request.Supervisor,
            request.TimeContractType,
            request.TimeContractAmount,
            request.HierarchicalPosition,
            request.Departments);

        await positionRepository.SaveChangesAsync(cancellationToken);

        var employee = await employeeRepository.GetByIdAsync(position.EmployeeId, cancellationToken);
        Guid? currentPositionId = null;
        if (employee is not null)
        {
            currentPositionId = await SyncEmployeePositionStateAsync(employee, cancellationToken);
        }

        return ToDto(position, position.Id == currentPositionId);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var position = await positionRepository.GetByIdAsync(id, cancellationToken);
        if (position is null)
        {
            return Result.Failure(Error.NotFound("Positions.NotFound", $"Position '{id}' was not found."));
        }

        foreach (var checker in referenceCheckers)
        {
            if (await checker.IsPositionReferencedAsync(id, cancellationToken))
            {
                return Result.Failure(Error.Conflict(
                    "Positions.InUse",
                    $"Position '{id}' cannot be deleted because it is linked to an existing contract or job sheet."));
            }
        }

        var employeeId = position.EmployeeId;
        positionRepository.Remove(position);
        await positionRepository.SaveChangesAsync(cancellationToken);

        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is not null)
        {
            await SyncEmployeePositionStateAsync(employee, cancellationToken);
        }

        return Result.Success();
    }

    private async Task<bool> CanReadPositionsAsync(Employee employee, CancellationToken cancellationToken)
    {
        if (HasPermission(HrPermissions.PositionsReadAll))
        {
            return true;
        }

        var isSelf = currentUser.EmployeeId == employee.Id || currentUser.UserId == employee.UserId;
        if (isSelf && HasPermission(HrPermissions.PositionsReadSelf))
        {
            return true;
        }

        if (currentUser.EmployeeId.HasValue && HasPermission(HrPermissions.PositionsReadTeam))
        {
            if (employee.DirectManagerId == currentUser.EmployeeId.Value)
            {
                return true;
            }

            return await positionRepository.IsManagerOfEmployeeAsync(
                currentUser.EmployeeId.Value,
                employee.Id,
                cancellationToken);
        }

        return false;
    }

    private bool HasPermission(string permission) =>
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<Guid?> SyncEmployeePositionStateAsync(Employee employee, CancellationToken cancellationToken)
    {
        var positions = await positionRepository.GetByEmployeeIdAsync(employee.Id, cancellationToken);
        var current = DetermineCurrentPosition(positions, dateTimeProvider.TodayUtc);

        employee.SetCurrentlyInPosition(current is not null);
        if (current is not null)
        {
            employee.SetDirectManager(current.ManagerId);
        }

        await employeeRepository.SaveChangesAsync(cancellationToken);
        return current?.Id;
    }

    private static Position? DetermineCurrentPosition(IReadOnlyList<Position> positions, DateOnly today) =>
        positions
            .Where(p => p.IsActiveOn(today))
            .OrderByDescending(p => p.StartAt)
            .ThenByDescending(p => p.CreatedAtUtc)
            .FirstOrDefault();

    private async Task<string> GenerateUniquePositionSlugAsync(string employeeSlug, CancellationToken cancellationToken)
    {
        var baseSlug = $"poste-{employeeSlug}";
        if (baseSlug.Length > 110)
        {
            baseSlug = baseSlug[..110].TrimEnd('-');
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await positionRepository.ExistsBySlugAsync(candidate, null, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static PositionDto ToDto(Position p, bool isCurrent) =>
        new(
            p.Id,
            p.Slug,
            p.EmployeeId,
            p.ManagerId,
            p.LocationId,
            p.JobTitle,
            p.StartAt,
            p.EndAt,
            isCurrent,
            p.ContractLabel,
            p.Coefficient,
            p.Rate,
            p.Supervisor,
            p.TimeContractType,
            p.TimeContractAmount,
            p.HierarchicalPosition,
            p.GetDepartments(),
            p.CreatedAtUtc,
            p.UpdatedAtUtc,
            Convert.ToBase64String(p.RowVersion));
}
