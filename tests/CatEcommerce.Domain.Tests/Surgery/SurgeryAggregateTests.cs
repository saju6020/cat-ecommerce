using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 §8.3 / §8.6 / §8.7 -- the status machine, the checklist and the start gate, recovery and discharge, as the aggregate enforces them.</summary>
public sealed class SurgeryAggregateTests
{
    private const string Zone = "Asia/Dhaka";

    // 12 Oct 2026, 09:00 Dhaka = 03:00 UTC.
    private static readonly DateTimeOffset StartsOn = new(2026, 10, 12, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset BookedAt = new(2026, 10, 1, 4, 0, 0, TimeSpan.Zero);
    private static readonly Guid Vet = Guid.NewGuid();

    private static UserContext User(Guid? id = null) => new(id ?? Guid.NewGuid(), Guid.NewGuid());

    private static SurgeryAggregate Scheduled(ServiceCategory? category = null, DateTimeOffset? startsOn = null)
    {
        var start = startsOn ?? StartsOn;
        var surgery = new SurgeryAggregate(Guid.NewGuid());
        surgery.Schedule(new SurgeryScheduleDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new ProcedureSnapshot("Spay", category ?? ServiceCategory.Spay, 60),
            new SurgeryPriceSnapshot(new Money(5000m, "BDT"), SurgeryPricingBasis.Standard, null, null),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Vet, Guid.NewGuid(), start, start.AddHours(1), start.AddHours(-1), "No food from midnight.", BookedAt, User(Vet), Guid.NewGuid()));
        return surgery;
    }

    private static void Complete(SurgeryAggregate s, PreOpChecklistItemKind kind) =>
        s.CompleteChecklistItem(new ChecklistCompleteDto(kind, "done", null, null, BookedAt, User(), Guid.NewGuid()));

