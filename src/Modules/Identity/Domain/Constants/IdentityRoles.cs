namespace Sirh.Modules.Identity.Domain.Constants;

public sealed record RoleDefinition(string Code, string Name, int Priority, string Description);

public static class IdentityRoles
{
    public const string Unknown = "UNKNOWN";
    public const string Apprentice = "APPRENTICE";
    public const string Employee = "EMPLOYEE";
    public const string Manager = "MANAGER";
    public const string Cse = "CSE";
    public const string Hr = "HR";
    public const string Admin = "ADMIN";
    public const string SuperAdmin = "SUPER_ADMIN";

    public static readonly IReadOnlyList<RoleDefinition> All =
    [
        new(Unknown, "Inconnu", 0, "Minimal fallback role without business permissions"),
        new(Apprentice, "Apprenti", 1, "Apprentice or intern with self-service onboarding access"),
        new(Employee, "Employe", 2, "Standard employee with self-service access"),
        new(Manager, "Manager", 3, "Team manager with read access to direct reports"),
        new(Cse, "CSE", 4, "Staff representative managing CSE documents and directory"),
        new(Hr, "RH", 5, "Human resources operational management"),
        new(Admin, "Admin", 6, "Organization and access administration"),
        new(SuperAdmin, "Super admin", 7, "Full platform administration")
    ];
}
