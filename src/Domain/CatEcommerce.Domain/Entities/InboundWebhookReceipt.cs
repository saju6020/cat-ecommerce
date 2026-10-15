namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-ORD-001 §8.13 (ADR-209, COM-3) -- the inbox row of a courier webhook event: <c>(Provider, EventId)</c> is unique, so a redelivered event is
/// recognised and processed once (US-ORD-18 AC2). A plain entity in schema <c>ops</c>. Holds no webhook body and no personal data -- only
/// the provider key, the provider's event id and when it arrived; the body is never stored or logged.
/// </summary>
public sealed class InboundWebhookReceipt
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = default!;
    public string EventId { get; set; } = default!;
    public DateTimeOffset ReceivedOn { get; set; }
}
