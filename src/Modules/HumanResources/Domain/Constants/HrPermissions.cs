namespace Sirh.Modules.HumanResources.Domain.Constants;

public static class HrPermissions
{
    public const string EmployeesReadSelf = "employees:read:self";
    public const string EmployeesReadTeam = "employees:read:team";
    public const string EmployeesReadAll = "employees:read:all";
    public const string EmployeesWrite = "employees:write";
    public const string EmployeesManagePrivileged = "employees:manage-privileged";

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
