namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

// HO-RPT-001 section 8.4 -- the keyless row types of the phase C (stock) views. No float or double anywhere (U-D4).

public sealed class RvStockLedgerRow
{
    public Guid EntryId { get; set; }
    public Guid SkuId { get; set; }
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public Guid BranchId { get; set; }
    public int MovementKind { get; set; }
    public decimal QuantityDelta { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public int SourceType { get; set; }
    public Guid SourceId { get; set; }
    public Guid? SourceLineId { get; set; }
}

public sealed class RvBatchRow
{
    public Guid BatchId { get; set; }
    public Guid SkuId { get; set; }
    public string SkuCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public DateOnly? ExpiryOn { get; set; }
    public int Status { get; set; }
    public Guid? ConsignmentId { get; set; }
    public Guid BranchId { get; set; }
    public DateTimeOffset ReceivedOn { get; set; }
    public decimal? LandedUnitCost { get; set; }
}

public sealed class RvStockLocationRow
{
    public Guid LocationId { get; set; }
    public Guid BranchId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Kind { get; set; }
    public bool IsActive { get; set; }
}

public sealed class RvConsignmentRow
{
    public Guid ConsignmentId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string ShipmentReference { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string OriginCountry { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public int PoCount { get; set; }
    public int ClearanceStatus { get; set; }
    public decimal? LandedCostTotal { get; set; }
    public bool HasPermit { get; set; }
    public bool HasHealthCertificate { get; set; }
    public bool HasCustomsDeclaration { get; set; }
    public int BatchesReceived { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
    public DateOnly? ClearedOn { get; set; }
}

public sealed class RvSkuRow
{
    public Guid SkuId { get; set; }
    public string SkuCode { get; set; } = string.Empty;
    public string? VariantLabel { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public int Category { get; set; }
    public int? SubCategory { get; set; }
    public string? CountryOfOrigin { get; set; }
    public decimal? CurrentPrice { get; set; }
    public string? CurrencyCode { get; set; }
    public bool RequiresVetAuthorisation { get; set; }
}

/// <summary>HO-RPT-001 R7 -- the position of a batch at a location with the facts the usability rule needs.</summary>
public sealed class RvStockPositionRow
{
    public Guid BatchId { get; set; }
    public Guid LocationId { get; set; }
    public Guid SkuId { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public int BatchStatus { get; set; }
    public DateOnly? ExpiryOn { get; set; }
    public string SkuCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string StockUnit { get; set; } = string.Empty;
}

/// <summary>HO-RPT-001 R7 -- a SKU's reorder policy at a location, with its preferred supplier.</summary>
public sealed class RvReorderPolicyRow
{
    public Guid SkuId { get; set; }
    public Guid LocationId { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal? ReorderQuantity { get; set; }
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public int? LeadTimeDays { get; set; }
}

/// <summary>HO-RPT-001 R7 -- what is still due on issued purchase orders per branch, SKU and delivery location.</summary>
public sealed class RvOnOrderRow
{
    public Guid BranchId { get; set; }
    public Guid SkuId { get; set; }
    public Guid LocationId { get; set; }
    public decimal Outstanding { get; set; }
}
