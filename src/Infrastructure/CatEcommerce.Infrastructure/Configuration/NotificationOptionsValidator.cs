using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// HO-NTF-001 §12.2/§8.9, U-B16 -- environment-dependent rules <c>DataAnnotations</c> cannot
/// express: <c>ApiBaseUrl</c> must be https and not a placeholder host outside Development/Testing;
/// the Development sender may only be selected in Development and the Capturing sender only in
/// Testing (both senders' own constructors also refuse to build outside their environment -- this
/// is the earlier, clearer failure at host start); the claim lease must outlive the send timeout.
///
/// <c>WebBaseUrl</c> is no longer this area's own setting -- it moved to the shared
/// <c>PublicSiteOptions.WebBaseUrl</c>, validated by <c>PublicSiteStartupGuardHostedService</c>
/// (integrator instruction, 2026-09-27, post HO-CAT-001-gates merge).
/// </summary>
public sealed class NotificationOptionsValidator(IHostEnvironment environment) : IValidateOptions<NotificationOptions>
{
    /// <summary>Gate fix 2026-09-27 (SECURITY/ARCHITECTURE LOW). RFC 2606 reserved TLDs/host used
    /// as committed placeholders (<c>appsettings.json</c>) or test defaults
    /// (<c>appsettings.Testing.json</c>) -- legitimate there, and outside Development/Testing they
    /// cannot resolve, so a deployment that forgets to set the real base URL would otherwise start
    /// cleanly and silently send dead links.</summary>
    private static readonly string[] PlaceholderHostSuffixes = [".invalid", ".example", ".test", "localhost"];

    public ValidateOptionsResult Validate(string? name, NotificationOptions options)
    {
        var failures = new List<string>();
        var isDevelopmentOrTesting = environment.IsDevelopment() || environment.EnvironmentName == "Testing";

        if (!environment.IsDevelopment())
        {
            if (!options.ApiBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                failures.Add("Notifications:ApiBaseUrl must be https outside Development.");
        }

        if (!isDevelopmentOrTesting)
        {
            CheckNotPlaceholderHost(options.ApiBaseUrl, "Notifications:ApiBaseUrl", failures);
        }

        if (!environment.IsDevelopment())
        {
            if (string.Equals(options.Channels.Sms.Sender, "Development", StringComparison.OrdinalIgnoreCase))
                failures.Add("Notifications:Channels:Sms:Sender cannot be 'Development' outside Development.");
            if (string.Equals(options.Channels.Email.Sender, "Development", StringComparison.OrdinalIgnoreCase))
                failures.Add("Notifications:Channels:Email:Sender cannot be 'Development' outside Development.");
        }

        if (environment.EnvironmentName != "Testing")
        {
            if (string.Equals(options.Channels.Sms.Sender, "Capturing", StringComparison.OrdinalIgnoreCase))
                failures.Add("Notifications:Channels:Sms:Sender cannot be 'Capturing' outside Testing.");
            if (string.Equals(options.Channels.Email.Sender, "Capturing", StringComparison.OrdinalIgnoreCase))
                failures.Add("Notifications:Channels:Email:Sender cannot be 'Capturing' outside Testing.");
        }

        // Gate fix 2026-09-27 (SECURITY/CODE_REVIEW LOW): the configured lease must outlive the
        // configured send timeout by a margin, or a slow send can outlast its own claim and a
        // second poll (or instance) re-claims and sends the same notification while the first send
        // is still in flight.
        if (options.Worker.LeaseSeconds <= options.Worker.SendTimeoutSeconds)
            failures.Add("Notifications:Worker:LeaseSeconds must be greater than Notifications:Worker:SendTimeoutSeconds.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void CheckNotPlaceholderHost(string url, string settingName, List<string> failures)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return; // malformed/empty is already caught above

        if (PlaceholderHostSuffixes.Any(suffix => uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            failures.Add($"{settingName} must not be a placeholder host ('{uri.Host}') outside Development/Testing.");
    }
}
