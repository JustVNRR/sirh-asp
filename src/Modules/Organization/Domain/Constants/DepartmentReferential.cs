namespace Sirh.Modules.Organization.Domain.Constants;

public sealed record DepartmentItem(string Code, string Label);

public static class DepartmentReferential
{
    public static readonly IReadOnlyList<DepartmentItem> All =
    [
        new("DEPT_HR", "Ressources Humaines"),
        new("DEPT_SALES", "Commercial"),
        new("DEPT_EMPLOYMENT_ADVISOR", "Conseiller Emploi"),
        new("DEPT_ACCOUNTING", "Comptabilité"),
        new("DEPT_PAY", "Paie"),
        new("DEPT_TRAINER", "Formateur"),
        new("DEPT_OTHER", "Autre")
    ];
}
