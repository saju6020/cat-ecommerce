namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-ORD-001 §8.3 (US-ORD-11) -- how an order left the shop: the courier (name snapshotted), the tracking number (manual -- there is no courier API,
/// SRS Q-4) and the tracking link built from the courier's https template, plus the last status a webhook reported. Replaced as a whole when tracking
/// is added or an event arrives.
/// </summary>
public sealed record Shipment
{
    public Guid CourierId { get; init; }
    public string CourierName { get; init; }
    public string? TrackingNumber { get; init; }
    public string? TrackingUrl { get; init; }
    public DateTimeOffset DispatchedOn { get; init; }
    public Guid DispatchedBy { get; init; }
    public string? LastTrackingStatus { get; init; }
    public DateTimeOffset? LastTrackingEventOn { get; init; }

    public Shipment(
        Guid courierId, string courierName, string? trackingNumber, string? trackingUrl, DateTimeOffset dispatchedOn, Guid dispatchedBy,
        string? lastTrackingStatus, DateTimeOffset? lastTrackingEventOn)
    {
        CourierId = courierId;
        CourierName = courierName;
        TrackingNumber = trackingNumber;
        TrackingUrl = trackingUrl;
        DispatchedOn = dispatchedOn;
        DispatchedBy = dispatchedBy;
        LastTrackingStatus = lastTrackingStatus;
        LastTrackingEventOn = lastTrackingEventOn;
    }

    private Shipment() => CourierName = default!;
}
