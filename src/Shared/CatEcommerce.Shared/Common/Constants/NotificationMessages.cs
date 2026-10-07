namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 -- user-facing/log strings kept in one place rather than inlined at each
/// call site.</summary>
public static class NotificationMessages
{
    public const string NoSmsProviderConfigured = "No SMS provider is configured";
    public const string NoEmailProviderConfigured = "No email provider is configured";

    public const string StartupNoSmsProviderWarning =
        "No SMS provider is configured: SMS notifications, including password reset, will not be delivered. See HO-NTF-001 OQ-1.";

    public const string StartupNoEmailProviderWarning =
        "No email provider is configured: email notifications, including password reset, will not be delivered. See HO-NTF-001 OQ-1.";

    public const string ResendSecretReason = "secret-bearing";
    public const string ResendInAppReason = "in-app copy";
    public const string ResendRecipientInactiveReason = "recipient inactive";
    public const string ResendRecipientOptedOutReason = "recipient has since opted out";
    public const string ResendStillDeliveringReason = "still being delivered";
}
