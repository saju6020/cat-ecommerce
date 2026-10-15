namespace CatEcommerce.Domain.Entities;

/// <summary>HO-ORD-001 §8.8 -- a district a delivery zone serves, optionally narrowed to postal codes. Owned by <c>DeliveryZoneAggregate</c>
/// and replaced wholesale when the zone is updated (a <c>CurrentState</c> collection, not a record).</summary>
public sealed class ZoneCoverage
{
    public Guid Id { get; private set; }
    public string District { get; private set; } = default!;
    public IReadOnlyList<string> PostalCodes { get; private set; } = [];

    private ZoneCoverage() { }

    public ZoneCoverage(Guid id, string district, IReadOnlyList<string> postalCodes)
    {
        Id = id;
        District = district;
        PostalCodes = postalCodes;
    }
}