    /// <summary>A surgery in PreOp with every item done or waived except consent (judged by the registry at start).</summary>
    private static SurgeryAggregate PreOpReadyExceptConsent()
    {
        var s = Scheduled();
        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "Young healthy cat", true, BookedAt, User(Vet), Guid.NewGuid()));
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 12), 1, Vet, BookedAt);
        s.RecordAsaGrade(new AsaGradeDto(AsaGrade.I, false, true, BookedAt, User(Vet), Guid.NewGuid()));
        s.Admit(new SurgeryAdmitDto(StartsOn.AddHours(-1), Zone, 12, User(), Guid.NewGuid()));
        return s;
    }

    private static SurgeryStartDto Start(bool consent, string? reason = null, bool vet = false) =>
        new(consent, reason, vet, StartsOn, User(Vet), Guid.NewGuid());

    private static SurgeryAggregate InRecovery(ServiceCategory? category = null, ProcedureOutcome? outcome = null)
    {
        var s = Scheduled(category);
        // go through the real path: all items, admit, start with consent, end.
        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "x y z", true, BookedAt, User(Vet), Guid.NewGuid()));
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), 1, Vet, BookedAt);
        s.RecordAsaGrade(new AsaGradeDto(AsaGrade.II, false, true, BookedAt, User(Vet), Guid.NewGuid()));
        s.Admit(new SurgeryAdmitDto(StartsOn.AddHours(-1), Zone, 12, User(), Guid.NewGuid()));
        s.Start(Start(consent: true));
        s.EndProcedure(new SurgeryEndProcedureDto(outcome ?? ProcedureOutcome.Completed, StartsOn.AddHours(1), Zone, User(Vet), Guid.NewGuid()));
        return s;
    }

    // ── U-A2 / U-A3 / U-A4 ───────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-A2 -- every transition appends exactly one StatusHistory row (and the creation row is the baseline).</summary>
    [Fact]
    public void U_A2_EveryTransition_AppendsOneStatusHistoryRow()
    {
        var s = Scheduled();
        Assert.Single(s.StatusHistory);
        Assert.Null(s.StatusHistory[0].From);

        void Step(Action act, SurgeryStatus expectedTo)
        {
            var before = s.StatusHistory.Count;
            act();
            Assert.Equal(before + 1, s.StatusHistory.Count);
            Assert.Equal(expectedTo, s.StatusHistory[^1].To);
            Assert.Equal(expectedTo, s.Status);
        }

        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "Young healthy cat", true, BookedAt, User(Vet), Guid.NewGuid()));
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), 1, Vet, BookedAt);
        s.RecordAsaGrade(new AsaGradeDto(AsaGrade.I, false, true, BookedAt, User(Vet), Guid.NewGuid()));

        Step(() => s.Admit(new SurgeryAdmitDto(StartsOn.AddHours(-2), Zone, 12, User(), Guid.NewGuid())), SurgeryStatus.PreOp);
        Step(() => s.Start(Start(consent: true)), SurgeryStatus.InTheatre);
        Step(() => s.EndProcedure(new SurgeryEndProcedureDto(ProcedureOutcome.Completed, StartsOn.AddHours(1), Zone, User(Vet), Guid.NewGuid())), SurgeryStatus.Recovery);
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.ReadyForDischarge, null, StartsOn.AddHours(2), User(), Guid.NewGuid()));
        Step(() => s.Discharge(new SurgeryDischargeDto("Rest.", [], false, StartsOn.AddHours(3), User(), Guid.NewGuid())), SurgeryStatus.Discharged);
    }

    /// <summary>U-A3 -- the early-admission window: 09:00 surgery, 12 h early. 14:00 the day before is too early; 21:30 the day before is allowed; the day itself is allowed.</summary>
    [Fact]
    public void U_A3_Admit_TheDayBeforeTooEarly_TheEveningBeforeAllowed()
    {
        var tooEarly = Scheduled();
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            tooEarly.Admit(new SurgeryAdmitDto(new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero) /* 14:00 Dhaka */, Zone, 12, User(), Guid.NewGuid())));
        Assert.Equal(BusinessViolationCodes.AdmissionNotToday, ex.ErrorCode);
        Assert.Equal(SurgeryStatus.Scheduled, tooEarly.Status);

        var evening = Scheduled();
        evening.Admit(new SurgeryAdmitDto(new DateTimeOffset(2026, 10, 11, 15, 30, 0, TimeSpan.Zero) /* 21:30 Dhaka, 11.5 h before */, Zone, 12, User(), Guid.NewGuid()));
        Assert.Equal(SurgeryStatus.PreOp, evening.Status);

        var sameDay = Scheduled();
        sameDay.Admit(new SurgeryAdmitDto(new DateTimeOffset(2026, 10, 11, 18, 5, 0, TimeSpan.Zero) /* 00:05 Dhaka on the day */, Zone, 0, User(), Guid.NewGuid()));
        Assert.Equal(SurgeryStatus.PreOp, sameDay.Status);
    }

    /// <summary>U-A4 -- a surgery already in theatre cannot be cancelled.</summary>
    [Fact]
    public void U_A4_Cancel_FromInTheatre_IsInvalid()
    {
        var s = PreOpReadyExceptConsent();
        s.Start(Start(consent: true));

        var ex = Assert.Throws<DomainRuleViolationException>(() => s.Cancel(new SurgeryCancelDto("Changed my mind", StartsOn, User(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidSurgeryTransition, ex.ErrorCode);
        Assert.Equal("A surgery in theatre cannot be cancelled.", ex.Message);
    }

    [Fact]
    public void Cancel_NeedsAReason_AndRaisesTheEvent()
    {
        var s = Scheduled();
        Assert.Equal(BusinessViolationCodes.CancellationReasonRequired, Assert.Throws<DomainRuleViolationException>(() => s.Cancel(new SurgeryCancelDto("  ", StartsOn, User(), Guid.NewGuid()))).ErrorCode);

        s.Cancel(new SurgeryCancelDto("Owner cancelled", BookedAt, User(), Guid.NewGuid()));

        Assert.Equal(SurgeryStatus.Cancelled, s.Status);
        Assert.Equal("Owner cancelled", s.CancellationReason);
        Assert.Contains(s.Events, e => e is SurgeryCancelledEvent);
    }

    // ── U-B8 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-B8 -- a reschedule changes the stamp, reverts the fasting item, keeps a weight still inside the new window and reverts one outside it.</summary>
    [Fact]
    public void U_B8_Reschedule_ChangesTheStamp_RevertsFasting_AndJudgesTheWeightAgainstTheNewDate()
    {
        var s = Scheduled();
        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 11), new DateOnly(2026, 10, 12), 1, Vet, BookedAt);
        var oldStamp = s.ScheduleStamp;

        // New date 13 Oct: a weight on 11 Oct is 2 days before -> outside a 1-day window; fasting reverts.
        var later = StartsOn.AddDays(1);
        s.Reschedule(new SurgeryRescheduleDto(s.TheatreId, s.SurgeonId, s.AssistantId, later, later.AddHours(1), later.AddHours(-1), "New fasting text", new DateOnly(2026, 10, 13), 1, BookedAt, User(), Guid.NewGuid()));

        Assert.NotEqual(oldStamp, s.ScheduleStamp);
        Assert.Equal(ChecklistItemStatus.Outstanding, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.FastingInstructionsIssued).Status);
        Assert.Equal(ChecklistItemStatus.Outstanding, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.WeightRecorded).Status);
        Assert.Equal("New fasting text", s.FastingInstructions);

        // A weight inside the new window survives a further move to the day after.
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 13), 1, Vet, BookedAt);
        var after = later.AddDays(1);
        s.Reschedule(new SurgeryRescheduleDto(s.TheatreId, s.SurgeonId, s.AssistantId, after, after.AddHours(1), after.AddHours(-1), "x", new DateOnly(2026, 10, 14), 1, BookedAt, User(), Guid.NewGuid()));
        Assert.Equal(ChecklistItemStatus.Done, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.WeightRecorded).Status);
    }

    [Fact]
    public void Reschedule_IsOnlyFromScheduled()
    {
        var s = PreOpReadyExceptConsent();
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            s.Reschedule(new SurgeryRescheduleDto(s.TheatreId, s.SurgeonId, s.AssistantId, StartsOn.AddDays(1), StartsOn.AddDays(1).AddHours(1), StartsOn, "x", new DateOnly(2026, 10, 13), 1, BookedAt, User(), Guid.NewGuid())));
        Assert.Equal(BusinessViolationCodes.InvalidSurgeryTransition, ex.ErrorCode);
    }

    // ── C: checklist and start ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-C1 -- a new surgery has five items, all Outstanding, in checklist order.</summary>
    [Fact]
    public void U_C1_ANewSurgery_HasFiveOutstandingItems_InOrder()
    {
        var s = Scheduled();

        Assert.Equal(PreOpChecklistRules.Order, s.Checklist.Select(i => i.Kind).ToList());
        Assert.All(s.Checklist, i => Assert.Equal(ChecklistItemStatus.Outstanding, i.Status));
        Assert.Equal(5, PreOpChecklistRules.Outstanding(s.Checklist).Count);
    }

    /// <summary>U-C2 -- the blood work is waivable by a vet with a reason; consent and fasting never are; a non-vet cannot waive.</summary>
    [Fact]
    public void U_C2_OnlyTheBloodWork_IsWaivable_ByAVet_WithAReason()
    {
        var s = Scheduled();

        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "Young healthy cat", true, BookedAt, User(Vet), Guid.NewGuid()));
        var bloods = s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.PreAnaestheticBloodWork);
        Assert.Equal(ChecklistItemStatus.Waived, bloods.Status);
        Assert.Equal("Young healthy cat", bloods.WaiverReason);

        foreach (var kind in new[] { PreOpChecklistItemKind.ConsentSigned, PreOpChecklistItemKind.FastingInstructionsIssued, PreOpChecklistItemKind.WeightRecorded })
            Assert.Equal(BusinessViolationCodes.ChecklistItemNotWaivable,
                Assert.Throws<DomainRuleViolationException>(() => s.WaiveChecklistItem(new ChecklistWaiveDto(kind, "because", true, BookedAt, User(Vet), Guid.NewGuid()))).ErrorCode);

        var other = Scheduled();
        Assert.Equal(BusinessViolationCodes.ChecklistItemNotWaivable,
            Assert.Throws<DomainRuleViolationException>(() => other.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "x", false, BookedAt, User(), Guid.NewGuid()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.ChecklistItemNotWaivable,
            Assert.Throws<DomainRuleViolationException>(() => other.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, " ", true, BookedAt, User(Vet), Guid.NewGuid()))).ErrorCode);
    }

    [Fact]
    public void ConsentAndAsaGrade_CannotBeCompletedByHand_AndAnItemIsCompletedOnce()
    {
        var s = Scheduled();

        Assert.Equal(BusinessViolationCodes.ChecklistItemNotManual,
            Assert.Throws<DomainRuleViolationException>(() => Complete(s, PreOpChecklistItemKind.ConsentSigned)).ErrorCode);
        Assert.Equal(BusinessViolationCodes.ChecklistItemNotManual,
            Assert.Throws<DomainRuleViolationException>(() => Complete(s, PreOpChecklistItemKind.AsaGradeAssessed)).ErrorCode);

        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        Assert.Equal(BusinessViolationCodes.ChecklistItemAlreadyDone,
            Assert.Throws<DomainRuleViolationException>(() => Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued)).ErrorCode);

        Assert.Equal(BusinessViolationCodes.VeterinarianRequired,
            Assert.Throws<DomainRuleViolationException>(() => s.RecordAsaGrade(new AsaGradeDto(AsaGrade.I, false, false, BookedAt, User(), Guid.NewGuid()))).ErrorCode);
    }

    /// <summary>U-C3 -- consent recorded and every item done: the surgery is InTheatre.</summary>
    [Fact]
    public void U_C3_Start_WithConsentRecorded_AndAllItemsDone_GoesInTheatre()
    {
        var s = PreOpReadyExceptConsent();

        s.Start(Start(consent: true));

        Assert.Equal(SurgeryStatus.InTheatre, s.Status);
        Assert.Null(s.ConsentOverride);
        var started = Assert.Single(s.Events.OfType<SurgeryStartedEvent>());
        Assert.False(started.ConsentWasOverridden);
    }

    /// <summary>U-C4 -- US-SRG-03 AC1: consent not recorded, no override -> ConsentNotRecorded, and the surgery stays PreOp.</summary>
    [Fact]
    public void U_C4_Start_WithoutConsent_AndNoOverride_IsRefused()
    {
        var s = PreOpReadyExceptConsent();

        var ex = Assert.Throws<DomainRuleViolationException>(() => s.Start(Start(consent: false)));

        Assert.Equal(BusinessViolationCodes.ConsentNotRecorded, ex.ErrorCode);
        Assert.Equal(SurgeryStatus.PreOp, s.Status);
        Assert.Null(s.ConsentOverride);
    }

    /// <summary>U-C5 -- US-SRG-03 AC2: a 9-character reason is refused; 10 characters starts the surgery, stores the override and raises the audit event.</summary>
    [Fact]
    public void U_C5_Override_NeedsAReasonOfTenCharacters_IsStored_AndRaisesTheEvent()
    {
        var s = PreOpReadyExceptConsent();

        var tooShort = Assert.Throws<DomainRuleViolationException>(() => s.Start(Start(false, "123456789", vet: true)));
        Assert.Equal(BusinessViolationCodes.ConsentOverrideReasonRequired, tooShort.ErrorCode);
        Assert.Equal(SurgeryStatus.PreOp, s.Status);
        Assert.Null(s.ConsentOverride);

        var notVet = Assert.Throws<DomainRuleViolationException>(() => s.Start(Start(false, "Emergency, owner unreachable", vet: false)));
        Assert.Equal(BusinessViolationCodes.ConsentOverrideNotPermitted, notVet.ErrorCode);

        var vet = User(Vet);
        s.Start(new SurgeryStartDto(false, "1234567890", true, StartsOn, vet, Guid.NewGuid()));

        Assert.Equal(SurgeryStatus.InTheatre, s.Status);
        Assert.Equal("1234567890", s.ConsentOverride!.Reason);
        Assert.Equal(Vet, s.ConsentOverride.OverriddenBy);
        Assert.Single(s.Events.OfType<SurgeryConsentOverriddenEvent>());
        Assert.True(Assert.Single(s.Events.OfType<SurgeryStartedEvent>()).ConsentWasOverridden);
    }

    [Fact]
    public void AnOverride_SentWhileConsentIsRecorded_IsIgnored()
    {
        var s = PreOpReadyExceptConsent();

        s.Start(Start(consent: true, "Emergency, owner unreachable", vet: true));

        Assert.Null(s.ConsentOverride);
        Assert.Empty(s.Events.OfType<SurgeryConsentOverriddenEvent>());
    }

    /// <summary>U-C6 -- consent recorded but the weight outstanding: PreOpChecklistIncomplete naming the weight.</summary>
    [Fact]
    public void U_C6_Start_WithAnOutstandingItem_NamesIt()
    {
        var s = Scheduled();
        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "Young healthy cat", true, BookedAt, User(Vet), Guid.NewGuid()));
        s.RecordAsaGrade(new AsaGradeDto(AsaGrade.I, false, true, BookedAt, User(Vet), Guid.NewGuid()));
        s.Admit(new SurgeryAdmitDto(StartsOn.AddHours(-1), Zone, 12, User(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => s.Start(Start(consent: true)));

        Assert.Equal(BusinessViolationCodes.PreOpChecklistIncomplete, ex.ErrorCode);
        Assert.Contains("weight recorded", ex.Message);
        Assert.DoesNotContain("fasting", ex.Message);
        Assert.Equal(SurgeryStatus.PreOp, s.Status);
    }

    /// <summary>U-C7 -- the registry wins: the checklist says consent is Done, but the registry (the handler's input) says it is not -> ConsentNotRecorded.</summary>
    [Fact]
    public void U_C7_TheRegistryWins_OverAStaleChecklistFlag()
    {
        var s = PreOpReadyExceptConsent();
        s.ApplyConsentCoverage(true, Guid.NewGuid(), Vet, BookedAt);
        Assert.Equal(ChecklistItemStatus.Done, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.ConsentSigned).Status);

        var ex = Assert.Throws<DomainRuleViolationException>(() => s.Start(Start(consent: false)));

        Assert.Equal(BusinessViolationCodes.ConsentNotRecorded, ex.ErrorCode);
    }

    /// <summary>U-C7 (other direction) -- the checklist flag is never consulted: consent on record starts the surgery even with the consent item still Outstanding.</summary>
    [Fact]
    public void U_C7_AnOutstandingConsentItem_DoesNotBlock_WhenTheRegistryHasConsent()
    {
        var s = PreOpReadyExceptConsent();
        Assert.Equal(ChecklistItemStatus.Outstanding, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.ConsentSigned).Status);

        s.Start(Start(consent: true));

        Assert.Equal(SurgeryStatus.InTheatre, s.Status);
    }

    /// <summary>U-C8 -- the weight handler's rule: 2 days before (validity 1) does not complete; the same day does; a repeated event changes nothing.</summary>
    [Fact]
    public void U_C8_Weight_TwoDaysBefore_DoesNotComplete_TheSameDayDoes_ARepeatIsAnOp()
    {
        var s = Scheduled();
        var weightId = Guid.NewGuid();
        var surgeryDate = new DateOnly(2026, 10, 12);

        Assert.False(s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 10), surgeryDate, 1, Vet, BookedAt));
        Assert.Equal(ChecklistItemStatus.Outstanding, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.WeightRecorded).Status);

        Assert.True(s.ApplyWeight(weightId, surgeryDate, surgeryDate, 1, Vet, BookedAt));
        var version = s.Version;
        Assert.False(s.ApplyWeight(weightId, surgeryDate, surgeryDate, 1, Vet, BookedAt));
        Assert.Equal(version, s.Version);
        Assert.Equal(weightId, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.WeightRecorded).ReferenceId);

        // A weight AFTER the surgery date does not count either.
        var later = Scheduled();
        Assert.False(later.ApplyWeight(Guid.NewGuid(), surgeryDate.AddDays(1), surgeryDate, 1, Vet, BookedAt));
    }

    [Fact]
    public void ConsentCoverage_CompletesAndReverts_TheConsentItem_Idempotently()
    {
        var s = Scheduled();
        var consentId = Guid.NewGuid();

        Assert.True(s.ApplyConsentCoverage(true, consentId, Vet, BookedAt));
        Assert.False(s.ApplyConsentCoverage(true, consentId, Vet, BookedAt));
        Assert.Equal(consentId, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.ConsentSigned).ReferenceId);

        Assert.True(s.ApplyConsentCoverage(false, null, Vet, BookedAt));
        Assert.False(s.ApplyConsentCoverage(false, null, Vet, BookedAt));
        Assert.Equal(ChecklistItemStatus.Outstanding, s.Checklist.First(i => i.Kind == PreOpChecklistItemKind.ConsentSigned).Status);
    }

    // ── E: end of procedure ─────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-E1 -- a Completed spay: Recovery, Recovering, and the sterilisation event carrying the branch-local date.</summary>
    [Fact]
    public void U_E1_EndProcedure_Completed_OnASpay_RaisesTheSterilisationEvent_WithTheLocalDate()
    {
        // 11 Oct 2026 20:30 UTC = 12 Oct 02:30 Dhaka: the local date is NOT the UTC date.
        var s = Scheduled();
        Complete(s, PreOpChecklistItemKind.FastingInstructionsIssued);
        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "x y z", true, BookedAt, User(Vet), Guid.NewGuid()));
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), 1, Vet, BookedAt);
        s.RecordAsaGrade(new AsaGradeDto(AsaGrade.I, false, true, BookedAt, User(Vet), Guid.NewGuid()));
        s.Admit(new SurgeryAdmitDto(StartsOn.AddHours(-1), Zone, 12, User(), Guid.NewGuid()));
        s.Start(Start(true));

        s.EndProcedure(new SurgeryEndProcedureDto(ProcedureOutcome.Completed, new DateTimeOffset(2026, 10, 11, 20, 30, 0, TimeSpan.Zero), Zone, User(Vet), Guid.NewGuid()));

        Assert.Equal(SurgeryStatus.Recovery, s.Status);
        Assert.Equal(RecoveryStatus.Recovering, s.RecoveryStatus);
        Assert.Equal(ProcedureOutcome.Completed, s.Outcome);
        var sterilised = Assert.Single(s.Events.OfType<SterilisationCompletedEvent>());
        Assert.Equal(new DateOnly(2026, 10, 12), sterilised.CompletedOnLocalDate);
        Assert.Equal(s.CatId, sterilised.CatId);
        Assert.Single(s.RecoveryHistory);
    }

    /// <summary>U-E2 -- an Abandoned spay, and a Completed dental: no sterilisation event.</summary>
    [Theory]
    [InlineData("Spay", "Abandoned")]
    [InlineData("Dental", "Completed")]
    [InlineData("Neuter", "Abandoned")]
    public void U_E2_NoSterilisationEvent_ForAnAbandonedSterilisation_OrANonSterilisingProcedure(string category, string outcome)
    {
        var s = InRecovery(Enumeration.FromDisplayName<ServiceCategory>(category), Enumeration.FromDisplayName<ProcedureOutcome>(outcome));

        Assert.Empty(s.Events.OfType<SterilisationCompletedEvent>());
        Assert.Single(s.Events.OfType<SurgeryProcedureEndedEvent>());
    }

    [Fact]
    public void ANeuter_Completed_AlsoRaisesTheSterilisationEvent()
    {
        var s = InRecovery(ServiceCategory.Neuter);
        Assert.Single(s.Events.OfType<SterilisationCompletedEvent>());
    }

    // ── F: recovery and discharge ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-F1 -- status changes append history rows; a change to the same status is a no-op.</summary>
    [Fact]
    public void U_F1_RecoveryStatus_ChangesAreRecorded_AndTheSameStatusIsANoOp()
    {
        var s = InRecovery();
        Assert.Single(s.RecoveryHistory);

        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.Stable, "Walking about", StartsOn.AddHours(2), User(), Guid.NewGuid()));
        Assert.Equal(2, s.RecoveryHistory.Count);
        Assert.Equal(RecoveryStatus.Stable, s.RecoveryStatus);
        Assert.Equal("Walking about", s.RecoveryHistory[^1].Note);

        var version = s.Version;
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.Stable, null, StartsOn.AddHours(3), User(), Guid.NewGuid()));
        Assert.Equal(2, s.RecoveryHistory.Count);
        Assert.Equal(version, s.Version);

        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.Recovering, null, StartsOn.AddHours(4), User(), Guid.NewGuid()));
        Assert.Equal(3, s.RecoveryHistory.Count);
    }

    /// <summary>U-F2 -- discharge while Stable is refused.</summary>
    [Fact]
    public void U_F2_Discharge_WhileNotReady_IsRefused()
    {
        var s = InRecovery();
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.Stable, null, StartsOn.AddHours(2), User(), Guid.NewGuid()));

        var ex = Assert.Throws<DomainRuleViolationException>(() => s.Discharge(new SurgeryDischargeDto("Rest.", [], false, StartsOn.AddHours(3), User(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.NotReadyForDischarge, ex.ErrorCode);
        Assert.Equal(SurgeryStatus.Recovery, s.Status);
    }

    // HO-BIL-001 Phase J (owner decision 2026-10-14, "owner at discharge"): the cat's owner at discharge is stored and carried on the event; the booking customer is unchanged.
    [Fact]
    public void Discharge_StoresTheOwnerAtDischarge_AndCarriesItOnTheEvent()
    {
        var s = InRecovery();
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.ReadyForDischarge, null, StartsOn.AddHours(2), User(), Guid.NewGuid()));
        var owner = Guid.NewGuid();

        s.Discharge(new SurgeryDischargeDto("Rest.", [], false, StartsOn.AddHours(3), User(), Guid.NewGuid(), owner));

        Assert.Equal(owner, s.OwnerAtDischargeId);
        Assert.Equal(owner, Assert.Single(s.Events.OfType<CatEcommerce.Domain.Events.Surgery.SurgeryDischargedEvent>()).OwnerAtDischargeId);
        Assert.NotEqual(owner, s.CustomerId);
    }

    [Fact]
    public void Discharge_NeedsInstructions_AndEachFollowUpInsideItsWindow()
    {
        var s = InRecovery();
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.ReadyForDischarge, null, StartsOn.AddHours(2), User(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.PostOpInstructionsRequired,
            Assert.Throws<DomainRuleViolationException>(() => s.Discharge(new SurgeryDischargeDto(" ", [], false, StartsOn.AddHours(3), User(), Guid.NewGuid()))).ErrorCode);

        // U-F4: a follow-up date outside its window.
        var outside = new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));
        Assert.Equal(BusinessViolationCodes.FollowUpOutsideWindow,
            Assert.Throws<DomainRuleViolationException>(() => s.Discharge(new SurgeryDischargeDto("Rest.", [outside], false, StartsOn.AddHours(3), User(), Guid.NewGuid()))).ErrorCode);

        var inside = new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));
        s.Discharge(new SurgeryDischargeDto("Rest.", [inside], false, StartsOn.AddHours(3), User(), Guid.NewGuid()));

        Assert.Equal(SurgeryStatus.Discharged, s.Status);
        Assert.Equal("Rest.", s.PostOpInstructions);
        Assert.Single(s.FollowUps);
        Assert.Null(s.RecoveryStatus);
    }

    /// <summary>U-F6 -- a deceased cat may be discharged (closed) from any recovery status, with no follow-ups and no instructions.</summary>
    [Fact]
    public void U_F6_ADeceasedCat_CanBeDischarged_FromAnyRecoveryStatus_WithNothingElse()
    {
        var s = InRecovery();
        Assert.Equal(RecoveryStatus.Recovering, s.RecoveryStatus);

        s.Discharge(new SurgeryDischargeDto(null, [new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16))], true, StartsOn.AddHours(3), User(), Guid.NewGuid()));

        Assert.Equal(SurgeryStatus.Discharged, s.Status);
        Assert.Empty(s.FollowUps);
        Assert.Null(s.PostOpInstructions);
        Assert.True(Assert.Single(s.Events.OfType<SurgeryDischargedEvent>()).CatWasDeceased);
    }

    [Fact]
    public void FollowUps_AreClosedOnce_CompletedOrCancelled()
    {
        var s = InRecovery();
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.ReadyForDischarge, null, StartsOn.AddHours(2), User(), Guid.NewGuid()));
        var a = new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));
        var b = new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.SutureRemoval, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 26));
        s.Discharge(new SurgeryDischargeDto("Rest.", [a, b], false, StartsOn.AddHours(3), User(), Guid.NewGuid()));

        s.CloseFollowUp(new FollowUpCloseDto(a.Id, FollowUpStatus.Done, null, StartsOn.AddDays(3), User(), Guid.NewGuid()));
        Assert.Equal(FollowUpStatus.Done, s.FollowUps.First(f => f.Id == a.Id).Status);

        Assert.Equal(BusinessViolationCodes.FollowUpNotPending,
            Assert.Throws<DomainRuleViolationException>(() => s.CloseFollowUp(new FollowUpCloseDto(a.Id, FollowUpStatus.Cancelled, "Changed mind", StartsOn.AddDays(3), User(), Guid.NewGuid()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.CancellationReasonRequired,
            Assert.Throws<DomainRuleViolationException>(() => s.CloseFollowUp(new FollowUpCloseDto(b.Id, FollowUpStatus.Cancelled, " ", StartsOn.AddDays(3), User(), Guid.NewGuid()))).ErrorCode);
        s.CloseFollowUp(new FollowUpCloseDto(b.Id, FollowUpStatus.Cancelled, "Absorbable sutures were used", StartsOn.AddDays(3), User(), Guid.NewGuid()));
        Assert.Equal(FollowUpStatus.Cancelled, s.FollowUps.First(f => f.Id == b.Id).Status);
        Assert.Equal(BusinessViolationCodes.SurgeryFollowUpNotFound,
            Assert.Throws<DomainRuleViolationException>(() => s.CloseFollowUp(new FollowUpCloseDto(Guid.NewGuid(), FollowUpStatus.Done, null, StartsOn, User(), Guid.NewGuid()))).ErrorCode);
    }

    [Fact]
    public void ReassignOwner_IsIdempotent()
    {
        var s = Scheduled();
        var newOwner = Guid.NewGuid();

        Assert.True(s.ReassignOwner(newOwner));
        Assert.False(s.ReassignOwner(newOwner));
        Assert.Equal(newOwner, s.CustomerId);
    }

    /// <summary>U-X1 -- reflection over Events/Surgery: no event carries a string property (no reasons, notes, instructions or names).</summary>
    [Fact]
    public void U_X1_NoSurgeryEvent_HasAStringProperty()
    {
        var events = typeof(SurgeryScheduledEvent).Assembly.GetTypes()
            .Where(t => t.Namespace == "CatEcommerce.Domain.Events.Surgery" && !t.IsAbstract && typeof(Platform.Infrastructure.Core.Events.Event).IsAssignableFrom(t)).ToList();
        Assert.NotEmpty(events);

        var offenders = events.SelectMany(t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly).Where(p => p.PropertyType == typeof(string)).Select(p => $"{t.Name}.{p.Name}")).ToList();
        Assert.True(offenders.Count == 0, "String properties on surgery events: " + string.Join(", ", offenders));
    }

    /// <summary>U-X2 -- the Surgery action table matches §12.3: OverrideConsent and LogDrug are Vet only; Administrator has no clinical write.</summary>
    [Fact]
    public void U_X2_TheSurgeryActionRoles_MatchSection12_3()
    {
        var table = ActionRoles.BuildTable(typeof(ActionRoles));

        string[] Roles(string action) => table[action].OrderBy(r => r).ToArray();

        Assert.Equal([StaffRoles.Vet], Roles(Actions.Surgery.OverrideConsent));
        Assert.Equal([StaffRoles.Vet], Roles(Actions.Surgery.LogDrug));
        Assert.Equal([StaffRoles.Vet], Roles(Actions.Surgery.AssessRisk));
        Assert.Equal([StaffRoles.Vet], Roles(Actions.Surgery.EndProcedure));
        Assert.Equal([StaffRoles.Vet], Roles(Actions.Surgery.WriteReport));
        Assert.Equal([StaffRoles.Administrator], Roles(Actions.Surgery.ManageCampaigns));
        Assert.Equal([StaffRoles.Administrator, StaffRoles.Vet], Roles(Actions.Surgery.ManageProtocols));
        Assert.Equal([StaffRoles.Administrator, StaffRoles.Nurse, StaffRoles.Reception, StaffRoles.Vet], Roles(Actions.Surgery.View));
        Assert.Equal([StaffRoles.Administrator, StaffRoles.Reception, StaffRoles.Vet], Roles(Actions.Surgery.Schedule));
        Assert.Equal([StaffRoles.Nurse, StaffRoles.Vet], Roles(Actions.Surgery.LogVitals));
        Assert.Equal([StaffRoles.Nurse, StaffRoles.Vet], Roles(Actions.Surgery.Start));
        Assert.Equal([StaffRoles.Nurse, StaffRoles.Vet], Roles(Actions.Surgery.ManageChecklist));
        Assert.Equal([StaffRoles.Nurse, StaffRoles.Vet], Roles(Actions.Surgery.ManageRecovery));
        Assert.Equal([StaffRoles.Nurse, StaffRoles.Reception, StaffRoles.Vet], Roles(Actions.Surgery.Admit));

        // Administrator holds no clinical write: not start, not log, not report, not end, not override.
        foreach (var action in new[] { Actions.Surgery.Start, Actions.Surgery.LogVitals, Actions.Surgery.LogDrug, Actions.Surgery.EndProcedure, Actions.Surgery.WriteReport, Actions.Surgery.OverrideConsent, Actions.Surgery.ManageRecovery })
            Assert.DoesNotContain(StaffRoles.Administrator, table[action]);
    }
}
