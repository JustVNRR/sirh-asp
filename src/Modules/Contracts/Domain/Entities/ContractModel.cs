using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Contracts.Domain.Entities;

public sealed class ContractModel : AuditableEntity
{
    private ContractModel()
    {
    }

    public ContractModel(
        string title,
        string? description,
        Guid? contractTypeId,
        string templateContent,
        string? customVariablesJson,
        bool isActive = true)
    {
        ApplyFields(title, description, contractTypeId, templateContent, customVariablesJson, isActive);
    }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ContractTypeId { get; private set; }
    public string TemplateContent { get; private set; } = string.Empty;
    public string? CustomVariablesJson { get; private set; }
    public bool IsActive { get; private set; }

    public ICollection<Contract> Contracts { get; private set; } = [];

    public void Update(
        string title,
        string? description,
        Guid? contractTypeId,
        string templateContent,
        string? customVariablesJson,
        bool isActive) =>
        ApplyFields(title, description, contractTypeId, templateContent, customVariablesJson, isActive);

    private void ApplyFields(
        string title,
        string? description,
        Guid? contractTypeId,
        string templateContent,
        string? customVariablesJson,
        bool isActive)
    {
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ContractTypeId = contractTypeId;
        TemplateContent = templateContent;
        CustomVariablesJson = string.IsNullOrWhiteSpace(customVariablesJson) ? null : customVariablesJson.Trim();
        IsActive = isActive;
    }
}
