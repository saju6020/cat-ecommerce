using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ORD-001 §8.3 (ADR-206, ADR-214) -- how and where an order is fulfilled, <b>snapshotted</b> at placement: the recipient, the address as the customer
/// had it, the zone, service level and slot it was priced with, or the pickup branch. Editing the customer's address book or the zone afterwards never
/// changes a placed order. Personal data: anonymisation of the customer redacts the recipient and the address lines of a terminal order (the district is
/// kept for reporting) -- <see cref="Redacted"/>.
/// </summary>
public sealed record FulfilmentDetails
{
    public const string RedactedRecipient = "Anonymised customer";
    public const string RedactedText = "[redacted]";

    public string RecipientName { get; init; }
    public string? RecipientPhone { get; init; }
    public string? Line1 { get; init; }
    public string? Line2 { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public string? PostalCode { get; init; }
    public string? DeliveryInstructions { get; init; }
    public Guid? DeliveryZoneId { get; init; }
    public string? ZoneName { get; init; }
    public DeliveryServiceLevel? ServiceLevel { get; init; }
    public DateOnly? SlotDate { get; init; }
    public string? SlotWindowLabel { get; init; }
    public TimeOnly? SlotWindowStart { get; init; }
    public TimeOnly? SlotWindowEnd { get; init; }
    public Guid? PickupBranchId { get; init; }

    public FulfilmentDetails(
        string recipientName, string? recipientPhone, string? line1, string? line2, string? city, string? district, string? postalCode, string? deliveryInstructions,
        Guid? deliveryZoneId, string? zoneName, DeliveryServiceLevel? serviceLevel, DateOnly? slotDate, string? slotWindowLabel, TimeOnly? slotWindowStart,
        TimeOnly? slotWindowEnd, Guid? pickupBranchId)
    {
        RecipientName = recipientName;
        RecipientPhone = recipientPhone;
        Line1 = line1;
        Line2 = line2;
        City = city;
        District = district;
        PostalCode = postalCode;
        DeliveryInstructions = deliveryInstructions;
        DeliveryZoneId = deliveryZoneId;
        ZoneName = zoneName;
        ServiceLevel = serviceLevel;
        SlotDate = slotDate;
        SlotWindowLabel = slotWindowLabel;
        SlotWindowStart = slotWindowStart;
        SlotWindowEnd = slotWindowEnd;
        PickupBranchId = pickupBranchId;
    }

    private FulfilmentDetails() => RecipientName = default!;

    /// <summary>The same order with its personal data removed (D-11): recipient, phone, address lines and instructions are replaced; the district, zone, level
    /// and slot -- reporting facts -- are kept.</summary>
    public FulfilmentDetails Redacted() => this with
    {
        RecipientName = RedactedRecipient,
        RecipientPhone = RecipientPhone is null ? null : RedactedText,
        Line1 = Line1 is null ? null : RedactedText,
        Line2 = Line2 is null ? null : RedactedText,
        City = City is null ? null : RedactedText,
        PostalCode = PostalCode is null ? null : RedactedText,
        DeliveryInstructions = DeliveryInstructions is null ? null : RedactedText,
    };
}
