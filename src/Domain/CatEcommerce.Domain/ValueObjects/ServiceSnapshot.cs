namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-APT-001 §8.2 -- read at booking, never re-read (HO-ADM-001 ADR-049). The performer, room and
/// price a customer was shown do not silently change under them if the service catalogue changes
/// after they booked.
///
/// EF materializes an owned type nested inside another owned type (this <c>Price</c> -- a
/// <see cref="Money"/> -- inside this <c>ServiceSnapshot</c>, itself owned by
/// <c>AppointmentAggregate</c>) through a parameterless constructor plus property setters, not a
/// constructor parameter -- a record's single positional constructor cannot bind a nested owned
/// navigation. The private parameterless constructor below exists for EF only; application code
/// always uses the public constructor.
/// </summary>
public sealed record ServiceSnapshot
{
    public string Name { get; init; }
    public CatEcommerce.Shared.Common.Enums.ServiceCategory Category { get; init; }
    public int DurationMinutes { get; init; }
    public Money Price { get; init; }

    public ServiceSnapshot(string name, CatEcommerce.Shared.Common.Enums.ServiceCategory category, int durationMinutes, Money price)
    {
        Name = name;
        Category = category;
        DurationMinutes = durationMinutes;
        Price = price;
    }

    private ServiceSnapshot()
    {
        Name = default!;
        Category = default!;
        Price = default!;
    }
}
