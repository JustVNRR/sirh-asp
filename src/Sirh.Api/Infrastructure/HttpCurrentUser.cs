using System.Security.Claims;
using Sirh.BuildingBlocks.Application.Abstractions;

namespace Sirh.Api.Infrastructure;

public sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ParseGuidClaim(ClaimTypes.NameIdentifier) ?? ParseGuidClaim("sub");

    public Guid? EmployeeId => ParseGuidClaim("employee_id");

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue("email");

    public IReadOnlyCollection<string> Roles => Principal?
        .Claims
        .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
        .Select(c => c.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    public IReadOnlyCollection<string> Permissions => Principal?
        .FindAll("permission")
        .Select(c => c.Value)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];

    private Guid? ParseGuidClaim(string claimType)
    {
        var raw = Principal?.FindFirstValue(claimType);
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }
}
