using System.Globalization;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Reporting;

/// <summary>Labels and zones shared by the schedule reads.</summary>
internal static class ReportScheduleReads
{
    public static string FrequencyLabel(ReportFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, bool lastDayOfMonth)
    {
        if (frequency == ReportFrequency.Weekly) return $"Weekly on {dayOfWeek}";
        if (frequency == ReportFrequency.Monthly) return lastDayOfMonth ? "Monthly on the last day" : $"Monthly on day {dayOfMonth}";
        return "Daily";
    }

    public static string SendAt(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static TimeZoneInfo Zone(string? timeZoneId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(string.IsNullOrWhiteSpace(timeZoneId) ? BillingRules.DefaultTimeZoneId : timeZoneId); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById(BillingRules.DefaultTimeZoneId); }
    }

    /// <summary>Every branch's name and zone, keyed by id (a handful of rows).</summary>
    public static async Task<Dictionary<Guid, (string Name, string TimeZoneId)>> BranchesAsync(CatEcommerceDbContext db, CancellationToken ct) =>
        (await db.Set<BranchAggregate>().AsNoTracking().Select(b => new { b.Id, b.Name, b.TimeZoneId }).ToListAsync(ct)).ToDictionary(b => b.Id, b => (b.Name, b.TimeZoneId));

    public static (string Name, string TimeZoneId) BranchOf(Dictionary<Guid, (string Name, string TimeZoneId)> branches, Guid? branchId)
    {
        if (branchId is { } id && branches.TryGetValue(id, out var branch)) return branch;
        var main = branches.GetValueOrDefault(AdministrationSeed.MainBranchId, (ReportRules.AllBranchesName, BillingRules.DefaultTimeZoneId));
        return branchId is null ? (ReportRules.AllBranchesName, main.Item2) : ("Unknown branch", main.Item2);
    }
}

/// <summary>HO-RPT-001 section 12.4 -- the schedules list.</summary>
public sealed class GetReportSchedulesQueryHandler(CatEcommerceDbContext db, IReportCatalogue catalogue) : IQueryHandlerAsync<GetReportSchedulesQuery, PagedResult<ReportScheduleListItemViewModel>>
{
    public async Task<QueryResponse<PagedResult<ReportScheduleListItemViewModel>>> HandleAsync(GetReportSchedulesQuery query)
    {
        var schedules = db.Set<ReportScheduleAggregate>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = Enumeration.GetAll<ReportScheduleStatus>().FirstOrDefault(s => string.Equals(s.DisplayName, query.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (status is null) return Result(new PagedResult<ReportScheduleListItemViewModel>([], 1, query.PageSize, 0));
            schedules = schedules.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.ReportKey)) schedules = schedules.Where(s => s.ReportKey == query.ReportKey);

        var pageSize = Math.Clamp(query.PageSize <= 0 ? 50 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);
        var total = await schedules.CountAsync();
        var page = await schedules.OrderBy(s => s.Name).ThenBy(s => s.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.Name, s.ReportKey, s.BranchId, s.Frequency, s.DayOfWeek, s.DayOfMonth, s.LastDayOfMonth, s.SendAtLocal, s.Format,
                RecipientCount = s.RecipientIds.Count, s.Status, s.NextRunOn, s.LastRunOn
            })
            .ToListAsync();

