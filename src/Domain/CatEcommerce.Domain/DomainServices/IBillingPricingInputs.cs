using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>A service's price and tax class as of an instant -- read at transaction time and
/// snapshotted by the caller (HO-ADM-001 ADR-049), never re-read afterwards.</summary>
public sealed record ServicePriceInfo(Guid ServiceId, string Name, bool IsActive, Money? Price, Guid TaxClassId);

/// <summary>
/// HO-BIL-001 §12.2 -- Billing's read-only view of Administration's tables ("prices and tax rates at
/// <i>t</i>", HO-ADM-001 §8.6). Billing never edits an Administration aggregate.
/// </summary>
public interface IBillingPricingInputs
{
    /// <summary>One entry per tax class that exists; <see cref="TaxRateInfo.RatePercent"/> is null when the
    /// class has no rate effective at <paramref name="at"/>. A class that does not exist is absent.</summary>
    Task<IReadOnlyDictionary<Guid, TaxRateInfo>> GetTaxRatesAsync(IReadOnlyCollection<Guid> taxClassIds, DateTimeOffset at, CancellationToken ct = default);

    Task<ServicePriceInfo?> GetServicePriceAsync(Guid serviceId, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>The service's <b>current</b> tax class (F34: an appointment's snapshot carries none).</summary>
    Task<Guid?> GetServiceTaxClassIdAsync(Guid serviceId, CancellationToken ct = default);

    /// <summary>The tax class of each catalogue SKU (Shop's published <c>SkuRows</c> projection) -- Phase J: a dispensed or surgically used medicine is
    /// billed under its SKU's class. An unknown SKU is absent from the result, never defaulted.</summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetSkuTaxClassIdsAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);
}
