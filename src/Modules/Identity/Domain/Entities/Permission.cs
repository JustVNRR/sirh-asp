using Sirh.BuildingBlocks.Domain.Primitives;

namespace Sirh.Modules.Identity.Domain.Entities;

public sealed class Permission : Entity
{
    private Permission()
    {
    }

    public Permission(string code, string module, string description)
    {
        Code = code;
        Module = module;
        Description = description;
    }

    public string Code { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
