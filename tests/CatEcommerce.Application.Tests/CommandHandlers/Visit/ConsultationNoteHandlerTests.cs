using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Visit;

/// <summary>HO-EMR-001 §15 Phase C -- U-C1/U-C2/U-C4/U-C13 at handler level, the draft and addendum handlers and
/// the two ADR-133 guards.</summary>
public sealed class ConsultationNoteHandlerTests
{
    private static readonly DateTimeOffset Now = ClinicalTestData.Now;

    private sealed class CapturingOutbox : INotificationOutbox
    {
        public List<NotificationRequestDto> Enqueued { get; } = [];
        public List<string> Log { get; init; } = [];
        public void Enqueue(NotificationRequestDto request)
        {
            Enqueued.Add(request);
            Log.Add("enqueue");
        }
    }

    private sealed class StubLinks : INotificationLinkBuilder
    {
        public string PasswordResetLink(string token) => "";
        public string CatCareLink(Guid catId, string category) => "";
        public string UnsubscribeLink(string token) => "";
        public string OneClickUnsubscribeUrl(string token) => "";
        public string AppointmentLink(Guid appointmentId) => "";
        public string InvoiceLink(Guid invoiceId) => "";
        public string ReceiptLink(Guid paymentId) => "";
        public string OrderLink(Guid orderId) => "";
        public string PrescriptionLink(Guid prescriptionId) => "";
        public string RepeatRequestLink(Guid prescriptionId) => "";
        public string DietAuthorisationsLink() => "";
        public string BookingLink() => "";
        public string ProductLink(string slug, Guid skuId) => "";
        public string VisitSummaryLink(Guid visitId) => $"https://portal.example.test/portal/visits/{visitId}";
    }

    private sealed class World
    {
        public BranchAggregate Branch { get; } = ClinicalTestData.Branch();
        public CatAggregate Cat { get; } = ClinicalTestData.Cat();
        public AppointmentAggregate Appointment { get; }
        public VisitAggregate Visit { get; }
        public FakeAggregateRootRepository<VisitAggregate> Visits { get; } = new();
        public FakeAggregateRootRepository<DiagnosisCodeAggregate> Codes { get; } = new();
        public FakeAggregateRootRepository<NoteTemplateAggregate> Templates { get; } = new();
        public FakeAggregateRootRepository<StaffAccountAggregate> Staff { get; } = new();
        public FakeAggregateRootRepository<CatAggregate> Cats { get; } = new();
        public FakeAggregateRootRepository<BranchAggregate> Branches { get; } = new();
        public FakeAggregateRootRepository<ConsultationDraftAggregate> Drafts { get; } = new();
        public FakeAggregateRootRepository<VisitAddendumAggregate> Addenda { get; } = new();
        public List<string> Log { get; } = [];
        public CapturingOutbox Outbox { get; }
        public FakeTimeProvider Clock { get; } = new(Now);

        public World()
        {
            Outbox = new CapturingOutbox { Log = Log };
            Appointment = ClinicalTestData.CheckedInAppointment(Branch.Id, Cat);
            Visit = ClinicalTestData.OpenVisit(Branch.Id, Appointment.Id, Cat.Id);
            Visits.Seed(Visit);
            Cats.Seed(Cat);
            Branches.Seed(Branch);
        }

        public DiagnosisCodeAggregate Code(string code, bool active = true)
        {
            var aggregate = new DiagnosisCodeAggregate(Guid.NewGuid());
            aggregate.Create(new DiagnosisCodeCreateDto(code, "Name of " + code, null, null, null, TestUserContext.Create(), Guid.NewGuid()));
            if (!active) aggregate.SetActive(new CatalogueActiveDto(false, TestUserContext.Create(), Guid.NewGuid()));
            Codes.Seed(aggregate);
            return aggregate;
        }

        public StaffAccountAggregate StaffWith(params string[] roles)
        {
            var staff = new StaffAccountAggregate(Guid.NewGuid());
            staff.Create(new StaffAccountCreationDto("Dr Test", MobileNumber.Create("01711111111"), null, roles, TestUserContext.Create(), Guid.NewGuid()));
            Staff.Seed(staff);
            return staff;
        }

        public SaveConsultationNoteCommandHandler NoteHandler() => new(
            new LoggingRepository<VisitAggregate>("visit", Log, Visits), Codes, Templates, Clock);

        public FinaliseVisitCommandHandler FinaliseHandler() => new(
            new LoggingRepository<VisitAggregate>("visit", Log, Visits), Staff, Cats, Branches, Outbox, new StubLinks(), Clock);
    }

