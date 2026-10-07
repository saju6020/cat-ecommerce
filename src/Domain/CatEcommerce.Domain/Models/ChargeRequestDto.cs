using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-BIL-001 §8.3/§8.4: the fields <c>ChargeAggregate.Record(ChargeRequestDto)</c> validates,
/// passed to <c>IBillableChargeRecorder.RecordAsync</c>. Producer rule 3 (§8.4): "Supply prices and
/// discounts already computed; Billing validates, never reprices."
/// </summary>
public sealed record ChargeRequestDto(
    Guid CustomerId,
    Guid BranchId,
    Guid? CatId,
    BusinessLine BusinessLine,
    ItemKind ItemKind,
    ChargeSourceKind SourceKind,
    Guid SourceId,
    string SourceLineRef,
    BillingGroupKind? BillingGroupKind,
    Guid? BillingGroupId,
    string Description,
    decimal Quantity,
    Money UnitPrice,
    Money LineDiscount,
    Guid TaxClassId,
    DateOnly ServiceDate,
    string? ReportingTag = null);
