using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.6, OQ-6 (amended FU1 D3, owner decision 2026-10-10) -- the one Billing-side fact Ordering needs that no published Billing contract provides: the tax
/// class an order's delivery charge is invoiced under, and its rate. A Billing charge always carries a tax class, so the delivery fee goes on the invoice under the
/// <see cref="DeliveryTaxFacts.ClassCode"/> tax class, seeded at 0 % and changed by an Administrator through the existing tax configuration (effective-dated rates).
/// Read-only over <c>admin.tax_class</c> / <c>admin.tax_rate</c>; implemented in Infrastructure.
/// </summary>
public interface IOrderBillingReader
{
    /// <summary>
    /// The delivery tax in force at <paramref name="asOf"/>: the active <c>DELIVERY</c> class with its latest rate in force; when that class is missing, inactive or has no
    /// rate yet, <c>(null, 0)</c> -- there is no fallback to another class (ADR-217, amended 2026-10-10) and a delivery charge cannot be quoted.
    /// </summary>
    Task<DeliveryTaxFacts> GetDeliveryTaxAsync(DateTimeOffset asOf, CancellationToken ct);
}
