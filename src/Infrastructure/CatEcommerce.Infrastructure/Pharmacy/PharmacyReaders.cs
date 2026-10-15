using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Pharmacy;

/// <summary>
/// HO-PHM-001 §8.12 -- Pharmacy's ONE adapter over HO-SHP-001's catalogue, through Shop's own published projection (<c>db.SkuRows()</c>), the same
/// pattern as <c>CatalogueSkuDirectory</c> for Inventory (ADR-111): if the catalogue's read model changes, this file changes and nothing else.
/// Read-only. A clinic-only SKU is one whose product is not listed on the storefront (<c>ListedOnStorefront = false</c>, OQ-5): stocked and priced, never
/// sold online.
/// </summary>
public sealed class CatalogueReader(CatEcommerceDbContext db) : ICatalogueReader
{
    public async Task<IReadOnlyList<CatalogueSku>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        if (skuIds.Count == 0) return [];
        var ids = skuIds.Distinct().ToList();
        var rows = await db.SkuRows().Where(s => ids.Contains(s.SkuId)).ToListAsync(ct);
        return rows.Select(ToSku).ToList();
    }

    public async Task<CatalogueProduct?> GetProductAsync(Guid productId, CancellationToken ct)
    {
        var rows = await db.SkuRows().Where(s => s.ProductId == productId).ToListAsync(ct);
        return ToProducts(rows).FirstOrDefault();
    }

    public async Task<CatalogueProduct?> FindProductBySkuAsync(Guid skuId, CancellationToken ct)
    {
        var productId = await db.SkuRows().Where(s => s.SkuId == skuId).Select(s => (Guid?)s.ProductId).FirstOrDefaultAsync(ct);
        return productId is null ? null : await GetProductAsync(productId.Value, ct);
    }

    public async Task<IReadOnlyList<CatalogueSku>> SearchSkusAsync(string? term, bool includeClinicOnly, int take, CancellationToken ct)
    {
        var query = db.SkuRows().Where(s => s.Kind == ProductKind.Standard);
        if (!includeClinicOnly) query = query.Where(s => s.ListedOnStorefront);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{Escape(term)}%";
            query = query.Where(s => EF.Functions.ILike(s.ProductName, pattern, "\\") || EF.Functions.ILike(s.SkuCode, pattern, "\\") || (s.Barcode != null && EF.Functions.ILike(s.Barcode, pattern, "\\")));
        }

        var rows = await query.OrderBy(s => s.ProductName).ThenBy(s => s.SkuCode).Take(Math.Clamp(take, 1, 100)).ToListAsync(ct);
        return rows.Select(ToSku).ToList();
    }

    public async Task<IReadOnlyList<CatalogueProduct>> SearchPrescriptionProductsAsync(string? term, int take, CancellationToken ct)
    {
        var query = db.SkuRows().Where(s => s.PrescriptionRequired && s.ProductStatus == ProductStatus.Active);
        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = $"%{Escape(term)}%";
            query = query.Where(s => EF.Functions.ILike(s.ProductName, pattern, "\\") || EF.Functions.ILike(s.Brand, pattern, "\\"));
        }

        var productIds = await query.Select(s => new { s.ProductId, s.ProductName }).Distinct()
            .OrderBy(p => p.ProductName).Take(Math.Clamp(take, 1, 100)).Select(p => p.ProductId).ToListAsync(ct);
        var rows = await db.SkuRows().Where(s => productIds.Contains(s.ProductId)).ToListAsync(ct);

        return ToProducts(rows).OrderBy(p => p.Name).ToList();
    }

    private static List<CatalogueProduct> ToProducts(IEnumerable<SkuRow> rows) =>
        rows.GroupBy(r => r.ProductId).Select(ToProduct).ToList();

    private static CatalogueProduct ToProduct(IGrouping<Guid, SkuRow> group)
    {
        var first = group.First();
        return new CatalogueProduct(first.ProductId, first.ProductName, first.Brand, first.PrescriptionRequired, first.ProductStatus == ProductStatus.Active, group.Select(r => r.SkuId).ToList());
    }

    private static CatalogueSku ToSku(SkuRow row) =>
        new(row.SkuId, row.ProductId, row.ProductName, row.Label, row.Barcode, row.SkuStatus == SkuStatus.Active, !row.ListedOnStorefront, row.PrescriptionRequired);

    private static string Escape(string term) => term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

/// <summary>HO-PHM-001 §8.7 / ADR-154 -- the price in force at an instant: the row with the greatest <c>EffectiveFrom &lt;= at</c> (HO-SHP-001 §8.5).</summary>
public sealed class SkuPriceReader(CatEcommerceDbContext db) : ISkuPriceReader
{
    public async Task<Money?> PriceAtAsync(Guid skuId, DateTimeOffset at, CancellationToken ct)
    {
        var row = await db.PriceRows().Where(p => p.SkuId == skuId && p.EffectiveFrom <= at).OrderByDescending(p => p.EffectiveFrom).FirstOrDefaultAsync(ct);
        return row is null ? null : new Money(row.Amount, row.CurrencyCode);
    }
}

/// <summary>HO-PHM-001 §8.12 -- the branch's "today", through HO-ADM-001's <c>BranchCalendar</c> so zone conversion stays in one place (ADR-045).</summary>
public sealed class PharmacyClock(CatEcommerceDbContext db, TimeProvider timeProvider) : IPharmacyClock
{
    private const string FallbackZone = "Asia/Dhaka";

    public async Task<DateOnly> LocalTodayAsync(Guid branchId, CancellationToken ct) =>
        BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), await ZoneAsync(branchId, ct));

    public async Task<DateOnly> LocalDateOfAsync(Guid branchId, DateTimeOffset instant, CancellationToken ct) =>
        BranchCalendar.LocalDateOf(instant, await ZoneAsync(branchId, ct));

    public Task<DateOnly> MainBranchTodayAsync(CancellationToken ct) =>
        LocalTodayAsync(CatEcommerce.Shared.Common.Constants.AdministrationSeed.MainBranchId, ct);

    private async Task<string> ZoneAsync(Guid branchId, CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct) ?? FallbackZone;
}

/// <summary>HO-PHM-001 §8.9 -- the formulary as a value, for areas that must not rehydrate an aggregate (ADR-151). Read-only.</summary>
public sealed class FormularyReader(CatEcommerceDbContext db) : IFormularyReader
{
    public async Task<FormularyMedicineSnapshot?> GetAsync(Guid medicineId, CancellationToken ct) =>
        (await db.FormularyMedicines.Where(m => m.Id == medicineId).ToSnapshotsAsync(ct)).FirstOrDefault();

    public async Task<FormularyMedicineSnapshot?> FindBySkuAsync(Guid skuId, CancellationToken ct) =>
        (await db.FormularyMedicines.Where(m => m.LinkedSkus.Any(l => l.SkuId == skuId)).ToSnapshotsAsync(ct)).FirstOrDefault();
}
