using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-APT-001 §8.7 -- U-C1..U-C5. Pure, no I/O.</summary>
public sealed class ClinicQueueTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static QueueEntryInput CheckedInWalkIn(TriagePriority priority, DateTimeOffset checkedInOn, int duration = 20) =>
        new(Guid.NewGuid(), priority, true, null, checkedInOn, null, duration, AppointmentStatus.CheckedIn, null);

    private static QueueEntryInput CheckedInBooked(TriagePriority priority, DateTimeOffset startsOn, Guid? performerId = null, int duration = 20) =>
        new(Guid.NewGuid(), priority, false, startsOn, startsOn, performerId, duration, AppointmentStatus.CheckedIn, null);

    /// <summary>U-C1 -- emergency first, then urgent, then routine by due time.</summary>
    [Fact]
    public void Order_MixOfPrioritiesAndDueTimes_EmergencyFirst()
    {
        var routineWalkIn = CheckedInWalkIn(TriagePriority.Routine, Now);
        var routineBooked = CheckedInBooked(TriagePriority.Routine, Now.AddMinutes(15));
        var urgentWalkIn = CheckedInWalkIn(TriagePriority.Urgent, Now.AddMinutes(20));
        var emergencyWalkIn = CheckedInWalkIn(TriagePriority.Emergency, Now.AddMinutes(30));

        var ordered = ClinicQueueOrdering.Order([routineWalkIn, routineBooked, urgentWalkIn, emergencyWalkIn]);

        Assert.Equal(emergencyWalkIn.AppointmentId, ordered[0].AppointmentId);
        Assert.Equal(urgentWalkIn.AppointmentId, ordered[1].AppointmentId);
        Assert.Equal(routineWalkIn.AppointmentId, ordered[2].AppointmentId);
        Assert.Equal(routineBooked.AppointmentId, ordered[3].AppointmentId);
    }

    /// <summary>U-C5 -- a vet's queue is their own entries plus unassigned walk-ins.</summary>
    [Fact]
    public void ForPerformer_ReturnsOwnEntries_PlusUnassignedWalkIns()
    {
        var performerId = Guid.NewGuid();
        var own = CheckedInBooked(TriagePriority.Routine, Now, performerId);
        var otherPerformer = CheckedInBooked(TriagePriority.Routine, Now, Guid.NewGuid());
        var unassigned = CheckedInWalkIn(TriagePriority.Routine, Now);

        var ordered = ClinicQueueOrdering.Order([own, otherPerformer, unassigned]);
        var filtered = ClinicQueueOrdering.ForPerformer(ordered, performerId);

        Assert.Equal(2, filtered.Count);
        Assert.Contains(filtered, e => e.AppointmentId == own.AppointmentId);
        Assert.Contains(filtered, e => e.AppointmentId == unassigned.AppointmentId);
        Assert.DoesNotContain(filtered, e => e.AppointmentId == otherPerformer.AppointmentId);
    }

    /// <summary>U-C2 -- ceil_to_5(65 / 2) = 35 with two performers, 30+15+20 min ahead.</summary>
    [Fact]
    public void EstimateWaitMinutes_TwoPerformers_CeilsToNearestFive()
    {
        var first = CheckedInWalkIn(TriagePriority.Routine, Now, duration: 30);
        var second = CheckedInWalkIn(TriagePriority.Routine, Now.AddMinutes(1), duration: 15);
        var third = CheckedInWalkIn(TriagePriority.Routine, Now.AddMinutes(2), duration: 20);
        var ordered = ClinicQueueOrdering.Order([first, second, third]);

        var waits = ClinicQueueEstimator.EstimateWaitMinutes(ordered, performersOnShift: 2, now: Now);

        Assert.Equal(0, waits[first.AppointmentId]); // nobody ahead
        Assert.Equal(15, waits[second.AppointmentId]); // ceil_to_5(30/2)=15
        Assert.Equal(25, waits[third.AppointmentId]); // ceil_to_5(45/2)=25 -> ceil(22.5/5)*5=25
    }

    /// <summary>U-C4 -- no performers on shift never divides by zero.</summary>
    [Fact]
    public void EstimateWaitMinutes_NoPerformersOnShift_DivisorIsOne()
    {
        var entry = CheckedInWalkIn(TriagePriority.Routine, Now, duration: 10);
        var ordered = ClinicQueueOrdering.Order([entry]);

        var waits = ClinicQueueEstimator.EstimateWaitMinutes(ordered, performersOnShift: 0, now: Now);

        Assert.Equal(0, waits[entry.AppointmentId]);
    }

    /// <summary>U-C3 -- adding an emergency increases every later entry's estimate.</summary>
    [Fact]
    public void EstimateWaitMinutes_AddingEmergencyAhead_IncreasesLaterEstimates()
    {
        var routine = CheckedInWalkIn(TriagePriority.Routine, Now, duration: 20);
        var before = ClinicQueueEstimator.EstimateWaitMinutes(ClinicQueueOrdering.Order([routine]), 1, Now);

        var emergency = CheckedInWalkIn(TriagePriority.Emergency, Now.AddMinutes(5), duration: 30);
        var after = ClinicQueueEstimator.EstimateWaitMinutes(ClinicQueueOrdering.Order([routine, emergency]), 1, Now);

        Assert.True(after[routine.AppointmentId] > before[routine.AppointmentId]);
    }
}
