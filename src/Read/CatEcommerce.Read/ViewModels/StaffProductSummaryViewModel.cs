namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-SHP-001 section 12.4 -- staff product list (UI-4).</summary>
public sealed record StaffProductSummaryViewModel(
    Guid ProductId,
    string Name,
    string Brand,
    string Category,
    string Status,
    string Kind,
    int SkuCount,
    bool ListedOnStorefront,
    DateTimeOffset LastUpdatedOn);
