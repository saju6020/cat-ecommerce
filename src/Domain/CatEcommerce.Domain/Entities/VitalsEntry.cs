using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-EMR-001 §8.2/§8.3 -- one set of measurements. <see cref="Id"/> is also the id of the cat's
/// weight record when <see cref="WeightKg"/> is present (ADR-121: the shared id is the link).
/// <see cref="ConfirmedOutOfRange"/> stores which signs fell outside their plausible range and were
/// confirmed by the person recording them (ADR-120), as <see cref="VitalSign"/> values.
/// </summary>
public sealed class VitalsEntry
{
    public Guid Id { get; private set; }
    public DateTimeOffset MeasuredOn { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? TemperatureC { get; private set; }
    public int? HeartRateBpm { get; private set; }
    public int? RespiratoryRatePerMin { get; private set; }
    public int? BodyConditionScore { get; private set; }
    public int? PainScore { get; private set; }
    public int[] ConfirmedOutOfRange { get; private set; } = [];
    public string? Note { get; private set; }
    public Guid RecordedBy { get; private set; }
    public DateTimeOffset RecordedOn { get; private set; }

    private VitalsEntry() { }

    public VitalsEntry(
        Guid id, DateTimeOffset measuredOn, decimal? weightKg, decimal? temperatureC, int? heartRateBpm,
        int? respiratoryRatePerMin, int? bodyConditionScore, int? painScore, IEnumerable<VitalSign> confirmedOutOfRange,
        string? note, Guid recordedBy, DateTimeOffset recordedOn)
    {
        Id = id;
        // timestamptz accepts only offset 0: normalise whatever offset the client sent (same instant).
        MeasuredOn = measuredOn.ToUniversalTime();
        
        WeightKg = weightKg;
        TemperatureC = temperatureC;
        HeartRateBpm = heartRateBpm;
        RespiratoryRatePerMin = respiratoryRatePerMin;
        BodyConditionScore = bodyConditionScore;
        PainScore = painScore;
        ConfirmedOutOfRange = confirmedOutOfRange.Select(s => s.Value).Distinct().OrderBy(v => v).ToArray();
        Note = note;
        RecordedBy = recordedBy;
        RecordedOn = recordedOn;
    }

    public IReadOnlyList<VitalSign> ConfirmedSigns() =>
        ConfirmedOutOfRange.Select(Enumeration.FromValue<VitalSign>).ToList();
}
