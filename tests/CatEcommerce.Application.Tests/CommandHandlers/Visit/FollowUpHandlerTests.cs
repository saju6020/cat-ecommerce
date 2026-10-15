using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Visit;

/// <summary>HO-EMR-001 §15 Phase G -- U-G1 (send time) at handler level and the follow-up rules of §8.9 (US-CLN-18).</summary>
public sealed class FollowUpHandlerTests
{
    private static readonly DateTimeOffset Now = ClinicalTestData.Now; // 2026-10-07 15:00 in Dhaka

    private sealed class CapturingOutbox : INotificationOutbox
    {
        public List<NotificationRequestDto> Enqueued { get; } = [];
        public void Enqueue(NotificationRequestDto request) => Enqueued.Add(request);
    }

    private sealed class Links : INotificationLinkBuilder
    {
        public string PasswordResetLink(string token) => "";
        public string CatCareLink(Guid catId, string category) => $"https://portal.example.test/book?catId={catId}&category={category}";
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
        public string VisitSummaryLink(Guid visitId) => "";
    }

    private sealed class World
    {
        public BranchAggregate Branch { get; } = ClinicalTestData.Branch();
        public CatAggregate Cat { get; } = ClinicalTestData.Cat();
        public VisitAggregate Visit { get; }
        public FakeAggregateRootRepository<VisitAggregate> Visits { get; } = new();
        public FakeAggregateRootRepository<AppointmentAggregate> Appointments { get; } = new();
        public FakeAggregateRootRepository<CatAggregate> Cats { get; } = new();
        public FakeAggregateRootRepository<BranchAggregate> Branches { get; } = new();
        public CapturingOutbox Outbox { get; } = new();
        public FakeTimeProvider Clock { get; } = new(Now);

        public World()
        {
            Visit = ClinicalTestData.OpenVisit(Branch.Id, Guid.NewGuid(), Cat.Id);
            Visits.Seed(Visit);
            Cats.Seed(Cat);
            Branches.Seed(Branch);
        }

        public SetFollowUpCommandHandler SetHandler(int leadDays = 3) => new(
            Visits, Appointments, Cats, Branches,
            new InMemoryBusinessSettings(new Dictionary<string, object> { [SettingKeys.ClinicalFollowUpReminderLeadDays] = leadDays }),
            Outbox, new Links(), Clock);

        public SetFollowUpCommand Command(string kind, DateOnly dueOn, Guid? appointmentId = null, Guid? branchId = null) => new()
        {
            VisitId = Visit.Id, BranchId = branchId ?? Branch.Id, Kind = kind, DueOn = dueOn, AppointmentId = appointmentId,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task Reminder_EnqueuesTheMessage_At0900BranchLocal_LeadDaysBeforeDue_WithTheGuardStamp() // U-G1
    {
        var world = new World();
        var handler = world.SetHandler(leadDays: 3);

        var response = await handler.HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 20)));

