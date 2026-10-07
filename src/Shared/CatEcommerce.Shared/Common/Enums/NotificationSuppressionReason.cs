namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-NTF-001 §8.5/§10.1 -- why a request produced no notification at all.</summary>
public sealed class NotificationSuppressionReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationSuppressionReason RecipientInactive = new(1, nameof(RecipientInactive));
    public static readonly NotificationSuppressionReason NoPermittedChannel = new(2, nameof(NoPermittedChannel));
    public static readonly NotificationSuppressionReason SubjectNoLongerApplies = new(3, nameof(SubjectNoLongerApplies));
    public static readonly NotificationSuppressionReason MissingVariable = new(4, nameof(MissingVariable));
    public static readonly NotificationSuppressionReason UnknownTemplate = new(5, nameof(UnknownTemplate));

    /// <summary>Gate fix 2026-09-27 (SECURITY/CODE_REVIEW MEDIUM 7). Rendering threw something
    /// other than the (now much rarer, thanks to TemplateRules' own validation) "missing variable"
    /// case -- a malformed Date value, a non-https Url value, or any other unexpected renderer
    /// failure. Distinguishes "this template/value combination cannot ever render" from
    /// <see cref="MissingVariable"/>'s specific, narrower cause.</summary>
    public static readonly NotificationSuppressionReason RenderFailed = new(6, nameof(RenderFailed));

    /// <summary>Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM). The request rendered
    /// successfully, but committing it (together with the notifications it produced) kept failing --
    /// for example a transient database error unrelated to rendering. The ONE previously-known cause
    /// of a persistent save failure (a rendered field overflowing its column) is now caught by
    /// <see cref="RenderFailed"/> before the save is ever attempted; this exists as a terminal
    /// backstop for any OTHER repeated save failure, so a single row can never retry forever and
    /// block every other request behind it in the queue (see
    /// <c>NotificationRequestRules.MaxExpansionAttempts</c>).</summary>
    public static readonly NotificationSuppressionReason PersistenceFailed = new(7, nameof(PersistenceFailed));
}
