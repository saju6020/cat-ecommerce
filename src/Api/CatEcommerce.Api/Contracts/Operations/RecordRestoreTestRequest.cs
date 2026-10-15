namespace CatEcommerce.Api.Contracts.Operations;

public sealed class RestoreCheckRequest
{
    public string? Name { get; init; }
    public bool Passed { get; init; }
    public string? Detail { get; init; }
}

/// <summary>
/// HO-XCT-001 section 12.4 -- <c>POST /api/admin/operations/restore-tests</c>. The fields of the drill script output (<c>restore-drill.sh</c>). There is no
/// outcome, RPO, RTO or performer: the system computes the first three and takes the performer from the signed-in caller.
/// </summary>
public sealed class RecordRestoreTestRequest
{
    public DateOnly PerformedOn { get; init; }
    public string? Environment { get; init; }
    public string? BackupSetLabel { get; init; }
    public DateTimeOffset SimulatedFailureAt { get; init; }
    public DateTimeOffset RecoveredTo { get; init; }
    public DateTimeOffset RestoreStartedAt { get; init; }
    public DateTimeOffset RestoreCompletedAt { get; init; }
    public List<RestoreCheckRequest>? Checks { get; init; }

    /// <summary>The drill script JSON, pasted. Stored as text.</summary>
    public string? DrillOutput { get; init; }

    public string? Notes { get; init; }
    public Guid? SupersedesId { get; init; }
}
