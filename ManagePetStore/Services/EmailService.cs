using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace ManagePetStore.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        var appPassword = _settings.Password.Replace(" ", string.Empty);

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.SmtpServer, _settings.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_settings.SenderEmail, appPassword)
        };

        await client.SendMailAsync(message);
    }
}
