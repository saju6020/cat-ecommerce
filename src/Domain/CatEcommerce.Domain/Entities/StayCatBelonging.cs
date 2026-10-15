using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BRD-001 section 8.7/8.11 -- one item the owner leaves with the cat, recorded at check-in with its photos (HO-FIL-001 file ids, purpose BoardingBelongings)
/// and ticked off at check-out. Owned by <see cref="StayCat"/>; written once.</summary>
public sealed class StayCatBelonging
{
    public Guid Id { get; private set; }
    public BelongingKind Kind { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public Guid[] FileIds { get; private set; } = [];
    public bool? Returned { get; private set; }
    public string? ReturnNote { get; private set; }

    private StayCatBelonging() { }

    public StayCatBelonging(Guid id, BelongingKind kind, string description, IEnumerable<Guid> fileIds)
    {
        Id = id;
        Kind = kind;
        Description = description;
        FileIds = fileIds.ToArray();
    }

    internal void MarkReturned(bool returned, string? note)
    {
        Returned = returned;
        ReturnNote = note;
    }
}
