using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.6 / ADR-049. Append-only, same "no back-dating" rule as
/// <see cref="ServicePriceAggregate"/>.</summary>
public sealed class TaxRateAggregate : AggregateRoot
{
    public Guid TaxClassId { get; private set; }
    public decimal RatePercent { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }

    private TaxRateAggregate() { }

    public TaxRateAggregate(Guid id) : base(id) { }

    public void Create(TaxRateCreateDto dto)
    {
        if (dto.EffectiveFrom < dto.Now)
            throw new DomainRuleViolationException(BusinessViolationCodes.PriceCannotBeBackdated, "A tax rate cannot be effective in the past.", nameof(dto.EffectiveFrom));

        if (dto.RatePercent is < 0 or > 100)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSettingValue, "A tax rate must be between 0 and 100 percent.", nameof(dto.RatePercent));

        TaxClassId = dto.TaxClassId;
        RatePercent = decimal.Round(dto.RatePercent, 2, MidpointRounding.ToEven);
        EffectiveFrom = dto.EffectiveFrom;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new TaxRateSetEvent { TaxClassId = TaxClassId, EffectiveFrom = EffectiveFrom, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
