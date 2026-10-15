using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Operations;

/// <summary>One named check from the drill output.</summary>
public sealed record RestoreCheckInput(string Name, bool Passed, string? Detail);

/// <summary>
/// HO-XCT-001 section 11.1 C12 -- records a restore drill (NFR-AVL-5). The achieved RPO/RTO and the outcome are computed by the aggregate; the performer is
/// the caller (<c>UserContext.UserId</c>), never a field of the request.
/// </summary>
public sealed class RecordRestoreTestCommand : Command
{
    public required Guid RestoreTestId { get; init; }
    public required DateOnly PerformedOn { get; init; }
    public required string Environment { get; init; }
    public required string BackupSetLabel { get; init; }
    public required DateTimeOffset SimulatedFailureAt { get; init; }
    public required DateTimeOffset RecoveredTo { get; init; }
    public required DateTimeOffset RestoreStartedAt { get; init; }
    public required DateTimeOffset RestoreCompletedAt { get; init; }
    public required IReadOnlyList<RestoreCheckInput> Checks { get; init; }
    public required string DrillOutput { get; init; }
    public string? Notes { get; init; }
    public Guid? SupersedesId { get; init; }
}
