namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-NTF-001 §10.3 -- fixed system-actor ids used as <c>UserContext.UserId</c> when no
/// human caller exists (the worker, the anonymous unsubscribe endpoint).</summary>
public static class SystemActors
{
    public static readonly Guid NotificationWorker = new("00000000-0000-0000-0000-000000000ffe");

    /// <summary>HO-IMP-001 §10.3 -- the <c>InventoryWorker</c>'s identity (alert passes, reconciliation).</summary>
    public static readonly Guid InventoryWorker = new("00000000-0000-0000-0000-000000000ff3");

    /// <summary>HO-ORD-001 §12.2 -- the <c>OrderWorker</c>'s identity (expiry of unpaid transfer orders, guest-cart and idempotency purges).</summary>
    public static readonly Guid OrderWorker = new("00000000-0000-0000-0000-000000000ff4");

    /// <summary>HO-ORD-001 §8.13 -- the identity of an accepted courier tracking webhook event (the caller is a provider, not a person).</summary>
    public static readonly Guid CourierWebhook = new("00000000-0000-0000-0000-000000000ff5");

    /// <summary>HO-XCT-001 §12.2 -- the <c>RetentionWorker</c>'s identity (erasure pass, purges, redaction).</summary>
    public static readonly Guid RetentionWorker = new("00000000-0000-0000-0000-000000000ff6");

    /// <summary>HO-XCT-001 ADR-309 -- the identity stored on anonymous telemetry: nobody (the empty id), whoever called.</summary>
    public static readonly Guid Anonymous = Guid.Empty;
}
