namespace Sirh.Modules.Contracts.Domain.Constants;

public static class ContractPermissions
{
    public const string ContractModelsRead = "contract-models:read";
    public const string ContractModelsWrite = "contract-models:write";

    public const string ContractsReadSelf = "contracts:read:self";
    public const string ContractsReadAll = "contracts:read:all";
    public const string ContractsWrite = "contracts:write";

    public const string JobSheetsReadSelf = "jobsheets:read:self";
    public const string JobSheetsReadTeam = "jobsheets:read:team";
    public const string JobSheetsReadAll = "jobsheets:read:all";
    public const string JobSheetsWrite = "jobsheets:write";
}
