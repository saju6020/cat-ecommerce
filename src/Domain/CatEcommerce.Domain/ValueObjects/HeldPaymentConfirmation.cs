namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ORD-001 §8.6 (ADR-204, FR-BIL-12, BR-17) -- a bank-transfer confirmation waiting for a <b>second, different</b> person to approve it because its
/// amount is above Billing's approval threshold. Since the 2026-10-09 architecture-gate fix (owner decision 4) the payment is <b>recorded in Billing at the
/// time of the hold, under the first person's real identity</b>, and waits there as <c>PendingApproval</c>: <see cref="PaymentId"/> names it. The second
/// person approves it through Billing's published <c>ApprovePaymentCommand</c> (Billing enforces "a different person" and the approval role itself);
/// Ordering never records a payment on someone else's behalf. Holds the reference the first person typed, so it is personal-adjacent and never appears in an
/// event or a log.
/// </summary>
public sealed record HeldPaymentConfirmation
{
    public decimal Amount { get; init; }
    public string Reference { get; init; }
    public DateTimeOffset ReceivedOn { get; init; }
    public Guid RecordedBy { get; init; }
    public DateTimeOffset RecordedOn { get; init; }

    /// <summary>The Billing payment waiting for approval. Null only for a hold made before the payment was recorded at hold time.</summary>
    public Guid? PaymentId { get; init; }

    public HeldPaymentConfirmation(decimal amount, string reference, DateTimeOffset receivedOn, Guid recordedBy, DateTimeOffset recordedOn, Guid? paymentId = null)
    {
        Amount = amount;
        Reference = reference;
        ReceivedOn = receivedOn;
        RecordedBy = recordedBy;
        RecordedOn = recordedOn;
        PaymentId = paymentId;
    }

    private HeldPaymentConfirmation() => Reference = default!;
}
