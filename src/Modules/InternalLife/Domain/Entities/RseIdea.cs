using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.InternalLife.Domain.Enums;

namespace Sirh.Modules.InternalLife.Domain.Entities;

public sealed class RseIdea : AuditableEntity
{
    private RseIdea()
    {
    }

    public RseIdea(
        Guid authorEmployeeId,
        Guid? locationId,
        string title,
        string ideaContent)
    {
        AuthorEmployeeId = authorEmployeeId;
        LocationId = locationId;
        Title = title.Trim();
        IdeaContent = ideaContent.Trim();
        Status = RseIdeaStatus.Submitted;
    }

    public Guid AuthorEmployeeId { get; private set; }
    public Guid? LocationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string IdeaContent { get; private set; } = string.Empty;

    public string? AttachmentFileKey { get; private set; }
    public string? AttachmentFileName { get; private set; }
    public string? AttachmentContentType { get; private set; }
    public long? AttachmentFileSizeBytes { get; private set; }

    public RseIdeaStatus Status { get; private set; }
    public string? RhComment { get; private set; }
    public Guid? TreatedByEmployeeId { get; private set; }
    public DateTime? TreatedAtUtc { get; private set; }

    public void UpdateContent(Guid? locationId, string title, string ideaContent)
    {
        LocationId = locationId;
        Title = title.Trim();
        IdeaContent = ideaContent.Trim();
    }

    public void SetAttachment(
        string fileKey,
        string fileName,
        string contentType,
        long fileSizeBytes)
    {
        AttachmentFileKey = fileKey;
        AttachmentFileName = fileName;
        AttachmentContentType = contentType;
        AttachmentFileSizeBytes = fileSizeBytes;
    }

    public void UpdateStatus(
        RseIdeaStatus status,
        string? rhComment,
        Guid? treatedByEmployeeId,
        DateTime treatedAtUtc)
    {
        Status = status;
        RhComment = string.IsNullOrWhiteSpace(rhComment) ? null : rhComment.Trim();
        TreatedByEmployeeId = treatedByEmployeeId;
        TreatedAtUtc = treatedAtUtc.Kind switch
        {
            DateTimeKind.Utc => treatedAtUtc,
            DateTimeKind.Local => treatedAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(treatedAtUtc, DateTimeKind.Utc)
        };
    }
}
