using System.Reflection;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Visit;

/// <summary>HO-EMR-001 §15 U-B1..B7, U-C1..C7, U-D10 and the follow-up rules -- the visit aggregate in
/// isolation (the handler-level cases are in the Application tests).</summary>
public sealed class VisitAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 7);

    private static VisitOpeningDto Opening(AppointmentStatus? status = null, CatLifeStatus? life = null) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), status ?? AppointmentStatus.CheckedIn, life ?? CatLifeStatus.Alive,
        Guid.NewGuid(), "Consultation", ServiceCategory.Consultation, Now, TestUserContext.Create(), Guid.NewGuid());

    private static VisitAggregate OpenVisit()
    {
        var visit = new VisitAggregate(Guid.NewGuid());
        visit.Open(Opening());
        return visit;
    }

    private static VitalsDto Vitals(
        decimal? weight = null, decimal? temperature = null, int? heartRate = null, int? respiratoryRate = null,
        int? bcs = null, int? pain = null, params VitalSign[] confirmed) => new(
        Guid.NewGuid(), Now, weight, temperature, heartRate, respiratoryRate, bcs, pain, confirmed, null,
        Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

    private static ProblemDto Problem(string code = "OTITIS", bool active = true, Guid? id = null, Guid? codeId = null) =>
        new(id ?? Guid.NewGuid(), codeId ?? Guid.NewGuid(), code, "Name of " + code, active, ProblemCertainty.Confirmed, null);

    private static SoapNoteDto Note(IReadOnlyList<ProblemDto>? problems = null, string assessment = "A", string plan = "P",
        Guid? templateId = null, int? templateVersion = null) => new(
        "S", "O", assessment, plan, "Keep warm.", problems ?? [], templateId, templateVersion, Guid.NewGuid(), Now,
        TestUserContext.Create(), Guid.NewGuid());

    private static VisitFinalisationDto Finalisation(params string[] roles) =>
        new(roles, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid(), Guid.NewGuid());

    private static VisitAggregate FinalisedVisit()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([Problem()]));
        visit.Finalise(Finalisation(StaffRoles.Vet));
        return visit;
    }

    private static DomainRuleViolationException Violation(Action action) => Assert.Throws<DomainRuleViolationException>(action);

    // ── U-B1, U-B2 ─────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Booked", false)]
    [InlineData("Completed", false)]
    [InlineData("CheckedIn", true)]
    [InlineData("InConsultation", true)]
    public void Open_OnlyForACheckedInOrInConsultationAppointment(string status, bool opens)
    {
        var visit = new VisitAggregate(Guid.NewGuid());
        var dto = Opening(Enumeration.FromDisplayName<AppointmentStatus>(status));

        if (opens)
        {
            visit.Open(dto);
            Assert.Equal(VisitStatus.Open, visit.Status);
            Assert.Equal(dto.AppointmentId, visit.AppointmentId);
            Assert.Equal(dto.CatId, visit.CatId);
            Assert.Single(visit.Events.OfType<VisitOpenedEvent>());
        }
        else
        {
            Assert.Equal(BusinessViolationCodes.AppointmentNotReadyForVisit, Violation(() => visit.Open(dto)).ErrorCode);
        }
    }

    [Fact]
    public void Open_ForADeceasedCat_IsRefused()
    {
        var visit = new VisitAggregate(Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.CatIsDeceased, Violation(() => visit.Open(Opening(life: CatLifeStatus.Deceased))).ErrorCode);
    }

    [Fact]
    public void Open_HoldsNoCustomerId_UntilFinalise()
    {
        // ADR-117: owner-facing reads resolve the cat's *current* owner; a frozen row cannot follow a merge. HO-BIL-001 Phase J (owner decision 2026-10-14,
        // ADR-344) adds ONE exception: Finalise stores the owner-at-finalising as the billing customer. An open visit still holds none.
        var visit = new VisitAggregate(Guid.NewGuid());
        visit.Open(Opening());

        Assert.Null(visit.CustomerId);
        Assert.Equal(["CustomerId"], typeof(VisitAggregate).GetProperties().Select(p => p.Name).Where(n => n.Contains("Customer", StringComparison.Ordinal)).ToArray());
    }

    // ── U-B3..B7 vitals ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RecordVitals_WithNoValue_IsRefused()
    {
        var visit = OpenVisit();
        Assert.Equal(BusinessViolationCodes.VitalsRequired, Violation(() => visit.RecordVitals(Vitals())).ErrorCode);
    }

    [Theory]
    [InlineData(46.0)]
    [InlineData(385.0)]
    public void RecordVitals_TemperatureOutsideTheAbsoluteRange_IsRefusedAndNoConfirmationSavesIt(double temperature)
    {
        var visit = OpenVisit();

        var plain = Violation(() => visit.RecordVitals(Vitals(temperature: (decimal)temperature)));
        var confirmed = Violation(() => visit.RecordVitals(Vitals(temperature: (decimal)temperature, confirmed: VitalSign.Temperature)));

        Assert.Equal(BusinessViolationCodes.VitalSignOutOfRange, plain.ErrorCode);
        Assert.Equal(BusinessViolationCodes.VitalSignOutOfRange, confirmed.ErrorCode);
        Assert.Empty(visit.Vitals);
    }

    [Fact]
    public void RecordVitals_TemperatureOutsidePlausible_NeedsConfirmation_AndStoresIt()
    {
        var visit = OpenVisit();

        var refused = Violation(() => visit.RecordVitals(Vitals(temperature: 42.0m)));
        Assert.Equal(BusinessViolationCodes.VitalsOutsidePlausibleRange, refused.ErrorCode);
        Assert.Equal(VitalSign.Temperature.DisplayName, refused.PropertyName);
        Assert.Empty(visit.Vitals);

        visit.RecordVitals(Vitals(temperature: 42.0m, confirmed: VitalSign.Temperature));

        var entry = Assert.Single(visit.Vitals);
        Assert.Equal([VitalSign.Temperature], entry.ConfirmedSigns());
    }

    [Fact]
    public void RecordVitals_TwoWarnings_OneConfirmed_ListsOnlyTheUnconfirmedOne()
    {
        var visit = OpenVisit();

        var refused = Violation(() => visit.RecordVitals(Vitals(temperature: 42.0m, heartRate: 300, confirmed: VitalSign.Temperature)));

        Assert.Equal(BusinessViolationCodes.VitalsOutsidePlausibleRange, refused.ErrorCode);
        Assert.Contains("HeartRate", refused.Message);
        Assert.DoesNotContain("Temperature", refused.Message);
    }

    [Fact]
    public void RecordVitals_ConfirmationOfAValueThatNeededNone_IsNotStored()
    {
        var visit = OpenVisit();

        visit.RecordVitals(Vitals(temperature: 38.5m, confirmed: VitalSign.Temperature));

        Assert.Empty(Assert.Single(visit.Vitals).ConfirmedOutOfRange);
    }

    [Theory]
    [InlineData(35.0, true)]
    [InlineData(41.5, true)]
    [InlineData(34.9, false)]
    [InlineData(41.6, false)]
    public void RecordVitals_PlausibleBoundsAreInclusive(double temperature, bool noWarning)
    {
        var visit = OpenVisit();
        var dto = Vitals(temperature: (decimal)temperature);

        if (noWarning) visit.RecordVitals(dto);
        else Assert.Equal(BusinessViolationCodes.VitalsOutsidePlausibleRange, Violation(() => visit.RecordVitals(dto)).ErrorCode);
    }

    [Fact]
    public void RecordVitals_AllSixSigns_AreStoredAsGiven()
    {
        var visit = OpenVisit();

        visit.RecordVitals(Vitals(4.2m, 38.6m, 180, 30, 5, 1));

        var entry = Assert.Single(visit.Vitals);
        Assert.Equal(4.2m, entry.WeightKg);
        Assert.Equal(38.6m, entry.TemperatureC);
        Assert.Equal(180, entry.HeartRateBpm);
        Assert.Equal(30, entry.RespiratoryRatePerMin);
        Assert.Equal(5, entry.BodyConditionScore);
        Assert.Equal(1, entry.PainScore);
        Assert.Single(visit.Events.OfType<VisitVitalsRecordedEvent>(), e => e.HasWeight);
    }

    [Fact]
    public void RecordVitals_MeasuredInTheFuture_IsRefused()
    {
        var visit = OpenVisit();
        var dto = Vitals(weight: 4.2m) with { MeasuredOn = Now.AddDays(1) };

        Assert.Equal(BusinessViolationCodes.ClinicalDateInFuture, Violation(() => visit.RecordVitals(dto)).ErrorCode);
    }

    [Fact]
    public void RecordVitals_TwentyOneEntries_IsRefused()
    {
        var visit = OpenVisit();
        for (var i = 0; i < ClinicalNoteRules.MaxVitalsEntries; i++) visit.RecordVitals(Vitals(pain: 1));

        Assert.Equal(BusinessViolationCodes.ClinicalLimitExceeded, Violation(() => visit.RecordVitals(Vitals(pain: 1))).ErrorCode);
    }

    // ── U-C1, U-C2 note and problems ───────────────────────────────────────────────────────────

    [Fact]
    public void SaveNote_WithAnInactiveCode_IsRefused()
    {
        var visit = OpenVisit();
        Assert.Equal(BusinessViolationCodes.DiagnosisCodeInactive, Violation(() => visit.SaveNote(Note([Problem(active: false)]))).ErrorCode);
    }

    [Fact]
    public void SaveNote_TheSameCodeTwice_IsRefused()
    {
        var visit = OpenVisit();
        var codeId = Guid.NewGuid();

        var dto = Note([Problem(codeId: codeId), Problem(codeId: codeId)]);

        Assert.Equal(BusinessViolationCodes.DuplicateProblem, Violation(() => visit.SaveNote(dto)).ErrorCode);
    }

    [Fact]
    public void SaveNote_SnapshotsCodeAndName_AndKeepsThemWhenResavedWithAChangedCatalogue()
    {
        var visit = OpenVisit();
        var problemId = Guid.NewGuid();
        var codeId = Guid.NewGuid();

        visit.SaveNote(Note([new ProblemDto(problemId, codeId, "OTITIS", "Otitis externa", true, ProblemCertainty.Provisional, null)]));
        // The catalogue entry was renamed and deactivated afterwards; a resave of the same problem keeps the snapshot.
        visit.SaveNote(Note([new ProblemDto(problemId, codeId, "OTITIS", "Renamed", false, ProblemCertainty.Confirmed, "improving")]));

        var problem = Assert.Single(visit.Problems);
        Assert.Equal("Otitis externa", problem.Name);
        Assert.Equal(ProblemCertainty.Confirmed, problem.Certainty);
        Assert.Equal("improving", problem.Note);
    }

    [Fact]
    public void SaveNote_OmittingAProblem_WithdrawsItAndNeverRemovesTheRow()
    {
        var visit = OpenVisit();
        var keep = Problem("KEEP");
        var drop = Problem("DROP");
        visit.SaveNote(Note([keep, drop]));

        visit.SaveNote(Note([keep]));

        Assert.Equal(2, visit.Problems.Count);
        Assert.Equal(["KEEP"], visit.ActiveProblems.Select(p => p.Code));
        Assert.NotNull(visit.Problems.Single(p => p.Code == "DROP").WithdrawnOn);
    }

    [Fact]
    public void SaveNote_RecordsTheAppliedTemplate_WhenSupplied_AndKeepsItOtherwise()
    {
        var visit = OpenVisit();
        var templateId = Guid.NewGuid();

        visit.SaveNote(Note(templateId: templateId, templateVersion: 3));
        visit.SaveNote(Note());

        Assert.Equal(templateId, visit.Note.AppliedTemplateId);
        Assert.Equal(3, visit.Note.AppliedTemplateVersion);
        Assert.True(visit.Note.HasBeenSaved);
    }

    [Fact]
    public void SaveNote_TwentyOneProblems_IsRefused()
    {
        var visit = OpenVisit();
        var problems = Enumerable.Range(0, ClinicalNoteRules.MaxProblems + 1).Select(i => Problem("C" + i)).ToList();

        Assert.Equal(BusinessViolationCodes.ClinicalLimitExceeded, Violation(() => visit.SaveNote(Note(problems))).ErrorCode);
    }

    // ── U-C3, U-C4, U-C6 finalise ──────────────────────────────────────────────────────────────

    [Fact]
    public void Finalise_WithNoProblem_IsRefused()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([]));

        Assert.Equal(BusinessViolationCodes.ProblemRequired, Violation(() => visit.Finalise(Finalisation(StaffRoles.Vet))).ErrorCode);
    }

    [Theory]
    [InlineData("", "plan")]
    [InlineData("assessment", "  ")]
    public void Finalise_WithABlankAssessmentOrPlan_IsRefused(string assessment, string plan)
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([Problem()], assessment, plan));

        Assert.Equal(BusinessViolationCodes.AssessmentAndPlanRequired, Violation(() => visit.Finalise(Finalisation(StaffRoles.Vet))).ErrorCode);
    }

    [Fact]
    public void Finalise_OnlyAVetMay_NotAnAdministratorNorANurse()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([Problem()]));

        Assert.Equal(BusinessViolationCodes.OnlyVeterinarianMayFinalise, Violation(() => visit.Finalise(Finalisation(StaffRoles.Administrator))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.OnlyVeterinarianMayFinalise, Violation(() => visit.Finalise(Finalisation(StaffRoles.Nurse))).ErrorCode);
        Assert.Equal(VisitStatus.Open, visit.Status);

        visit.Finalise(Finalisation(StaffRoles.Nurse, StaffRoles.Vet));

        Assert.Equal(VisitStatus.Finalised, visit.Status);
        Assert.NotNull(visit.FinalisedOn);
        Assert.NotNull(visit.FinalisedBy);
        Assert.Single(visit.Events.OfType<VisitFinalisedEvent>());
    }

    // HO-BIL-001 Phase J (owner decision 2026-10-14): the cat's owner at finalising is stored on the visit and carried on the event.
    [Fact]
    public void Finalise_StoresTheOwnerAtFinalising_AndCarriesItOnTheEvent()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([Problem()]));
        var owner = Guid.NewGuid();

        visit.Finalise(Finalisation(StaffRoles.Vet) with { CustomerId = owner });

        Assert.Equal(owner, visit.CustomerId);
        Assert.Equal(owner, Assert.Single(visit.Events.OfType<VisitFinalisedEvent>()).CustomerId);
    }

    [Fact]
    public void Finalise_WithNoOwner_IsRefused_AndNothingChanges()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([Problem()]));

        var ex = Violation(() => visit.Finalise(Finalisation(StaffRoles.Vet) with { CustomerId = Guid.Empty }));

        Assert.Equal(BusinessViolationCodes.CatNotFound, ex.ErrorCode);
        Assert.Equal(VisitStatus.Open, visit.Status);
        Assert.Null(visit.CustomerId);
        Assert.Empty(visit.Events.OfType<VisitFinalisedEvent>());
    }

    [Fact]
    public void Finalise_Twice_TheSecondIsVisitFinalised_AndTheStoredOwnerNeverChanges()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([Problem()]));
        var first = Guid.NewGuid();
        visit.Finalise(Finalisation(StaffRoles.Vet) with { CustomerId = first });

        Violation(() => visit.Finalise(Finalisation(StaffRoles.Vet) with { CustomerId = Guid.NewGuid() }));

        Assert.Equal(first, visit.CustomerId);
    }

    [Fact]
    public void Finalise_Twice_TheSecondIsVisitFinalised()
    {
        var visit = FinalisedVisit();

        Assert.Equal(BusinessViolationCodes.VisitFinalised, Violation(() => visit.Finalise(Finalisation(StaffRoles.Vet))).ErrorCode);
    }

    [Fact]
    public void Finalise_ARoutineVisitWithTheSystemCode_Succeeds()
    {
        var visit = OpenVisit();
        visit.SaveNote(Note([new ProblemDto(Guid.NewGuid(), DiagnosisCodeSeed.RoutineNoAbnormality.Id, DiagnosisCodeSeed.RoutineNoAbnormality.Code,
            DiagnosisCodeSeed.RoutineNoAbnormality.Name, true, ProblemCertainty.Confirmed, null)]));

        visit.Finalise(Finalisation(StaffRoles.Vet));

        Assert.Equal(VisitStatus.Finalised, visit.Status);
    }

    // ── U-C5, U-C7 immutability ────────────────────────────────────────────────────────────────

    /// <summary>One invocation of every mutation method (except <c>Open</c>) on a finalised visit.</summary>
    private static readonly Dictionary<string, Action<VisitAggregate>> Mutations = new()
    {
        [nameof(VisitAggregate.RecordVitals)] = v => v.RecordVitals(Vitals(weight: 4.0m)),
        [nameof(VisitAggregate.SaveNote)] = v => v.SaveNote(Note([Problem()])),
        [nameof(VisitAggregate.AttachPhoto)] = v => v.AttachPhoto(new ClinicalPhotoDto(Guid.NewGuid(), Guid.NewGuid(), Today, null, Guid.NewGuid(), Today, Now, TestUserContext.Create(), Guid.NewGuid())),
        [nameof(VisitAggregate.SetFollowUp)] = v => v.SetFollowUp(new FollowUpDto(FollowUpKind.Reminder, Today.AddDays(10), null, false, Today, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid())),
        [nameof(VisitAggregate.ClearFollowUp)] = v => v.ClearFollowUp(new FollowUpClearDto(Now, TestUserContext.Create(), Guid.NewGuid())),
        [nameof(VisitAggregate.Finalise)] = v => v.Finalise(Finalisation(StaffRoles.Vet)),
        [nameof(VisitAggregate.RecordTreatment)] = v => v.RecordTreatment(TreatmentTestData.Procedure()),
        [nameof(VisitAggregate.ReverseTreatment)] = v => v.ReverseTreatment(TreatmentTestData.Reversal(Guid.NewGuid())),
    };

    [Fact]
    public void EveryMutationMethodOnAFinalisedVisit_IsVisitFinalised_WithAMessagePointingToAddenda()
    {
        var visit = FinalisedVisit();

        foreach (var (name, mutate) in Mutations)
        {
            var violation = Violation(() => mutate(visit));
            Assert.True(violation.ErrorCode == BusinessViolationCodes.VisitFinalised, $"{name} must refuse a finalised visit.");
            Assert.Contains("addendum", violation.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>U-C7 -- reflection over the public methods: a mutation method added later without an
    /// entry here (and so without proof it calls EnsureOpen) fails this test.</summary>
    [Fact]
    public void EveryPublicMethodExceptOpen_IsCoveredByTheFinalisedVisitCheck()
    {
        var methods = typeof(VisitAggregate)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .Select(m => m.Name)
            .Where(n => n != nameof(VisitAggregate.Open))
            .ToHashSet();

        Assert.Equal(Mutations.Keys.ToHashSet(), methods);
    }

    // ── U-D10 photo ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AttachPhoto_TheTwentyFirst_AndATakenOnTomorrow_AreRefused()
    {
        var visit = OpenVisit();
        ClinicalPhotoDto Photo(DateOnly takenOn) => new(Guid.NewGuid(), Guid.NewGuid(), takenOn, "note", Guid.NewGuid(), Today, Now, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(BusinessViolationCodes.ClinicalDateInFuture, Violation(() => visit.AttachPhoto(Photo(Today.AddDays(1)))).ErrorCode);

        for (var i = 0; i < ClinicalNoteRules.MaxPhotos; i++) visit.AttachPhoto(Photo(Today));

        Assert.Equal(BusinessViolationCodes.ClinicalLimitExceeded, Violation(() => visit.AttachPhoto(Photo(Today))).ErrorCode);
    }

    // ── follow-up (US-CLN-18) ──────────────────────────────────────────────────────────────────

    private static FollowUpDto FollowUp(FollowUpKind kind, DateOnly due, Guid? appointmentId = null, bool valid = true) =>
        new(kind, due, appointmentId, valid, Today, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void SetFollowUp_DueTodayOrEarlier_IsRefused()
    {
        var visit = OpenVisit();
        Assert.Equal(BusinessViolationCodes.FollowUpDateInPast, Violation(() => visit.SetFollowUp(FollowUp(FollowUpKind.Reminder, Today))).ErrorCode);
    }

    [Fact]
    public void SetFollowUp_AppointmentKind_NeedsAValidAppointment_AndReminderKindNone()
    {
        var visit = OpenVisit();

        Assert.Equal(BusinessViolationCodes.FollowUpAppointmentInvalid, Violation(() => visit.SetFollowUp(FollowUp(FollowUpKind.Appointment, Today.AddDays(5)))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.FollowUpAppointmentInvalid, Violation(() => visit.SetFollowUp(FollowUp(FollowUpKind.Appointment, Today.AddDays(5), Guid.NewGuid(), valid: false))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.FollowUpAppointmentInvalid, Violation(() => visit.SetFollowUp(FollowUp(FollowUpKind.Reminder, Today.AddDays(5), Guid.NewGuid()))).ErrorCode);

        visit.SetFollowUp(FollowUp(FollowUpKind.Appointment, Today.AddDays(5), Guid.NewGuid()));
        Assert.NotNull(visit.FollowUp);
    }

    [Fact]
    public void SetFollowUp_EverySetGetsANewStamp_AndClearRemovesIt()
    {
        var visit = OpenVisit();

        visit.SetFollowUp(FollowUp(FollowUpKind.Reminder, Today.AddDays(10)));
        var first = visit.FollowUp!.Stamp;
        visit.SetFollowUp(FollowUp(FollowUpKind.Reminder, Today.AddDays(10)));

        Assert.NotEqual(first, visit.FollowUp!.Stamp);

        visit.ClearFollowUp(new FollowUpClearDto(Now, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Null(visit.FollowUp);
        Assert.Single(visit.Events.OfType<FollowUpClearedEvent>());
    }
}
