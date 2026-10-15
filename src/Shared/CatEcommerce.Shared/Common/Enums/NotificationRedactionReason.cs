namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// HO-NTF-001 gate fix (2026-09-27, SECURITY/ARCHITECTURE/CODE_REVIEW HIGH: anonymisation was a
/// no-op on an already-secret-redacted row). §8.6's <c>Redact(reason)</c> needs to know WHY a row
/// was redacted, both so the read side can produce §12.4's <c>redactedReason</c> field, and so an
/// <see cref="Anonymisation"/> redaction can still take its extra effect (nulling the destination
/// and the unsubscribe link) even when the row was already redacted for a different reason.
/// </summary>
public sealed class NotificationRedactionReason(int value, string displayName) : Enumeration(value, displayName)
{
    /// <summary>§8.10 -- a secret-bearing template's body is redacted once the notification
    /// reaches a terminal state.</summary>
    public static readonly NotificationRedactionReason Secret = new(1, nameof(Secret));

    /// <summary>ADR-041 -- the recipient's account was anonymised; also nulls the destination and
    /// the unsubscribe link, regardless of any prior <see cref="Secret"/> redaction.</summary>
    public static readonly NotificationRedactionReason Anonymisation = new(2, nameof(Anonymisation));

    /// <summary>HO-XCT-001 §5.3 / ADR-305 -- the retention worker redacts notification content
    /// <c>retention.notificationContentMonths</c> after creation; like <see cref="Anonymisation"/> it also
    /// nulls the destination and the unsubscribe link. Metadata (channel, template, status, timestamps,
    /// recipient id) is kept.</summary>
    public static readonly NotificationRedactionReason Retention = new(3, nameof(Retention));
}
