using System.ComponentModel.DataAnnotations;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// HO-NTF-001 §12.2. Bound from configuration section "Notifications".
///
/// Integrator instruction (2026-09-27, post HO-CAT-001-gates merge): <c>WebBaseUrl</c> is REMOVED
/// -- the site's public web address is now the ONE shared <c>PublicSiteOptions.WebBaseUrl</c>
/// (<c>CatProfilesModule.cs</c>), used for both the cage-card QR code (US-CAT-11) and every
/// notification link this area builds (<c>NotificationLinkBuilder</c>). Validated at real host
/// startup by <c>PublicSiteStartupGuardHostedService</c>, not by this class's own DataAnnotations.
/// <c>ApiBaseUrl</c> stays a Notifications-only setting (no shared equivalent exists).
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    [Required, Url]
    public string ApiBaseUrl { get; set; } = "";

    public NotificationChannelsOptions Channels { get; set; } = new();
    public NotificationEmailOptions Email { get; set; } = new();
    public NotificationSmsOptions Sms { get; set; } = new();
    public NotificationWorkerOptions Worker { get; set; } = new();
    public RateLimitPolicyOptions Unsubscribe { get; set; } = new() { PermitLimit = 20, WindowMinutes = 1 };
    public RateLimitPolicyOptions TemplatePreview { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };
}

public sealed class NotificationChannelsOptions
{
    /// <summary>Gate fix 2026-09-27 (ARCHITECTURE LOW): §12.2 specifies
    /// <c>Channels:Sms:Sender</c>/<c>Channels:Email:Sender</c>, not a bare string directly under
    /// <c>Channels:Sms</c>/<c>Channels:Email</c> -- settled now, before a real provider (the
    /// ADR-320 adapters) needs sibling keys under each channel (an API key, a base URL, ...).</summary>
    public NotificationChannelSenderOptions Sms { get; set; } = new();
    public NotificationChannelSenderOptions Email { get; set; } = new();
}

public sealed class NotificationChannelSenderOptions
{
    /// <summary>"Development" | "Capturing" | "Unconfigured". Default is <c>Unconfigured</c> --
    /// fails safe when nothing is set (ADR-061).</summary>
    public string Sender { get; set; } = "Unconfigured";
}

public sealed class NotificationEmailOptions
{
    public string FromAddress { get; set; } = "no-reply@example.com";
    public string FromName { get; set; } = "PurrCare";
}

public sealed class NotificationSmsOptions
{
    public string SenderId { get; set; } = "PurrCare";
}

public sealed class NotificationWorkerOptions
{
    public bool Enabled { get; set; } = true;

    [Range(1, 60)]
    public int PollIntervalSeconds { get; set; } = 5;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 3600)]
    public int LeaseSeconds { get; set; } = 120;

    [Range(1, 120)]
    public int SendTimeoutSeconds { get; set; } = 10;

    [Range(1, 1440)]
    public int ReminderSweepIntervalMinutes { get; set; } = 60;
}
