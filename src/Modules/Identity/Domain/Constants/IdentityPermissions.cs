namespace Sirh.Modules.Identity.Domain.Constants;

public sealed record PermissionDefinition(string Code, string Module, string Description);

public static class IdentityPermissions
{
    public const string RolesRead = "roles:read";
    public const string RolesManagePermissions = "roles:manage-permissions";

    public const string CompaniesRead = "companies:read";
    public const string CompaniesWrite = "companies:write";
    public const string LocationsRead = "locations:read";
    public const string LocationsWrite = "locations:write";
    public const string ContractTypesRead = "contract-types:read";
    public const string ContractTypesWrite = "contract-types:write";

    public const string EmployeesReadSelf = "employees:read:self";
    public const string EmployeesReadTeam = "employees:read:team";
    public const string EmployeesReadAll = "employees:read:all";
    public const string EmployeesWrite = "employees:write";
    public const string EmployeesManagePrivileged = "employees:manage-privileged";

    public const string PositionsReadSelf = "positions:read:self";
    public const string PositionsReadTeam = "positions:read:team";
    public const string PositionsReadAll = "positions:read:all";
    public const string PositionsWrite = "positions:write";

    public const string EmergencyContactsReadSelf = "emergency-contacts:read:self";
    public const string EmergencyContactsReadAll = "emergency-contacts:read:all";
    public const string EmergencyContactsWriteSelf = "emergency-contacts:write:self";
    public const string EmergencyContactsWriteAll = "emergency-contacts:write:all";

    public const string ContractModelsRead = "contract-models:read";
    public const string ContractModelsWrite = "contract-models:write";
    public const string ContractsReadSelf = "contracts:read:self";
    public const string ContractsReadAll = "contracts:read:all";
    public const string ContractsWrite = "contracts:write";
    public const string JobSheetsReadSelf = "jobsheets:read:self";
    public const string JobSheetsReadTeam = "jobsheets:read:team";
    public const string JobSheetsReadAll = "jobsheets:read:all";
    public const string JobSheetsWrite = "jobsheets:write";

    public const string EmployeeDocsReadSelf = "employee-docs:read:self";
    public const string EmployeeDocsReadTeam = "employee-docs:read:team";
    public const string EmployeeDocsReadAll = "employee-docs:read:all";
    public const string EmployeeDocsReadSensitive = "employee-docs:read:sensitive";
    public const string EmployeeDocsWriteSelf = "employee-docs:write:self";
    public const string EmployeeDocsWriteAll = "employee-docs:write:all";

    public const string MedicalVisitsReadSelf = "medical-visits:read:self";
    public const string MedicalVisitsReadAll = "medical-visits:read:all";
    public const string MedicalVisitsWrite = "medical-visits:write";
    public const string NotificationsReadSelf = "notifications:read:self";
    public const string NotificationsWriteSelf = "notifications:write:self";

