namespace Sirh.BuildingBlocks.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";
    public string LocalRootPath { get; set; } = "./storage";
    public long MaxFileSizeBytes { get; set; } = 20L * 1024 * 1024;
    public IReadOnlyCollection<string> AllowedExtensions { get; set; } =
        [".pdf", ".png", ".jpg", ".jpeg", ".webp"];
}
