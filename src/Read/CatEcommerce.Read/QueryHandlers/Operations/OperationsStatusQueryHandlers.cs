using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Operations;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Operations;

/// <summary>
/// HO-XCT-001 section 12.6 Q7. The latest log row per kind, staleness from <see cref="RecoveryObjectives"/> and the injected clock, and the restore-test due
/// date from the last <c>Passed</c> record plus <c>operations.restoreTestIntervalDays</c> (a failed test does not reset it; no passed test ever means due today
/// and overdue). Explicit projections, <c>AsNoTracking()</c>.
/// </summary>
public sealed class GetBackupStatusQueryHandler(CatEcommerceDbContext db, IOperationsClock clock, IBusinessSettings settings)
    : IQueryHandlerAsync<GetBackupStatusQuery, BackupStatusViewModel>
{
    public async Task<QueryResponse<BackupStatusViewModel>> HandleAsync(GetBackupStatusQuery query)
    {
        var time = await clock.NowAsync(CancellationToken.None);

        var recent = await db.BackupRuns.AsNoTracking()
            .Where(r => r.FinishedOn > time.Utc.AddDays(-60))
            .Select(r => new { r.Kind, r.FinishedOn, r.Succeeded, r.Repository, r.BackupLabel, r.SizeBytes, r.WalArchivedUntil })
            .ToListAsync();

        BackupRunStatusViewModel? Latest(string kind) => recent.Where(r => r.Kind == kind).OrderByDescending(r => r.FinishedOn)
            .Select(r => new BackupRunStatusViewModel(r.FinishedOn, r.Succeeded, r.Repository, r.BackupLabel, r.SizeBytes)).FirstOrDefault();

        var lastBackupSuccess = recent.Where(r => r.Succeeded && (r.Kind == "full" || r.Kind == "diff")).Select(r => (DateTimeOffset?)r.FinishedOn).Max();
        var lastArchive = recent.Where(r => r.Succeeded && r.Kind == "archive-check").OrderByDescending(r => r.FinishedOn).FirstOrDefault();

        var isBackupStale = lastBackupSuccess is null || time.Utc - lastBackupSuccess.Value > TimeSpan.FromHours(RecoveryObjectives.BackupStaleHours);
        var archiveLimit = TimeSpan.FromMinutes(RecoveryObjectives.ArchiveStaleMinutes);
        var isArchiveStale = lastArchive is null || time.Utc - lastArchive.FinishedOn > archiveLimit
            || (lastArchive.WalArchivedUntil is { } walUntil && time.Utc - walUntil > archiveLimit);

        var lastTest = await db.RestoreTestRecords.AsNoTracking()
            .OrderByDescending(t => t.PerformedOn).ThenByDescending(t => t.CreatedDate)
            .Select(t => new LastRestoreTestViewModel(t.Id, t.PerformedOn, t.Outcome.DisplayName, t.AchievedRpoMinutes, t.AchievedRtoMinutes))
            .FirstOrDefaultAsync();
        var lastPassed = await db.RestoreTestRecords.AsNoTracking()
            .Where(t => t.Outcome == RestoreTestOutcome.Passed)
            .Select(t => (DateOnly?)t.PerformedOn).OrderByDescending(d => d).FirstOrDefaultAsync();

        var due = NextDueOn(lastPassed, settings.GetInt(SettingKeys.OperationsRestoreTestIntervalDays), time.LocalDate);

        return new QueryResponse<BackupStatusViewModel>
        {
            Result = new BackupStatusViewModel(
                Latest("full"), Latest("diff"), Latest("archive-check"), Latest("erasure-journal"),
                lastArchive?.WalArchivedUntil, isBackupStale, isArchiveStale, lastTest, lastPassed, due, due <= time.LocalDate,
                new RecoveryObjectivesViewModel(RecoveryObjectives.RpoMinutes, RecoveryObjectives.RtoMinutes))
        };
    }

    /// <summary>Public for the unit tests (U-F7). Last passed test plus the interval; never passed means due today.</summary>
    public static DateOnly NextDueOn(DateOnly? lastPassedOn, int intervalDays, DateOnly today) =>
        lastPassedOn is { } last ? last.AddDays(intervalDays) : today;
}

/// <summary>Q8 list -- explicit projection, paged, newest first.</summary>
public sealed class GetRestoreTestsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRestoreTestsQuery, PagedResult<RestoreTestSummaryViewModel>>
{
    public async Task<QueryResponse<PagedResult<RestoreTestSummaryViewModel>>> HandleAsync(GetRestoreTestsQuery query)
    {
        var tests = db.RestoreTestRecords.AsNoTracking();
        var total = await tests.CountAsync();

        var rows = await tests.OrderByDescending(t => t.PerformedOn).ThenByDescending(t => t.CreatedDate).ThenBy(t => t.Id)
            .Skip(query.Skip).Take(query.Take)
            .Select(t => new { t.Id, t.PerformedOn, t.Outcome, t.AchievedRpoMinutes, t.AchievedRtoMinutes, t.Environment, t.BackupSetLabel, t.SupersedesId })
            .ToListAsync();

        var items = rows.Select(t => new RestoreTestSummaryViewModel(
            t.Id, t.PerformedOn, t.Outcome.DisplayName, t.AchievedRpoMinutes, t.AchievedRtoMinutes, t.Environment, t.BackupSetLabel, t.SupersedesId)).ToList();

        return new QueryResponse<PagedResult<RestoreTestSummaryViewModel>> { Result = new PagedResult<RestoreTestSummaryViewModel>(items, query.PageNumber, query.PageSize, total) };
    }
}

/// <summary>Q8 detail -- checks, drill output and the supersedes chain (the record it corrects, and the records that correct it).</summary>
public sealed class GetRestoreTestQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetRestoreTestQuery, RestoreTestViewModel?>
{
    public async Task<QueryResponse<RestoreTestViewModel?>> HandleAsync(GetRestoreTestQuery query)
    {
        var row = await db.RestoreTestRecords.AsNoTracking()
            .Where(t => t.Id == query.RestoreTestId)
            .Select(t => new
            {
                t.Id, t.PerformedOn, t.PerformedByStaffId, t.Environment, t.BackupSetLabel, t.SimulatedFailureAt, t.RecoveredTo, t.RestoreStartedAt, t.RestoreCompletedAt,
                t.AchievedRpoMinutes, t.AchievedRtoMinutes, t.Outcome, t.DrillOutput, t.Notes, t.SupersedesId,
                Checks = t.Checks.Select(c => new { c.Name, c.Passed, c.Detail }).ToList(),
            })
            .FirstOrDefaultAsync();

        if (row is null) return new QueryResponse<RestoreTestViewModel?> { Result = null };

        var supersededBy = await db.RestoreTestRecords.AsNoTracking().Where(t => t.SupersedesId == row.Id).OrderBy(t => t.PerformedOn).Select(t => t.Id).ToListAsync();

        return new QueryResponse<RestoreTestViewModel?>
        {
            Result = new RestoreTestViewModel(
                row.Id, row.PerformedOn, row.PerformedByStaffId, row.Environment, row.BackupSetLabel, row.SimulatedFailureAt, row.RecoveredTo, row.RestoreStartedAt,
                row.RestoreCompletedAt, row.AchievedRpoMinutes, row.AchievedRtoMinutes, row.Outcome.DisplayName,
                row.Checks.Select(c => new RestoreCheckViewModel(c.Name, c.Passed, c.Detail)).ToList(), row.DrillOutput, row.Notes, row.SupersedesId, supersededBy)
        };
    }
}
