using CatEcommerce.Application.Commands.Inventory;

namespace CatEcommerce.Api.Contracts.Inventory;

/// <summary>HO-IMP-001 §12.4 request bodies -- consignments. Documents are METADATA plus a storage key; the API has no upload route and
/// no file byte anywhere (§4.3, §20.8).</summary>
public sealed class PlanConsignmentRequest
{
    public required string OriginCountryCode { get; init; }
    public required string ShipmentReference { get; init; }
    public required IReadOnlyList<Guid> PurchaseOrderIds { get; init; }
    public IReadOnlyList<HsCodeInput> HsCodes { get; init; } = [];
    public string? ImportPermitNumber { get; init; }
    public string? HealthCertificateNumber { get; init; }
    public string? CustomsDeclarationNumber { get; init; }
    public DateOnly? ExpectedArrivalOn { get; init; }
}

public sealed class UpdateConsignmentRequest
{
    public required int ExpectedVersion { get; init; }
    public required string OriginCountryCode { get; init; }
    public required string ShipmentReference { get; init; }
    public required IReadOnlyList<Guid> PurchaseOrderIds { get; init; }
    public IReadOnlyList<HsCodeInput> HsCodes { get; init; } = [];
    public string? ImportPermitNumber { get; init; }
    public string? HealthCertificateNumber { get; init; }
    public string? CustomsDeclarationNumber { get; init; }
    public DateOnly? ExpectedArrivalOn { get; init; }
}

public sealed class AdvanceConsignmentStatusRequest
{
    public required string To { get; init; }
    public required DateOnly On { get; init; }
}

public sealed class CancelConsignmentRequest
{
    public required string Reason { get; init; }
}

public sealed class ConsignmentDocumentRequest
{
    public required string Kind { get; init; }
    public string? DocumentNumber { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public required string FileName { get; init; }
    public required string StorageKey { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
}

public sealed class ReplaceConsignmentCostsRequest
{
    public required int ExpectedVersion { get; init; }
    public IReadOnlyList<CostComponentInput> CostComponents { get; init; } = [];
    public IReadOnlyList<ExchangeRateInput> ExchangeRates { get; init; } = [];
}
