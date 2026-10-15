using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ORD-001 §8.3 (US-ORD-20 AC2) -- the cash collected on delivery: how much, by whom (a courier by name, or a staff member), and when. Recorded by a
/// named person (BR-17); the money itself is recorded in Billing.
/// </summary>
public sealed record CodCollection
{
    public decimal Amount { get; init; }
    public CollectorKind CollectorKind { get; init; }
    public string CollectorName { get; init; }
    public Guid? CollectorStaffId { get; init; }
    public DateTimeOffset CollectedOn { get; init; }
    public Guid RecordedBy { get; init; }

    public CodCollection(decimal amount, CollectorKind collectorKind, string collectorName, Guid? collectorStaffId, DateTimeOffset collectedOn, Guid recordedBy)
    {
        Amount = amount;
        CollectorKind = collectorKind;
        CollectorName = collectorName;
        CollectorStaffId = collectorStaffId;
        CollectedOn = collectedOn;
        RecordedBy = recordedBy;
    }

    private CodCollection()
    {
        CollectorKind = default!;
        CollectorName = default!;
    }
}
