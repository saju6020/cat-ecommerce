using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The branch a report runs for: one branch, or every branch, or a refusal.</summary>
public sealed record ReportBranchScope(Guid? BranchId, bool IsAllBranches, string? RefusalCode, string? RefusalMessage)
{
    public bool IsRefused => RefusalCode is not null;

    public static ReportBranchScope Single(Guid branchId) => new(branchId, false, null, null);

    public static readonly ReportBranchScope All = new(null, true, null, null);

    public static ReportBranchScope Refused(string code, string message) => new(null, false, code, message);
}

/// <summary>
/// HO-RPT-001 section 8.13 / 8.8 (ADR-281) -- the one place that decides what a staff caller may open, which columns they see and which branches they may ask for. <b>Pure</b>, so the
/// API, the worker, the command handlers and the notification guard all use the same rules.
/// </summary>
public static class ReportAccessPolicy
{
    public static bool Holds(IEnumerable<string> roles, string action) =>
        ActionRoles.TryGetRoles(action, out var allowed) && allowed.Intersect(roles, StringComparer.Ordinal).Any();

    /// <summary>The report's own category action (the in-handler half of the two-layer check, section 12.3).</summary>
    public static bool CanOpen(ReportDefinition definition, IEnumerable<string> roles) => Holds(roles, ReportRules.ActionFor(definition.Category));

    public static bool CanOpen(ReportCategory category, IEnumerable<string> roles) => Holds(roles, ReportRules.ActionFor(category));

    /// <summary>The columns the caller may see: a restricted column is removed entirely when the caller does not hold its action.</summary>
    public static IReadOnlyList<ReportColumn> VisibleColumns(ReportDefinition definition, IReadOnlyCollection<string> roles) =>
        definition.Columns.Where(c => c.RestrictedToAction is null || Holds(roles, c.RestrictedToAction)).ToList();

    /// <summary>
    /// The columns <b>every</b> recipient may see -- the intersection of their restricted actions (ADR-288). Nobody receives a column they could not open on screen.
    /// </summary>
    public static IReadOnlyList<ReportColumn> VisibleColumnsForAll(ReportDefinition definition, IEnumerable<IReadOnlyCollection<string>> recipientRoles)
    {
        var all = recipientRoles.ToList();
        return definition.Columns.Where(c => c.RestrictedToAction is null || all.All(roles => Holds(roles, c.RestrictedToAction))).ToList();
    }

    /// <summary>
    /// Whether a staff member may be sent a scheduled report right now (ADR-288: nobody receives what they could not open on screen): they hold the report's category action AND, for an
    /// all-branches schedule (<paramref name="scheduleBranchId"/> null), a cross-branch role, or, for a branch schedule, their current assignment includes that branch. Pure; used when a schedule is
    /// saved, when it is read, and again by the delivery guard at send time.
    /// </summary>
    public static bool CanReceive(ReportDefinition definition, IReadOnlyCollection<string> roles, Guid? scheduleBranchId, IReadOnlyCollection<Guid> assignedBranchIds) =>
        CanOpen(definition, roles) && (scheduleBranchId is null ? CanUseAllBranches(roles) : assignedBranchIds.Contains(scheduleBranchId.Value));

    public static bool CanUseAllBranches(IEnumerable<string> roles) => roles.Intersect(ReportRules.CrossBranchRoles, StringComparer.Ordinal).Any();

    /// <summary>
    /// Resolves the <c>branchId</c> filter (section 8.7). No value means the caller's active branch; <c>all</c> needs a cross-branch role; any other branch must be in the caller's assignment.
    /// </summary>
    public static ReportBranchScope ResolveBranch(
        string? requested, Guid? activeBranchId, IReadOnlyCollection<Guid> assignedBranchIds, IReadOnlyCollection<string> roles)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return activeBranchId is { } active
                ? ReportBranchScope.Single(active)
                : ReportBranchScope.Refused(BusinessViolationCodes.BranchNotAssigned, ReportingMessages.BranchNotAssigned);
        }

        if (string.Equals(requested.Trim(), ReportRules.AllBranches, StringComparison.OrdinalIgnoreCase))
        {
            return CanUseAllBranches(roles)
                ? ReportBranchScope.All
                : ReportBranchScope.Refused(BusinessViolationCodes.CrossBranchReportNotPermitted, ReportingMessages.CrossBranchReportNotPermitted);
        }

        if (!Guid.TryParse(requested, out var branchId))
            return ReportBranchScope.Refused(BusinessViolationCodes.BranchNotAssigned, ReportingMessages.BranchNotAssigned);

        // The active branch was already validated by HO-ADM-001's accessor (it also covers a caller with no assignment on a single-branch install).
        if (branchId == activeBranchId || assignedBranchIds.Contains(branchId)) return ReportBranchScope.Single(branchId);

        return ReportBranchScope.Refused(BusinessViolationCodes.BranchNotAssigned, ReportingMessages.BranchNotAssigned);
    }
}
