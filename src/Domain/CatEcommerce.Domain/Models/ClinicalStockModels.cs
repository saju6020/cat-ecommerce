namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-EMR-001 §8.12 SHP-1 as it is in the merged code: what a treatment needs to know about a catalogue SKU. HO-SHP-001 publishes no
/// <c>ISkuReader</c>, no stock unit and no controlled flag, so this is assembled from the places that do own each fact (see
/// <see cref="DomainServices.IClinicalSkuReader"/>). <see cref="AllowsFractionalQuantity"/> is false for every SKU today (HO-IMP-001 OQ-19).
/// </summary>
public sealed record ClinicalSku(
    Guid SkuId, string Name, string StockUnit, bool IsActive, bool AllowsFractionalQuantity, Guid TaxClassId, bool IsControlledDrug);

/// <summary>One batch held at the branch's clinic dispensary, as the batch picker shows it. <see cref="RefusalCode"/> is null when Inventory
/// would let the batch be used; <see cref="Available"/> is the free (on hand minus reserved) quantity.</summary>
public sealed record DispensaryBatchChoice(
    Guid BatchId, string LotNumber, DateOnly? ExpiresOn, Guid LocationId, decimal Available, string? RefusalCode, string? RefusalMessage, bool IsFefoDefault)
{
    public bool IsIssuable => RefusalCode is null && Available > 0m;
}

/// <summary>The branch's dispensary and what it holds of one SKU, FEFO-ordered (usable batches first).</summary>
public sealed record DispensaryStock(DispensaryLocation Location, IReadOnlyList<DispensaryBatchChoice> Batches);
