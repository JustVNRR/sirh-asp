using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Domain.Entities;

public sealed class StaffRepresentative : AuditableEntity
{
    private StaffRepresentative()
    {
    }

    public StaffRepresentative(
        Guid companyId,
        Guid? employeeId,
        string firstName,
        string lastName,
        string email,
        StaffRepresentativeRole representativeRole,
        ElectoralCollege college,
        bool isHarassmentAdvisor)
    {
        Update(companyId, employeeId, firstName, lastName, email, representativeRole, college, isHarassmentAdvisor);
    }

    public Guid CompanyId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public StaffRepresentativeRole RepresentativeRole { get; private set; }
    public ElectoralCollege College { get; private set; }
    public bool IsHarassmentAdvisor { get; private set; }

    public void Update(
        Guid companyId,
        Guid? employeeId,
        string firstName,
        string lastName,
        string email,
        StaffRepresentativeRole representativeRole,
        ElectoralCollege college,
        bool isHarassmentAdvisor)
    {
        CompanyId = companyId;
        EmployeeId = employeeId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Email = email.Trim().ToLowerInvariant();
        RepresentativeRole = representativeRole;
        College = college;
        IsHarassmentAdvisor = isHarassmentAdvisor;
    }
}
