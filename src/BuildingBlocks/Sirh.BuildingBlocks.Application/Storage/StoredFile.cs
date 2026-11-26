namespace Sirh.BuildingBlocks.Application.Storage;

public sealed record StoredFile(
    string FileKey,
    string FileName,
    string ContentType,
    long SizeBytes);
