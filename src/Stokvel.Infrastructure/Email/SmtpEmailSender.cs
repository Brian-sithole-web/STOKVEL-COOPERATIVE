using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stokvel.Application.Services;

namespace Stokvel.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly MailSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<MailSettings> settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var pickupDirectory = Path.Combine(Directory.GetCurrentDirectory(), "mail-pickup");
        Directory.CreateDirectory(pickupDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(pickupDirectory, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.html"),
            $"To: {toEmail}\nSubject: {subject}\n\n{htmlBody}",
            cancellationToken);

        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.UserName) || string.IsNullOrWhiteSpace(_settings.Password))
            throw new InvalidOperationException("Mail is not configured. Set Mail:UserName and Mail:Password (a Gmail app password) so invitations can be emailed.");

        using var message = new MailMessage
        {
            From = new MailAddress(string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.UserName : _settings.FromAddress, _settings.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

#pragma warning disable SYSLIB0014
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_settings.UserName, _settings.Password)
        };
#pragma warning restore SYSLIB0014

        await client.SendMailAsync(message, cancellationToken);
        _logger.LogInformation("Invitation email sent to {Email}", toEmail);
    }
}