        var ids = page.Select(s => s.Id).ToList();
        var runs = await db.Set<ScheduledReportRunAggregate>().AsNoTracking().Where(r => ids.Contains(r.ScheduleId))
            .Select(r => new { r.ScheduleId, r.Status, r.GeneratedOn }).ToListAsync();
        var latest = runs.GroupBy(r => r.ScheduleId).ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.GeneratedOn).First().Status.DisplayName);

        var branches = await ReportScheduleReads.BranchesAsync(db, CancellationToken.None);
        var items = page.Select(s =>
        {
            var (branchName, zoneId) = ReportScheduleReads.BranchOf(branches, s.BranchId);
            return new ReportScheduleListItemViewModel(
                s.Id, s.Name, s.ReportKey, catalogue.Find(s.ReportKey)?.Title ?? s.ReportKey, branchName,
                ReportScheduleReads.FrequencyLabel(s.Frequency, s.DayOfWeek, s.DayOfMonth, s.LastDayOfMonth), ReportScheduleReads.SendAt(s.SendAtLocal), zoneId, s.Format.DisplayName,
                s.RecipientCount, s.Status.DisplayName, s.NextRunOn, s.LastRunOn, latest.GetValueOrDefault(s.Id));
        }).ToList();

        return Result(new PagedResult<ReportScheduleListItemViewModel>(items, pageNumber, pageSize, total));
    }

    private static QueryResponse<PagedResult<ReportScheduleListItemViewModel>> Result(PagedResult<ReportScheduleListItemViewModel> result) => new() { Result = result };
}

/// <summary>One schedule in full. A recipient is "still permitted" while their account exists and holds the report's category action (and, for a branch schedule, is assigned to the branch).</summary>
public sealed class GetReportScheduleQueryHandler(CatEcommerceDbContext db, IReportCatalogue catalogue) : IQueryHandlerAsync<GetReportScheduleQuery, ReportScheduleDetailViewModel?>
{
    public async Task<QueryResponse<ReportScheduleDetailViewModel?>> HandleAsync(GetReportScheduleQuery query)
    {
        var schedule = await db.Set<ReportScheduleAggregate>().AsNoTracking().Where(s => s.Id == query.ScheduleId)
            .Select(s => new
            {
                s.Id, s.Version, s.Name, s.ReportKey, s.BranchId, s.Period, s.Parameters, s.Format, s.Frequency, s.DayOfWeek, s.DayOfMonth, s.LastDayOfMonth, s.SendAtLocal,
                s.RecipientIds, s.Status, s.NextRunOn, s.LastRunOn
            })
            .FirstOrDefaultAsync();
        if (schedule is null) return new QueryResponse<ReportScheduleDetailViewModel?> { Result = null };

        var definition = catalogue.Find(schedule.ReportKey);
        var ids = schedule.RecipientIds.ToList();
        var staff = await db.StaffAccounts.AsNoTracking().Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.FullName, HasEmail = s.EmailAddress != null, s.RoleIds }).ToListAsync();
        var assignments = (await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking().Where(a => ids.Contains(a.Id)).Select(a => new { a.Id, a.BranchIds }).ToListAsync())
            .ToDictionary(a => a.Id, a => (IReadOnlyCollection<Guid>)a.BranchIds.ToList());

        var recipients = ids.Select(id =>
        {
            var account = staff.FirstOrDefault(s => s.Id == id);
            var permitted = account is not null && definition is not null
                && ReportAccessPolicy.CanReceive(definition, account.RoleIds.ToList(), schedule.BranchId, assignments.GetValueOrDefault(id) ?? []);
            return new ReportScheduleRecipientViewModel(id, account?.FullName ?? "Unknown account", account?.HasEmail ?? false, permitted);
        }).ToList();

        var branches = await ReportScheduleReads.BranchesAsync(db, CancellationToken.None);
        var (branchName, zoneId) = ReportScheduleReads.BranchOf(branches, schedule.BranchId);

        var detail = new ReportScheduleDetailViewModel(
            schedule.Id, schedule.Version, schedule.Name, schedule.ReportKey, definition?.Title ?? schedule.ReportKey, schedule.BranchId, branchName, schedule.Period.DisplayName,
            schedule.Parameters, schedule.Format.DisplayName, schedule.Frequency.DisplayName, schedule.DayOfWeek?.ToString(), schedule.DayOfMonth, schedule.LastDayOfMonth,
            ReportScheduleReads.FrequencyLabel(schedule.Frequency, schedule.DayOfWeek, schedule.DayOfMonth, schedule.LastDayOfMonth), ReportScheduleReads.SendAt(schedule.SendAtLocal), zoneId,
            recipients, schedule.Status.DisplayName, schedule.NextRunOn, schedule.LastRunOn);

        return new QueryResponse<ReportScheduleDetailViewModel?> { Result = detail };
    }
}

