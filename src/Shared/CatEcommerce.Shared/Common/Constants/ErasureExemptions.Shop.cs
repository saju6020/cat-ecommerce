namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Shop (HO-SHP-001) is exempted from <c>IPersonalDataEraser</c> registration (HO-XCT-001
/// §5.2 C-ALL-2): it stores a customer id on wishlists, stock alerts and reviews, and nothing
/// else personal. The wishlist is emptied and pending alerts are closed by the
/// <c>CustomerAccountAnonymisedEvent</c> handler (ADR-096); a published review stays public,
/// credited to "A verified buyer" (its author name is derived at read time from the account and
/// never stored here).</summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> ShopEntries =
    [
        ("shop", "Customer id only on wishlists, stock alerts and reviews; wishlist cleared and pending alerts closed on anonymisation, published reviews retained as 'A verified buyer' (HO-SHP-001 ADR-096)."),
    ];
}
