namespace Sirh.Modules.HumanResources.Domain.Constants;

public static class HrPermissions
{
    public const string EmployeesReadSelf = "employees:read:self";
    public const string EmployeesReadTeam = "employees:read:team";
    public const string EmployeesReadAll = "employees:read:all";
    public const string EmployeesWrite = "employees:write";
    public const string EmployeesManagePrivileged = "employees:manage-privileged";

    public const string EmergencyContactsReadSelf = "emergency-contacts:read:self";
    public const string EmergencyContactsReadAll = "emergency-contacts:read:all";
    public const string EmergencyContactsWriteSelf = "emergency-contacts:write:self";
    public const string EmergencyContactsWriteAll = "emergency-contacts:write:all";

    public const string PositionsReadSelf = "positions:read:self";
    public const string PositionsReadTeam = "positions:read:team";
    public const string PositionsReadAll = "positions:read:all";
    public const string PositionsWrite = "positions:write";

    public const string EmployeeDocsReadSelf = "employee-docs:read:self";
    public const string EmployeeDocsReadTeam = "employee-docs:read:team";
    public const string EmployeeDocsReadAll = "employee-docs:read:all";
    public const string EmployeeDocsReadSensitive = "employee-docs:read:sensitive";
    public const string EmployeeDocsWriteSelf = "employee-docs:write:self";
    public const string EmployeeDocsWriteAll = "employee-docs:write:all";

    public static readonly IReadOnlyList<string> TimeContractTypes =
    [
        "Hours",
        "Days"
    ];

    public const string RoleEmployee = "EMPLOYEE";
    public const string RoleAdmin = "ADMIN";
    public const string RoleSuperAdmin = "SUPER_ADMIN";
    public const string RoleSuperAdminDisplayName = "Super admin";

    public static readonly IReadOnlySet<string> PrivilegedRoles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            RoleAdmin,
            RoleSuperAdmin
        };

    public static readonly IReadOnlyList<string> HonorificValues =
    [
        "M",
        "Mme",
        "Autre"
    ];
}
