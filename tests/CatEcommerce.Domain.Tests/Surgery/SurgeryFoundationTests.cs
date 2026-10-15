using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 phase A -- U-A1, U-A5, U-A6, U-A7 (definitions) and the constants the later phases rely on.</summary>
public sealed class SurgeryFoundationTests
{
    // §8.3 written out independently of SurgeryTransitionRules, so the table is checked against the handover, not against itself.
    private static readonly Dictionary<string, SurgeryStatus[]> Arrows = new()
    {
        [SurgeryTransitionRules.Reschedule] = [SurgeryStatus.Scheduled],
        [SurgeryTransitionRules.Cancel] = [SurgeryStatus.Scheduled, SurgeryStatus.PreOp],
        [SurgeryTransitionRules.Admit] = [SurgeryStatus.Scheduled],
        [SurgeryTransitionRules.Start] = [SurgeryStatus.PreOp],
        [SurgeryTransitionRules.EndProcedure] = [SurgeryStatus.InTheatre],
        [SurgeryTransitionRules.SetRecoveryStatus] = [SurgeryStatus.Recovery],
        [SurgeryTransitionRules.Discharge] = [SurgeryStatus.Recovery],
        [SurgeryTransitionRules.CloseFollowUp] = [SurgeryStatus.Discharged],
    };

    private static readonly SurgeryStatus[] AllStatuses =
        [SurgeryStatus.Scheduled, SurgeryStatus.PreOp, SurgeryStatus.InTheatre, SurgeryStatus.Recovery, SurgeryStatus.Discharged, SurgeryStatus.Cancelled];

    /// <summary>U-A1 -- every (status, action) pair: only §8.3's arrows are allowed, every other pair is InvalidSurgeryTransition naming both states.</summary>
    [Fact]
    public void U_A1_OnlyTheArrowsOfThe_StatusMachine_AreAllowed_AndEveryOtherPairNamesBothStates()
    {
        foreach (var (action, allowedFrom) in Arrows)
        {
            foreach (var status in AllStatuses)
            {
                if (allowedFrom.Contains(status))
                {
                    Assert.True(SurgeryTransitionRules.IsAllowed(status, action), $"{status.DisplayName} -> {action} must be allowed.");
                    SurgeryTransitionRules.EnsureAllowed(status, action);
                    continue;
                }

                var ex = Assert.Throws<DomainRuleViolationException>(() => SurgeryTransitionRules.EnsureAllowed(status, action));
                Assert.Equal(BusinessViolationCodes.InvalidSurgeryTransition, ex.ErrorCode);
                Assert.Contains(status.DisplayName.ToLowerInvariant().Replace("preop", "pre-op").Replace("intheatre", "theatre"), ex.Message.ToLowerInvariant());
            }
        }
    }

    [Fact]
    public void U_A1_TheMessage_ReadsLikeTheHandover_AndAllowedActionsAreTheReadSideList()
    {
        var ex = Assert.Throws<DomainRuleViolationException>(() => SurgeryTransitionRules.EnsureAllowed(SurgeryStatus.Recovery, SurgeryTransitionRules.Admit));
        Assert.Equal("A surgery in recovery cannot be admitted.", ex.Message);

        Assert.Equal([SurgeryTransitionRules.Cancel, SurgeryTransitionRules.Start], SurgeryTransitionRules.AllowedActions(SurgeryStatus.PreOp).OrderBy(a => a).ToArray());
        Assert.Empty(SurgeryTransitionRules.AllowedActions(SurgeryStatus.Cancelled));
    }

