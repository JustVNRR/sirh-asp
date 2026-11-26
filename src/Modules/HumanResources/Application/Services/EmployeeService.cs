using FluentValidation;
using Microsoft.Extensions.Logging;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.BuildingBlocks.Application.Pagination;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.BuildingBlocks.Domain.Text;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Application.Services;

public sealed class EmployeeService(
    IEmployeeRepository employeeRepository,
    IOrganizationLookup organizationLookup,
    IIdentityUserProvisioningService identityService,
    IDataProtectorService dataProtector,
    ICurrentUser currentUser,
    IValidator<CreateEmployeeRequest> createValidator,
    IValidator<UpdateEmployeeRequest> updateValidator,
    ILogger<EmployeeService> logger) : IEmployeeService
{
    public async Task<Result<PagedResult<EmployeeSummaryDto>>> GetPagedAsync(
        EmployeeQuery query,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken = default)
    {
        var hasReadAll = HasPermission(HrPermissions.EmployeesReadAll);
        var hasReadTeam = HasPermission(HrPermissions.EmployeesReadTeam);

        if (!hasReadAll && !hasReadTeam)
        {
            return Error.Forbidden(
                "Employees.ReadForbidden",
                "You do not have permission to list employees.");
        }

        var effectiveManagerId = query.ManagerId;
        if (!hasReadAll)
        {
            if (!currentUser.EmployeeId.HasValue)
            {
                return PagedResult<EmployeeSummaryDto>.Create(
                    [],
                    0,
                    pagedRequest.NormalizedPage,
                    pagedRequest.NormalizedPageSize);
            }

            effectiveManagerId = currentUser.EmployeeId.Value;
        }

        IReadOnlyCollection<Guid>? userIdsFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            userIdsFilter = await identityService.GetUserIdsByRoleAsync(query.Role, cancellationToken);
            if (userIdsFilter.Count == 0)
            {
                return PagedResult<EmployeeSummaryDto>.Create(
                    [],
                    0,
                    pagedRequest.NormalizedPage,
                    pagedRequest.NormalizedPageSize);
            }
        }

        var filter = new EmployeeFilter(
            query.CompanyId,
            effectiveManagerId,
            query.Department,
            query.Active,
            query.InPosition,
            userIdsFilter,
            query.Search);

        var paged = await employeeRepository.GetPagedAsync(filter, pagedRequest, cancellationToken);
        var roleMap = await identityService.GetRoleCodesByUserIdsAsync(
            paged.Items.Select(e => e.UserId).ToArray(),
            cancellationToken);

        var summaries = paged.Items
            .Select(e => ToSummaryDto(e, roleMap.GetValueOrDefault(e.UserId, [])))
            .ToArray();

        return PagedResult<EmployeeSummaryDto>.Create(summaries, paged.TotalCount, paged.Page, paged.PageSize);
    }

    public async Task<Result<EmployeeDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var employee = await employeeRepository.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{id}' was not found.");
        }

        var hasReadAll = HasPermission(HrPermissions.EmployeesReadAll);
        var isSelf = (currentUser.EmployeeId == employee.Id || currentUser.UserId == employee.UserId)
                     && HasPermission(HrPermissions.EmployeesReadSelf);
        var isDirectManager = currentUser.EmployeeId.HasValue
                              && employee.DirectManagerId == currentUser.EmployeeId.Value
                              && HasPermission(HrPermissions.EmployeesReadTeam);

        if (!hasReadAll && !isSelf && !isDirectManager)
        {
            return Error.Forbidden(
                "Employees.AccessDenied",
                "You do not have permission to view this employee profile.");
        }

        var roleMap = await identityService.GetRoleCodesByUserIdsAsync([employee.UserId], cancellationToken);
        var roles = roleMap.GetValueOrDefault(employee.UserId, []);

        var includeSensitivePii = hasReadAll || isSelf;
        return ToDetailDto(employee, roles, includeSensitivePii);
    }

    public async Task<Result<EmployeeDetailDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var company = await organizationLookup.GetCompanyByIdAsync(request.CompanyId, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("Companies.NotFound", $"Company '{request.CompanyId}' was not found.");
        }

        if (request.DirectManagerId.HasValue &&
            !await employeeRepository.ExistsAsync(request.DirectManagerId.Value, cancellationToken))
        {
            return Error.NotFound("Employees.ManagerNotFound", $"Manager '{request.DirectManagerId.Value}' was not found.");
        }

        var rolesResult = await identityService.ResolveRoleCodesAsync(request.RoleCodes, cancellationToken);
        if (rolesResult.IsFailure)
        {
            return rolesResult.Error;
        }

        var escalationCheck = CheckPrivilegedRoleAccess(rolesResult.Value);
        if (escalationCheck.IsFailure)
        {
            return escalationCheck.Error;
        }

        var employeeId = Guid.CreateVersion7();
        var slug = await GenerateUniqueSlugAsync(request.FirstName, request.LastName, null, cancellationToken);
        var email = await GenerateUniqueEmailAsync(
            request.FirstName,
            request.LastName,
            company.Slug,
            null,
            null,
            cancellationToken);

        var nationalIdEncrypted = string.IsNullOrWhiteSpace(request.NationalId)
            ? null
            : dataProtector.EncryptString(request.NationalId.Trim());

        var provisionedResult = await identityService.CreateUserAsync(
            email,
            request.Password,
            employeeId,
            rolesResult.Value,
            cancellationToken);

        if (provisionedResult.IsFailure)
        {
            return provisionedResult.Error;
        }

        var normalizedHonorific = NormalizeHonorific(request.Honorific);
        var employee = new Employee(
            employeeId,
            provisionedResult.Value.UserId,
            company.Id,
            request.DirectManagerId,
            slug,
            normalizedHonorific,
            request.FirstName,
            request.LastName,
            request.BornAt,
            request.BirthPlace,
            request.BirthDepartmentCode,
            nationalIdEncrypted,
            email,
            request.Landline,
            request.Mobile,
            request.PersonalEmail,
            request.PersonalLandline,
            request.PersonalMobile,
            request.Address?.Street,
            request.Address?.PostalCode,
            request.Address?.City,
            request.Address?.AdditionalInfo,
            request.Departments);

        await employeeRepository.AddAsync(employee, cancellationToken);
        await employeeRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created employee {EmployeeId} ({Email}) in company {CompanyId}.",
            employee.Id,
            employee.Email,
            employee.CompanyId);

        return ToDetailDto(employee, provisionedResult.Value.RoleCodes, includeSensitivePii: true);
    }

    public async Task<Result<EmployeeDetailDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var employee = await employeeRepository.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{id}' was not found.");
        }

        var concurrency = HrConcurrencyHelper.ValidateRowVersion(request.RowVersion, employee.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        var currentRolesMap = await identityService.GetRoleCodesByUserIdsAsync([employee.UserId], cancellationToken);
        var existingRoles = currentRolesMap.GetValueOrDefault(employee.UserId, []);

        var existingEscalation = CheckPrivilegedRoleAccess(existingRoles);
        if (existingEscalation.IsFailure)
        {
            return existingEscalation.Error;
        }

        IReadOnlyList<string>? targetRoleCodes = null;
        if (request.RoleCodes is not null)
        {
            var resolved = await identityService.ResolveRoleCodesAsync(request.RoleCodes, cancellationToken);
            if (resolved.IsFailure)
            {
                return resolved.Error;
            }

            var requestedEscalation = CheckPrivilegedRoleAccess(resolved.Value);
            if (requestedEscalation.IsFailure)
            {
                return requestedEscalation.Error;
            }

            targetRoleCodes = resolved.Value;
        }

        if (request.DirectManagerId == id)
        {
            return Error.Validation("Employees.SelfManager", "An employee cannot be their own direct manager.");
        }

        if (request.DirectManagerId.HasValue &&
            !await employeeRepository.ExistsAsync(request.DirectManagerId.Value, cancellationToken))
        {
            return Error.NotFound("Employees.ManagerNotFound", $"Manager '{request.DirectManagerId.Value}' was not found.");
        }

        var company = await organizationLookup.GetCompanyByIdAsync(request.CompanyId, cancellationToken);
        if (company is null)
        {
            return Error.NotFound("Companies.NotFound", $"Company '{request.CompanyId}' was not found.");
        }

        var identityNeedsEmailRecalc =
            !string.Equals(employee.FirstName, request.FirstName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(employee.LastName, request.LastName.Trim(), StringComparison.OrdinalIgnoreCase) ||
            employee.CompanyId != request.CompanyId;

        var email = identityNeedsEmailRecalc
            ? await GenerateUniqueEmailAsync(
                request.FirstName,
                request.LastName,
                company.Slug,
                employee.Id,
                employee.UserId,
                cancellationToken)
            : employee.Email;

        IReadOnlyList<string> finalRoles = existingRoles;
        if (identityNeedsEmailRecalc || targetRoleCodes is not null)
        {
            var identityUpdateResult = await identityService.UpdateUserAsync(
                employee.UserId,
                email,
                targetRoleCodes,
                cancellationToken);

            if (identityUpdateResult.IsFailure)
            {
                return identityUpdateResult.Error;
            }

            finalRoles = identityUpdateResult.Value;
        }

        var nationalIdEncrypted = request.NationalId switch
        {
            null => employee.NationalIdEncrypted,
            _ when string.IsNullOrWhiteSpace(request.NationalId) => null,
            _ => dataProtector.EncryptString(request.NationalId.Trim())
        };

        employee.Update(
            request.CompanyId,
            request.DirectManagerId,
            NormalizeHonorific(request.Honorific),
            request.FirstName,
            request.LastName,
            request.BornAt,
            request.BirthPlace,
            request.BirthDepartmentCode,
            nationalIdEncrypted,
            email,
            request.Landline,
            request.Mobile,
            request.PersonalEmail,
            request.PersonalLandline,
            request.PersonalMobile,
            request.Address?.Street,
            request.Address?.PostalCode,
            request.Address?.City,
            request.Address?.AdditionalInfo,
            request.Departments);

        await employeeRepository.SaveChangesAsync(cancellationToken);

        return ToDetailDto(employee, finalRoles, includeSensitivePii: true);
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await employeeRepository.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return Result.Failure(Error.NotFound("Employees.NotFound", $"Employee '{id}' was not found."));
        }

        if (currentUser.UserId == employee.UserId)
        {
            return Result.Failure(Error.Validation("Employees.SelfDeactivation", "You cannot deactivate your own account."));
        }

        var roleMap = await identityService.GetRoleCodesByUserIdsAsync([employee.UserId], cancellationToken);
        var roles = roleMap.GetValueOrDefault(employee.UserId, []);
        var escalationCheck = CheckPrivilegedRoleAccess(roles);
        if (escalationCheck.IsFailure)
        {
            return escalationCheck;
        }

        var identityResult = await identityService.DeactivateUserAsync(
            employee.UserId,
            "Employee deactivated",
            cancellationToken);
        if (identityResult.IsFailure)
        {
            return identityResult;
        }

        employee.Deactivate();
        await employeeRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deactivated employee {EmployeeId} and revoked user {UserId} sessions.", employee.Id, employee.UserId);
        return Result.Success();
    }

    public async Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await employeeRepository.GetByIdAsync(id, cancellationToken);
        if (employee is null)
        {
            return Result.Failure(Error.NotFound("Employees.NotFound", $"Employee '{id}' was not found."));
        }

        var roleMap = await identityService.GetRoleCodesByUserIdsAsync([employee.UserId], cancellationToken);
        var roles = roleMap.GetValueOrDefault(employee.UserId, []);
        var escalationCheck = CheckPrivilegedRoleAccess(roles);
        if (escalationCheck.IsFailure)
        {
            return escalationCheck;
        }

        var identityResult = await identityService.ActivateUserAsync(employee.UserId, cancellationToken);
        if (identityResult.IsFailure)
        {
            return identityResult;
        }

        employee.Activate();
        await employeeRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Reactivated employee {EmployeeId} and user {UserId}.", employee.Id, employee.UserId);
        return Result.Success();
    }

    private Result CheckPrivilegedRoleAccess(IReadOnlyCollection<string> targetRoles)
    {
        var containsSuperAdmin = targetRoles.Any(r =>
            string.Equals(r, HrPermissions.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase));
        if (containsSuperAdmin && !IsCallerSuperAdmin())
        {
            return Result.Failure(Error.Forbidden(
                "Employees.SuperAdminProtected",
                "Only a SUPER_ADMIN user can create or modify SUPER_ADMIN accounts."));
        }

        var containsPrivileged = targetRoles.Any(r => HrPermissions.PrivilegedRoles.Contains(r));
        if (containsPrivileged && !HasPermission(HrPermissions.EmployeesManagePrivileged))
        {
            return Result.Failure(Error.Forbidden(
                "Employees.PrivilegedRoleForbidden",
                "You do not have permission to assign or manage Admin or SuperAdmin accounts."));
        }

        return Result.Success();
    }

    private bool HasPermission(string permission) =>
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private bool IsCallerSuperAdmin() =>
        currentUser.Roles.Any(r =>
            string.Equals(r, HrPermissions.RoleSuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, HrPermissions.RoleSuperAdminDisplayName, StringComparison.OrdinalIgnoreCase));

    private async Task<string> GenerateUniqueSlugAsync(
        string firstName,
        string lastName,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var baseSlug = SlugGenerator.Generate($"{firstName} {lastName}", maxLength: 90);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "employee";
        }

        var candidate = baseSlug;
        var suffix = 2;
        while (await employeeRepository.ExistsBySlugAsync(candidate, excludeId, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private async Task<string> GenerateUniqueEmailAsync(
        string firstName,
        string lastName,
        string companySlug,
        Guid? excludeEmployeeId,
        Guid? excludeUserId,
        CancellationToken cancellationToken)
    {
        var baseLocalPart = EmployeeEmailGenerator.BuildBaseLocalPart(firstName, lastName);
        var suffix = 1;

        while (true)
        {
            var candidate = EmployeeEmailGenerator.FormatEmail(baseLocalPart, companySlug, suffix);
            var existsInHr = await employeeRepository.ExistsByEmailAsync(candidate, excludeEmployeeId, cancellationToken);
            var existsInIdentity = await identityService.EmailExistsAsync(candidate, excludeUserId, cancellationToken);

            if (!existsInHr && !existsInIdentity)
            {
                return candidate;
            }

            suffix++;
        }
    }

    private static string NormalizeHonorific(string honorific)
    {
        var match = HrPermissions.HonorificValues
            .FirstOrDefault(h => string.Equals(h, honorific.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? honorific.Trim();
    }

    private static EmployeeSummaryDto ToSummaryDto(Employee e, IReadOnlyList<string> roles) =>
        new(
            e.Id,
            e.UserId,
            e.CompanyId,
            e.DirectManagerId,
            e.Slug,
            e.Honorific,
            e.FirstName,
            e.LastName,
            e.Email,
            e.Mobile,
            e.GetDepartments(),
            roles,
            e.IsActive,
            e.CurrentlyInPosition);

    private EmployeeDetailDto ToDetailDto(Employee e, IReadOnlyList<string> roles, bool includeSensitivePii)
    {
        string? nationalId = null;
        if (includeSensitivePii && !string.IsNullOrWhiteSpace(e.NationalIdEncrypted))
        {
            nationalId = dataProtector.DecryptString(e.NationalIdEncrypted);
        }

        var address = includeSensitivePii &&
                      (e.AddressStreet is not null || e.AddressPostalCode is not null ||
                       e.AddressCity is not null || e.AddressAdditionalInfo is not null)
            ? new EmployeeAddressDto(e.AddressStreet, e.AddressPostalCode, e.AddressCity, e.AddressAdditionalInfo)
            : null;

        return new EmployeeDetailDto(
            e.Id,
            e.UserId,
            e.CompanyId,
            e.DirectManagerId,
            e.Slug,
            e.Honorific,
            e.FirstName,
            e.LastName,
            includeSensitivePii ? e.BornAt : null,
            includeSensitivePii ? e.BirthPlace : null,
            includeSensitivePii ? e.BirthDepartmentCode : null,
            nationalId,
            e.Email,
            e.Landline,
            e.Mobile,
            includeSensitivePii ? e.PersonalEmail : null,
            includeSensitivePii ? e.PersonalLandline : null,
            includeSensitivePii ? e.PersonalMobile : null,
            address,
            e.GetDepartments(),
            roles,
            e.IsActive,
            e.CurrentlyInPosition,
            e.CreatedAtUtc,
            e.UpdatedAtUtc,
            Convert.ToBase64String(e.RowVersion));
    }
}