/// <summary>A schedule's runs, newest first, each with the delivery status of every email it produced (HO-NTF-001's notification rows, read).</summary>
public sealed class GetReportScheduleRunsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetReportScheduleRunsQuery, PagedResult<ReportScheduleRunViewModel>?>
{
    public async Task<QueryResponse<PagedResult<ReportScheduleRunViewModel>?>> HandleAsync(GetReportScheduleRunsQuery query)
    {
        if (!await db.Set<ReportScheduleAggregate>().AsNoTracking().AnyAsync(s => s.Id == query.ScheduleId))
            return new QueryResponse<PagedResult<ReportScheduleRunViewModel>?> { Result = null };

        var runs = db.Set<ScheduledReportRunAggregate>().AsNoTracking().Where(r => r.ScheduleId == query.ScheduleId);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 200);
        var pageNumber = Math.Max(query.PageNumber, 1);
        var total = await runs.CountAsync();
        var page = await runs.OrderByDescending(r => r.GeneratedOn).ThenBy(r => r.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.DueOn, r.Trigger, r.Status, r.PeriodFrom, r.PeriodTo, r.Format, r.RowCount, r.ContentLength, r.AttachmentOmitted, r.FailureCode, r.GeneratedOn, r.PurgedOn
            })
            .ToListAsync();

        var runIds = page.Select(r => r.Id).ToList();
        var subject = NotificationSubjectKind.ScheduledReport;
        var notifications = await db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.SubjectKind == subject && n.SubjectId != null && runIds.Contains(n.SubjectId.Value))
            .Select(n => new { n.SubjectId, n.RecipientId, n.Status })
            .ToListAsync();

        var recipientIds = notifications.Select(n => n.RecipientId).Distinct().ToList();
        var names = await db.StaffAccounts.AsNoTracking().Where(s => recipientIds.Contains(s.Id)).Select(s => new { s.Id, s.FullName }).ToDictionaryAsync(s => s.Id, s => s.FullName);

        var items = page.Select(r => new ReportScheduleRunViewModel(
            r.Id, r.DueOn, r.Trigger.DisplayName, r.Status.DisplayName, r.PeriodFrom, r.PeriodTo, r.Format.DisplayName, r.RowCount, r.ContentLength, r.AttachmentOmitted, r.FailureCode,
            r.GeneratedOn, r.PurgedOn,
            notifications.Where(n => n.SubjectId == r.Id).Select(n => new ReportScheduleDeliveryViewModel(names.GetValueOrDefault(n.RecipientId, "Unknown account"), n.Status.DisplayName)).ToList()))
            .ToList();

        return new QueryResponse<PagedResult<ReportScheduleRunViewModel>?> { Result = new PagedResult<ReportScheduleRunViewModel>(items, pageNumber, pageSize, total) };
    }
}

