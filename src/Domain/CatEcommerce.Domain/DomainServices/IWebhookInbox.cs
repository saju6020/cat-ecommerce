namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.13 (ADR-209, COM-3, US-ORD-18 AC2) -- the inbox of inbound courier webhooks: <c>(provider, eventId)</c> is recorded once, so a redelivered
/// event is recognised and processed exactly once. The row holds the provider key, the provider's event id and when it arrived -- <b>never the body</b>.
/// <see cref="TryRecordAsync"/> runs <c>INSERT ... ON CONFLICT DO NOTHING</c> on the caller's connection, <b>inside the caller's transaction</b>, so the receipt
/// and the order update commit together and a failed update leaves no receipt (the provider's retry then processes it). Implemented in Infrastructure.
/// </summary>
public interface IWebhookInbox
{
    /// <summary>True when this is the first time the event was seen (it was recorded); false for a duplicate.</summary>
    Task<bool> TryRecordAsync(string provider, string eventId, DateTimeOffset receivedOn, CancellationToken ct);
}