    private static SaveConsultationNoteCommand NoteCommand(World world, IEnumerable<(Guid CodeId, string Certainty)> problems, int? expectedVersion = null,
        string assessment = "Mild otitis", string plan = "Drops for 7 days", Guid? branchId = null, UserContext? user = null) => new()
    {
        VisitId = world.Visit.Id, BranchId = branchId ?? world.Branch.Id, ExpectedVersion = expectedVersion ?? world.Visit.Version,
        Subjective = "Shaking head", Objective = "Red ear canal", Assessment = assessment, Plan = plan, DischargeInstructions = "Keep dry",
        Problems = problems.Select(p => new ProblemInput { ProblemId = Guid.NewGuid(), DiagnosisCodeId = p.CodeId, Certainty = p.Certainty }).ToList(),
        UserContext = user ?? TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    private static FinaliseVisitCommand FinaliseCommand(World world, UserContext user, int? expectedVersion = null) => new()
    {
        VisitId = world.Visit.Id, BranchId = world.Branch.Id, ExpectedVersion = expectedVersion ?? world.Visit.Version,
        UserContext = user, CorrelationId = Guid.NewGuid()
    };

    // ── C8 Note ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveNote_WithAnInactiveCode_IsRefused() // U-C1
    {
        var world = new World();
        var inactive = world.Code("OLD-CODE", active: false);

        var response = await world.NoteHandler().HandleAsync(NoteCommand(world, [(inactive.Id, "Confirmed")]));

        Assert.Equal(BusinessViolationCodes.DiagnosisCodeInactive, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Visit.Problems);
    }

    [Fact]
    public async Task SaveNote_TheSameCodeTwice_IsRefused() // U-C1
    {
        var world = new World();
        var code = world.Code("OTITIS");

        var response = await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed"), (code.Id, "Provisional")]));

        Assert.Equal(BusinessViolationCodes.DuplicateProblem, response.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task SaveNote_SnapshotsTheCodeAndName_AndReturnsTheNewVersion() // U-C2
    {
        var world = new World();
        var code = world.Code("OTITIS");
        var versionBefore = world.Visit.Version;

        var response = await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<SaveNoteResult>(response.Result);
        Assert.Equal(world.Visit.Version, result.Version);
        Assert.True(result.Version > versionBefore);
        var problem = Assert.Single(world.Visit.Problems);
        Assert.Equal("OTITIS", problem.Code);
        Assert.Equal("Name of OTITIS", problem.Name);
        Assert.Equal(ProblemCertainty.Confirmed, problem.Certainty);
        Assert.Equal(result.SavedOn, world.Visit.Note.NoteSavedOn);

        // Renaming the code later never changes the visit's snapshot.
        code.Update(new DiagnosisCodeUpdateDto("Renamed", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal("Name of OTITIS", world.Visit.Problems.Single().Name);
    }

    [Fact]
    public async Task SaveNote_WithAStaleExpectedVersion_IsRefused_AndNothingIsSaved() // I-C5 at handler level
    {
        var world = new World();
        var code = world.Code("OTITIS");

        var response = await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")], expectedVersion: world.Visit.Version + 5));

        Assert.Equal(BusinessViolationCodes.StaleVisitVersion, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Visits.Updated);
        Assert.False(world.Visit.Note.HasBeenSaved);
    }

    [Fact]
    public async Task SaveNote_ForAnotherBranchsVisit_IsNotFound()
    {
        var world = new World();

        var response = await world.NoteHandler().HandleAsync(NoteCommand(world, [], branchId: Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.VisitNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task SaveNote_WithAnUnknownCode_OrTemplate_IsRefused()
    {
        var world = new World();

        var unknownCode = await world.NoteHandler().HandleAsync(NoteCommand(world, [(Guid.NewGuid(), "Confirmed")]));
        Assert.Equal(BusinessViolationCodes.DiagnosisCodeNotFound, unknownCode.ValidationResult!.Errors.Single().ErrorCode);

        var command = NoteCommand(world, []);
        var withTemplate = new SaveConsultationNoteCommand
        {
            VisitId = command.VisitId, BranchId = command.BranchId, ExpectedVersion = command.ExpectedVersion, AppliedTemplateId = Guid.NewGuid(), AppliedTemplateVersion = 1,
            UserContext = command.UserContext, CorrelationId = command.CorrelationId
        };
        var unknownTemplate = await world.NoteHandler().HandleAsync(withTemplate);
        Assert.Equal(BusinessViolationCodes.NoteTemplateNotFound, unknownTemplate.ValidationResult!.Errors.Single().ErrorCode);
    }

    // ── C9 Finalise ────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(StaffRoles.Administrator)]
    [InlineData(StaffRoles.Nurse)]
    public async Task Finalise_ByANonVet_IsRefused_NothingEnqueued_NothingSaved(string role) // U-C4 (ADR-119)
    {
        var world = new World();
        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));
        world.Log.Clear();
        var caller = world.StaffWith(role);

        var response = await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, new UserContext(caller.Id, Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.OnlyVeterinarianMayFinalise, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Equal(VisitStatus.Open, world.Visit.Status);
        Assert.Empty(world.Outbox.Enqueued);
        Assert.Empty(world.Log);
    }

    [Fact]
    public async Task Finalise_ByAVet_EnqueuesTheSummaryMessage_ToTheCurrentOwner_BeforeTheSave() // U-C13
    {
        var world = new World();
        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));
        world.Log.Clear();
        var vet = world.StaffWith(StaffRoles.Vet);
        var command = FinaliseCommand(world, new UserContext(vet.Id, Guid.NewGuid()));

        var response = await world.FinaliseHandler().HandleAsync(command);

        Assert.True(response.IsSuccess);
        Assert.Equal(VisitStatus.Finalised, world.Visit.Status);
        Assert.Equal(vet.Id, world.Visit.FinalisedBy);
        Assert.Equal(["enqueue", "update:visit"], world.Log); // enqueued before the save, in the same handler

        var message = Assert.Single(world.Outbox.Enqueued);
        Assert.Equal(NotificationTemplateCatalogue.VisitSummaryAvailable, message.TemplateKey);
        Assert.Equal(NotificationRecipientKind.Customer, message.RecipientKind);
        Assert.Equal(world.Cat.OwnerId, message.RecipientId);
        Assert.Equal("Mitu", message.Variables["catName"]);
        Assert.Equal($"https://portal.example.test/portal/visits/{world.Visit.Id}", message.Variables["summaryLink"]);
        Assert.Equal("2026-10-07", message.Variables["visitDate"]);
        // No clinical content in the message (ADR-130): only the three declared variables.
        Assert.Equal(["catName", "summaryLink", "visitDate"], message.Variables.Keys.Order().ToArray());
    }

    [Fact]
    public async Task Finalise_Twice_TheSecondIsVisitFinalised_NotAStaleVersion()
    {
        var world = new World();
        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));
        var vet = world.StaffWith(StaffRoles.Vet);
        var user = new UserContext(vet.Id, Guid.NewGuid());
        await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, user));

        var second = await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, user, expectedVersion: 0));

