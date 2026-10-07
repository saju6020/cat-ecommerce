using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.3/§12.6 (ADR-067, US-CLN-10 AC2) -- the one table of allowed
/// <c>(current status, action)</c> pairs, used both by <see cref="Aggregates.Clinic.AppointmentAggregate"/>
/// to refuse an invalid transition and by <c>GetAppointmentDetailQuery</c> to compute
/// <c>allowedTransitions[]</c> for the read side, so the two can never disagree.
/// </summary>
public static class AppointmentTransitionRules
{
    public const string Confirm = nameof(Confirm);
    public const string ReleaseHold = nameof(ReleaseHold);
    public const string ExpireHold = nameof(ExpireHold);
    public const string Reschedule = nameof(Reschedule);
    public const string Cancel = nameof(Cancel);
    public const string CheckIn = nameof(CheckIn);
    public const string SetTriagePriority = nameof(SetTriagePriority);
    public const string Start = nameof(Start);
    public const string Complete = nameof(Complete);
    public const string RecordNoShow = nameof(RecordNoShow);

    private static readonly Dictionary<string, AppointmentStatus[]> AllowedFrom = new(StringComparer.Ordinal)
    {
        [Confirm] = [AppointmentStatus.Held],
        [ReleaseHold] = [AppointmentStatus.Held],
        [ExpireHold] = [AppointmentStatus.Held],
        [Reschedule] = [AppointmentStatus.Booked],
        [Cancel] = [AppointmentStatus.Booked, AppointmentStatus.CheckedIn],
        [CheckIn] = [AppointmentStatus.Booked],
        [SetTriagePriority] = [AppointmentStatus.CheckedIn],
        [Start] = [AppointmentStatus.CheckedIn],
        [Complete] = [AppointmentStatus.InConsultation],
        [RecordNoShow] = [AppointmentStatus.Booked],
    };

    private static readonly Dictionary<string, string> Descriptions = new(StringComparer.Ordinal)
    {
        [Confirm] = "be confirmed",
        [ReleaseHold] = "have its hold released",
        [ExpireHold] = "have its hold expired",
        [Reschedule] = "be rescheduled",
        [Cancel] = "be cancelled",
        [CheckIn] = "be checked in",
        [SetTriagePriority] = "have its triage changed",
        [Start] = "be started",
        [Complete] = "be completed",
        [RecordNoShow] = "be marked a no-show",
    };

    /// <summary>Throws <c>InvalidAppointmentTransition</c>, naming both the current state and the
    /// attempted action, when <paramref name="action"/> is not allowed from <paramref name="current"/>.</summary>
    public static void EnsureAllowed(AppointmentStatus current, string action)
    {
        if (IsAllowed(current, action)) return;

        throw new DomainRuleViolationException(
            BusinessViolationCodes.InvalidAppointmentTransition,
            $"A {current.DisplayName.ToLowerInvariant()} appointment cannot {Describe(action)}.",
            action);
    }

    public static bool IsAllowed(AppointmentStatus current, string action) =>
        AllowedFrom.TryGetValue(action, out var allowed) && allowed.Contains(current);

    /// <summary>Every action the aggregate would currently accept -- the read side's
    /// <c>allowedTransitions[]</c> (§12.4), restricting a walk-in's still-pending performer/room
    /// assignment is the caller's job, not this table's.</summary>
    public static IReadOnlyList<string> AllowedActions(AppointmentStatus current) =>
        AllowedFrom.Where(kv => kv.Value.Contains(current)).Select(kv => kv.Key).ToList();

    private static string Describe(string action) => Descriptions.GetValueOrDefault(action, action);
}
