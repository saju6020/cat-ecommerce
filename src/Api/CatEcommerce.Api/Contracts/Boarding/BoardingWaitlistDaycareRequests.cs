namespace CatEcommerce.Api.Contracts.Boarding;

// HO-BRD-001 section 12.4 (Phase H) request bodies.

public sealed class JoinWaitlistRequest
{
    public Guid BranchId { get; init; }
    public DateOnly ArrivalOn { get; init; }
    public DateOnly DepartureOn { get; init; }
    public int CatCount { get; init; }

    /// <summary>A unit type name, or omitted for any.</summary>
    public string? UnitType { get; init; }
}

public sealed class BookDaycareRequest
{
    public Guid BranchId { get; init; }
    public Guid CatId { get; init; }
    public DateOnly LocalDate { get; init; }

    /// <summary>Hourly or Day.</summary>
    public string Basis { get; init; } = default!;
    public int? ExpectedHours { get; init; }
}

/// <summary>Reception books for a customer (the branch is the active branch).</summary>
public sealed class StaffBookDaycareRequest
{
    public Guid CustomerId { get; init; }
    public Guid CatId { get; init; }
    public DateOnly LocalDate { get; init; }
    public string Basis { get; init; } = default!;
    public int? ExpectedHours { get; init; }
}

public sealed class SetDaycareRateCardRequest
{
    public string HourlyRate { get; init; } = default!;
    public string DayRate { get; init; } = default!;
    public Guid TaxClassId { get; init; }
    public DateTimeOffset EffectiveFrom { get; init; }
}
