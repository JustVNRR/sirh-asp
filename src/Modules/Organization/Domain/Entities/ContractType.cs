using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Organization.Domain.Entities;

public sealed class ContractType : AuditableEntity
{
    private ContractType()
    {
    }

    public ContractType(string code, string label, bool isSystem, bool isActive = true)
    {
        Code = NormalizeCode(code);
        Label = label.Trim();
        IsSystem = isSystem;
        IsActive = isActive;
    }

    public string Code { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }

    public void Update(string code, string label, bool isActive)
    {
        Code = NormalizeCode(code);
        Label = label.Trim();
        IsActive = isActive;
    }

    public static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
}
