namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-SHP-001 section 12.4. <c>OnSale</c> is false for an archived/unlisted product or a
/// discontinued SKU: such an item stays on the wishlist and reads back as not on sale.</summary>
public sealed record WishlistItemViewModel(
    Guid SkuId, Guid ProductId, string Slug, string ProductName, string SkuLabel, MoneyViewModel? Price,
    string Availability, bool OnSale, DateTimeOffset AddedOn, ProductImageRef? PrimaryImage);

public sealed record WishlistViewModel(IReadOnlyList<WishlistItemViewModel> Items);

public sealed record StockAlertViewModel(
    Guid StockAlertId, Guid SkuId, string? Slug, string? ProductName, string? SkuLabel, string Status,
    DateTimeOffset RequestedOn, DateTimeOffset? NotifiedOn);

/// <summary>HO-SHP-001 section 8.11 / F33 / OQ-2 -- the real routing decision for
/// <c>shop.back-in-stock</c>, computed by HO-NTF-001's own <c>NotificationRoutingPolicy</c>, so the
/// client can tell a customer who granted no marketing consent that nothing will reach them.</summary>
public sealed record StockAlertDeliverabilityViewModel(bool WillNotify, IReadOnlyList<string> Channels, bool InboxCopy);