    public const string CommonDocsRead = "common-docs:read";
    public const string CommonDocsWrite = "common-docs:write";
    public const string CommonDocsWriteCse = "common-docs:write:cse";
    public const string WelcomeKitsRead = "welcome-kits:read";
    public const string WelcomeKitsWrite = "welcome-kits:write";
    public const string StaffRepsRead = "staff-reps:read";
    public const string StaffRepsWrite = "staff-reps:write";
    public const string RseReadSelf = "rse:read:self";
    public const string RseReadAll = "rse:read:all";
    public const string RseWrite = "rse:write";
    public const string RseTreat = "rse:treat";
    public const string QuizzesRead = "quizzes:read";
    public const string QuizzesWrite = "quizzes:write";
    public const string QuizzesTake = "quizzes:take";
    public const string QuizzesResultsAll = "quizzes:results:all";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(RolesRead, "Identity", "Read roles and permissions"),
        new(RolesManagePermissions, "Identity", "Manage permissions assigned to roles"),
        new(CompaniesRead, "Organization", "Read companies"),
        new(CompaniesWrite, "Organization", "Create, update and deactivate companies"),
        new(LocationsRead, "Organization", "Read locations"),
        new(LocationsWrite, "Organization", "Create, update and delete locations"),
        new(ContractTypesRead, "Organization", "Read contract types"),
        new(ContractTypesWrite, "Organization", "Create, update and delete contract types"),
        new(EmployeesReadSelf, "HumanResources", "Read own employee profile"),
        new(EmployeesReadTeam, "HumanResources", "Read direct reports profiles"),
        new(EmployeesReadAll, "HumanResources", "Read all employee profiles"),
        new(EmployeesWrite, "HumanResources", "Create, update and deactivate employees"),
        new(EmployeesManagePrivileged, "HumanResources", "Manage Admin and SuperAdmin accounts"),
        new(PositionsReadSelf, "HumanResources", "Read own positions"),
        new(PositionsReadTeam, "HumanResources", "Read direct reports positions"),
        new(PositionsReadAll, "HumanResources", "Read all positions"),
        new(PositionsWrite, "HumanResources", "Create, update and delete positions"),
        new(EmergencyContactsReadSelf, "HumanResources", "Read own emergency contacts"),
        new(EmergencyContactsReadAll, "HumanResources", "Read all emergency contacts"),
        new(EmergencyContactsWriteSelf, "HumanResources", "Manage own emergency contacts"),
        new(EmergencyContactsWriteAll, "HumanResources", "Manage all emergency contacts"),
        new(ContractModelsRead, "Contracts", "Read contract models"),
        new(ContractModelsWrite, "Contracts", "Create, update and delete contract models"),
        new(ContractsReadSelf, "Contracts", "Read own contracts"),
        new(ContractsReadAll, "Contracts", "Read all contracts"),
        new(ContractsWrite, "Contracts", "Generate, upload, update and delete contracts"),
        new(JobSheetsReadSelf, "Contracts", "Read own job sheets"),
        new(JobSheetsReadTeam, "Contracts", "Read direct reports job sheets"),
        new(JobSheetsReadAll, "Contracts", "Read all job sheets"),
        new(JobSheetsWrite, "Contracts", "Create, update and delete job sheets"),
        new(EmployeeDocsReadSelf, "HumanResources", "Read own non-sensitive employee documents"),
        new(EmployeeDocsReadTeam, "HumanResources", "Read limited team documents"),
        new(EmployeeDocsReadAll, "HumanResources", "Read all employee documents"),
        new(EmployeeDocsReadSensitive, "HumanResources", "Read disciplinary and departure documents"),
        new(EmployeeDocsWriteSelf, "HumanResources", "Upload allowed self documents"),
        new(EmployeeDocsWriteAll, "HumanResources", "Manage all employee documents"),
        new(MedicalVisitsReadSelf, "Health", "Read own medical visits"),
        new(MedicalVisitsReadAll, "Health", "Read all medical visits"),
        new(MedicalVisitsWrite, "Health", "Create, update and delete medical visits"),
        new(NotificationsReadSelf, "Health", "Read own notifications"),
        new(NotificationsWriteSelf, "Health", "Mark read and delete own notifications"),
        new(CommonDocsRead, "InternalLife", "Read common documents"),
        new(CommonDocsWrite, "InternalLife", "Manage all common documents"),
        new(CommonDocsWriteCse, "InternalLife", "Manage CSE common documents"),
        new(WelcomeKitsRead, "InternalLife", "Read welcome kits"),
        new(WelcomeKitsWrite, "InternalLife", "Manage welcome kits"),
        new(StaffRepsRead, "InternalLife", "Read staff representatives directory"),
        new(StaffRepsWrite, "InternalLife", "Manage staff representatives directory"),
        new(RseReadSelf, "InternalLife", "Read own RSE ideas"),
        new(RseReadAll, "InternalLife", "Read all RSE ideas"),
        new(RseWrite, "InternalLife", "Submit and edit own RSE ideas"),
        new(RseTreat, "InternalLife", "Review and change RSE idea status"),
        new(QuizzesRead, "InternalLife", "Read onboarding quizzes"),
        new(QuizzesWrite, "InternalLife", "Create, edit and publish onboarding quizzes"),
        new(QuizzesTake, "InternalLife", "Take onboarding quizzes"),
        new(QuizzesResultsAll, "InternalLife", "View all onboarding quiz results")
    ];

    private static readonly string[] SelfServiceBase =
    [
        CompaniesRead, LocationsRead, ContractTypesRead,
        EmployeesReadSelf, PositionsReadSelf,
        EmergencyContactsReadSelf, EmergencyContactsWriteSelf,
        ContractsReadSelf, JobSheetsReadSelf,
        EmployeeDocsReadSelf, EmployeeDocsWriteSelf,
        MedicalVisitsReadSelf, NotificationsReadSelf, NotificationsWriteSelf,
        CommonDocsRead, WelcomeKitsRead, StaffRepsRead,
        RseReadSelf, RseWrite, QuizzesRead, QuizzesTake
    ];

    private static readonly string[] HrFull =
    [
        ..SelfServiceBase,
        RolesRead,
        LocationsWrite, ContractTypesWrite,
        EmployeesReadTeam, EmployeesReadAll, EmployeesWrite,
        PositionsReadTeam, PositionsReadAll, PositionsWrite,
        EmergencyContactsReadAll, EmergencyContactsWriteAll,
        ContractModelsRead, ContractModelsWrite,
        ContractsReadAll, ContractsWrite,
        JobSheetsReadTeam, JobSheetsReadAll, JobSheetsWrite,
        EmployeeDocsReadTeam, EmployeeDocsReadAll, EmployeeDocsReadSensitive, EmployeeDocsWriteAll,
        MedicalVisitsReadAll, MedicalVisitsWrite,
        CommonDocsWrite, CommonDocsWriteCse, WelcomeKitsWrite,
        StaffRepsWrite, RseReadAll, RseTreat,
        QuizzesWrite, QuizzesResultsAll
    ];

    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> DefaultRoleMappings { get; } =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [IdentityRoles.Unknown] = [],
            [IdentityRoles.Apprentice] = SelfServiceBase,
            [IdentityRoles.Employee] = SelfServiceBase,
            [IdentityRoles.Manager] =
            [
                ..SelfServiceBase,
                EmployeesReadTeam, PositionsReadTeam, JobSheetsReadTeam, EmployeeDocsReadTeam
            ],
            [IdentityRoles.Cse] =
            [
                ..SelfServiceBase,
                CommonDocsWriteCse, StaffRepsWrite
            ],
            [IdentityRoles.Hr] = HrFull,
            [IdentityRoles.Admin] =
            [
                ..HrFull,
                RolesManagePermissions, CompaniesWrite, EmployeesManagePrivileged
            ],
            [IdentityRoles.SuperAdmin] = All.Select(p => p.Code).ToArray()
        };
}
