using CatEcommerce.Domain.Events.Vaccination;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Vaccination;

/// <summary>
/// A lookup, never mutated by a cat command. RecordCatVaccination loads it read-only to obtain
/// IsCore and ValidityMonths; the CatAggregate computes NextDueOn -- HO-CAT-001 section 8.1.
/// </summary>
public sealed class VaccineAggregate : AggregateRoot
{
    public const int MinValidityMonths = 1;
    public const int MaxValidityMonths = 120;

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsCore { get; private set; }
    public int ValidityMonths { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Description { get; private set; }

    private VaccineAggregate() { }

    public VaccineAggregate(Guid id) : base(id) { }

    public void Register(VaccineRegistrationDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Code))
            throw new DomainRuleViolationException(
                BusinessViolationCodes.CodeRequired,
                "A vaccine code is required.",
                nameof(dto.Code));

        if (dto.ValidityMonths is < MinValidityMonths or > MaxValidityMonths)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.ValidityMonthsOutOfRange,
                $"Validity months must be between {MinValidityMonths} and {MaxValidityMonths}.",
                nameof(dto.ValidityMonths));

        Code = dto.Code;
        Name = dto.Name;
        IsCore = dto.IsCore;
        ValidityMonths = dto.ValidityMonths;
        Description = dto.Description;
        IsActive = true;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new VaccineRegisteredEvent
        {
            VaccineId = Id,
            Code = Code,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }
}
