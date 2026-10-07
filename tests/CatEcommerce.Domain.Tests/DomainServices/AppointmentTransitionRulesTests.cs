using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-APT-001 §8.3/U-A1/U-A2 -- US-CLN-10 AC2: only §8.3's arrows are allowed; every other
/// pair is refused, naming both states.</summary>
public sealed class AppointmentTransitionRulesTests
{
    private static readonly (string Action, AppointmentStatus From)[] Allowed =
    [
        (AppointmentTransitionRules.Confirm, AppointmentStatus.Held),
        (AppointmentTransitionRules.ReleaseHold, AppointmentStatus.Held),
        (AppointmentTransitionRules.ExpireHold, AppointmentStatus.Held),
        (AppointmentTransitionRules.Reschedule, AppointmentStatus.Booked),
        (AppointmentTransitionRules.Cancel, AppointmentStatus.Booked),
        (AppointmentTransitionRules.Cancel, AppointmentStatus.CheckedIn),
        (AppointmentTransitionRules.CheckIn, AppointmentStatus.Booked),
        (AppointmentTransitionRules.SetTriagePriority, AppointmentStatus.CheckedIn),
        (AppointmentTransitionRules.Start, AppointmentStatus.CheckedIn),
        (AppointmentTransitionRules.Complete, AppointmentStatus.InConsultation),
        (AppointmentTransitionRules.RecordNoShow, AppointmentStatus.Booked),
    ];

    private static readonly string[] AllActions =
    [
        AppointmentTransitionRules.Confirm, AppointmentTransitionRules.ReleaseHold, AppointmentTransitionRules.ExpireHold,
        AppointmentTransitionRules.Reschedule, AppointmentTransitionRules.Cancel, AppointmentTransitionRules.CheckIn,
        AppointmentTransitionRules.SetTriagePriority, AppointmentTransitionRules.Start, AppointmentTransitionRules.Complete,
        AppointmentTransitionRules.RecordNoShow,
    ];

    private static readonly AppointmentStatus[] AllStatuses =
    [
        AppointmentStatus.Held, AppointmentStatus.HoldExpired, AppointmentStatus.HoldReleased, AppointmentStatus.Booked,
        AppointmentStatus.CheckedIn, AppointmentStatus.InConsultation, AppointmentStatus.Completed, AppointmentStatus.Cancelled,
        AppointmentStatus.NoShow,
    ];

    /// <summary>U-A1 -- the full table over every (from, action) pair.</summary>
    [Fact]
    public void EveryPair_MatchesTheAllowedTable()
    {
        foreach (var status in AllStatuses)
        {
            foreach (var action in AllActions)
            {
                var expectedAllowed = Array.Exists(Allowed, a => a.Action == action && a.From == status);
                Assert.Equal(expectedAllowed, AppointmentTransitionRules.IsAllowed(status, action));
            }
        }
    }

    [Fact]
    public void EnsureAllowed_Disallowed_ThrowsNamingBothStates()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => AppointmentTransitionRules.EnsureAllowed(AppointmentStatus.Completed, AppointmentTransitionRules.CheckIn));

        Assert.Equal(BusinessViolationCodes.InvalidAppointmentTransition, ex.ErrorCode);
        Assert.Contains("completed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("checked in", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureAllowed_Allowed_DoesNotThrow()
    {
        AppointmentTransitionRules.EnsureAllowed(AppointmentStatus.CheckedIn, AppointmentTransitionRules.Start);
    }

    [Fact]
    public void AllowedActions_Booked_ReturnsReschedule_Cancel_CheckIn_RecordNoShow()
    {
        var actions = AppointmentTransitionRules.AllowedActions(AppointmentStatus.Booked);

        Assert.Equal(
            new[] { AppointmentTransitionRules.Reschedule, AppointmentTransitionRules.Cancel, AppointmentTransitionRules.CheckIn, AppointmentTransitionRules.RecordNoShow }.OrderBy(a => a),
            actions.OrderBy(a => a));
    }
}
