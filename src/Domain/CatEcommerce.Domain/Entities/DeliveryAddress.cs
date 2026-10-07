namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-ACC-001 §8.6. Owned by <c>CustomerAddressBookAggregate</c>. No <c>IsDefault</c> column --
/// the root's single <c>DefaultAddressId</c> is the only place "default" is represented, so two
/// defaults are not representable.
/// </summary>
public sealed class DeliveryAddress
{
    public Guid Id { get; private set; }
    public string Label { get; private set; } = default!;
    public string Line1 { get; private set; } = default!;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = default!;
    public string District { get; private set; } = default!;
    public string? PostalCode { get; private set; }
    public string? DeliveryInstructions { get; private set; }
    public DateTimeOffset AddedOn { get; private set; }
    public DateTimeOffset LastChangedOn { get; private set; }

    private DeliveryAddress() { }

    public DeliveryAddress(
        Guid id, string label, string line1, string? line2, string city, string district,
        string? postalCode, string? deliveryInstructions, DateTimeOffset addedOn)
    {
        Id = id;
        Label = label;
        Line1 = line1;
        Line2 = line2;
        City = city;
        District = district;
        PostalCode = postalCode;
        DeliveryInstructions = deliveryInstructions;
        AddedOn = addedOn;
        LastChangedOn = addedOn;
    }

    internal void Update(
        string label, string line1, string? line2, string city, string district,
        string? postalCode, string? deliveryInstructions, DateTimeOffset changedOn)
    {
        Label = label;
        Line1 = line1;
        Line2 = line2;
        City = city;
        District = district;
        PostalCode = postalCode;
        DeliveryInstructions = deliveryInstructions;
        LastChangedOn = changedOn;
    }
}
