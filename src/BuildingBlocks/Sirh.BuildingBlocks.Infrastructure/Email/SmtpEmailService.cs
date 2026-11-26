using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sirh.BuildingBlocks.Application.Abstractions;

namespace Sirh.BuildingBlocks.Infrastructure.Email;

public sealed class SmtpEmailService(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation(
                "Email delivery is disabled. Skipping email to {Recipient} with subject {Subject}.",
                message.To,
                message.Subject);
            return;
        }

        using var mailMessage = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName, Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.HtmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        mailMessage.To.Add(new MailAddress(message.To));

        if (!string.IsNullOrWhiteSpace(message.PlainTextBody))
        {
            mailMessage.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    message.PlainTextBody,
                    Encoding.UTF8,
                    MediaTypeNames.Text.Plain));
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        await client.SendMailAsync(mailMessage, cancellationToken);

        logger.LogInformation(
            "Email sent via SMTP ({Host}:{Port}) to {Recipient} with subject {Subject}.",
            settings.Host,
            settings.Port,
            message.To,
            message.Subject);
    }
}