        Assert.True(response.IsSuccess);
        var message = Assert.Single(world.Outbox.Enqueued);
        Assert.Equal(NotificationTemplateCatalogue.VisitFollowUpDue, message.TemplateKey);
        Assert.Equal(world.Cat.OwnerId, message.RecipientId);
        Assert.Equal(NotificationSubjectKind.VisitFollowUp, message.SubjectKind);
        Assert.Equal(world.Visit.Id, message.SubjectId);
        Assert.Equal(world.Visit.FollowUp!.Stamp, message.SubjectStamp);
        Assert.Equal(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.FromHours(6)), message.SendAfter); // 2026-10-17 09:00 +06:00
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(6)), message.NotAfter);
        Assert.Equal(["bookingLink", "catName", "dueOn"], message.Variables.Keys.Order().ToArray());
        Assert.Equal("2026-10-20", message.Variables["dueOn"]);
        Assert.Contains("category=FollowUp", message.Variables["bookingLink"]);
    }

    [Fact]
    public async Task Reminder_DueTomorrow_IsSentNow_NotInThePast() // U-G1
    {
        var world = new World();

        await world.SetHandler(leadDays: 3).HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 8)));

        Assert.Equal(Now, Assert.Single(world.Outbox.Enqueued).SendAfter);
    }

    [Fact]
    public async Task EverySet_MintsANewStamp_SoTheEarlierReminderStandsDown() // ADR-131
    {
        var world = new World();
        var handler = world.SetHandler();

        await handler.HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 20)));
        await handler.HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 25)));

        Assert.Equal(2, world.Outbox.Enqueued.Count);
        Assert.NotEqual(world.Outbox.Enqueued[0].SubjectStamp, world.Outbox.Enqueued[1].SubjectStamp);
        Assert.Equal(world.Visit.FollowUp!.Stamp, world.Outbox.Enqueued[1].SubjectStamp);
    }

    [Fact]
    public async Task Reminder_ForADateTodayOrEarlier_IsRefused_AndNothingIsEnqueued()
    {
        var world = new World();

        var response = await world.SetHandler().HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 7)));

        Assert.Equal(BusinessViolationCodes.FollowUpDateInPast, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Outbox.Enqueued);
        Assert.Null(world.Visit.FollowUp);
    }

    [Fact]
    public async Task Appointment_LinkedToABookedFutureAppointmentOfTheSameCat_IsAccepted_WithNoMessage() // I-G6
    {
        var world = new World();
        var booked = ClinicalTestData.BookedAppointment(world.Branch.Id, world.Cat);
        world.Appointments.Seed(booked);

        var response = await world.SetHandler().HandleAsync(world.Command("Appointment", new DateOnly(2026, 10, 8), booked.Id));

        Assert.True(response.IsSuccess);
        Assert.Equal(booked.Id, world.Visit.FollowUp!.AppointmentId);
        Assert.Empty(world.Outbox.Enqueued); // the booking already sent its own confirmation and reminders
    }

    [Fact]
    public async Task Appointment_OfAnotherCat_OrNotBooked_OrMissing_IsRefused() // I-G6
    {
        var world = new World();
        var otherCat = ClinicalTestData.Cat();
        var otherCatsAppointment = ClinicalTestData.BookedAppointment(world.Branch.Id, otherCat);
        var checkedIn = ClinicalTestData.CheckedInAppointment(world.Branch.Id, world.Cat); // not Booked
        world.Appointments.Seed(otherCatsAppointment);
        world.Appointments.Seed(checkedIn);
        var handler = world.SetHandler();

        foreach (var appointmentId in new Guid?[] { otherCatsAppointment.Id, checkedIn.Id, Guid.NewGuid(), null })
        {
            var response = await handler.HandleAsync(world.Command("Appointment", new DateOnly(2026, 10, 8), appointmentId));
            Assert.Equal(BusinessViolationCodes.FollowUpAppointmentInvalid, response.ValidationResult!.Errors.Single().ErrorCode);
        }

        Assert.Null(world.Visit.FollowUp);
    }

    [Fact]
    public async Task Clear_RemovesTheFollowUp_AndAFinalisedVisitCannotBeChanged() // I-C8
    {
        var world = new World();
        await world.SetHandler().HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 20)));

        var clear = await new ClearFollowUpCommandHandler(world.Visits, world.Clock).HandleAsync(new ClearFollowUpCommand
        { VisitId = world.Visit.Id, BranchId = world.Branch.Id, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        Assert.True(clear.IsSuccess);
        Assert.Null(world.Visit.FollowUp);

        var code = new DiagnosisCodeAggregate(Guid.NewGuid());
        code.Create(new DiagnosisCodeCreateDto("X", "x", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        world.Visit.SaveNote(new SoapNoteDto("s", "o", "a", "p", "", [new ProblemDto(Guid.NewGuid(), code.Id, "X", "x", true, ProblemCertainty.Confirmed, null)], null, null,
            Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        world.Visit.Finalise(new VisitFinalisationDto([StaffRoles.Vet], Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid(), Guid.NewGuid()));

        var set = await world.SetHandler().HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 25)));
        Assert.Equal(BusinessViolationCodes.VisitFinalised, set.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Single(world.Outbox.Enqueued); // only the first set ever enqueued
    }

    [Fact]
    public async Task SetFollowUp_OnAnotherBranchsVisit_IsNotFound()
    {
        var world = new World();

        var response = await world.SetHandler().HandleAsync(world.Command("Reminder", new DateOnly(2026, 10, 20), branchId: Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.VisitNotFound, response.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Empty(world.Outbox.Enqueued);
    }
}
