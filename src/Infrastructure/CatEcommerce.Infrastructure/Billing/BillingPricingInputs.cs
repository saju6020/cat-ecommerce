using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 §12.2 -- Billing's <b>read-only</b> view of Administration's tables (HO-ADM-001 §8.6: "price and
/// tax rate at <i>t</i> = the row with the greatest <c>EffectiveFrom</c> not after <i>t</i>"). Always
/// <c>AsNoTracking</c>; Billing never edits an Administration aggregate.
/// </summary>
public sealed class BillingPricingInputs(CatEcommerceDbContext db) : IBillingPricingInputs
{
    public async Task<IReadOnlyDictionary<Guid, TaxRateInfo>> GetTaxRatesAsync(
        IReadOnlyCollection<Guid> taxClassIds, DateTimeOffset at, CancellationToken ct = default)
    {
        if (taxClassIds.Count == 0) return new Dictionary<Guid, TaxRateInfo>();

        var ids = taxClassIds.Distinct().ToList();

        var classes = await db.Set<TaxClassAggregate>().AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new { t.Id, t.Code })
            .ToListAsync(ct);

        var rates = await db.Set<TaxRateAggregate>().AsNoTracking()
            .Where(r => ids.Contains(r.TaxClassId) && r.EffectiveFrom <= at)
            .Select(r => new { r.TaxClassId, r.RatePercent, r.EffectiveFrom })
            .ToListAsync(ct);

        var latest = rates
            .GroupBy(r => r.TaxClassId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.EffectiveFrom).First().RatePercent);

        return classes.ToDictionary(c => c.Id, c => new TaxRateInfo(c.Id, c.Code, latest.TryGetValue(c.Id, out var rate) ? rate : null));
    }

    public async Task<ServicePriceInfo?> GetServicePriceAsync(Guid serviceId, DateTimeOffset at, CancellationToken ct = default)
    {
        var service = await db.Set<ClinicServiceAggregate>().AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => new { s.Id, s.Name, s.IsActive, s.TaxClassId })
            .FirstOrDefaultAsync(ct);
        if (service is null) return null;

        var price = await db.Set<ServicePriceAggregate>().AsNoTracking()
            .Where(p => p.ClinicServiceId == serviceId && p.EffectiveFrom <= at)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => new { p.Amount.Amount, p.Amount.CurrencyCode })
            .FirstOrDefaultAsync(ct);

        return new ServicePriceInfo(service.Id, service.Name, service.IsActive, price is null ? null : new Money(price.Amount, price.CurrencyCode), service.TaxClassId);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetSkuTaxClassIdsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default)
    {
        if (skuIds.Count == 0) return new Dictionary<Guid, Guid>();

        var ids = skuIds.Distinct().ToList();
        var rows = await db.SkuRows().Where(s => ids.Contains(s.SkuId)).Select(s => new { s.SkuId, s.TaxClassId }).ToListAsync(ct);
        return rows.ToDictionary(r => r.SkuId, r => r.TaxClassId);
    }

    public async Task<Guid?> GetServiceTaxClassIdAsync(Guid serviceId, CancellationToken ct = default) =>
        await db.Set<ClinicServiceAggregate>().AsNoTracking()
            .Where(s => s.Id == serviceId)
            .Select(s => (Guid?)s.TaxClassId)
            .FirstOrDefaultAsync(ct);
}
