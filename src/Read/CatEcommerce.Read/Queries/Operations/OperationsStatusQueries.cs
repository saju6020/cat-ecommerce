using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Operations;

/// <summary>HO-XCT-001 section 12.6 Q7 -- backup health and the restore-test due date, from the backup job log and the restore records.</summary>
public sealed class GetBackupStatusQuery : Query<BackupStatusViewModel>;

/// <summary>Q8 -- restore tests, newest first, paged.</summary>
public sealed class GetRestoreTestsQuery : Query<PagedResult<RestoreTestSummaryViewModel>>;

/// <summary>Q8 -- one restore test with its checks, the drill output (text) and the supersedes chain.</summary>
public sealed class GetRestoreTestQuery : Query<RestoreTestViewModel?>
{
    public required Guid RestoreTestId { get; init; }
}
