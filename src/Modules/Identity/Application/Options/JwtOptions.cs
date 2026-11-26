namespace Sirh.Modules.Identity.Application.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinSigningKeyLength = 32;

    public string SigningKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Sirh.Api";
    public string Audience { get; set; } = "Sirh.Clients";
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
