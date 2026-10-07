namespace CatEcommerce.Domain.Models;

/// <summary>
/// One posted movement line -- HO-IMP-001 §8.8. Enough for a pick list or a medicine label
/// (US-PHM-05).
/// </summary>
public sealed record StockMovementLine(
    Guid BatchId,
    string LotNumber,
    DateOnly? ExpiresOn,
    Guid LocationId,
    decimal Quantity,
    bool FefoOverridden);
