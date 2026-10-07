using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.5 -- append-only, effective-dated SKU price (HO-ADM-001 ADR-049 applied to
/// goods, ADR-080). Created by <c>ScheduleSkuPriceCommand</c> only; no other mutation exists.
/// "SKU exists" and "no duplicate effective date for this SKU" are the handler's repository checks
/// (they span more than this one row); positivity, back-dating (with clock-skew tolerance) and the
/// configured currency are this aggregate's own invariants.
/// </summary>
public sealed class SkuPriceAggregate : AggregateRoot
{
    /// <summary>HO-ADM-001 ADR-049's clock-skew tolerance, applied here too (section 8.5).</summary>
    public static readonly TimeSpan BackdatingTolerance = TimeSpan.FromSeconds(60);

    public Guid SkuId { get; private set; }
    public Money Amount { get; private set; } = default!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid SetBy { get; private set; }

    private SkuPriceAggregate() { }

    public SkuPriceAggregate(Guid id) : base(id) { }

    public void Create(SkuPriceCreateDto dto, string expectedCurrencyCode)
    {
        if (dto.Amount.Amount <= 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.PriceMustBePositive, "A price must be greater than zero.", nameof(dto.Amount));

        if (dto.Amount.CurrencyCode != expectedCurrencyCode)
            throw new DomainRuleViolationException(BusinessViolationCodes.CurrencyMismatch, $"Prices must be in {expectedCurrencyCode}.", nameof(dto.Amount));

        if (dto.EffectiveFrom < dto.Now - BackdatingTolerance)
            throw new DomainRuleViolationException(BusinessViolationCodes.PriceCannotBeBackdated, "A price cannot be scheduled in the past.", nameof(dto.EffectiveFrom));

        SkuId = dto.SkuId;
        Amount = dto.Amount;
        EffectiveFrom = dto.EffectiveFrom;
        SetBy = dto.SetBy;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new SkuPriceScheduledEvent { SkuId = SkuId, EffectiveFrom = EffectiveFrom, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
