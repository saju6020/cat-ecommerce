using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-BIL-001 §8.8 / ADR-185 -- one append-only <c>Allocate</c> or <c>Release</c> of a payment against
/// an invoice. History is never rewritten: the effective allocation to an invoice is
/// <c>Σ Allocate − Σ Release</c>. No method on this type changes it after construction (C-ALL-4).
/// </summary>
public sealed class AllocationEntry
{
    public Guid Id { get; private set; }
    public AllocationEntryKind Kind { get; private set; } = default!;
    public Guid InvoiceId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset OccurredOn { get; private set; }
    public Guid By { get; private set; }
    public AllocationReleaseReason? ReleaseReason { get; private set; }

    private AllocationEntry() { }

    public AllocationEntry(Guid id, AllocationEntryKind kind, Guid invoiceId, decimal amount, DateTimeOffset occurredOn, Guid by, AllocationReleaseReason? releaseReason)
    {
        Id = id;
        Kind = kind;
        InvoiceId = invoiceId;
        Amount = amount;
        OccurredOn = occurredOn;
        By = by;
        ReleaseReason = releaseReason;
    }
}
