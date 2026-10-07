using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>
/// HO-ADM-001 §8.6 / ADR-049. Append-only: created once, never mutated again -- a correction is a
/// new row with a later <see cref="EffectiveFrom"/> (C-ALL-4). "No back-dating" is this aggregate's
/// own state, given "now" by the handler (TimeProvider) -- the aggregate never reads the clock
/// itself.
/// </summary>
public sealed class ServicePriceAggregate : AggregateRoot
{
    // Named ClinicServiceId, not ServiceId -- the platform's own Entity base class already
    // declares a ServiceId property (CS0108 hide warning otherwise; §14 "0 warnings").
    public Guid ClinicServiceId { get; private set; }
    public Money Amount { get; private set; } = default!;
    public DateTimeOffset EffectiveFrom { get; private set; }
    public Guid SetBy { get; private set; }

    private ServicePriceAggregate() { }

    public ServicePriceAggregate(Guid id) : base(id) { }

    public void Create(ServicePriceCreateDto dto)
    {
        if (dto.EffectiveFrom < dto.Now)
            throw new DomainRuleViolationException(BusinessViolationCodes.PriceCannotBeBackdated, "A price cannot be effective in the past.", nameof(dto.EffectiveFrom));

        if (dto.Amount.Amount < 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidServiceDuration, "A price cannot be negative.", nameof(dto.Amount));

        ClinicServiceId = dto.ServiceId;
        Amount = dto.Amount;
        EffectiveFrom = dto.EffectiveFrom;
        SetBy = dto.SetBy;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new ServicePriceSetEvent { ServiceId = ClinicServiceId, EffectiveFrom = EffectiveFrom, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }
}
