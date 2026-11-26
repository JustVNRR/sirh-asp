using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using Sirh.BuildingBlocks.Application.Abstractions;
using Sirh.BuildingBlocks.Application.Contracts;
using Sirh.Modules.Health.Application.Abstractions;
using Sirh.Modules.Health.Application.DTOs;
using Sirh.Modules.Health.Domain.Entities;
using Sirh.Modules.Health.Domain.Enums;

namespace Sirh.Modules.Health.Application.Services;

public sealed class DailyHealthRemindersJob(
    IMedicalVisitRepository visitRepository,
    INotificationRepository notificationRepository,
    IHumanResourcesLookup humanResourcesLookup,
    IIdentityUserProvisioningService identityLookup,
    IEmailService emailService,
    IDateTimeProvider dateTimeProvider,
    ILogger<DailyHealthRemindersJob> logger) : IDailyHealthRemindersJob
{
    private const int MedicalVisitLeadDays = 5;
    private const int DocumentExpiryMaxDays = 30;
    private const int DocumentExpiryUrgentDays = 7;

    public async Task<ReminderRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = dateTimeProvider.UtcNow;
        var todayUtc = dateTimeProvider.TodayUtc;
        var notificationsToCreate = new List<Notification>();

        var medicalVisitsSent = 0;
        var alerts30Sent = 0;
        var alerts7Sent = 0;
        var emailsSent = 0;

        // 1. Future medical visits in the next 5 days (never past visits)
        var dueVisits = await visitRepository.GetVisitsDueForReminderAsync(
            nowUtc,
            nowUtc.AddDays(MedicalVisitLeadDays),
            cancellationToken);

        if (dueVisits.Count > 0)
        {
            var employeeIds = dueVisits.Select(v => v.EmployeeId).Distinct().ToArray();
            var contacts = await humanResourcesLookup.GetEmployeeContactsByIdsAsync(employeeIds, cancellationToken);

            foreach (var visit in dueVisits)
            {
                if (!contacts.TryGetValue(visit.EmployeeId, out var contact) || !contact.IsActive)
                {
                    continue;
                }

                var dateLabel = visit.VisitDateTimeUtc.ToString("dd/MM/yyyy 'à' HH:mm 'UTC'", CultureInfo.InvariantCulture);
                var centerPart = string.IsNullOrWhiteSpace(visit.MedicalCenter)
                    ? string.Empty
                    : $" au centre « {visit.MedicalCenter} »";

                var subject = $"Rappel : visite médicale prévue le {visit.VisitDateTimeUtc:dd/MM/yyyy}";
                var content = $"Bonjour {contact.FirstName}, votre visite médicale ({visit.VisitType}) est planifiée le {dateLabel}{centerPart}.";

                var notification = new Notification(
                    contact.EmployeeId,
                    NotificationCategory.MedicalVisitReminder,
                    "MedicalVisit",
                    visit.Id,
                    subject,
                    content);

                if (await TrySendEmailAsync(contact.Email, subject, content, cancellationToken))
                {
                    notification.MarkEmailSent();
                    emailsSent++;
                }

                notificationsToCreate.Add(notification);
                visit.MarkReminderSent(nowUtc);
                medicalVisitsSent++;
            }
        }

        // 2. RQTH & ResidentPermit expiry alerts (<= 30 days and <= 7 days)
        var expiringDocs = await humanResourcesLookup.GetExpiringDocumentsAsync(
            todayUtc,
            DocumentExpiryMaxDays,
            cancellationToken);

        if (expiringDocs.Count > 0)
        {
            var hrUserIds = await identityLookup.GetUserIdsByRoleAsync("HR", cancellationToken);
            var hrContacts = hrUserIds.Count > 0
                ? await humanResourcesLookup.GetActiveEmployeesByUserIdsAsync(hrUserIds, cancellationToken)
                : [];

            foreach (var doc in expiringDocs)
            {
                var daysRemaining = doc.ExpiryDate.DayNumber - todayUtc.DayNumber;
                int threshold;

                if (daysRemaining <= DocumentExpiryUrgentDays && doc.ExpiryReminder7SentAtUtc is null)
                {
                    threshold = DocumentExpiryUrgentDays;
                }
                else if (daysRemaining <= DocumentExpiryMaxDays && doc.ExpiryReminder30SentAtUtc is null)
                {
                    threshold = DocumentExpiryMaxDays;
                }
                else
                {
                    continue;
                }

                var isPermit = string.Equals(doc.DocumentType, "ResidentPermit", StringComparison.OrdinalIgnoreCase);
                var docLabel = isPermit
                    ? $"Titre de séjour{(string.IsNullOrWhiteSpace(doc.ReferenceNumber) ? string.Empty : $" n° {doc.ReferenceNumber}")}"
                    : "Attestation RQTH";

                var subject = $"Alerte échéance ({threshold}j) : {docLabel} expire le {doc.ExpiryDate:dd/MM/yyyy}";
                var content = $"{docLabel} de {doc.EmployeeFirstName} {doc.EmployeeLastName} expire le {doc.ExpiryDate:dd/MM/yyyy} (J-{Math.Max(0, daysRemaining)}). Merci d'engager le renouvellement.";

                var recipients = new Dictionary<Guid, EmployeeContactDto>
                {
                    [doc.EmployeeId] = new(
                        doc.EmployeeId,
                        Guid.Empty,
                        doc.EmployeeFirstName,
                        doc.EmployeeLastName,
                        doc.EmployeeEmail,
                        true)
                };

                foreach (var hr in hrContacts)
                {
                    recipients.TryAdd(hr.EmployeeId, hr);
                }

                foreach (var recipient in recipients.Values)
                {
                    var notification = new Notification(
                        recipient.EmployeeId,
                        NotificationCategory.DocumentExpiryAlert,
                        doc.DocumentType,
                        doc.DocumentId,
                        subject,
                        content);

                    if (await TrySendEmailAsync(recipient.Email, subject, content, cancellationToken))
                    {
                        notification.MarkEmailSent();
                        emailsSent++;
                    }

                    notificationsToCreate.Add(notification);
                }

                await humanResourcesLookup.MarkDocumentExpiryReminderSentAsync(
                    doc.DocumentId,
                    doc.DocumentType,
                    threshold,
                    nowUtc,
                    cancellationToken);

                if (threshold == DocumentExpiryUrgentDays)
                {
                    alerts7Sent++;
                }
                else
                {
                    alerts30Sent++;
                }
            }
        }

        if (notificationsToCreate.Count > 0)
        {
            await notificationRepository.AddRangeAsync(notificationsToCreate, cancellationToken);
        }

        if (notificationsToCreate.Count > 0 || medicalVisitsSent > 0)
        {
            await notificationRepository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "Daily health reminders completed: {MedicalVisitsSent} visits, {Alerts30Sent} 30d docs, {Alerts7Sent} 7d docs, {NotificationsCount} notifications, {EmailsSent} emails.",
            medicalVisitsSent,
            alerts30Sent,
            alerts7Sent,
            notificationsToCreate.Count,
            emailsSent);

        return new ReminderRunSummary(
            medicalVisitsSent,
            alerts30Sent,
            alerts7Sent,
            notificationsToCreate.Count,
            emailsSent);
    }

    private async Task<bool> TrySendEmailAsync(
        string recipientEmail,
        string subject,
        string plainTextContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return false;
        }

        try
        {
            var htmlBody = $"<p>{WebUtility.HtmlEncode(plainTextContent)}</p>";
            await emailService.SendAsync(
                new EmailMessage(recipientEmail, subject, htmlBody, plainTextContent),
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send reminder email to {Email}.", recipientEmail);
            return false;
        }
    }
}
