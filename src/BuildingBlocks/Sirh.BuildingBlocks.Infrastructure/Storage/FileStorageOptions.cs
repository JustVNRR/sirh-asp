namespace Sirh.BuildingBlocks.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "Storage";
    public const string LocalProvider = "Local";
    public const string AzureBlobProvider = "AzureBlob";

    public string Provider { get; set; } = LocalProvider;
    public string LocalRootPath { get; set; } = "./storage";
    public string? AzureConnectionString { get; set; }
    public string? AzureServiceUri { get; set; }
    public string EmployeeSensitiveContainer { get; set; } = "employees-documents";
    public string CommonPublicContainer { get; set; } = "common-documents";
    public bool CreateContainersIfMissing { get; set; } = true;
    public long MaxFileSizeBytes { get; set; } = 20L * 1024 * 1024;
    public IReadOnlyCollection<string> AllowedExtensions { get; set; } =
        [".pdf", ".png", ".jpg", ".jpeg", ".webp"];
}
