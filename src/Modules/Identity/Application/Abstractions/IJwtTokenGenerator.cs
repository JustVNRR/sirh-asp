using Sirh.Modules.Identity.Domain.Entities;

namespace Sirh.Modules.Identity.Application.Abstractions;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateAccessToken(
        ApplicationUser user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions);

    string GenerateOpaqueRefreshToken();

    string HashToken(string rawToken);
}
