using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Inventory;

// HO-IMP-001 §11.1 C5-C7, C13. Documents are METADATA plus a storage key -- there is no file byte in any command (§4.3, ADR-113).

public sealed record HsCodeInput(string Code, string? Description);

public sealed record CostComponentInput(string Kind, decimal Amount, string CurrencyCode, string? Note);

public sealed record ExchangeRateInput(string CurrencyCode, decimal RateToBase, DateOnly RecordedOn);

/// <summary>C5 -- <c>Procurement.Manage</c>.</summary>
public sealed class PlanConsignmentCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string OriginCountryCode { get; init; }
    public required string ShipmentReference { get; init; }
    public required IReadOnlyList<Guid> PurchaseOrderIds { get; init; }
    public IReadOnlyList<HsCodeInput> HsCodes { get; init; } = [];
    public string? ImportPermitNumber { get; init; }
    public string? HealthCertificateNumber { get; init; }
    public string? CustomsDeclarationNumber { get; init; }
    public DateOnly? ExpectedArrivalOn { get; init; }
}

public sealed class UpdateConsignmentDetailsCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
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

public sealed class AdvanceConsignmentStatusCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string To { get; init; }
    public required DateOnly On { get; init; }
}

public sealed class CancelConsignmentCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C6.</summary>
public sealed class AddConsignmentDocumentCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid DocumentId { get; init; }
    public required string Kind { get; init; }
    public string? DocumentNumber { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public required string FileName { get; init; }
    public required string StorageKey { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
}

public sealed class SupersedeConsignmentDocumentCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required Guid SupersededDocumentId { get; init; }
    public required Guid DocumentId { get; init; }
    public required string Kind { get; init; }
    public string? DocumentNumber { get; init; }
    public DateOnly? IssuedOn { get; init; }
    public required string FileName { get; init; }
    public required string StorageKey { get; init; }
    public required string ContentType { get; init; }
    public required long SizeInBytes { get; init; }
}

/// <summary>C7 -- <c>Consignments.ManageCosts</c>. Replaces costs and rates wholesale; a stale <see cref="ExpectedVersion"/> is 409.</summary>
public sealed class ReplaceConsignmentCostsCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
    public IReadOnlyList<CostComponentInput> CostComponents { get; init; } = [];
    public IReadOnlyList<ExchangeRateInput> ExchangeRates { get; init; } = [];
}

/// <summary>C13 -- <c>Consignments.ManageCosts</c>. Only the consignment is written; batches are not touched (rule 1).</summary>
public sealed class AllocateLandedCostCommand : Command
{
    public required Guid ConsignmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required int ExpectedVersion { get; init; }
}
