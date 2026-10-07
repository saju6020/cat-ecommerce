using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.6 -- adds notes, featuring and order to a country of origin (US-SHP-05,
/// ADR-081). An origin appears in "Shop by Country" when at least one Active, listed food product
/// has that country of origin -- that join is the read side's job, not this aggregate's.
/// </summary>
public sealed class SourcingOriginAggregate : AggregateRoot
{
    public string CountryCode { get; private set; } = default!;
    public string? SourcingNotes { get; private set; }
    public bool IsFeatured { get; private set; }
    public int SortOrder { get; private set; }

    private SourcingOriginAggregate() { }

    public SourcingOriginAggregate(Guid id) : base(id) { }

    public void Create(SourcingOriginCreateDto dto)
    {
        ValidateCountryCode(dto.CountryCode);
        ValidateSortOrder(dto.SortOrder);
        ValidateNotes(dto.SourcingNotes);

        CountryCode = CountryCodes.Normalise(dto.CountryCode);
        SourcingNotes = string.IsNullOrWhiteSpace(dto.SourcingNotes) ? null : dto.SourcingNotes;
        IsFeatured = dto.IsFeatured;
        SortOrder = dto.SortOrder;

        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new SourcingOriginCreatedEvent { CountryCode = CountryCode, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    public void Update(SourcingOriginUpdateDto dto)
    {
        ValidateSortOrder(dto.SortOrder);
        ValidateNotes(dto.SourcingNotes);

        SourcingNotes = string.IsNullOrWhiteSpace(dto.SourcingNotes) ? null : dto.SourcingNotes;
        IsFeatured = dto.IsFeatured;
        SortOrder = dto.SortOrder;

        AddDomainEvent(new SourcingOriginUpdatedEvent { CountryCode = CountryCode, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });
        UpdateVersion();
    }

    private static void ValidateCountryCode(string countryCode)
    {
        if (!CountryCodes.IsValid(countryCode))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCountryCode, $"'{countryCode}' is not a known country code.", nameof(CountryCode));
    }

    private static void ValidateSortOrder(int sortOrder)
    {
        if (sortOrder is < 0 or > 999)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCountryCode, "Sort order must be between 0 and 999.", nameof(SortOrder));
    }

    private static void ValidateNotes(string? notes)
    {
        if (string.IsNullOrEmpty(notes)) return;
        if (notes.Length > 2000)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCountryCode, "Sourcing notes cannot exceed 2000 characters.", nameof(SourcingNotes));

        var markupError = LimitedMarkup.Validate(notes);
        if (markupError is not null)
            throw new DomainRuleViolationException(markupError, "Sourcing notes failed markup validation.", nameof(SourcingNotes));
    }
}
