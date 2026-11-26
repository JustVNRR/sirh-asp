using Sirh.BuildingBlocks.Domain.Primitives;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Domain.Entities;

public sealed class Notification : AuditableEntity
{
    private Notification()
    {
    }

    public Notification(
        Guid recipientEmployeeId,
        NotificationCategory category,
        string? referenceEntity,
        Guid? referenceId,
        string subject,
        string content)
    {
        RecipientEmployeeId = recipientEmployeeId;
        Category = category;
        ReferenceEntity = string.IsNullOrWhiteSpace(referenceEntity) ? null : referenceEntity.Trim();
        ReferenceId = referenceId;
        Subject = subject.Trim();
        Content = content.Trim();
        IsRead = false;
    }

    public Guid RecipientEmployeeId { get; private set; }
    public NotificationCategory Category { get; private set; }
    public string? ReferenceEntity { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
    public bool EmailSent { get; private set; }

    public void MarkAsRead(DateTime readAtUtc)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAtUtc = readAtUtc.Kind switch
        {
            DateTimeKind.Utc => readAtUtc,
            DateTimeKind.Local => readAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(readAtUtc, DateTimeKind.Utc)
        };
    }

    public void MarkEmailSent()
    {
        EmailSent = true;
    }
}
