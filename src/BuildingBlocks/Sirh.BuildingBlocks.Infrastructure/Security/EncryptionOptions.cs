namespace Sirh.BuildingBlocks.Infrastructure.Security;

public sealed class EncryptionOptions
{
    public const string SectionName = "Encryption";
    public const int MinMasterKeyLength = 32;

    public string MasterKey { get; set; } = string.Empty;
}
