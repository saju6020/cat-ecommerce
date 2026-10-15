namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-ORD-001 §11.2 / rule "client instants are UTC before <c>timestamptz</c>". A <see cref="DateTimeOffset"/> a client sends with an offset
/// (<c>2026-10-09T10:00:00+06:00</c>) is the same instant as its UTC form, but Npgsql refuses to write a non-zero offset to
/// <c>timestamptz</c> and the request would end as a <c>500</c>. Every command mapper that takes a client instant (<c>ReceivedOn</c>,
/// <c>DeliveredOn</c>, <c>CollectedOn</c>, …) normalises it here before it reaches an aggregate.
/// </summary>
public static class UtcInstant
{
    public static DateTimeOffset ToUtc(this DateTimeOffset instant) => instant.ToUniversalTime();

    public static DateTimeOffset? ToUtc(this DateTimeOffset? instant) => instant?.ToUniversalTime();
}
