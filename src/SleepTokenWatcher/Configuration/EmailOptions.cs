using System.ComponentModel.DataAnnotations;

namespace SleepTokenWatcher.Configuration;

/// <summary>
/// SMTP delivery settings. Defaults target Gmail with an app password.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string SmtpHost { get; set; } = "smtp.gmail.com";

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    /// <summary>Gmail address that authenticates to SMTP.</summary>
    [Required]
    [EmailAddress]
    public string Username { get; set; } = string.Empty;

    /// <summary>Google App Password (16 characters, 2FA must be enabled). Not your normal password.</summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>Envelope sender. Gmail rewrites this to the authenticated account, so leave it matching Username.</summary>
    [Required]
    [EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Merch Store Watcher";

    /// <summary>Where alerts go. Multiple recipients allowed.</summary>
    [Required]
    [MinLength(1)]
    public string[] ToAddresses { get; set; } = [];

    /// <summary>Transport security. Gmail on 587 uses StartTls; port 465 servers use SslOnConnect.</summary>
    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    /// <summary>Set false for an unauthenticated local relay. Gmail requires it.</summary>
    public bool UseAuthentication { get; set; } = true;
}

public enum SmtpSecurity
{
    /// <summary>Upgrade a plaintext connection with STARTTLS. Correct for Gmail on port 587.</summary>
    StartTls,

    /// <summary>TLS from the first byte. Correct for port 465.</summary>
    SslOnConnect,

    /// <summary>Let MailKit choose based on the port.</summary>
    Auto,

    /// <summary>No encryption. Only appropriate for a relay on localhost or a private network.</summary>
    None,
}
