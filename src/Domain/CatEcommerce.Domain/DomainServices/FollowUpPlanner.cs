using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>One follow-up rule of a protocol: due <paramref name="DayOffset"/> days after the procedure, movable inside the window.</summary>
public sealed record FollowUpRule(SurgeryFollowUpKind Kind, int DayOffset, int WindowFromDay, int WindowToDay);

/// <summary>A planned follow-up: the default due date and the window staff may move it within (local dates).</summary>
public sealed record PlannedFollowUp(SurgeryFollowUpKind Kind, DateOnly DueOn, DateOnly WindowFrom, DateOnly WindowTo);

/// <summary>
/// HO-SRG-001 §8.10/§8.12 (US-SRG-07, U-A5, U-A6) -- protocol + procedure date -> the default follow-ups, each with the window it may be moved within.
/// Pure: no clock, no I/O.
/// </summary>
public static class FollowUpPlanner
{
    public static IReadOnlyList<PlannedFollowUp> Plan(IReadOnlyList<FollowUpRule> rules, DateOnly procedureDate) =>
        rules.Select(r => new PlannedFollowUp(r.Kind, procedureDate.AddDays(r.DayOffset), procedureDate.AddDays(r.WindowFromDay), procedureDate.AddDays(r.WindowToDay)))
            .OrderBy(p => p.DueOn)
            .ToList();

    /// <summary>The window rules themselves (a protocol must satisfy them to be saved).</summary>
    public static void EnsureRuleValid(FollowUpRule rule)
    {
        if (rule.DayOffset is < 0 or > 60 || rule.WindowFromDay is < 0 or > 60 || rule.WindowToDay is < 0 or > 60
            || rule.WindowFromDay > rule.DayOffset || rule.DayOffset > rule.WindowToDay)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FollowUpOutsideWindow,
                "A follow-up day must be between 0 and 60 and sit inside its own window.", nameof(rule.DayOffset));
    }

    /// <summary>A due date chosen by staff must sit inside the protocol window for that kind.</summary>
    public static void EnsureDateInWindow(PlannedFollowUp planned, DateOnly dueOn)
    {
        if (dueOn < planned.WindowFrom || dueOn > planned.WindowTo)
            throw new DomainRuleViolationException(
                BusinessViolationCodes.FollowUpOutsideWindow,
                $"A {planned.Kind.DisplayName} follow-up must fall between {planned.WindowFrom:yyyy-MM-dd} and {planned.WindowTo:yyyy-MM-dd}.", nameof(dueOn));
    }
}
