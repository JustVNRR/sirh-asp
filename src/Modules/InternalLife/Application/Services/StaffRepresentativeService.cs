using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.InternalLife.Application.Abstractions;
using Sirh.Modules.InternalLife.Application.DTOs;
using Sirh.Modules.InternalLife.Domain.Entities;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Application.Services;

public sealed class StaffRepresentativeService(
    IStaffRepresentativeRepository repository,
    IOrganizationLookup organizationLookup,
    IHumanResourcesLookup humanResourcesLookup) : IStaffRepresentativeService
{
    public async Task<Result<PagedResult<StaffRepresentativeDto>>> GetPagedAsync(
        Guid? companyId,
        StaffRepresentativeRole? role,
        bool? harassmentAdvisor,
        string? search,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var page = await repository.GetPagedAsync(
            companyId,
            role,
            harassmentAdvisor,
            search,
            pagedRequest,
            cancellationToken);

        var mapped = page.Items.Select(MapToDto).ToArray();
        return PagedResult<StaffRepresentativeDto>.Create(mapped, page.TotalCount, page.Page, page.PageSize);
    }

    public async Task<Result<StaffRepresentativeDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var representative = await repository.GetByIdAsync(id, cancellationToken);
        if (representative is null)
        {
            return Error.NotFound("StaffRepresentatives.NotFound", $"Staff representative '{id}' was not found.");
        }

        return MapToDto(representative);
    }

    public async Task<Result<StaffRepresentativeDto>> CreateAsync(
        CreateStaffRepresentativeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            request.CompanyId,
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.RepresentativeRole,
            request.College,
            excludeId: null,
            cancellationToken);

        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var representative = new StaffRepresentative(
            request.CompanyId,
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.RepresentativeRole,
            request.College,
            request.IsHarassmentAdvisor);

        await repository.AddAsync(representative, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return MapToDto(representative);
    }

    public async Task<Result<StaffRepresentativeDto>> UpdateAsync(
        Guid id,
        UpdateStaffRepresentativeRequest request,
        CancellationToken cancellationToken = default)
    {
        var representative = await repository.GetByIdAsync(id, cancellationToken);
        if (representative is null)
        {
            return Error.NotFound("StaffRepresentatives.NotFound", $"Staff representative '{id}' was not found.");
        }

        var rowVersionResult = InternalLifeConcurrencyHelper.ValidateRowVersion(request.RowVersion, representative.RowVersion);
        if (rowVersionResult.IsFailure)
        {
            return rowVersionResult.Error;
        }

        var validation = await ValidateAsync(
            request.CompanyId,
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.RepresentativeRole,
            request.College,
            excludeId: id,
            cancellationToken);

        if (validation.IsFailure)
        {
            return validation.Error;
        }

        representative.Update(
            request.CompanyId,
            request.EmployeeId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.RepresentativeRole,
            request.College,
            request.IsHarassmentAdvisor);

        await repository.SaveChangesAsync(cancellationToken);
        return MapToDto(representative);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var representative = await repository.GetByIdAsync(id, cancellationToken);
        if (representative is null)
        {
            return Result.Failure(Error.NotFound(
                "StaffRepresentatives.NotFound",
                $"Staff representative '{id}' was not found."));
        }

        repository.Remove(representative);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> ValidateAsync(
        Guid companyId,
        Guid? employeeId,
        string firstName,
        string lastName,
        string email,
        StaffRepresentativeRole role,
        ElectoralCollege college,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        if (companyId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("StaffRepresentatives.CompanyRequired", "CompanyId is required."));
        }

        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
        {
            return Result.Failure(Error.Validation("StaffRepresentatives.NameRequired", "FirstName and LastName are required."));
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return Result.Failure(Error.Validation("StaffRepresentatives.InvalidEmail", "A valid email address is required."));
        }

        if (!Enum.IsDefined(role))
        {
            return Result.Failure(Error.Validation("StaffRepresentatives.InvalidRole", $"Unsupported role '{role}'."));
        }

        if (!Enum.IsDefined(college))
        {
            return Result.Failure(Error.Validation("StaffRepresentatives.InvalidCollege", $"Unsupported college '{college}'."));
        }

        if (await organizationLookup.GetCompanyByIdAsync(companyId, cancellationToken) is null)
        {
            return Result.Failure(Error.NotFound("Companies.NotFound", $"Company '{companyId}' was not found."));
        }

        if (employeeId.HasValue && !await humanResourcesLookup.EmployeeExistsAsync(employeeId.Value, cancellationToken))
        {
            return Result.Failure(Error.NotFound("Employees.NotFound", $"Employee '{employeeId.Value}' was not found."));
        }

        if (await repository.ExistsByEmailAsync(companyId, email, excludeId, cancellationToken))
        {
            return Result.Failure(Error.Conflict(
                "StaffRepresentatives.DuplicateEmail",
                $"A staff representative with email '{email.Trim()}' already exists in this company."));
        }

        if (employeeId.HasValue &&
            await repository.ExistsByEmployeeIdAsync(companyId, employeeId.Value, excludeId, cancellationToken))
        {
            return Result.Failure(Error.Conflict(
                "StaffRepresentatives.DuplicateEmployee",
                $"Employee '{employeeId.Value}' is already registered as a staff representative in this company."));
        }

        return Result.Success();
    }

    private static StaffRepresentativeDto MapToDto(StaffRepresentative representative) =>
        new(
            representative.Id,
            representative.CompanyId,
            representative.EmployeeId,
            representative.FirstName,
            representative.LastName,
            representative.Email,
            representative.RepresentativeRole,
            representative.College,
            representative.IsHarassmentAdvisor,
            representative.CreatedAtUtc,
            representative.UpdatedAtUtc,
            Convert.ToBase64String(representative.RowVersion));
}
