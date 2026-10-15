using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Operations;

/// <summary>HO-XCT-001 section 12.6 Q3 -- the retention schedule: every record class with its policy, period and action at the end. Settings only.</summary>
public sealed class GetRetentionScheduleQuery : Query<RetentionScheduleViewModel>;

/// <summary>Q4 -- retention runs, newest first, paged.</summary>
public sealed class GetRetentionRunsQuery : Query<PagedResult<RetentionRunSummaryViewModel>>;

/// <summary>Q4 -- one run with every pass.</summary>
public sealed class GetRetentionRunQuery : Query<RetentionRunViewModel?>
{
    public required Guid RetentionRunId { get; init; }
}

/// <summary>Q5 -- records past their statutory period, counted by every registered review source. Nothing is deleted (ADR-301).</summary>
public sealed class GetRetentionReviewQuery : Query<RetentionReviewViewModel>;
