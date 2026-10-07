using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-SHP-001 section 8.10 -- owned by <c>ProductAggregate</c>. Curated by the shop
/// manager.</summary>
public sealed class RelatedProductLink
{
    public Guid Id { get; private set; }
    public Guid RelatedProductId { get; private set; }
    public RelatedProductKind Kind { get; private set; } = default!;
    public int SortOrder { get; private set; }

    private RelatedProductLink() { }

    public RelatedProductLink(Guid id, Guid relatedProductId, RelatedProductKind kind, int sortOrder)
    {
        Id = id;
        RelatedProductId = relatedProductId;
        Kind = kind;
        SortOrder = sortOrder;
    }
}