/// <summary>Eligible recipients: staff who hold the report's category action and (for a branch) are assigned to it. Null result = unknown report.</summary>
public sealed class GetEligibleReportRecipientsQueryHandler(CatEcommerceDbContext db, IReportCatalogue catalogue)
    : IQueryHandlerAsync<GetEligibleReportRecipientsQuery, IReadOnlyList<EligibleReportRecipientViewModel>?>
{
    /// <summary>The picker shows at most this many names (a schedule has at most 20 recipients).</summary>
    public const int EligibleRecipientLimit = 200;

    public async Task<QueryResponse<IReadOnlyList<EligibleReportRecipientViewModel>?>> HandleAsync(GetEligibleReportRecipientsQuery query)
    {
        var definition = catalogue.Find(query.ReportKey);
        if (definition is null) return new QueryResponse<IReadOnlyList<EligibleReportRecipientViewModel>?> { Result = null };

        // Filtered in SQL and bounded: only the roles that hold the report's action (and, for an all-branches schedule, a cross-branch role), only staff assigned to the branch.
        // The same rule as ReportAccessPolicy.CanReceive: one role holds the category action, and (all-branches) any role -- possibly another -- is a cross-branch role.
        var allowed = (ActionRoles.TryGetRoles(ReportRules.ActionFor(definition.Category), out var roles) ? roles : []).ToArray();
        var crossBranch = ReportRules.CrossBranchRoles.ToArray();

        var staff = db.StaffAccounts.AsNoTracking().Where(s => s.RoleIds.Any(r => allowed.Contains(r)));
        if (query.BranchId is null) staff = staff.Where(s => s.RoleIds.Any(r => crossBranch.Contains(r)));
        if (query.BranchId is { } branch)
        {
            var assigned = db.Set<StaffBranchAssignmentAggregate>().AsNoTracking().Where(a => a.BranchIds.Contains(branch)).Select(a => a.Id);
            staff = staff.Where(s => assigned.Contains(s.Id));
        }

        var found = await staff.OrderBy(s => s.FullName).ThenBy(s => s.Id).Take(EligibleRecipientLimit)
            .Select(s => new { s.Id, s.FullName, s.RoleIds, HasEmail = s.EmailAddress != null }).ToListAsync();

        IReadOnlyList<EligibleReportRecipientViewModel> eligible = found
            .Select(s => new EligibleReportRecipientViewModel(s.Id, s.FullName, s.RoleIds.ToList(), s.HasEmail))
            .ToList();

        return new QueryResponse<IReadOnlyList<EligibleReportRecipientViewModel>?> { Result = eligible };
    }
}

/// <summary>
/// The preview (section 12.4): the draft is checked with the aggregate's own rules (a throwaway instance, never saved), then the next three runs, the period the first would cover, and -- because the email
/// is Normal urgency -- when quiet hours would hold its delivery. Nothing is written.
/// </summary>
public sealed class PreviewReportScheduleQueryHandler(CatEcommerceDbContext db, IReportCatalogue catalogue, IQuietHoursCalendar quietHours, TimeProvider timeProvider)
    : IQueryHandlerAsync<PreviewReportScheduleQuery, ReportSchedulePreviewOutcome>
{
    public async Task<QueryResponse<ReportSchedulePreviewOutcome>> HandleAsync(PreviewReportScheduleQuery query)
    {
        var definition = catalogue.Find(query.ReportKey);
        if (definition is null) return Result(ReportSchedulePreviewOutcome.Refused(BusinessViolationCodes.UnknownReport, ReportingMessages.UnknownReport, nameof(query.ReportKey)));

        var branches = await ReportScheduleReads.BranchesAsync(db, CancellationToken.None);
        var (_, zoneId) = ReportScheduleReads.BranchOf(branches, query.BranchId);
        var zone = ReportScheduleReads.Zone(zoneId);
        var now = timeProvider.GetUtcNow();

        var draft = new ReportScheduleAggregate(Guid.NewGuid());
        try
        {
            draft.Create(new ReportScheduleDto(
                "Preview", definition, query.BranchId, query.Period, new Dictionary<string, string>(), ReportFormat.Csv, query.Frequency, query.DayOfWeek, query.DayOfMonth,
                query.LastDayOfMonth, query.SendAtLocal, [Guid.NewGuid()], zone, now, new Platform.Infrastructure.Common.Security.UserContext(ReportingSystemActors.ReportingWorker, Guid.Empty)));
        }
        catch (DomainRuleViolationException ex)
        {
            return Result(ReportSchedulePreviewOutcome.Refused(ex.ErrorCode, ex.Message, ex.PropertyName));
        }

        var runs = ReportScheduleCalculator.NextRuns(query.Frequency, query.DayOfWeek, query.DayOfMonth, query.LastDayOfMonth, query.SendAtLocal, now, zone, ReportScheduleRules.PreviewRuns);
        var (from, to) = ReportPeriodResolver.Resolve(query.Period, runs[0], zone);
        var held = await quietHours.NextAllowedInstantAsync(runs[0], query.BranchId);

        return Result(ReportSchedulePreviewOutcome.Success(new ReportSchedulePreviewViewModel(runs, new ReportSchedulePreviewPeriodViewModel(from, to), held)));
    }

    private static QueryResponse<ReportSchedulePreviewOutcome> Result(ReportSchedulePreviewOutcome outcome) => new() { Result = outcome };
}

