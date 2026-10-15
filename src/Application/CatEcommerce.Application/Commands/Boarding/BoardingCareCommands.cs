using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

// HO-BRD-001 section 11.1 C12, C14-C17 (Phase E). Staff commands carry the active BranchId; the worker's GenerateCareTasks and the reassign events carry none.

public sealed record BelongingItemInput(string Kind, string Description, IReadOnlyList<Guid> FileIds);

public sealed record SignatureInput(string SignerName, string Method, string? Strokes, bool ConfirmedStatement);

public sealed record CheckInCatInput(
    Guid CatId, bool IdentityConfirmed, bool VaccinationProofVerified, decimal? WeightKg, string? ArrivalHealthObservation,
    IReadOnlyList<BelongingItemInput> Belongings, bool NoBelongings, SignatureInput? Signature);

public sealed class CheckInBoardingStayCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required IReadOnlyList<CheckInCatInput> Cats { get; init; }
}

/// <summary>C14 -- worker only. Creates the tasks of one local date that do not exist yet; idempotent.</summary>
public sealed class GenerateCareTasksCommand : Command
{
    public required Guid StayId { get; init; }
    public required DateOnly LocalDate { get; init; }
}

public sealed class CompleteCareTaskCommand : Command
{
    public required Guid TaskId { get; init; }
    public required Guid BranchId { get; init; }

    /// <summary>Optional: the request needs no body (the tablet's second tap is the whole request).</summary>
    public string? Note { get; init; }
}

public sealed class SkipCareTaskCommand : Command
{
    public required Guid TaskId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

public sealed class RecordCareObservationCommand : Command
{
    public required Guid ObservationId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required string PortionEaten { get; init; }
    public required string WaterIntake { get; init; }
    public required string LitterOutput { get; init; }
    public required string Mood { get; init; }
    public string? Notes { get; init; }
}

public sealed class FlagSuspectedInfectiousCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Note { get; init; }
    public Guid? TargetIsolationUnitId { get; init; }
}

public sealed class ClearSuspectedInfectiousCommand : Command
{
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public string? Note { get; init; }
}

public sealed class RecordIsolationEntryCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Purpose { get; init; }
}

public sealed class RecordIsolationExitCommand : Command
{
    public required Guid EntryId { get; init; }
    public required Guid BranchId { get; init; }
}
