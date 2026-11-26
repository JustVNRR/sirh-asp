using Sirh.BuildingBlocks.Domain.Results;
using Sirh.Modules.HumanResources.Application.DTOs;

namespace Sirh.Modules.HumanResources.Application.Abstractions;

public interface IEmergencyContactService
{
    Task<Result<IReadOnlyList<EmergencyContactDto>>> GetByEmployeeIdAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmergencyContactDto>> CreateAsync(
        Guid employeeId,
        CreateEmergencyContactRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmergencyContactDto>> UpdateAsync(
        Guid id,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
