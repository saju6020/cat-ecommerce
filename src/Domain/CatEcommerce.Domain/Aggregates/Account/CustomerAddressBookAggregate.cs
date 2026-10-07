using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Account;

/// <summary>
/// HO-ACC-001 §8.6 / ADR-035. Id is the customer id -- a 1:1 shared key, created on first write.
/// No <c>IsDefault</c> column: <see cref="DefaultAddressId"/> is the only place "default" is
/// represented, so two defaults are not representable.
/// </summary>
public sealed class CustomerAddressBookAggregate : AggregateRoot
{
    private readonly List<DeliveryAddress> _addresses = [];

    public Guid? DefaultAddressId { get; private set; }
    public IReadOnlyList<DeliveryAddress> Addresses => _addresses;

    private CustomerAddressBookAggregate() { }

    public CustomerAddressBookAggregate(Guid id) : base(id) { }

    public void Open(CustomerAddressBookOpenDto dto)
    {
        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new CustomerAddressBookOpenedEvent
        {
            CustomerId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
        EnsureDefaultIsConsistent();
    }

    public void AddAddress(DeliveryAddressAddDto dto)
    {
        if (_addresses.Count >= DeliveryAddressRules.MaxAddresses)
            throw Violation(BusinessViolationCodes.DeliveryAddressLimitExceeded,
                $"No more than {DeliveryAddressRules.MaxAddresses} addresses are allowed.", nameof(dto.AddressId));

        ValidateFields(dto.Label, dto.Line1, dto.Line2, dto.City, dto.District, dto.PostalCode, dto.DeliveryInstructions);

        var becomesDefault = _addresses.Count == 0;

        _addresses.Add(new DeliveryAddress(
            dto.AddressId, dto.Label, dto.Line1, dto.Line2, dto.City, dto.District,
            dto.PostalCode, dto.DeliveryInstructions, dto.AddedOn));

        if (becomesDefault) DefaultAddressId = dto.AddressId;

        AddDomainEvent(new DeliveryAddressAddedEvent
        {
            CustomerId = Id,
            AddressId = dto.AddressId,
            BecameDefault = becomesDefault,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
        EnsureDefaultIsConsistent();
    }

    public void UpdateAddress(DeliveryAddressUpdateDto dto)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == dto.AddressId)
            ?? throw Violation(BusinessViolationCodes.DeliveryAddressNotFound, "This delivery address was not found.", nameof(dto.AddressId));

        ValidateFields(dto.Label, dto.Line1, dto.Line2, dto.City, dto.District, dto.PostalCode, dto.DeliveryInstructions);

        address.Update(dto.Label, dto.Line1, dto.Line2, dto.City, dto.District, dto.PostalCode, dto.DeliveryInstructions, dto.ChangedOn);

        AddDomainEvent(new DeliveryAddressUpdatedEvent
        {
            CustomerId = Id,
            AddressId = dto.AddressId,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
        EnsureDefaultIsConsistent();
    }

    public void RemoveAddress(DeliveryAddressRemoveDto dto)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == dto.AddressId)
            ?? throw Violation(BusinessViolationCodes.DeliveryAddressNotFound, "This delivery address was not found.", nameof(dto.AddressId));

        if (DefaultAddressId == dto.AddressId && _addresses.Count > 1)
            throw Violation(BusinessViolationCodes.DefaultAddressCannotBeRemoved,
                "The default address cannot be removed while other addresses exist. Set a different default first.", nameof(dto.AddressId));

        _addresses.Remove(address);
        if (DefaultAddressId == dto.AddressId) DefaultAddressId = null;

        AddDomainEvent(new DeliveryAddressRemovedEvent
        {
            CustomerId = Id,
            AddressId = dto.AddressId,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
        EnsureDefaultIsConsistent();
    }

    public void SetDefaultAddress(DeliveryAddressSetDefaultDto dto)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == dto.AddressId)
            ?? throw Violation(BusinessViolationCodes.DeliveryAddressNotFound, "This delivery address was not found.", nameof(dto.AddressId));

        if (DefaultAddressId == address.Id) return; // already default -- no event, no version bump

        DefaultAddressId = address.Id;

        AddDomainEvent(new DefaultDeliveryAddressChangedEvent
        {
            CustomerId = Id,
            AddressId = address.Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
        EnsureDefaultIsConsistent();
    }

    /// <summary>Anonymisation only (§8.10a).</summary>
    public void Clear(CustomerAddressBookClearDto dto)
    {
        _addresses.Clear();
        DefaultAddressId = null;

        AddDomainEvent(new CustomerAddressBookClearedEvent
        {
            CustomerId = Id,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
        EnsureDefaultIsConsistent();
    }

    private static void ValidateFields(
        string label, string line1, string? line2, string city, string district, string? postalCode, string? deliveryInstructions)
    {
        if (string.IsNullOrWhiteSpace(label) || label.Length > DeliveryAddressRules.LabelMaxLength) throw InvalidAddress(nameof(label));
        if (string.IsNullOrWhiteSpace(line1) || line1.Length > DeliveryAddressRules.Line1MaxLength) throw InvalidAddress(nameof(line1));
        if (line2 is not null && line2.Length > DeliveryAddressRules.Line2MaxLength) throw InvalidAddress(nameof(line2));
        if (string.IsNullOrWhiteSpace(city) || city.Length > DeliveryAddressRules.CityMaxLength) throw InvalidAddress(nameof(city));
        if (string.IsNullOrWhiteSpace(district) || district.Length > DeliveryAddressRules.DistrictMaxLength) throw InvalidAddress(nameof(district));
        if (postalCode is not null && postalCode.Length > DeliveryAddressRules.PostalCodeMaxLength) throw InvalidAddress(nameof(postalCode));
        if (deliveryInstructions is not null && deliveryInstructions.Length > DeliveryAddressRules.DeliveryInstructionsMaxLength) throw InvalidAddress(nameof(deliveryInstructions));
    }

    private static DomainRuleViolationException InvalidAddress(string property) =>
        new(BusinessViolationCodes.InvalidDeliveryAddress, "This delivery address is invalid.", property);

    private static DomainRuleViolationException Violation(string code, string message, string property) =>
        new(code, message, property);

    /// <summary><c>UpdateVersion()</c> on the root is load-bearing: it makes EF update the root
    /// row, bringing its <c>xmin</c> into play, so two concurrent adds at nine addresses cannot
    /// leave eleven.</summary>
    private void EnsureDefaultIsConsistent()
    {
        if ((_addresses.Count == 0) != (DefaultAddressId is null))
            throw new InvalidOperationException("The address book's default address is inconsistent with its address list.");
    }
}
