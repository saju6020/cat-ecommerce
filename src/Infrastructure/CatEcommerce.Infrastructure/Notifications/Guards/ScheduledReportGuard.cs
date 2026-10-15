using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-RPT-001 section 11.7 item 6 (ADR-288) -- valid while the run (<c>subjectId</c>) exists and is <c>Generated</c>, its file (when it has one) still exists, and the recipient still may
/// <b>receive</b> the report (<see cref="ReportAccessPolicy.CanReceive"/>): the account exists and holds the report's category action, holds a cross-branch role when the schedule covers every branch
/// or is still assigned to the schedule's branch, and -- if the run included a restricted column -- the restricted action too. Losing a role or a branch between generation and delivery therefore
/// cancels the email: nobody receives what they could no longer open on screen. Reads only, and only the columns it needs (no aggregate is rehydrated). A staff account has no separate "inactive"
/// flag in this codebase; deactivation removes its roles, which fails the same check.
/// </summary>
public sealed class ScheduledReportGuard(CatEcommerceDbContext db, IReportCatalogue catalogue) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.ScheduledReport;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null) return false;

        var run = await db.Set<ScheduledReportRunAggregate>().AsNoTracking().Where(r => r.Id == subjectId.Value)
            .Select(r => new { r.Status, r.FileId, r.ScheduleId, r.ColumnsIncluded }).FirstOrDefaultAsync(ct);
        if (run is null || run.Status != ScheduledReportRunStatus.Generated) return false;

        if (run.FileId is { } fileId && !await db.StoredFiles.AsNoTracking().AnyAsync(f => f.Id == fileId, ct)) return false;

        var schedule = await db.Set<ReportScheduleAggregate>().AsNoTracking().Where(s => s.Id == run.ScheduleId).Select(s => new { s.ReportKey, s.BranchId }).FirstOrDefaultAsync(ct);
        var definition = schedule is null ? null : catalogue.Find(schedule.ReportKey);
        if (schedule is null || definition is null) return false;

        var staff = await db.StaffAccounts.AsNoTracking().Where(s => s.Id == recipientId).Select(s => new { s.RoleIds }).FirstOrDefaultAsync(ct);
        if (staff is null) return false;

        var roles = staff.RoleIds.ToList();
        IReadOnlyCollection<Guid> assigned = schedule.BranchId is null
            ? []
            : (await db.Set<StaffBranchAssignmentAggregate>().AsNoTracking().Where(a => a.Id == recipientId).Select(a => a.BranchIds).FirstOrDefaultAsync(ct))?.ToList() ?? [];

        if (!ReportAccessPolicy.CanReceive(definition, roles, schedule.BranchId, assigned)) return false;

        return definition.Columns
            .Where(c => c.RestrictedToAction is not null && run.ColumnsIncluded.Contains(c.Key))
            .All(c => ReportAccessPolicy.Holds(roles, c.RestrictedToAction!));
    }
}
