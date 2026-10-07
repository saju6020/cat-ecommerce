namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-SHP-001 §8.3.2 -- owned by <c>ProductAggregate</c>. References an uploaded file by id only
/// (HO-FIL-001; the same shape as <c>CatPhoto</c>) -- content type and size are
/// <c>StoredFileAggregate</c>'s own state, read at serve/validate time, not copied here where they
/// could go stale. <c>SkuId</c> null means a product-level image; otherwise it must be one of this
/// product's own SKUs (<c>InvalidImage</c>, checked by the aggregate). The whole set is replaced at
/// once by <c>ProductAggregate.SetImages</c>.
/// </summary>
public sealed class ProductImage
{
    public Guid Id { get; private set; }
    public Guid? SkuId { get; private set; }
    public Guid FileId { get; private set; }
    public string AltText { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public bool IsPrimary { get; private set; }

    private ProductImage() { }

    public ProductImage(Guid id, Guid? skuId, Guid fileId, string altText, int sortOrder, bool isPrimary)
    {
        Id = id;
        SkuId = skuId;
        FileId = fileId;
        AltText = altText;
        SortOrder = sortOrder;
        IsPrimary = isPrimary;
    }
}
