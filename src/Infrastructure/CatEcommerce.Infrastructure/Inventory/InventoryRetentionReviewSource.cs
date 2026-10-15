using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 / HO-XCT-001 section 5.2 C-ALL-3, D-9 -- Inventory's own count of FINANCIAL records past their retention period: purchase orders (from the date they were
/// issued) and goods receipts (from the date received). The period is <see cref="InventoryRules.DefaultFinancialRetentionYears"/> until HO-XCT-001 registers
/// <c>retention.financialYears</c>. A review flag only: nothing is deleted or anonymised automatically (ADR-301).
/// </summary>
public sealed class InventoryRetentionReviewSource(CatEcommerceDbContext db) : IRetentionReviewSource
{
    public string SourceKey => "inventory-financial";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = new DateTimeOffset(asOf.AddYears(-InventoryRules.DefaultFinancialRetentionYears).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var orders = await db.PurchaseOrders.AsNoTracking().Where(p => (p.IssuedOn ?? p.CreatedDate) < cutoff)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Oldest = g.Min(p => p.IssuedOn ?? p.CreatedDate) }).FirstOrDefaultAsync(ct);
        var receipts = await db.GoodsReceipts.AsNoTracking().Where(r => r.ReceivedOn < cutoff)
            .GroupBy(_ => 1).Select(g => new { Count = g.Count(), Oldest = g.Min(r => r.ReceivedOn) }).FirstOrDefaultAsync(ct);

        var result = new List<RetentionReviewCount>();
        if (orders is not null) result.Add(new RetentionReviewCount(RecordClass.Financial, "purchase-order", orders.Count, DateOnly.FromDateTime(orders.Oldest.UtcDateTime)));
        if (receipts is not null) result.Add(new RetentionReviewCount(RecordClass.Financial, "goods-receipt", receipts.Count, DateOnly.FromDateTime(receipts.Oldest.UtcDateTime)));
        return result;
    }
}