    private static SurgeryProtocolDto Protocol(params FollowUpRule[] rules) =>
        new(Guid.NewGuid(), "No food from midnight.", 8, "Rest.", rules, new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), TestUserContext.Create());

    /// <summary>U-A5 -- a follow-up whose DayOffset lies outside its own window is refused (and so is an out-of-range day).</summary>
    [Theory]
    [InlineData(1, 2, 4)]   // offset before the window
    [InlineData(5, 2, 4)]   // offset after the window
    [InlineData(3, 4, 2)]   // inverted window
    [InlineData(61, 2, 4)]  // beyond 60 days
    public void U_A5_AProtocolWhoseOffsetIsOutsideItsWindow_IsRefused(int offset, int from, int to)
    {
        var protocol = new SurgeryProtocolAggregate(Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() => protocol.Upsert(Protocol(new FollowUpRule(SurgeryFollowUpKind.WoundCheck, offset, from, to))));

        Assert.Equal(BusinessViolationCodes.FollowUpOutsideWindow, ex.ErrorCode);
        Assert.Empty(protocol.FollowUps);
    }

    [Fact]
    public void U_A5_AValidProtocol_IsStored_AndReplacedWholesaleOnTheNextUpsert()
    {
        var protocol = new SurgeryProtocolAggregate(Guid.NewGuid());
        protocol.Upsert(Protocol(new FollowUpRule(SurgeryFollowUpKind.WoundCheck, 3, 2, 4), new FollowUpRule(SurgeryFollowUpKind.SutureRemoval, 12, 10, 14)));
        Assert.Equal(2, protocol.FollowUps.Count);

        protocol.Upsert(Protocol(new FollowUpRule(SurgeryFollowUpKind.Recheck, 7, 6, 8)));

        Assert.Single(protocol.FollowUps);
        Assert.Equal(SurgeryFollowUpKind.Recheck, protocol.FollowUps[0].Kind);
    }

    [Fact]
    public void U_A5_FastingHours_AndTextLengths_AreBounded()
    {
        var protocol = new SurgeryProtocolAggregate(Guid.NewGuid());
        Assert.Throws<DomainRuleViolationException>(() => protocol.Upsert(Protocol() with { FastingHours = 25 }));
        Assert.Throws<DomainRuleViolationException>(() => protocol.Upsert(Protocol() with { PostOpInstructions = new string('x', SurgeryRules.PostOpInstructionsMax + 1) }));
        Assert.Throws<DomainRuleViolationException>(() => protocol.Upsert(Protocol() with { FastingInstructions = "  " }));
    }

    /// <summary>U-A6 -- the SRS example values for a surgery on 10 October: wound check 13 October, suture removal 22 October.</summary>
    [Fact]
    public void U_A6_FollowUpPlanner_WithTheDefaults_PlansWoundCheckOn13Oct_AndSutureRemovalOn22Oct()
    {
        var rules = SurgeryProtocolDefaults.FollowUps
            .Select(f => new FollowUpRule(Enumeration.FromDisplayName<SurgeryFollowUpKind>(f.Kind), f.DayOffset, f.WindowFromDay, f.WindowToDay)).ToList();

        var plan = FollowUpPlanner.Plan(rules, new DateOnly(2026, 10, 10));

        Assert.Equal(2, plan.Count);
        Assert.Equal(SurgeryFollowUpKind.WoundCheck, plan[0].Kind);
        Assert.Equal(new DateOnly(2026, 10, 13), plan[0].DueOn);
        Assert.Equal(new DateOnly(2026, 10, 12), plan[0].WindowFrom);
        Assert.Equal(new DateOnly(2026, 10, 14), plan[0].WindowTo);
        Assert.Equal(SurgeryFollowUpKind.SutureRemoval, plan[1].Kind);
        Assert.Equal(new DateOnly(2026, 10, 22), plan[1].DueOn);
        Assert.Equal(new DateOnly(2026, 10, 20), plan[1].WindowFrom);
        Assert.Equal(new DateOnly(2026, 10, 24), plan[1].WindowTo);
    }

    [Fact]
    public void U_A6_ADueDateOutsideItsWindow_IsRefused_AndInsideIsAccepted()
    {
        var planned = FollowUpPlanner.Plan([new FollowUpRule(SurgeryFollowUpKind.WoundCheck, 3, 2, 4)], new DateOnly(2026, 10, 10)).Single();

        FollowUpPlanner.EnsureDateInWindow(planned, new DateOnly(2026, 10, 14));
        var ex = Assert.Throws<DomainRuleViolationException>(() => FollowUpPlanner.EnsureDateInWindow(planned, new DateOnly(2026, 10, 15)));
        Assert.Equal(BusinessViolationCodes.FollowUpOutsideWindow, ex.ErrorCode);
    }

    /// <summary>U-A7 (definitions) -- the five keys of §10.2 with their bounds, defaults and branch-override flags.</summary>
    [Theory]
    [InlineData(SettingKeys.SurgeryAdmissionLeadMinutes, "0", "720", "60", true)]
    [InlineData(SettingKeys.SurgeryEarlyAdmissionHours, "0", "24", "12", true)]
    [InlineData(SettingKeys.SurgeryPreOpReminderHours, "0", "72", "18", true)]
    [InlineData(SettingKeys.SurgeryWeightValidityDays, "0", "7", "1", false)]
    [InlineData(SettingKeys.SurgeryFollowUpReminderLeadDays, "0", "7", "1", false)]
    public void U_A7_TheFiveSurgerySettings_AreRegistered_WithTheirBounds(string key, string min, string max, string @default, bool branchOverride)
    {
        var definition = SettingDefinitions.Find(key);

        Assert.NotNull(definition);
        Assert.Equal(SettingType.Integer, definition!.Type);
        Assert.Equal(min, definition.Min);
        Assert.Equal(max, definition.Max);
        Assert.Equal(@default, definition.Default);
        Assert.Equal(branchOverride, definition.BranchOverridable);
        Assert.Equal("Surgery", definition.Group);
    }

    [Fact]
    public void TheEnumerations_AreOrderedAndNamedAsInSection10_1()
    {
        Assert.Equal(["I", "II", "III", "IV", "V"], new[] { AsaGrade.I, AsaGrade.II, AsaGrade.III, AsaGrade.IV, AsaGrade.V }.OrderBy(g => g.Value).Select(g => g.DisplayName));
        Assert.Equal(5, new[] { PreOpChecklistItemKind.FastingInstructionsIssued, PreOpChecklistItemKind.PreAnaestheticBloodWork, PreOpChecklistItemKind.WeightRecorded, PreOpChecklistItemKind.ConsentSigned, PreOpChecklistItemKind.AsaGradeAssessed }.Select(k => k.Value).Distinct().Count());
        Assert.Equal(5, BookingChannel.Surgery.Value);
        Assert.NotEqual(NotificationSubjectKind.SurgeryPreOp.Value, NotificationSubjectKind.SurgeryFollowUp.Value);
    }
}
