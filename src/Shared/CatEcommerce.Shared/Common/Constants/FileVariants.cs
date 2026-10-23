using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// ADR-370 (owner, 2026-10-18): a resized WebP copy of an uploaded image, stored next to the original at
/// <c>{storageKey}.{Name}.webp</c>. The original is never changed (a stored file's content is immutable); the copies are
/// made from it once, at upload or by the backfill, and are deleted with it.
/// </summary>
public sealed record FileVariant(string Name, int MaxPixels)
{
    public string KeyFor(string storageKey) => $"{storageKey}.{Name}.webp";
}

public static class FileVariants
{
    public const string ContentType = "image/webp";

    /// <summary>Catalogue cards, cart, checkout, orders, wishlist: sharp at the card's size on a 2x phone screen.</summary>
    public static readonly FileVariant Card = new("w480", 480);

    /// <summary>The product page's gallery.</summary>
    public static readonly FileVariant Detail = new("w1200", 1200);

    private static readonly IReadOnlyList<FileVariant> ProductImageVariants = [Card, Detail];

    /// <summary>The copies a purpose gets. Only product photos have them today.</summary>
    public static IReadOnlyList<FileVariant> For(FilePurpose purpose) =>
        purpose == FilePurpose.ProductImage ? ProductImageVariants : [];

    /// <summary>
    /// The copies stored beside an object, read from its key (<c>{purpose}/{yyyy}/{MM}/{id}</c>, see StorageKeyFactory),
    /// so a store can delete them with the original without a database lookup.
    /// </summary>
    public static IReadOnlyList<FileVariant> ForStorageKey(string storageKey) =>
        storageKey.StartsWith(FilePurpose.ProductImage.DisplayName + "/", StringComparison.Ordinal) ? ProductImageVariants : [];
}
