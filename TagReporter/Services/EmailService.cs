using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using TagReporter.Domains;

namespace TagReporter.Services;

/// <summary>
/// Service for sending emails with wireless tag report files 
/// </summary>
public class EmailService {
    private EmailSettings Settings { get; }
        
    public EmailService(IOptions<EmailSettings> settings)
    {
        Settings = settings.Value;
    }

    public void SendReport(MailboxAddress recipient, MimeEntity body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("WST Reporter", Settings.Email));
        message.To.Add(recipient);
        message.Subject = "Отчёты";
        message.Body = body;
            
        using var client = new SmtpClient();
        client.Connect(Settings.SmtpServer, Settings.SmtpServerPort, true);
        client.Authenticate(Settings.Email, Settings.Password);
            
        client.Send(message);
        client.Disconnect(true);
    }
}