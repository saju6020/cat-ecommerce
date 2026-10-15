namespace CatEcommerce.Api.Contracts.Visit;

/// <summary>HO-EMR-001 §12.4 request bodies. Ids are minted in the controller, never taken from the body.</summary>
public sealed class OpenVisitRequest
{
    public required Guid AppointmentId { get; init; }
}

public sealed class RecordVitalsRequest
{
    public DateTimeOffset? MeasuredOn { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? TemperatureC { get; init; }
    public int? HeartRateBpm { get; init; }
    public int? RespiratoryRatePerMin { get; init; }
    public int? BodyConditionScore { get; init; }
    public int? PainScore { get; init; }

    /// <summary><c>VitalSign</c> names the caller has seen the plausible-range warning for (ADR-120).</summary>
    public IReadOnlyList<string> ConfirmedOutOfRange { get; init; } = [];
    public string? Note { get; init; }
}

/// <summary>HO-EMR-001 §12.4 -- <c>PUT /api/staff/visits/{visitId}/follow-up</c>.</summary>
public sealed class SetFollowUpRequest
{
    /// <summary><c>Appointment</c> or <c>Reminder</c>.</summary>
    public required string Kind { get; init; }
    public required DateOnly DueOn { get; init; }
    public Guid? AppointmentId { get; init; }
}
