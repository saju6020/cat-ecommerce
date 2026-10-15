using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>HO-ORD-001 OQ-6 (amended FU1 D3) -- <see cref="IOrderBillingReader"/>: the delivery tax class and its rate in force. <c>AsNoTracking</c> projections over
/// <c>admin.*</c>; nothing is written.</summary>
public sealed class OrderBillingReader(CatEcommerceDbContext db) : IOrderBillingReader
{
    public async Task<DeliveryTaxFacts> GetDeliveryTaxAsync(DateTimeOffset asOf, CancellationToken ct)
    {
        // FU1 ORD-54 (ADR-217): the delivery charge is always invoiced under the DELIVERY class -- no fallback to any other class. Missing, inactive or rate-less
        // gives (null, 0), which the quote refuses (DeliveryTaxNotConfigured) so no customer is quoted something that cannot be placed.
        var delivery = await db.Set<TaxClassAggregate>().AsNoTracking()
            .Where(c => c.Code == DeliveryTaxFacts.ClassCode && c.IsActive).Select(c => new { c.Id }).FirstOrDefaultAsync(ct);
        if (delivery is null) return new DeliveryTaxFacts(null, 0m);

        var rate = await db.Set<TaxRateAggregate>().AsNoTracking()
            .Where(r => r.TaxClassId == delivery.Id && r.EffectiveFrom <= asOf)
            .OrderByDescending(r => r.EffectiveFrom).Select(r => (decimal?)r.RatePercent).FirstOrDefaultAsync(ct);

        return rate is { } configured ? new DeliveryTaxFacts(delivery.Id, configured) : new DeliveryTaxFacts(null, 0m);
    }
}