        Assert.Equal(BusinessViolationCodes.VisitFinalised, second.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Single(world.Outbox.Enqueued);
    }

    [Fact]
    public async Task Finalise_WithoutAProblem_OrWithABlankPlan_IsRefused() // U-C3
    {
        var world = new World();
        var vet = world.StaffWith(StaffRoles.Vet);
        var user = new UserContext(vet.Id, Guid.NewGuid());

        var noProblem = await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, user));
        Assert.Equal(BusinessViolationCodes.ProblemRequired, noProblem.ValidationResult!.Errors.Single().ErrorCode);

        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")], plan: "   "));
        var blankPlan = await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, user));
        Assert.Equal(BusinessViolationCodes.AssessmentAndPlanRequired, blankPlan.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Outbox.Enqueued);
    }

    // ── C7 Draft and C10 Addendum ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveDraft_CreatesThenOverwrites_OneDraftPerAuthor() // ADR-122
    {
        var world = new World();
        var handler = new SaveConsultationDraftCommandHandler(world.Visits, world.Drafts, world.Clock);
        var author = TestUserContext.Create();

        SaveConsultationDraftCommand Command(string text) => new()
        {
            VisitId = world.Visit.Id, DraftId = Guid.NewGuid(), BranchId = world.Branch.Id, Subjective = text, UserContext = author, CorrelationId = Guid.NewGuid()
        };

        Assert.True((await handler.HandleAsync(Command("first"))).IsSuccess);
        world.Clock.Advance(TimeSpan.FromSeconds(5));
        Assert.True((await handler.HandleAsync(Command("second"))).IsSuccess);

        var draft = Assert.Single(world.Drafts.Saved);
        Assert.Single(world.Drafts.Updated);
        Assert.Equal("second", draft.Subjective);
        Assert.Equal(author.UserId, draft.AuthorStaffId);
        Assert.Equal(Now.AddSeconds(5), draft.SavedOn);
    }

    [Fact]
    public async Task SaveDraft_OnAFinalisedVisit_IsRefused()
    {
        var world = new World();
        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));
        var vet = world.StaffWith(StaffRoles.Vet);
        await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, new UserContext(vet.Id, Guid.NewGuid())));

        var response = await new SaveConsultationDraftCommandHandler(world.Visits, world.Drafts, world.Clock).HandleAsync(new SaveConsultationDraftCommand
        {
            VisitId = world.Visit.Id, DraftId = Guid.NewGuid(), BranchId = world.Branch.Id, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(BusinessViolationCodes.VisitFinalised, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Drafts.Saved);
    }

    [Fact]
    public async Task AddAddendum_OnAnOpenVisit_IsRefused_OnAFinalisedOne_IsRecordedWithAuthorAndTime() // U-C8
    {
        var world = new World();
        var handler = new AddVisitAddendumCommandHandler(world.Visits, world.Addenda, world.Clock);
        var author = TestUserContext.Create();
        AddVisitAddendumCommand Command() => new()
        {
            AddendumId = Guid.NewGuid(), VisitId = world.Visit.Id, BranchId = world.Branch.Id, Text = "Weight was 4.1 kg.", Reason = "typo",
            UserContext = author, CorrelationId = Guid.NewGuid()
        };

        var open = await handler.HandleAsync(Command());
        Assert.Equal(BusinessViolationCodes.AddendumRequiresFinalisedVisit, open.ValidationResult!.Errors.Single().ErrorCode);

        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));
        var vet = world.StaffWith(StaffRoles.Vet);
        await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, new UserContext(vet.Id, Guid.NewGuid())));

        var added = await handler.HandleAsync(Command());
        Assert.True(added.IsSuccess);
        var addendum = Assert.Single(world.Addenda.Saved);
        Assert.Equal(author.UserId, addendum.AuthorStaffId);
        Assert.Equal(Now, addendum.AddedOn);
        Assert.Equal(world.Cat.Id, addendum.CatId);
    }

    // ── ADR-133 ────────────────────────────────────────────────────────────────────────────────

    private static CompleteAppointmentCommand CompleteCommand(AppointmentAggregate appointment, Guid branchId) => new()
    {
        AppointmentId = appointment.Id, BranchId = branchId, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public async Task CompleteAppointment_WhileTheVisitIsOpen_IsRefused_AfterFinalising_ItCompletes() // I-C12 at handler level
    {
        var world = new World();
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        appointments.Seed(world.Appointment);
        // A walk-in is CheckedIn; Complete needs InConsultation, so start it first.
        world.Appointment.Start(new AppointmentStartDto(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        var handler = new CompleteAppointmentCommandHandler(appointments, world.Clock, world.Visits);

        var refused = await handler.HandleAsync(CompleteCommand(world.Appointment, world.Branch.Id));
        Assert.Equal(BusinessViolationCodes.VisitNotFinalised, refused.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Equal(AppointmentStatus.InConsultation, world.Appointment.Status);

        var code = world.Code("OTITIS");
        await world.NoteHandler().HandleAsync(NoteCommand(world, [(code.Id, "Confirmed")]));
        var vet = world.StaffWith(StaffRoles.Vet);
        await world.FinaliseHandler().HandleAsync(FinaliseCommand(world, new UserContext(vet.Id, Guid.NewGuid())));

        var completed = await handler.HandleAsync(CompleteCommand(world.Appointment, world.Branch.Id));
        Assert.True(completed.IsSuccess, completed.ValidationResult?.Errors.FirstOrDefault()?.ErrorCode);
        Assert.Equal(AppointmentStatus.Completed, world.Appointment.Status);
    }

    [Fact]
    public async Task CompleteAppointment_WithNoVisit_StillCompletes() // OQ-8: only an appointment WITH an open visit is blocked
    {
        var world = new World();
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        var cat = ClinicalTestData.Cat();
        var appointment = ClinicalTestData.CheckedInAppointment(world.Branch.Id, cat);
        appointment.Start(new AppointmentStartDto(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        appointments.Seed(appointment);

        var response = await new CompleteAppointmentCommandHandler(appointments, world.Clock, world.Visits).HandleAsync(CompleteCommand(appointment, world.Branch.Id));

        Assert.True(response.IsSuccess);
    }

    [Fact]
    public async Task StaffCancel_OfACheckedInAppointment_WithAVisit_IsRefused() // I-C13 at handler level
    {
        var world = new World();
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        appointments.Seed(world.Appointment);
        var handler = new CancelAppointmentCommandHandler(
            appointments, world.Branches, world.Cats, world.Staff,
            new InMemoryBusinessSettings(new Dictionary<string, object>()), new CapturingOutbox(), new StubLinks(), world.Clock, world.Visits);

        var response = await handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = world.Appointment.Id, BranchId = world.Branch.Id, IsStaff = true, Reason = "changed mind",
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(BusinessViolationCodes.VisitInProgress, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Equal(AppointmentStatus.CheckedIn, world.Appointment.Status);
    }
}
