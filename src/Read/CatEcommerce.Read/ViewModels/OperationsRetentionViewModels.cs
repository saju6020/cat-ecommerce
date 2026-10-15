namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-XCT-001 section 12.4 Q3. <c>EffectivePeriod</c> is null where the class has no period (kept indefinitely, or erased on request).</summary>
public sealed record RetentionScheduleTableViewModel(string Schema, string Table, string HardDeletePolicy, string Basis);

public sealed record RetentionScheduleClassViewModel(
    string RecordClass, IReadOnlyList<string> HardDeletePolicies, IReadOnlyList<string> Bases, int? EffectivePeriod, string? PeriodUnit,
    string ActionAtEnd, int TableCount, IReadOnlyList<RetentionScheduleTableViewModel> Tables);

public sealed record RetentionScheduleViewModel(IReadOnlyList<RetentionScheduleClassViewModel> Classes);

/// <summary>Q4. Counts and codes only.</summary>
public sealed record RetentionPassViewModel(
    string Kind, DateTimeOffset? Cutoff, int Examined, int Affected, int Held, int Failed, IReadOnlyList<string> ErrorCodes, int DurationMs);

public sealed record RetentionRunSummaryViewModel(
    Guid RetentionRunId, string Trigger, string Status, DateTimeOffset RequestedOn, DateTimeOffset? StartedOn, DateTimeOffset? FinishedOn,
    int PassCount, int TotalAffected, int TotalFailed);

public sealed record RetentionRunViewModel(
    Guid RetentionRunId, string Trigger, string Status, DateTimeOffset RequestedOn, DateTimeOffset? StartedOn, DateTimeOffset? FinishedOn,
    IReadOnlyList<RetentionPassViewModel> Passes);

/// <summary>Q5.</summary>
public sealed record RetentionReviewRowViewModel(string RecordClass, string RecordKind, int Count, DateOnly? OldestAnchor, string SourceKey);

public sealed record RetentionReviewViewModel(DateOnly AsOf, IReadOnlyList<RetentionReviewRowViewModel> Rows, string Note);
