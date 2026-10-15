using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-BIL-001 §8.6 -- owned by <c>InvoiceAggregate</c> (≤ 500). A line is a <b>draft</b> line until the
/// invoice is issued; <see cref="Freeze"/> writes the tax class code, the frozen rate and the computed
/// amounts exactly once, in the same save that issues the invoice (<see cref="IsFrozen"/> is what the
/// database trigger keys on -- ADR-180). <see cref="InvoiceDiscarded"/> is the denormalised flag the
/// filtered unique index <c>ix_invoice_line_charge</c> keys on, the only column a draft line changes
/// besides its own fields.
/// </summary>
public sealed class InvoiceLine
{
    public Guid Id { get; private set; }
    public Guid? ChargeId { get; private set; }
    public Guid? CorrectsInvoiceLineId { get; private set; }
    public BusinessLine BusinessLine { get; private set; } = default!;
    public ItemKind ItemKind { get; private set; } = default!;
    public ChargeSourceKind SourceKind { get; private set; } = default!;
    public Guid SourceId { get; private set; }
    public BillingGroupKind? BillingGroupKind { get; private set; }
    public Guid? BillingGroupId { get; private set; }
    public Guid? CatId { get; private set; }
    public string Description { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineDiscount { get; private set; }
    public decimal InvoiceDiscountShare { get; private set; }
    public Guid TaxClassId { get; private set; }
    public string? TaxClassCode { get; private set; }
    public decimal TaxRatePercent { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public int SortOrder { get; private set; }
    public bool InvoiceDiscarded { get; private set; }
    public bool IsFrozen { get; private set; }

    private InvoiceLine() { }

    public InvoiceLine(Guid id, DraftInvoiceLineInput input, int sortOrder)
    {
        Id = id;
        ChargeId = input.ChargeId;
        CorrectsInvoiceLineId = input.CorrectsInvoiceLineId;
        BusinessLine = input.BusinessLine;
        ItemKind = input.ItemKind;
        SourceKind = input.SourceKind;
        SourceId = input.SourceId;
        BillingGroupKind = input.BillingGroupKind;
        BillingGroupId = input.BillingGroupId;
        CatId = input.CatId;
        Description = input.Description;
        Quantity = input.Quantity;
        UnitPrice = input.UnitPrice;
        LineDiscount = input.LineDiscount;
        TaxClassId = input.TaxClassId;
        SortOrder = sortOrder;
    }

    /// <summary>A corrected draft's editable fields (C18) -- description, quantity, unit price,
    /// discount, tax class. Never called on a frozen line; the aggregate guards status.</summary>
    internal void ReviseDraft(DraftInvoiceLineInput input, int sortOrder)
    {
        Description = input.Description;
        Quantity = input.Quantity;
        UnitPrice = input.UnitPrice;
        LineDiscount = input.LineDiscount;
        TaxClassId = input.TaxClassId;
        SortOrder = sortOrder;
    }

    internal void SetSortOrder(int sortOrder) => SortOrder = sortOrder;

    internal void Freeze(string taxClassCode, decimal taxRatePercent, decimal invoiceDiscountShare, decimal netAmount, decimal taxAmount, decimal grossAmount)
    {
        TaxClassCode = taxClassCode;
        TaxRatePercent = taxRatePercent;
        InvoiceDiscountShare = invoiceDiscountShare;
        NetAmount = netAmount;
        TaxAmount = taxAmount;
        GrossAmount = grossAmount;
        IsFrozen = true;
    }

    internal void MarkInvoiceDiscarded() => InvoiceDiscarded = true;
}
