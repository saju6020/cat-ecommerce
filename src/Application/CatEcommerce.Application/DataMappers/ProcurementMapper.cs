using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>
/// HO-IMP-001 §11.3 -- suppliers, purchase orders, consignments and goods receipts. Static <c>ToDomainDto()</c> extensions; enumeration-typed
/// command fields go through <see cref="InventoryMapper.ParseEnum{T}"/> (the validator has already rejected an unrecognised value).
/// </summary>
public static class ProcurementMapper
{
    public static SupplierDto ToDomainDto(this CreateSupplierCommand c) =>
        new(c.Code, c.Name, c.CountryCode, c.ContactName, c.Phone, c.Email, c.Address, c.CurrencyCode, c.LeadTimeDays, c.PaymentTerms, c.PaymentTermDays, c.Notes, c.ToActor());

    /// <summary>The code is immutable: the aggregate ignores it on update, so the stored one is passed through.</summary>
    public static SupplierDto ToDomainDto(this UpdateSupplierCommand c, string existingCode) =>
        new(existingCode, c.Name, c.CountryCode, c.ContactName, c.Phone, c.Email, c.Address, c.CurrencyCode, c.LeadTimeDays, c.PaymentTerms, c.PaymentTermDays, c.Notes, c.ToActor());

    /// <summary>One PO line: the SKU facts come from <see cref="ISkuDirectory"/> (snapshotted onto the line).</summary>
    public static PurchaseOrderLineDto ToDomainDto(this PurchaseOrderLineInput line, SkuStockProfile sku) =>
        new(line.LineId, line.SkuId, sku.Snapshot, sku.AllowsFractionalQuantity, line.OrderedQuantity, line.UnitCostAmount);

    // ── consignments (§8.5) ─────────────────────────────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<HsCodeDto> HsCodes(IReadOnlyList<HsCodeInput> inputs) => inputs.Select(h => new HsCodeDto(h.Code, h.Description)).ToList();

    public static ConsignmentDetailsDto ToDetailsDto(this PlanConsignmentCommand c) =>
        new(c.OriginCountryCode, c.ShipmentReference, c.PurchaseOrderIds, HsCodes(c.HsCodes), c.ImportPermitNumber, c.HealthCertificateNumber,
            c.CustomsDeclarationNumber, c.ExpectedArrivalOn, c.ToActor());

    public static ConsignmentDetailsDto ToDetailsDto(this UpdateConsignmentDetailsCommand c) =>
        new(c.OriginCountryCode, c.ShipmentReference, c.PurchaseOrderIds, HsCodes(c.HsCodes), c.ImportPermitNumber, c.HealthCertificateNumber,
            c.CustomsDeclarationNumber, c.ExpectedArrivalOn, c.ToActor());

    public static ConsignmentDocumentDto ToDocumentDto(this AddConsignmentDocumentCommand c, DateTimeOffset uploadedOn) =>
        new(c.DocumentId, InventoryMapper.ParseEnum<ImportDocumentKind>(c.Kind), c.DocumentNumber, c.IssuedOn, c.FileName, c.StorageKey, c.ContentType,
            c.SizeInBytes, uploadedOn, c.ToActor());

    public static ConsignmentDocumentDto ToDocumentDto(this SupersedeConsignmentDocumentCommand c, DateTimeOffset uploadedOn) =>
        new(c.DocumentId, InventoryMapper.ParseEnum<ImportDocumentKind>(c.Kind), c.DocumentNumber, c.IssuedOn, c.FileName, c.StorageKey, c.ContentType,
            c.SizeInBytes, uploadedOn, c.ToActor());

    public static ConsignmentCostsDto ToDomainDto(this ReplaceConsignmentCostsCommand c) =>
        new(c.CostComponents.Select(k => new CostComponentDto(InventoryMapper.ParseEnum<CostComponentKind>(k.Kind), k.Amount, k.CurrencyCode, k.Note)).ToList(),
            c.ExchangeRates.Select(r => new ExchangeRateDto(r.CurrencyCode, r.RateToBase, r.RecordedOn)).ToList(), c.ToActor());

    // ── goods receipt (§8.6) ────────────────────────────────────────────────────────────────────────────────────────────────────

    public static GoodsReceiptLineDto ToDomainDto(this GoodsReceiptLineInput line) =>
        new(line.LineId, line.SkuId, line.PurchaseOrderLineId, line.LotNumber, line.ManufacturedOn, line.ExpiresOn, line.QuantityAccepted, line.QuantityRejected,
            InventoryMapper.ParseNullableEnum<RejectionReason>(line.RejectionReason), line.RejectionNote, line.LocationId, line.HoldForInspection,
            line.UnitCostAmount, line.UnitCostCurrency, line.ExchangeRateToBase, line.BatchId);
}