/// <summary>Active schedules whose next run is due, oldest first (section 8.9 step 1).</summary>
public sealed class GetDueReportSchedulesQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetDueReportSchedulesQuery, IReadOnlyList<DueReportScheduleViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<DueReportScheduleViewModel>>> HandleAsync(GetDueReportSchedulesQuery query)
    {
        var active = ReportScheduleStatus.Active;
        var due = await db.Set<ReportScheduleAggregate>().AsNoTracking()
            .Where(s => s.Status == active && s.NextRunOn != null && s.NextRunOn <= query.Now)
            .OrderBy(s => s.NextRunOn).ThenBy(s => s.Id)
            .Take(Math.Max(1, query.Limit))
            .Select(s => new { s.Id, s.NextRunOn })
            .ToListAsync();

        IReadOnlyList<DueReportScheduleViewModel> result = due.Select(s => new DueReportScheduleViewModel(s.Id, s.NextRunOn!.Value)).ToList();
        return new QueryResponse<IReadOnlyList<DueReportScheduleViewModel>> { Result = result };
    }
}

/// <summary>What the worker needs to run one schedule (section 8.9 steps 2 and 3).</summary>
public sealed class GetReportSchedulePlanQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<GetReportSchedulePlanQuery, ReportSchedulePlanViewModel?>
{
    public async Task<QueryResponse<ReportSchedulePlanViewModel?>> HandleAsync(GetReportSchedulePlanQuery query)
    {
        var schedule = await db.Set<ReportScheduleAggregate>().AsNoTracking().Where(s => s.Id == query.ScheduleId)
            .Select(s => new
            {
                s.Id, s.ReportKey, s.BranchId, s.Period, s.Parameters, s.Format, s.Status, s.NextRunOn, s.Frequency, s.DayOfWeek, s.DayOfMonth, s.LastDayOfMonth, s.SendAtLocal,
                s.RecipientIds
            })
            .FirstOrDefaultAsync();
        if (schedule is null) return new QueryResponse<ReportSchedulePlanViewModel?> { Result = null };

        var ids = schedule.RecipientIds.ToList();
        var staff = await db.StaffAccounts.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => new { s.Id, s.RoleIds }).ToListAsync();
        var branches = await ReportScheduleReads.BranchesAsync(db, CancellationToken.None);
        var (_, zoneId) = ReportScheduleReads.BranchOf(branches, schedule.BranchId);

        var recipients = ids.Select(id => new ReportSchedulePlanRecipientViewModel(id, staff.FirstOrDefault(s => s.Id == id)?.RoleIds.ToList() ?? [])).ToList();
        var plan = new ReportSchedulePlanViewModel(
            schedule.Id, schedule.ReportKey, schedule.BranchId, schedule.Period.DisplayName, schedule.Parameters, schedule.Format.DisplayName, zoneId, schedule.Status.DisplayName, schedule.NextRunOn,
            recipients, schedule.Frequency.DisplayName, schedule.DayOfWeek, schedule.DayOfMonth, schedule.LastDayOfMonth, schedule.SendAtLocal);

        return new QueryResponse<ReportSchedulePlanViewModel?> { Result = plan };
    }
}

/// <summary>Failed runs a slot already has.</summary>
public sealed class CountFailedReportRunsQueryHandler(CatEcommerceDbContext db) : IQueryHandlerAsync<CountFailedReportRunsQuery, int>
{
    public async Task<QueryResponse<int>> HandleAsync(CountFailedReportRunsQuery query)
    {
        var failed = ScheduledReportRunStatus.Failed;
        var schedule = ReportRunTrigger.Schedule;
        var count = await db.Set<ScheduledReportRunAggregate>().AsNoTracking()
            .CountAsync(r => r.ScheduleId == query.ScheduleId && r.DueOn == query.DueOn && r.Status == failed && r.Trigger == schedule);

        return new QueryResponse<int> { Result = count };
    }
}
