using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Clinic;

/// <summary>Backlog rows #8, #25 and #33 (docs/backlog/APT.md, HO-APT-001) at handler level: the
/// staff/customer decision comes from the command's resolved caller kind, a staff caller only
/// reaches their active branch's appointments, and a customer who no longer owns the cat cannot
/// act on its appointment.</summary>
public sealed class AppointmentBacklogFixHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);

    private sealed class NullOutbox : INotificationOutbox
    {
        public int Enqueued { get; private set; }
        public void Enqueue(NotificationRequestDto request) => Enqueued++;
    }

    private sealed class StubLinkBuilder : INotificationLinkBuilder
    {
        public string PasswordResetLink(string token) => "";
        public string CatCareLink(Guid catId, string category) => "";
        public string UnsubscribeLink(string token) => "";
        public string OneClickUnsubscribeUrl(string token) => "";
        public string AppointmentLink(Guid appointmentId) => "";
        public string BookingLink() => "";
        public string ProductLink(string slug, Guid skuId) => "";
        public string VisitSummaryLink(Guid visitId) => "";
        public string InvoiceLink(Guid invoiceId) => "";
        public string ReceiptLink(Guid paymentId) => "";
        public string OrderLink(Guid orderId) => "";
        public string PrescriptionLink(Guid prescriptionId) => "";
        public string RepeatRequestLink(Guid prescriptionId) => "";
        public string DietAuthorisationsLink() => "";
    }

    private static InMemoryBusinessSettings Settings() => new(new Dictionary<string, object>
    {
        [SettingKeys.BookingChangeCutoffHours] = 4,
        [SettingKeys.BookingFirstReminderHours] = 24,
        [SettingKeys.BookingSecondReminderHours] = 2,
    });

    private static UserContext Context(Guid userId, string userName) => new(userId, Guid.NewGuid()) { UserName = userName };

    private static CatAggregate CatOwnedBy(Guid ownerId)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, "Mitu", "Cat", CatSex.Female, DateOnly.FromDateTime(Now.UtcDateTime).AddYears(-2),
            DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", DateOnly.FromDateTime(Now.UtcDateTime),
            Context(ownerId, "customer"), Guid.NewGuid()));
        return cat;
    }

    private static AppointmentAggregate BookedAppointment(Guid branchId, Guid customerId, Guid catId, TimeSpan startsIn)
    {
        var startsOn = Now.Add(startsIn);
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.BookDirect(new DirectBookingDto(
            branchId, customerId, catId, Guid.NewGuid(), new ServiceSnapshot("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT")),
            Guid.NewGuid(), Guid.NewGuid(), startsOn, startsOn.AddMinutes(30), null, Context(customerId, "customer"), Guid.NewGuid()));
        return appointment;
    }

    private sealed record CancelRig(
        CancelAppointmentCommandHandler Handler,
        FakeAggregateRootRepository<AppointmentAggregate> Appointments,
        FakeAggregateRootRepository<CatAggregate> Cats);

    private static CancelRig NewCancelRig()
    {
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        var handler = new CancelAppointmentCommandHandler(
            appointments, new FakeAggregateRootRepository<BranchAggregate>(), cats,
            new FakeAggregateRootRepository<StaffAccountAggregate>(), Settings(), new NullOutbox(), new StubLinkBuilder(),
            new FakeTimeProvider(Now), new FakeAggregateRootRepository<CatEcommerce.Domain.Aggregates.Visit.VisitAggregate>());
        return new CancelRig(handler, appointments, cats);
    }

    /// <summary>#33 -- the caller kind is the command's <c>IsStaff</c>; a staff caller whose user
    /// name is anything but the literal "staff" (the Development fallback is "dev-user") is still
    /// staff: no change cut-off, and the cancellation is by the clinic.</summary>
    [Fact]
    public async Task Cancel_StaffCallerWithAnyUserName_IsTreatedAsStaff()
    {
        var rig = NewCancelRig();
        var branchId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cat = CatOwnedBy(customerId);
        rig.Cats.Seed(cat);
        var appointment = BookedAppointment(branchId, customerId, cat.Id, TimeSpan.FromHours(1)); // inside the 4 h cut-off
        rig.Appointments.Seed(appointment);

        var response = await rig.Handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id, Reason = "Vet unwell", IsStaff = true, BranchId = branchId,
            UserContext = Context(Guid.NewGuid(), "dev-user"), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.False(appointment.CancelledByCustomer);
    }

    /// <summary>#33 -- the converse: a customer whose user name happens to be "staff" is not staff.</summary>
    [Fact]
    public async Task Cancel_CustomerCallerNamedStaff_IsStillBoundByTheCutoff()
    {
        var rig = NewCancelRig();
        var customerId = Guid.NewGuid();
        var cat = CatOwnedBy(customerId);
        rig.Cats.Seed(cat);
        var appointment = BookedAppointment(Guid.NewGuid(), customerId, cat.Id, TimeSpan.FromHours(1));
        rig.Appointments.Seed(appointment);

        var response = await rig.Handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id, IsStaff = false,
            UserContext = Context(customerId, "staff"), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    /// <summary>#33 -- the reason-required rule follows the same flag.</summary>
    [Fact]
    public void CancelValidator_StaffWithoutReason_Fails_CustomerWithoutReason_Passes()
    {
        var validator = new CancelAppointmentCommandValidator();

        var staff = validator.Validate(new CancelAppointmentCommand
        { AppointmentId = Guid.NewGuid(), IsStaff = true, UserContext = Context(Guid.NewGuid(), "dev-user"), CorrelationId = Guid.NewGuid() });
        var customer = validator.Validate(new CancelAppointmentCommand
        { AppointmentId = Guid.NewGuid(), IsStaff = false, UserContext = Context(Guid.NewGuid(), "staff"), CorrelationId = Guid.NewGuid() });

        Assert.Contains(staff.Errors, e => e.ErrorCode == BusinessViolationCodes.CancellationReasonRequired);
        Assert.True(customer.IsValid);
    }

    /// <summary>#8 -- staff cancel of another branch's appointment is "not found" and changes nothing.</summary>
    [Fact]
    public async Task Cancel_StaffOnAnotherBranchsAppointment_IsNotFound_AndNothingChanges()
    {
        var rig = NewCancelRig();
        var customerId = Guid.NewGuid();
        var cat = CatOwnedBy(customerId);
        rig.Cats.Seed(cat);
        var appointment = BookedAppointment(Guid.NewGuid(), customerId, cat.Id, TimeSpan.FromDays(3));
        rig.Appointments.Seed(appointment);

        var response = await rig.Handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id, Reason = "x", IsStaff = true, BranchId = Guid.NewGuid(),
            UserContext = Context(Guid.NewGuid(), "staff"), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.AppointmentNotFound);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Empty(rig.Appointments.Updated);
    }

    /// <summary>#8 -- a staff command carrying no branch fails closed.</summary>
    [Fact]
    public async Task Cancel_StaffWithNoBranch_IsNotFound()
    {
        var rig = NewCancelRig();
        var customerId = Guid.NewGuid();
        var cat = CatOwnedBy(customerId);
        rig.Cats.Seed(cat);
        var appointment = BookedAppointment(Guid.NewGuid(), customerId, cat.Id, TimeSpan.FromDays(3));
        rig.Appointments.Seed(appointment);

        var response = await rig.Handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id, Reason = "x", IsStaff = true, BranchId = null,
            UserContext = Context(Guid.NewGuid(), "staff"), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    /// <summary>#25 -- after a cat ownership transfer the appointment still carries the previous
    /// owner's CustomerId (it is only flagged); the previous owner can no longer cancel it.</summary>
    [Fact]
    public async Task Cancel_PreviousOwnerAfterCatTransfer_IsNotFound()
    {
        var rig = NewCancelRig();
        var previousOwnerId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();
        var cat = CatOwnedBy(newOwnerId);
        rig.Cats.Seed(cat);
        var appointment = BookedAppointment(Guid.NewGuid(), previousOwnerId, cat.Id, TimeSpan.FromDays(3));
        rig.Appointments.Seed(appointment);

        var response = await rig.Handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id, IsStaff = false,
            UserContext = Context(previousOwnerId, "customer"), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.AppointmentNotFound);
        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
    }

    /// <summary>#25 -- while they still own the cat, the booking customer cancels as before.</summary>
    [Fact]
    public async Task Cancel_CurrentOwner_Succeeds()
    {
        var rig = NewCancelRig();
        var ownerId = Guid.NewGuid();
        var cat = CatOwnedBy(ownerId);
        rig.Cats.Seed(cat);
        var appointment = BookedAppointment(Guid.NewGuid(), ownerId, cat.Id, TimeSpan.FromDays(3));
        rig.Appointments.Seed(appointment);

        var response = await rig.Handler.HandleAsync(new CancelAppointmentCommand
        {
            AppointmentId = appointment.Id, IsStaff = false,
            UserContext = Context(ownerId, "customer"), CorrelationId = Guid.NewGuid()
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    /// <summary>#25 -- reschedule: the ownership check runs before anything else (the scheduling
    /// ports are never reached, so they are not supplied).</summary>
    [Fact]
    public async Task Reschedule_PreviousOwnerAfterCatTransfer_IsNotFound()
    {
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        var previousOwnerId = Guid.NewGuid();
        var cat = CatOwnedBy(Guid.NewGuid());
        cats.Seed(cat);
        var appointment = BookedAppointment(Guid.NewGuid(), previousOwnerId, cat.Id, TimeSpan.FromDays(3));
        appointments.Seed(appointment);

        var handler = new RescheduleAppointmentCommandHandler(
            appointments, new FakeAggregateRootRepository<BranchAggregate>(), cats,
            new FakeAggregateRootRepository<StaffAccountAggregate>(), null!, Settings(), new FakeStaleHoldReleaser(),
            new NullOutbox(), new StubLinkBuilder(), new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(new RescheduleAppointmentCommand
        {
            AppointmentId = appointment.Id, StartsOn = Now.AddDays(4),
            UserContext = Context(previousOwnerId, "customer"), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.AppointmentNotFound);
    }

    /// <summary>#25 -- confirm: a hold placed before the cat was transferred cannot be confirmed by
    /// the previous owner.</summary>
    [Fact]
    public async Task Confirm_PreviousOwnerAfterCatTransfer_IsHoldNotFound()
    {
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        var previousOwnerId = Guid.NewGuid();
        var cat = CatOwnedBy(Guid.NewGuid());
        cats.Seed(cat);

        var hold = new AppointmentAggregate(Guid.NewGuid());
        hold.PlaceHold(new SlotHoldDto(
            Guid.NewGuid(), previousOwnerId, cat.Id, Guid.NewGuid(),
            new ServiceSnapshot("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT")),
            Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(3), Now.AddDays(3).AddMinutes(30), BookingChannel.Online, null,
            Now.AddMinutes(10), Context(previousOwnerId, "customer"), Guid.NewGuid()));
        appointments.Seed(hold);

        var handler = new ConfirmAppointmentCommandHandler(
            appointments, new FakeAggregateRootRepository<WaitlistEntryAggregate>(), cats,
            new FakeAggregateRootRepository<BranchAggregate>(), new FakeAggregateRootRepository<StaffAccountAggregate>(),
            new NullOutbox(), new StubLinkBuilder(), Settings(), new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(new ConfirmAppointmentCommand
        {
            AppointmentId = hold.Id, UserContext = Context(previousOwnerId, "customer"), CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.HoldNotFound);
        Assert.Equal(AppointmentStatus.Held, hold.Status);
    }

    /// <summary>#8 -- every staff by-id handler refuses another branch's appointment as not found.</summary>
    [Fact]
    public async Task StaffByIdHandlers_OnAnotherBranchsAppointment_AreNotFound()
    {
        var appointments = new FakeAggregateRootRepository<AppointmentAggregate>();
        var customerId = Guid.NewGuid();
        var appointment = BookedAppointment(Guid.NewGuid(), customerId, Guid.NewGuid(), TimeSpan.FromDays(3));
        appointments.Seed(appointment);
        var otherBranch = Guid.NewGuid();
        var user = Context(Guid.NewGuid(), "staff");

        var checkIn = await new CheckInAppointmentCommandHandler(appointments, new FakeAggregateRootRepository<BranchAggregate>(), Settings(), new FakeTimeProvider(Now))
            .HandleAsync(new CheckInAppointmentCommand { AppointmentId = appointment.Id, BranchId = otherBranch, UserContext = user, CorrelationId = Guid.NewGuid() });
        var triage = await new SetTriagePriorityCommandHandler(appointments)
            .HandleAsync(new SetTriagePriorityCommand { AppointmentId = appointment.Id, BranchId = otherBranch, TriagePriority = "Urgent", UserContext = user, CorrelationId = Guid.NewGuid() });
        var complete = await new CompleteAppointmentCommandHandler(appointments, new FakeTimeProvider(Now), new FakeAggregateRootRepository<CatEcommerce.Domain.Aggregates.Visit.VisitAggregate>())
            .HandleAsync(new CompleteAppointmentCommand { AppointmentId = appointment.Id, BranchId = otherBranch, UserContext = user, CorrelationId = Guid.NewGuid() });
        var noShow = await new RecordNoShowCommandHandler(appointments, Settings(), new FakeTimeProvider(Now))
            .HandleAsync(new RecordNoShowCommand { AppointmentId = appointment.Id, BranchId = otherBranch, UserContext = user, CorrelationId = Guid.NewGuid() });
        var start = await new StartAppointmentCommandHandler(
                appointments, new FakeAggregateRootRepository<StaffAccountAggregate>(), new FakeAggregateRootRepository<ClinicServiceAggregate>(),
                new FakeAggregateRootRepository<RoomAggregate>(), new FakeAggregateRootRepository<StaffBranchAssignmentAggregate>(), new FakeTimeProvider(Now))
            .HandleAsync(new StartAppointmentCommand { AppointmentId = appointment.Id, BranchId = otherBranch, UserContext = user, CorrelationId = Guid.NewGuid() });

        foreach (var response in new[] { checkIn, triage, complete, noShow, start })
        {
            Assert.False(response.IsSuccess);
            Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.AppointmentNotFound);
        }

        Assert.Equal(AppointmentStatus.Booked, appointment.Status);
        Assert.Empty(appointments.Updated);
    }
}
