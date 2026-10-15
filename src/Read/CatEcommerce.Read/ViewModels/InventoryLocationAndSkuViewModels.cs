namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-IMP-001 §8.4 -- how a product must be kept (US-IMP-16): returned on SKU lookup, receipt, batch and stock reads.
/// <c>null</c> where no profile exists.</summary>
public sealed record StorageViewModel(string Condition, decimal? MinTemperatureC, decimal? MaxTemperatureC, string? HandlingNotes);

public sealed record StorageProfileViewModel(Guid ProductId, StorageViewModel Storage, int Version);

/// <summary>HO-IMP-001 §12.4 -- one location of the branch; <c>PermittedMovements</c> is <c>StockLocationRules</c>' row for its kind.</summary>
public sealed record StockLocationViewModel(Guid Id, string Code, string Name, string Kind, bool IsActive, IReadOnlyList<string> PermittedMovements);

/// <summary>HO-IMP-001 §8.2 / §12.4 -- <c>SkuStockProfile</c> as the receiving screen needs it, plus <c>storage</c>.</summary>
public sealed record SkuLookupViewModel(
    Guid SkuId, Guid ProductId, string SkuCode, string DisplayName, string? Barcode, string StockClass, bool ExpiryRequired,
    string StockUnit, bool AllowsFractionalQuantity, string? CountryOfOriginCode, bool IsActive, StorageViewModel? Storage);
