using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-BIL-001 §8.8 -- a deposit: money set aside for one order or boarding stay. Allocates only to an
/// invoice that contains a matching line until released (<c>EarmarkMismatch</c>). <c>Release*</c> are
/// set by <c>PaymentAggregate.ReleaseEarmark</c>; an earmark is replaced wholesale, never edited in
/// place, so this is an immutable record.
/// </summary>
public sealed record PaymentEarmark
{
    public EarmarkKind Kind { get; init; }
    public Guid SourceId { get; init; }
    public string Label { get; init; }
    public DateTimeOffset? ReleasedOn { get; init; }
    public Guid? ReleasedBy { get; init; }
    public string? ReleaseReason { get; init; }

    public bool IsReleased => ReleasedOn is not null;

    public PaymentEarmark(EarmarkKind kind, Guid sourceId, string label, DateTimeOffset? releasedOn = null, Guid? releasedBy = null, string? releaseReason = null)
    {
        Kind = kind;
        SourceId = sourceId;
        Label = label;
        ReleasedOn = releasedOn;
        ReleasedBy = releasedBy;
        ReleaseReason = releaseReason;
    }

    private PaymentEarmark()
    {
        Kind = default!;
        Label = default!;
    }
}
