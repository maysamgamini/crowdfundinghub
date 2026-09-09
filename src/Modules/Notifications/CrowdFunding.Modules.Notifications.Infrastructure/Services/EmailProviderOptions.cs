namespace CrowdFunding.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Binds the <c>Notifications:EmailProvider</c> configuration section (mirrors the
/// <c>Messaging:Provider</c> config-driven-implementation pattern already used for
/// <c>IMessageBus</c>). Defaults to <see cref="LoggingEmailNotificationService"/> so local
/// development and the test suite never require real SendGrid/SES credentials.
/// </summary>
public sealed class EmailProviderOptions
{
    public const string SectionName = "Notifications";

    /// <summary><c>"Logging"</c> (default) or <c>"Http"</c>.</summary>
    public string EmailProvider { get; set; } = "Logging";

    public Uri BaseUrl { get; set; } = new("https://api.sendgrid.com");

    public string? ApiKey { get; set; }
}
