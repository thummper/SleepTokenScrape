using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SleepTokenWatcher.Configuration;
using SleepTokenWatcher.Detection;

namespace SleepTokenWatcher.Notifications;

/// <summary>
/// Sends alerts over SMTP (Gmail by default, using an app password).
/// Throws on failure so the worker can decline to advance its state and retry the same changes next cycle.
/// </summary>
public sealed class EmailNotifier(
    IOptions<EmailOptions> emailOptions,
    IOptions<WatcherOptions> watcherOptions,
    ILogger<EmailNotifier> logger)
{
    private readonly EmailOptions _options = emailOptions.Value;
    private readonly string _displayTimeZone = watcherOptions.Value.DisplayTimeZone;

    public async Task SendChangesAsync(StoreOptions store, StoreChanges changes, CancellationToken cancellationToken)
    {
        var builder = new ChangeEmailBuilder(store, _displayTimeZone);

        await SendAsync(
            builder.BuildSubject(changes),
            builder.BuildHtmlBody(changes),
            builder.BuildTextBody(changes),
            cancellationToken);
    }

    public async Task SendStartupTestAsync(StoreOptions store, int productCount, CancellationToken cancellationToken)
    {
        var text = $"""
                    {store.Name} watcher started successfully.

                    It is tracking {productCount} products and will email you when products are added,
                    come back in stock, or change price.
                    """;

        var html = $"""
                    <div style="font-family:-apple-system,'Segoe UI',Helvetica,Arial,sans-serif;font-size:14px;">
                      <p><strong>{WebUtility.HtmlEncode(store.Name)} watcher started successfully.</strong></p>
                      <p>Tracking {productCount} products. You will get an email when products are added,
                         come back in stock, or change price.</p>
                    </div>
                    """;

        await SendAsync($"{store.Name}: watcher started", html, text, cancellationToken);
    }

    private async Task SendAsync(string subject, string htmlBody, string textBody, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));

        foreach (var recipient in _options.ToAddresses)
        {
            message.To.Add(MailboxAddress.Parse(recipient));
        }

        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody, TextBody = textBody }.ToMessageBody();

        using var client = new SmtpClient();

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, ResolveSecurity(_options.Security), cancellationToken);

        if (_options.UseAuthentication)
        {
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Sent \"{Subject}\" to {Recipients}.", subject, string.Join(", ", _options.ToAddresses));
    }

    private static SecureSocketOptions ResolveSecurity(SmtpSecurity security) => security switch
    {
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.Auto => SecureSocketOptions.Auto,
        SmtpSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.StartTls,
    };
}
