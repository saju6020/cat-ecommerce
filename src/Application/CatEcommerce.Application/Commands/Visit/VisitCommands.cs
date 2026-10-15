using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 §11.1 -- visit commands. Data only; ids are minted in the controller; ValidateCommand = true
// everywhere (F3). BranchId is the caller's active branch (X-Branch-Id, ADR-046): a visit of another
// branch is "not found", never a refusal that would confirm it exists (HO-ADM-001 §8.2, backlog #8).

/// <summary>C5 -- opens the visit of a checked-in appointment. Idempotent: an existing visit for the
/// appointment is returned as success (<see cref="OpenVisitResult.AlreadyExisted"/>).</summary>
public sealed class OpenVisitCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>What <see cref="OpenVisitCommand"/> returns: the visit's id and whether it already existed.</summary>
public sealed record OpenVisitResult(Guid VisitId, bool AlreadyExisted);

/// <summary>C6 -- <see cref="MeasuredOn"/> defaults to the server's now. Every measurement is optional but
/// at least one is required (aggregate rule). <see cref="ConfirmedOutOfRange"/> lists
/// <c>VitalSign</c> names the caller has seen the plausible-range warning for (ADR-120).</summary>
public sealed class RecordVitalsCommand : Command
{
    public required Guid VisitId { get; init; }
    public required Guid VitalsEntryId { get; init; }
    public required Guid BranchId { get; init; }
    public DateTimeOffset? MeasuredOn { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? TemperatureC { get; init; }
    public int? HeartRateBpm { get; init; }
    public int? RespiratoryRatePerMin { get; init; }
    public int? BodyConditionScore { get; init; }
    public int? PainScore { get; init; }
    public IReadOnlyList<string> ConfirmedOutOfRange { get; init; } = [];
    public string? Note { get; init; }
}

/// <summary>The structured detail of a <c>VitalsOutsidePlausibleRange</c> refusal (§8.3): each unconfirmed
/// sign with its value and plausible range, returned in <c>CommandResponse.Result</c> on the failure.</summary>
public sealed record OutOfRangeSign(string Sign, string Value, string Unit, string PlausibleMin, string PlausibleMax);
