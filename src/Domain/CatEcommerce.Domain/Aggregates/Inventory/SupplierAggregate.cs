using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.5 (US-IMP-01). Name, country, contact, currency, lead time and payment terms. Payment terms are TEXT and
/// days only -- nothing here takes or records money (§2). <c>Code</c> is upper-case and unique.
/// </summary>
public sealed class SupplierAggregate : AggregateRoot
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string CountryCode { get; private set; } = default!;
    public SupplierContact Contact { get; private set; } = new(null, null, null, null);
    public string CurrencyCode { get; private set; } = default!;
    public int LeadTimeDays { get; private set; }
    public string PaymentTerms { get; private set; } = default!;
    public int? PaymentTermDays { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; }

    private SupplierAggregate() { }

    public SupplierAggregate(Guid id) : base(id) { }

    public void Create(SupplierDto dto)
    {
        var code = InventoryViolation.Normalise(dto.Code).ToUpperInvariant();
        if (code.Length is 0 or > 20)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "A supplier code is 1-20 characters.", nameof(Code));

        Apply(dto);
        Code = code;
        IsActive = true;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new SupplierCreatedEvent { SupplierId = Id, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }

    /// <summary>The code is immutable once created; <paramref name="dto"/>'s code is ignored.</summary>
    public void Update(SupplierDto dto)
    {
        Apply(dto);
        Raise(dto.Actor);
    }

    public void Deactivate(InventoryActor actor)
    {
        if (!IsActive) return;
        IsActive = false;
        Raise(actor);
    }

    public void Reactivate(InventoryActor actor)
    {
        if (IsActive) return;
        IsActive = true;
        Raise(actor);
    }

    private void Raise(InventoryActor actor)
    {
        AddDomainEvent(new SupplierUpdatedEvent { SupplierId = Id, IsActive = IsActive, CorrelationId = actor.CorrelationId, UserContext = actor.UserContext });
        UpdateVersion();
    }

    private void Apply(SupplierDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 200)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "A supplier name is required (at most 200 characters).", nameof(Name));
        if (!InventoryRules.IsValidCountryCode(dto.CountryCode))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidCountryCode, "The country must be a valid ISO-3166 two-letter code.", nameof(CountryCode));
        if (!InventoryRules.IsValidCurrencyCode(dto.CurrencyCode))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidCurrencyCode, "The currency must be a three-letter ISO-4217 code in upper case.", nameof(CurrencyCode));
        if (dto.LeadTimeDays is < 0 or > 365)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "Lead time must be between 0 and 365 days.", nameof(LeadTimeDays));
        if (dto.PaymentTermDays is < 0 or > 365)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "Payment term days must be between 0 and 365.", nameof(PaymentTermDays));
        if (string.IsNullOrWhiteSpace(dto.PaymentTerms) || dto.PaymentTerms.Length > 500)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "Payment terms are required (at most 500 characters).", nameof(PaymentTerms));
        if (dto.Notes is { Length: > 1000 })
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "Notes cannot exceed 1000 characters.", nameof(Notes));
        if ((dto.ContactName?.Length ?? 0) > 200 || (dto.Phone?.Length ?? 0) > 30 || (dto.Address?.Length ?? 0) > 500)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "A contact field is too long.", "Contact");

        string? email = null;
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            try { email = EmailAddress.Create(dto.Email).Value; }
            catch (Exceptions.DomainRuleViolationException) { throw InventoryViolation.Of(BusinessViolationCodes.InvalidSupplier, "The contact email is not valid.", "Contact.Email"); }
        }

        Name = dto.Name.Trim();
        CountryCode = dto.CountryCode;
        Contact = new SupplierContact(Blank(dto.ContactName), Blank(dto.Phone), email, Blank(dto.Address));
        CurrencyCode = dto.CurrencyCode;
        LeadTimeDays = dto.LeadTimeDays;
        PaymentTerms = dto.PaymentTerms.Trim();
        PaymentTermDays = dto.PaymentTermDays;
        Notes = Blank(dto.Notes);
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
