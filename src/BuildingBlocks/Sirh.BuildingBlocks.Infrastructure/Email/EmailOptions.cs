namespace Sirh.BuildingBlocks.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string FromAddress { get; set; } = "no-reply@sirh.local";

    public string FromName { get; set; } = "SIRH Platform";

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool UseSsl { get; set; }
}
