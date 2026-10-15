namespace CatEcommerce.Read.ViewModels;

/// <summary>One row of the published availability contract (ADR-116). In-process consumers get the decimal itself.</summary>
public sealed record SellableStockViewModel(Guid SkuId, decimal SellableQuantity, DateOnly? NextExpiresOn);

public sealed record FefoPreviewLineViewModel(Guid BatchId, string LotNumber, DateOnly? ExpiresOn, Guid LocationId, string LocationCode, string Quantity);

/// <summary>The plan FEFO would make, or why it refuses (<c>RefusalCode</c>/<c>RefusalMessage</c>). <c>Shortfall</c> is what could not be
/// allocated (0 when the demand is fully met).</summary>
public sealed record FefoPreviewViewModel(
    IReadOnlyList<FefoPreviewLineViewModel> Lines, string Shortfall, string StockUnit, string? RefusalCode, string? RefusalMessage);
