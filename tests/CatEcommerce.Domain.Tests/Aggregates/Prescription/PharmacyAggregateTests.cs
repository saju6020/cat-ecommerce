using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Aggregates.Prescription;

/// <summary>HO-PHM-001 §15 groups B (formulary), P (prescription), R (repeats) and G (diet authorisation) -- the aggregates' own rules.</summary>
public sealed class PharmacyAggregateTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    private static PharmacyActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    private static DomainRuleViolationException Violation(Action action) => Assert.Throws<DomainRuleViolationException>(action);

    // ── B: formulary ────────────────────────────────────────────────────────────────────────────

    private static FormularyMedicineDto FormularyDto(
        DosingBasis? basis = null, DoseRangeInput? range = null, bool withRange = true, InteractionInput[]? interactions = null,
        AdministrationRoute? defaultRoute = null, AdministrationRoute[]? allowed = null) => new(
        "Meloxicam", ["meloxicam"], ["nsaid"], MedicineForm.OralLiquid, new StrengthInput(1.5m, StrengthUnit.Mg, 1m, DispensingUnit.Millilitre),
        defaultRoute ?? AdministrationRoute.Oral, allowed ?? [AdministrationRoute.Oral], basis ?? DosingBasis.PerKg,
        withRange ? range ?? new DoseRangeInput(0.05m, 0.1m) : null, "None", interactions ?? [], DispensingUnit.Millilitre, [], Actor());

    [Fact]
    public void U_B1_APerKgMedicineNeedsARange_AFixedMedicineMayNotHaveOne()
    {
        Assert.Equal(BusinessViolationCodes.DoseRangeRequired, Violation(() => new FormularyMedicineAggregate(Guid.NewGuid()).Create("MEL", FormularyDto(withRange: false))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidDoseRange, Violation(() => new FormularyMedicineAggregate(Guid.NewGuid()).Create("MEL", FormularyDto(basis: DosingBasis.Fixed))).ErrorCode);
    }

    [Fact]
    public void U_B2_AMinimumAboveTheMaximum_IsAnInvalidRange() =>
        Assert.Equal(BusinessViolationCodes.InvalidDoseRange, Violation(() => new FormularyMedicineAggregate(Guid.NewGuid()).Create("MEL", FormularyDto(range: new DoseRangeInput(0.2m, 0.1m)))).ErrorCode);

    [Fact]
    public void U_B3_AControlledMedicineNeedsASchedule_AndClearingItWithRegisterEntriesNeedsAReason()
    {
        var medicine = new FormularyMedicineAggregate(Guid.NewGuid());
        medicine.Create("MEL", FormularyDto());

        Assert.Equal(BusinessViolationCodes.ControlledScheduleRequired, Violation(() => medicine.SetControlled(true, " ", null, false, Actor())).ErrorCode);

        medicine.SetControlled(true, "CD Schedule 2", null, false, Actor());
        Assert.True(medicine.IsControlled);
        Assert.Equal(BusinessViolationCodes.ControlledFlagChangeReasonRequired, Violation(() => medicine.SetControlled(false, null, null, true, Actor())).ErrorCode);

        medicine.SetControlled(false, null, "Reclassified by the regulator", true, Actor());
        Assert.False(medicine.IsControlled);
        Assert.Null(medicine.ControlledSchedule);
    }

    [Fact]
    public void U_B4_AnInteractionNamesExactlyOneTarget()
    {
        var both = new InteractionInput(Guid.NewGuid(), "nsaid", InteractionSeverity.Major, "x");
        var neither = new InteractionInput(null, null, InteractionSeverity.Major, "x");

        foreach (var bad in new[] { both, neither })
        {
            var medicine = new FormularyMedicineAggregate(Guid.NewGuid());
            Assert.Equal(BusinessViolationCodes.InvalidInteraction, Violation(() => medicine.Create("MEL", FormularyDto(interactions: [bad]))).ErrorCode);
        }
    }

    [Fact]
    public void U_B5_ADefaultRouteOutsideTheAllowedRoutes_IsAViolation() =>
        Assert.Equal(BusinessViolationCodes.RouteNotAllowedForMedicine,
            Violation(() => new FormularyMedicineAggregate(Guid.NewGuid()).Create("MEL", FormularyDto(defaultRoute: AdministrationRoute.Subcutaneous, allowed: [AdministrationRoute.Oral]))).ErrorCode);

    // ── P / R: the prescription ─────────────────────────────────────────────────────────────────

    private static FormularyMedicineSnapshot FixedMedicine() => new(
        Guid.NewGuid(), "AMX", "Amoxicillin", MedicineForm.OralLiquid, new Strength(50m, StrengthUnit.Mg, 1m, DispensingUnit.Millilitre), AdministrationRoute.Oral,
        [AdministrationRoute.Oral], DosingBasis.Fixed, null, "None", ["amoxicillin"], ["penicillin"], [], false, null, DispensingUnit.Millilitre, [], true);

    private static PrescriptionAggregate Issue(
        bool repeatable = false, int repeats = 0, DateOnly? validUntil = null, DateOnly? startsOn = null, int durationDays = 5, int maxRepeatValidityDays = 180)
    {
        var medicine = FixedMedicine();
        var dose = new Dose(2m, DoseUnit.Ml);
        var assessment = PrescriptionSafetyAssessor.Assess(new SafetyInput(
            medicine, dose, AdministrationRoute.Oral, DoseFrequency.OnceDaily, "Mitu", null, Today, 30, [], true, [], []));

        var prescription = new PrescriptionAggregate(Guid.NewGuid());
        prescription.Issue(new PrescriptionIssueDto(
            "RX-2026-000001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, Guid.NewGuid(), medicine, dose, AdministrationRoute.Oral,
            DoseFrequency.OnceDaily, durationDays, 10m, "Give with food.", startsOn ?? Today, repeatable, repeats, validUntil, Today, 30, maxRepeatValidityDays, assessment,
            new SafetyAcknowledgementInput(null, null, null), Now, null, Actor()));
        return prescription;
    }

    [Fact]
    public void U_P1_OnlyTheArrowsOfTheStateMachineAreAllowed()
    {
        var expected = new Dictionary<string, PrescriptionStatus[]>
        {
            [PrescriptionActions.Dispense] = [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
            [PrescriptionActions.Cancel] = [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
            [PrescriptionActions.Expire] = [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
            [PrescriptionActions.Renew] = [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed, PrescriptionStatus.Expired],
            [PrescriptionActions.ReverseSupply] = [PrescriptionStatus.Dispensed, PrescriptionStatus.Completed],
            [PrescriptionActions.DeclineRepeat] = [PrescriptionStatus.Issued, PrescriptionStatus.Dispensed],
        };

        foreach (var status in Enumeration.GetAll<PrescriptionStatus>())
        {
            foreach (var action in PrescriptionActionRules.AllActions)
                Assert.Equal(expected[action].Contains(status), PrescriptionActionRules.IsAllowed(status, action));
        }
    }

    [Fact]
    public void U_P1b_AnArrowThatIsNotAllowed_IsRefused_NamingBothStates()
    {
        var cancelled = Issue();
        cancelled.Cancel(Guid.NewGuid(), "Wrong cat", Now, Actor());

        var violation = Violation(() => cancelled.Cancel(Guid.NewGuid(), "Again", Now, Actor()));
        Assert.Equal(BusinessViolationCodes.InvalidPrescriptionTransition, violation.ErrorCode);
        Assert.Contains("Cancelled", violation.Message);

        Assert.Equal(BusinessViolationCodes.InvalidPrescriptionTransition, Violation(() => cancelled.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now)).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidPrescriptionTransition, Violation(() => cancelled.MarkRenewed(Guid.NewGuid())).ErrorCode);
    }

    [Fact]
    public void U_P2_ASupplyOnValidUntilIsAllowed_TheDayAfterIsNot()
    {
        var prescription = Issue(validUntil: Today.AddDays(10));
        prescription.RecordSupply(Guid.NewGuid(), Today.AddDays(10), null, Guid.NewGuid(), Now);
        Assert.Equal(1, prescription.SuppliesDispensed);

        var late = Issue(validUntil: Today.AddDays(10), repeatable: true, repeats: 2);
        Assert.Equal(BusinessViolationCodes.PrescriptionValidityEnded, Violation(() => late.RecordSupply(Guid.NewGuid(), Today.AddDays(11), null, Guid.NewGuid(), Now)).ErrorCode);
    }

    [Fact]
    public void U_P3_TwoRepeats_AllowThreeSupplies_ThenNoSuppliesRemain()
    {
        var prescription = Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(90));

        prescription.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);
        Assert.Equal(PrescriptionStatus.Dispensed, prescription.Status);
        prescription.RecordSupply(Guid.NewGuid(), Today.AddDays(5), null, Guid.NewGuid(), Now);
        Assert.Equal(PrescriptionStatus.Dispensed, prescription.Status);
        prescription.RecordSupply(Guid.NewGuid(), Today.AddDays(10), null, Guid.NewGuid(), Now);
        Assert.Equal(PrescriptionStatus.Completed, prescription.Status);

        Assert.Equal(BusinessViolationCodes.NoSuppliesRemaining, Violation(() => prescription.RecordSupply(Guid.NewGuid(), Today.AddDays(15), null, Guid.NewGuid(), Now)).ErrorCode);
    }

    [Fact]
    public void U_P4_RepeatValidityIsCapped_AndANonRepeatableDefaultsToThirtyDays()
    {
        Assert.Equal(BusinessViolationCodes.InvalidValidity, Violation(() => Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(181))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.RepeatsRequireValidity, Violation(() => Issue(repeatable: true, repeats: 2)).ErrorCode);
        Assert.Equal(Today.AddDays(30), Issue().ValidUntil);
    }

    [Fact]
    public void U_P5_OnlyTheLatestSupplyCanBeReversed()
    {
        var prescription = Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(90));
        prescription.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);
        prescription.RecordSupply(Guid.NewGuid(), Today.AddDays(5), null, Guid.NewGuid(), Now);

        Assert.Equal(BusinessViolationCodes.DispensingNotReversible, Violation(() => prescription.ReverseSupply(1, null)).ErrorCode);

        prescription.ReverseSupply(2, Today);
        Assert.Equal(1, prescription.SuppliesDispensed);
        Assert.Equal(Today, prescription.LastDispensedOn);
        Assert.Equal(PrescriptionStatus.Dispensed, prescription.Status);
    }

    [Fact]
    public void U_P6_CancellingDeclinesAnOpenRepeatRequest_WithTheReason()
    {
        var prescription = Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(90));
        prescription.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);
        prescription.RequestRepeat(new RepeatRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Today, Now, Actor()));

        prescription.Cancel(Guid.NewGuid(), "Wrong dose", Now, Actor());

        var request = Assert.Single(prescription.RepeatRequests);
        Assert.Equal(RepeatRequestStatus.Declined, request.Status);
        Assert.Equal("Wrong dose", request.DeclineReason);
    }

    [Fact]
    public void U_R1_ARequestInsideTheWindowWithSuppliesLeft_IsRequested()
    {
        var prescription = Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(90));
        prescription.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);

        prescription.RequestRepeat(new RepeatRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Running low", Today.AddDays(4), Now, Actor()));

        Assert.Equal(RepeatRequestStatus.Requested, Assert.Single(prescription.RepeatRequests).Status);
    }

    [Fact]
    public void U_R2_ARequestAfterValidUntil_SaysWhenItEnded_AndThatAVetMustAuthoriseANewOne()
    {
        var prescription = Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(20));
        prescription.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);

        var violation = Violation(() => prescription.RequestRepeat(new RepeatRequestDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, Today.AddDays(21), Now, Actor())));

        Assert.Equal(BusinessViolationCodes.PrescriptionValidityEnded, violation.ErrorCode);
        Assert.Contains("2026", violation.Message);
        Assert.Contains("a vet needs to authorise a new prescription", violation.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void U_R3_TheOtherRefusals_AreEachTheirOwnCode()
    {
        RepeatRequestDto Request(DateOnly? on = null) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, on ?? Today, Now, Actor());

        var notRepeatable = Issue();
        Assert.Equal(BusinessViolationCodes.NotRepeatable, Violation(() => notRepeatable.RequestRepeat(Request())).ErrorCode);

        var firstNotOut = Issue(repeatable: true, repeats: 1, validUntil: Today.AddDays(60));
        Assert.Equal(BusinessViolationCodes.FirstSupplyNotDispensed, Violation(() => firstNotOut.RequestRepeat(Request())).ErrorCode);

        var used = Issue(repeatable: true, repeats: 1, validUntil: Today.AddDays(60));
        used.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);
        used.RecordSupply(Guid.NewGuid(), Today.AddDays(5), null, Guid.NewGuid(), Now);
        Assert.Equal(BusinessViolationCodes.NoRepeatsRemaining, Violation(() => used.RequestRepeat(Request())).ErrorCode);

        var open = Issue(repeatable: true, repeats: 2, validUntil: Today.AddDays(60));
        open.RecordSupply(Guid.NewGuid(), Today, null, Guid.NewGuid(), Now);
        open.RequestRepeat(Request());
        Assert.Equal(BusinessViolationCodes.RepeatRequestAlreadyOpen, Violation(() => open.RequestRepeat(Request())).ErrorCode);
    }

    [Fact]
    public void U_R4_TheRefillSchedule_RemindsInsideTheLeadWindow_AndNeverForARefillThatCannotBeMade()
    {
        var lastDispensed = new DateOnly(2026, 9, 1);
        var validUntil = new DateOnly(2026, 12, 31);

        RefillSchedule? Due(DateOnly today, int lead = 3, DateOnly? until = null) => RefillScheduleCalculator.Due(
            true, PrescriptionStatus.Dispensed, 1, lastDispensed, 30, until ?? validUntil, today, lead);

        Assert.Null(Due(new DateOnly(2026, 9, 27)));
        Assert.NotNull(Due(new DateOnly(2026, 9, 28)));
        Assert.NotNull(Due(new DateOnly(2026, 10, 1)));
        Assert.Null(Due(new DateOnly(2026, 10, 2)));
        Assert.Equal(new DateOnly(2026, 10, 1), Due(new DateOnly(2026, 9, 29))!.RefillDueOn);

        Assert.Null(Due(new DateOnly(2026, 9, 29), until: new DateOnly(2026, 9, 30))); // RefillDueOn > ValidUntil
        Assert.Null(Due(new DateOnly(2026, 9, 29), lead: 0)); // reminders off
    }

    // ── G: diet authorisation ───────────────────────────────────────────────────────────────────

    private static DietAuthorisationAggregate Authorise(DateOnly? from = null, DateOnly? to = null)
    {
        var authorisation = new DietAuthorisationAggregate(Guid.NewGuid());
        authorisation.Authorise(new DietAuthorisationDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Renal diet", Guid.NewGuid(), Guid.NewGuid(), from ?? Today, to ?? Today.AddDays(30), null, Today, 365, Now, Actor()));
        return authorisation;
    }

    [Fact]
    public void U_G2_ValidityBeyondThreeHundredSixtyFiveDays_IsRefused()
    {
        Assert.Equal(BusinessViolationCodes.InvalidDietValidity, Violation(() => Authorise(to: Today.AddDays(366))).ErrorCode);
        Authorise(to: Today.AddDays(365));
        Assert.Equal(BusinessViolationCodes.InvalidDietValidity, Violation(() => Authorise(from: Today.AddDays(-2))).ErrorCode);
    }

    [Fact]
    public void U_G3_AnAuthorisationThatEndedTenDaysAgoCanBeReactivated_OneThatEndedFortyDaysAgoCannot()
    {
        var recent = Authorise(to: Today.AddDays(5));
        var later = Today.AddDays(15); // ended 10 days ago
        recent.Expire(later, Actor());
        recent.Extend(later.AddDays(30), later, 365, Actor());
        Assert.Equal(DietAuthorisationStatus.Active, recent.Status);

        var old = Authorise(to: Today.AddDays(5));
        var muchLater = Today.AddDays(45); // ended 40 days ago
        old.Expire(muchLater, Actor());
        Assert.Equal(BusinessViolationCodes.DietAuthorisationNotExtendable, Violation(() => old.Extend(muchLater.AddDays(30), muchLater, 365, Actor())).ErrorCode);
    }

    [Fact]
    public void U_G3b_RevokingNeedsAReason_AndOnlyAnActiveAuthorisationCanBeRevoked()
    {
        var authorisation = Authorise();
        Assert.Equal(BusinessViolationCodes.RevocationReasonRequired, Violation(() => authorisation.Revoke(Guid.NewGuid(), " ", Now, Actor())).ErrorCode);

        authorisation.Revoke(Guid.NewGuid(), "Diagnosis changed", Now, Actor());
        Assert.Equal(DietAuthorisationStatus.Revoked, authorisation.Status);
        Assert.Equal(BusinessViolationCodes.DietAuthorisationNotExtendable, Violation(() => authorisation.Revoke(Guid.NewGuid(), "Again", Now, Actor())).ErrorCode);
        Assert.False(authorisation.CanExpire(Today.AddDays(100)));
    }
}
