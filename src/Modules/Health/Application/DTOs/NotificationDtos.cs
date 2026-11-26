using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid RecipientEmployeeId,
    NotificationCategory Category,
    string? ReferenceEntity,
    Guid? ReferenceId,
    string Subject,
    string Content,
    bool IsRead,
    DateTime? ReadAtUtc,
    bool EmailSent,
    DateTime CreatedAtUtc);

public sealed record UnreadCountDto(int UnreadCount);

public sealed record ReminderRunSummary(
    int MedicalVisitRemindersSent,
    int DocumentExpiryAlerts30DaysSent,
    int DocumentExpiryAlerts7DaysSent,
    int NotificationsCreated,
    int EmailsSent);
