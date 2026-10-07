using CatEcommerce.Domain.Events.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Administration;

/// <summary>HO-ADM-001 §8.6.</summary>
public sealed class TaxClassAggregate : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; }

    private TaxClassAggregate() { }

    public TaxClassAggregate(Guid id) : base(id) { }

    public void Create(TaxClassCreateDto dto)
    {
        ValidateName(dto.Name);

        Code = dto.Code;
        Name = dto.Name;
        IsActive = true;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new TaxClassCreatedEvent { TaxClassId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(TaxClassUpdateDto dto)
    {
        ValidateName(dto.Name);

        Name = dto.Name;
        IsActive = dto.IsActive;

        AddDomainEvent(new TaxClassUpdatedEvent { TaxClassId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
            throw new DomainRuleViolationException(BusinessViolationCodes.NameRequired, "A tax class name is required.", nameof(Name));
    }
}
