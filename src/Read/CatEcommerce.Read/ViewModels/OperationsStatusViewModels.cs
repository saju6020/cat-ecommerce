namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-XCT-001 section 12.4 Q7. The latest log row of one kind of backup job run.</summary>
public sealed record BackupRunStatusViewModel(DateTimeOffset FinishedOn, bool Succeeded, string Repository, string? Label, long? SizeBytes);

public sealed record RecoveryObjectivesViewModel(int RpoMinutes, int RtoMinutes);

public sealed record LastRestoreTestViewModel(Guid Id, DateOnly PerformedOn, string Outcome, decimal AchievedRpoMinutes, decimal AchievedRtoMinutes);

public sealed record BackupStatusViewModel(
    BackupRunStatusViewModel? LastFull, BackupRunStatusViewModel? LastDiff, BackupRunStatusViewModel? LastArchiveCheck, BackupRunStatusViewModel? LastErasureJournal,
    DateTimeOffset? WalArchivedUntil, bool IsBackupStale, bool IsArchiveStale, LastRestoreTestViewModel? LastRestoreTest, DateOnly? LastPassedRestoreTestOn,
    DateOnly NextRestoreTestDueOn, bool IsRestoreTestOverdue, RecoveryObjectivesViewModel Objectives);

/// <summary>Q8.</summary>
public sealed record RestoreTestSummaryViewModel(
    Guid Id, DateOnly PerformedOn, string Outcome, decimal AchievedRpoMinutes, decimal AchievedRtoMinutes, string Environment, string BackupSetLabel, Guid? SupersedesId);

public sealed record RestoreCheckViewModel(string Name, bool Passed, string? Detail);

/// <summary><c>DrillOutput</c> is text, to be shown as text and never rendered as markup.</summary>
public sealed record RestoreTestViewModel(
    Guid Id, DateOnly PerformedOn, Guid PerformedByStaffId, string Environment, string BackupSetLabel, DateTimeOffset SimulatedFailureAt, DateTimeOffset RecoveredTo,
    DateTimeOffset RestoreStartedAt, DateTimeOffset RestoreCompletedAt, decimal AchievedRpoMinutes, decimal AchievedRtoMinutes, string Outcome,
    IReadOnlyList<RestoreCheckViewModel> Checks, string DrillOutput, string? Notes, Guid? SupersedesId, IReadOnlyList<Guid> SupersededByIds);
