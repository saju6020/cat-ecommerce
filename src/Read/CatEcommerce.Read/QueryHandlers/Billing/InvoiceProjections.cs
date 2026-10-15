using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Billing;

/// <summary>An invoice header as the read side needs it -- scalars and the two snapshots, selected in SQL. The read side never rehydrates an
/// <c>InvoiceAggregate</c> (ADR-012, read-side-pattern section 4).</summary>
internal sealed record InvoiceHeader(
    Guid Id, string? Number, string? FinancialYear, InvoiceStatus Status, int Version, Guid BranchId, Guid CustomerId, string CurrencyCode,
    DateTimeOffset? IssuedOn, DateOnly? IssueDate, DateOnly? DueOn, decimal NetTotal, decimal DiscountTotal, decimal TaxTotal, decimal Total,
    decimal PaidTotal, decimal CreditedTotal, int? LoyaltyPoints, decimal? LoyaltyValue, Guid? LoyaltyEntryId, Guid? CorrectsInvoiceId,
    Guid? CorrectsCreditNoteId, string? Notes, SellerSnapshot? Seller, BillToSnapshot? BillTo)
{
    public decimal Outstanding => CatEcommerce.Shared.Common.Constants.InvoiceStatusRules.Outstanding(Total, PaidTotal, CreditedTotal);

}

/// <summary>One invoice line, selected in SQL.</summary>
internal sealed record InvoiceLineRow(
    Guid Id, Guid? ChargeId, BusinessLine BusinessLine, ItemKind ItemKind, ChargeSourceKind SourceKind, Guid SourceId, Guid? CatId, string Description,
    decimal Quantity, decimal UnitPrice, decimal LineDiscount, decimal InvoiceDiscountShare, Guid TaxClassId, string? TaxClassCode, decimal TaxRatePercent,
    decimal NetAmount, decimal TaxAmount, decimal GrossAmount, int SortOrder);

internal static class InvoiceProjections
{
    /// <summary>The first invoice of an already-filtered query as a header; null when there is none.</summary>
    public static Task<InvoiceHeader?> HeaderAsync(IQueryable<InvoiceAggregate> invoices) =>
        invoices.Select(i => new InvoiceHeader(
            i.Id, i.Number, i.FinancialYear, i.Status, i.Version, i.BranchId, i.CustomerId, i.CurrencyCode, i.IssuedOn, i.IssueDate, i.DueOn, i.NetTotal,
            i.DiscountTotal, i.TaxTotal, i.Total, i.PaidTotal, i.CreditedTotal,
            i.LoyaltyRedemption != null ? (int?)i.LoyaltyRedemption.Points : null,
            i.LoyaltyRedemption != null ? (decimal?)i.LoyaltyRedemption.Value : null,
            i.LoyaltyRedemption != null ? (Guid?)i.LoyaltyRedemption.LoyaltyEntryId : null,
            i.CorrectsInvoiceId, i.CorrectsCreditNoteId, i.Notes, i.Seller, i.BillTo)).FirstOrDefaultAsync();

    public static async Task<IReadOnlyList<InvoiceLineRow>> LinesAsync(CatEcommerceDbContext db, Guid invoiceId) =>
        await db.Set<InvoiceAggregate>().AsNoTracking().Where(i => i.Id == invoiceId)
            .SelectMany(i => i.Lines)
            .OrderBy(l => l.SortOrder)
            .Select(l => new InvoiceLineRow(
                l.Id, l.ChargeId, l.BusinessLine, l.ItemKind, l.SourceKind, l.SourceId, l.CatId, l.Description, l.Quantity, l.UnitPrice, l.LineDiscount,
                l.InvoiceDiscountShare, l.TaxClassId, l.TaxClassCode, l.TaxRatePercent, l.NetAmount, l.TaxAmount, l.GrossAmount, l.SortOrder))
            .ToListAsync();
}
