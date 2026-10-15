namespace CatEcommerce.Infrastructure.Operations;

/// <summary>
/// HO-XCT-001 section 8.7 / ADR-312. A row of <c>ops.backup_run</c>, written ONLY by the backup job (as <c>catecommerce_backup_reporter</c>, INSERT only) and
/// read here. Not an aggregate: there is no repository, no event, no audit; it is queried with <c>AsNoTracking()</c> and never tracked for change. The error
/// summary is at most 500 characters and carries no secret (the script strips the connection string and keys).
/// </summary>
public sealed class BackupRunReadModel
{
    public const int MaxErrorSummaryLength = 500;

    public Guid Id { get; init; }

    /// <summary><c>full</c>, <c>diff</c>, <c>archive-check</c> or <c>erasure-journal</c>.</summary>
    public string Kind { get; init; } = default!;

    public DateTimeOffset StartedOn { get; init; }
    public DateTimeOffset FinishedOn { get; init; }
    public bool Succeeded { get; init; }

    /// <summary><c>repo1</c> or <c>repo2</c>.</summary>
    public string Repository { get; init; } = default!;

    public string? BackupLabel { get; init; }
    public long? SizeBytes { get; init; }
    public DateTimeOffset? WalArchivedUntil { get; init; }
    public string? ErrorSummary { get; init; }
}
