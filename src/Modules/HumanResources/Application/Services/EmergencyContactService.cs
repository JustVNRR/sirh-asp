using FluentValidation;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.Abstractions;
using Sirh.Modules.HumanResources.Application.DTOs;
using Sirh.Modules.HumanResources.Domain.Constants;
using Sirh.Modules.HumanResources.Domain.Entities;

namespace Sirh.Modules.HumanResources.Application.Services;

public sealed class EmergencyContactService(
    IEmergencyContactRepository contactRepository,
    IEmployeeRepository employeeRepository,
    IDataProtectorService dataProtector,
    ICurrentUser currentUser,
    IValidator<CreateEmergencyContactRequest> createValidator,
    IValidator<UpdateEmergencyContactRequest> updateValidator) : IEmergencyContactService
{
    public async Task<Result<IReadOnlyList<EmergencyContactDto>>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{employeeId}' was not found.");
        }

        if (!CanReadContacts(employee))
        {
            return Error.Forbidden(
                "EmergencyContacts.ReadForbidden",
                "You do not have permission to view this employee's emergency contacts.");
        }

        var contacts = await contactRepository.GetByEmployeeIdAsync(employeeId, cancellationToken);
        IReadOnlyList<EmergencyContactDto> result = contacts.Select(ToDto).ToArray();
        return Result.Success(result);
    }

    public async Task<Result<EmergencyContactDto>> CreateAsync(
        Guid employeeId,
        CreateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var employee = await employeeRepository.GetByIdAsync(employeeId, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{employeeId}' was not found.");
        }

        if (!CanWriteContacts(employee))
        {
            return Error.Forbidden(
                "EmergencyContacts.WriteForbidden",
                "You do not have permission to manage this employee's emergency contacts.");
        }

        var existingCount = await contactRepository.CountByEmployeeIdAsync(employeeId, cancellationToken);
        if (existingCount >= EmergencyContact.MaxContactsPerEmployee)
        {
            return Error.UnprocessableEntity(
                "EmergencyContacts.MaxLimitReached",
                $"An employee cannot have more than {EmergencyContact.MaxContactsPerEmployee} emergency contacts.");
        }

        var contact = new EmergencyContact(
            employeeId,
            dataProtector.EncryptString(request.FullName.Trim()),
            dataProtector.EncryptString(request.Relationship.Trim()),
            EncryptOptional(request.MobilePhone),
            EncryptOptional(request.HomePhone),
            EncryptOptional(request.WorkPhone),
            request.PriorityOrder);

        await contactRepository.AddAsync(contact, cancellationToken);
        await contactRepository.SaveChangesAsync(cancellationToken);

        return ToDto(contact);
    }

    public async Task<Result<EmergencyContactDto>> UpdateAsync(
        Guid id,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        await updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var contact = await contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact is null)
        {
            return Error.NotFound("EmergencyContacts.NotFound", $"Emergency contact '{id}' was not found.");
        }

        var employee = await employeeRepository.GetByIdAsync(contact.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employees.NotFound", $"Employee '{contact.EmployeeId}' was not found.");
        }

        if (!CanWriteContacts(employee))
        {
            return Error.Forbidden(
                "EmergencyContacts.WriteForbidden",
                "You do not have permission to manage this employee's emergency contacts.");
        }

        var concurrency = HrConcurrencyHelper.ValidateRowVersion(request.RowVersion, contact.RowVersion);
        if (concurrency.IsFailure)
        {
            return concurrency.Error;
        }

        contact.Update(
            dataProtector.EncryptString(request.FullName.Trim()),
            dataProtector.EncryptString(request.Relationship.Trim()),
            EncryptOptional(request.MobilePhone),
            EncryptOptional(request.HomePhone),
            EncryptOptional(request.WorkPhone),
            request.PriorityOrder);

        await contactRepository.SaveChangesAsync(cancellationToken);
        return ToDto(contact);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contact = await contactRepository.GetByIdAsync(id, cancellationToken);
        if (contact is null)
        {
            return Result.Failure(Error.NotFound(
                "EmergencyContacts.NotFound",
                $"Emergency contact '{id}' was not found."));
        }

        var employee = await employeeRepository.GetByIdAsync(contact.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return Result.Failure(Error.NotFound(
                "Employees.NotFound",
                $"Employee '{contact.EmployeeId}' was not found."));
        }

        if (!CanWriteContacts(employee))
        {
            return Result.Failure(Error.Forbidden(
                "EmergencyContacts.WriteForbidden",
                "You do not have permission to delete this employee's emergency contacts."));
        }

        contactRepository.Remove(contact);
        await contactRepository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private bool CanReadContacts(Employee employee) =>
        HasPermission(HrPermissions.EmergencyContactsReadAll) ||
        (IsSelf(employee) && HasPermission(HrPermissions.EmergencyContactsReadSelf));

    private bool CanWriteContacts(Employee employee) =>
        HasPermission(HrPermissions.EmergencyContactsWriteAll) ||
        (IsSelf(employee) && HasPermission(HrPermissions.EmergencyContactsWriteSelf));

    private bool IsSelf(Employee employee) =>
        currentUser.EmployeeId == employee.Id || currentUser.UserId == employee.UserId;

    private bool HasPermission(string permission) =>
        currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private string? EncryptOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : dataProtector.EncryptString(value.Trim());

    private string? DecryptOptional(string? encrypted) =>
        string.IsNullOrWhiteSpace(encrypted) ? null : dataProtector.DecryptString(encrypted);

    private EmergencyContactDto ToDto(EmergencyContact contact) =>
        new(
            contact.Id,
            contact.EmployeeId,
            dataProtector.DecryptString(contact.FullNameEncrypted),
            dataProtector.DecryptString(contact.RelationshipEncrypted),
            DecryptOptional(contact.MobilePhoneEncrypted),
            DecryptOptional(contact.HomePhoneEncrypted),
            DecryptOptional(contact.WorkPhoneEncrypted),
            contact.PriorityOrder,
            contact.CreatedAtUtc,
            contact.UpdatedAtUtc,
            Convert.ToBase64String(contact.RowVersion));
}
