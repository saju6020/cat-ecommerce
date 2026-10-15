using CatEcommerce.Application.CommandHandlers.Surgery;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Surgery;

/// <summary>HO-SRG-001 §8.10 -- U-B9 (the pre-op guard), U-B10 (a pre-op reminder whose time has passed is not enqueued), U-F3's reminder times and U-F5 (the follow-up guard).</summary>
public sealed class SurgeryNotificationAndGuardTests
{
    private static readonly DateTimeOffset BookedAt = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 10:00 Dhaka
    private static readonly Guid CustomerId = Guid.NewGuid();

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
        public string BookingLink() => "";
        public string ProductLink(string slug, Guid skuId) => "";
        public string VisitSummaryLink(Guid visitId) => "";
        public string InvoiceLink(Guid invoiceId) => "";
        public string ReceiptLink(Guid paymentId) => "";
        public string OrderLink(Guid orderId) => "";
        public string PrescriptionLink(Guid prescriptionId) => "";
        public string RepeatRequestLink(Guid prescriptionId) => "";
        public string DietAuthorisationsLink() => "";
        public string SurgeryLink(Guid surgeryId) => $"https://portal.example.test/portal/surgeries/{surgeryId}";
    }

    private static CatAggregate Cat(Guid? ownerId = null)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(ownerId ?? CustomerId, "Mitu", "Cat", CatSex.Female, new DateOnly(2024, 1, 1), DateOfBirthPrecision.Exact, "DSH", "Tabby",
            new DateOnly(2026, 10, 1), TestUserContext.Create(), Guid.NewGuid()));
        return cat;
    }

    private static SurgeryAggregate Surgery(CatAggregate cat, DateTimeOffset startsOn)
    {
        var surgery = new SurgeryAggregate(Guid.NewGuid());
        surgery.Schedule(new SurgeryScheduleDto(
            Guid.NewGuid(), cat.Id, CustomerId, Guid.NewGuid(), new ProcedureSnapshot("Spay", ServiceCategory.Spay, 60),
            new SurgeryPriceSnapshot(new Money(5000m, "BDT"), SurgeryPricingBasis.Standard, null, null), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            startsOn, startsOn.AddHours(1), startsOn.AddHours(-1), "No food from midnight.", BookedAt, TestUserContext.Create(), Guid.NewGuid()));
        return surgery;
    }

    private static SurgeryNotifications.MessageContext Ctx() => new("Mitu", "Gulshan Clinic", "House 12", "Asia/Dhaka");

    private static InMemoryBusinessSettings Settings(int preOpHours = 18, int followUpLead = 1) => new(new Dictionary<string, object>
    {
        [SettingKeys.SurgeryPreOpReminderHours] = preOpHours,
        [SettingKeys.SurgeryFollowUpReminderLeadDays] = followUpLead,
    });

    // ── U-B10 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-B10 -- StartsOn minus 18 h is already past: no reminder is enqueued (the booked message is the only one).</summary>
    [Fact]
    public void U_B10_APreOpReminder_WhoseTimeHasPassed_IsNotEnqueued()
    {
        var surgery = Surgery(Cat(), BookedAt.AddHours(10)); // 18 h before is 8 h ago
        var outbox = new CapturingOutbox();

        SurgeryNotifications.EnqueuePreOpReminder(outbox, new Links(), Settings(), surgery, Ctx(), BookedAt, Guid.NewGuid(), TestUserContext.Create());

        Assert.Empty(outbox.Enqueued);
    }

    [Fact]
    public void APreOpReminder_InTheFuture_IsEnqueued_WithItsSendAfter_NotAfter_AndStamp()
    {
        var surgery = Surgery(Cat(), BookedAt.AddHours(30));
        var outbox = new CapturingOutbox();

        SurgeryNotifications.EnqueuePreOpReminder(outbox, new Links(), Settings(), surgery, Ctx(), BookedAt, Guid.NewGuid(), TestUserContext.Create());

        var request = Assert.Single(outbox.Enqueued);
        Assert.Equal(NotificationTemplateCatalogue.SurgeryPreOpReminder, request.TemplateKey);
        Assert.Equal(NotificationSubjectKind.SurgeryPreOp, request.SubjectKind);
        Assert.Equal(surgery.Id, request.SubjectId);
        Assert.Equal(surgery.StartsOn.AddHours(-18), request.SendAfter);
        Assert.Equal(surgery.StartsOn, request.NotAfter);
        Assert.Equal(surgery.ScheduleStamp, request.SubjectStamp);
        Assert.Equal(surgery.CustomerId, request.RecipientId);
        Assert.Equal($"https://portal.example.test/portal/surgeries/{surgery.Id}", request.Variables["detailsLink"]);
        Assert.Equal("Mon 12 Oct 2026, 10:00", SurgeryNotifications.FormatLocal(BookedAt, "Asia/Dhaka"));
    }

    [Fact]
    public void APreOpReminder_SwitchedOff_IsNeverEnqueued()
    {
        var outbox = new CapturingOutbox();

        SurgeryNotifications.EnqueuePreOpReminder(outbox, new Links(), Settings(preOpHours: 0), Surgery(Cat(), BookedAt.AddDays(5)), Ctx(), BookedAt, Guid.NewGuid(), TestUserContext.Create());

        Assert.Empty(outbox.Enqueued);
    }

    [Fact]
    public void TheBooked_Rescheduled_AndCancelledMessages_AreUrgentTemplates_ToTheOwner_WithNoSecretsOrReasons()
    {
        var surgery = Surgery(Cat(), BookedAt.AddDays(2));
        var outbox = new CapturingOutbox();

        SurgeryNotifications.EnqueueBooked(outbox, new Links(), surgery, Ctx(), Guid.NewGuid(), TestUserContext.Create());
        SurgeryNotifications.EnqueueRescheduled(outbox, new Links(), surgery, Ctx(), BookedAt, Guid.NewGuid(), TestUserContext.Create());
        SurgeryNotifications.EnqueueCancelled(outbox, surgery, Ctx(), Guid.NewGuid(), TestUserContext.Create());

        Assert.Equal([NotificationTemplateCatalogue.SurgeryBooked, NotificationTemplateCatalogue.SurgeryRescheduled, NotificationTemplateCatalogue.SurgeryCancelled],
            outbox.Enqueued.Select(r => r.TemplateKey).ToArray());
        Assert.All(outbox.Enqueued, r => Assert.Equal(CustomerId, r.RecipientId));
        Assert.Equal("Mon 12 Oct 2026, 10:00", outbox.Enqueued[1].Variables["previousStartsOnLocal"]);
        Assert.False(outbox.Enqueued[2].Variables.ContainsKey("detailsLink"));
        Assert.All(outbox.Enqueued, r => Assert.Null(r.SendAfter));
    }

    // ── U-F3 (reminder times) ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>U-F3 -- one reminder per follow-up: SendAfter = due date minus one day at 10:00 branch-local, NotAfter = the end of the due date; valid while the follow-up is Pending.</summary>
    [Fact]
    public void U_F3_AFollowUpReminder_IsSentTheDayBeforeAt10Local_AndExpiresAtTheEndOfTheDueDate()
    {
        var surgery = Surgery(Cat(), BookedAt);
        var followUp = new SurgeryFollowUp(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16));
        var outbox = new CapturingOutbox();

        SurgeryNotifications.EnqueueFollowUpReminder(outbox, new Links(), Settings(), surgery, followUp, Ctx(), BookedAt, Guid.NewGuid(), TestUserContext.Create());

        var request = Assert.Single(outbox.Enqueued);
        Assert.Equal(NotificationTemplateCatalogue.SurgeryFollowUpDue, request.TemplateKey);
        Assert.Equal(NotificationSubjectKind.SurgeryFollowUp, request.SubjectKind);
        Assert.Equal(surgery.Id, request.SubjectId);
        Assert.Equal(followUp.Id, request.SubjectRef);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 4, 0, 0, TimeSpan.Zero), request.SendAfter); // 14 Oct 10:00 Dhaka
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero), request.NotAfter); // end of 15 Oct Dhaka
        Assert.Equal("2026-10-15", request.Variables["dueOn"]);
        Assert.Equal("Wound check", request.Variables["followUpName"]);
        Assert.Contains("category=FollowUp", request.Variables["bookingLink"]);
    }

    [Fact]
    public void AFollowUp_DueSoonerThanTheLeadTime_IsRemindedAtOnce_NotNever()
    {
        var surgery = Surgery(Cat(), BookedAt);
        var followUp = new SurgeryFollowUp(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 14));
        var outbox = new CapturingOutbox();

        SurgeryNotifications.EnqueueFollowUpReminder(outbox, new Links(), Settings(), surgery, followUp, Ctx(), BookedAt, Guid.NewGuid(), TestUserContext.Create());

        Assert.Equal(BookedAt, Assert.Single(outbox.Enqueued).SendAfter);
    }

    // ── U-B9 / U-F5: the guards ─────────────────────────────────────────────────────────────────────────────────────────────

    private static (SurgeryPreOpGuard Guard, SurgeryAggregate Surgery, CatAggregate Cat) PreOpWorld()
    {
        var cat = Cat();
        var surgery = Surgery(cat, BookedAt.AddDays(2));
        var surgeries = new FakeAggregateRootRepository<SurgeryAggregate>();
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        surgeries.Seed(surgery);
        cats.Seed(cat);
        return (new SurgeryPreOpGuard(surgeries, cats), surgery, cat);
    }

    /// <summary>U-B9 -- scheduled with the same stamp: valid. Rescheduled (new stamp), cancelled, admitted, cat deceased, owner changed: invalid.</summary>
    [Fact]
    public async Task U_B9_ThePreOpGuard_IsValidOnlyWhileScheduled_WithTheSameStamp_ForALiveCatOfTheSameOwner()
    {
        var (guard, surgery, cat) = PreOpWorld();
        var stamp = surgery.ScheduleStamp;

        Assert.True(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, stamp));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, null)); // no stamp, no validity
        Assert.False(await guard.IsStillValidAsync(Guid.NewGuid(), surgery.Id, null, null, stamp)); // another recipient
        Assert.False(await guard.IsStillValidAsync(CustomerId, Guid.NewGuid(), null, null, stamp)); // unknown surgery

        cat.MarkDeceased(new CatDeceasedDto(new DateOnly(2026, 10, 12), null, new DateOnly(2026, 10, 12), TestUserContext.Create(), Guid.NewGuid()));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, stamp));
    }

    [Fact]
    public async Task U_B9_ARescheduledOrCancelledSurgery_FailsThePreOpGuard()
    {
        var (guard, surgery, _) = PreOpWorld();
        var oldStamp = surgery.ScheduleStamp;

        var later = surgery.StartsOn.AddDays(1);
        surgery.Reschedule(new SurgeryRescheduleDto(surgery.TheatreId, surgery.SurgeonId, surgery.AssistantId, later, later.AddHours(1), later.AddHours(-1), "x", new DateOnly(2026, 10, 15), 1,
            BookedAt, TestUserContext.Create(), Guid.NewGuid()));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, oldStamp));
        Assert.True(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, surgery.ScheduleStamp));

        surgery.Cancel(new SurgeryCancelDto("Owner cancelled", BookedAt, TestUserContext.Create(), Guid.NewGuid()));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, surgery.ScheduleStamp));
    }

    [Fact]
    public async Task ThePreOpGuard_FailsOnceTheOwnerHasChanged()
    {
        var (guard, surgery, cat) = PreOpWorld();
        cat.TransferOwnership(new CatOwnershipTransferDto(Guid.NewGuid(), "Sold", TestUserContext.Create(), Guid.NewGuid()));

        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null, surgery.ScheduleStamp));
    }

    /// <summary>U-F5 -- a pending follow-up: valid; done or cancelled: invalid; cat deceased: invalid.</summary>
    [Fact]
    public async Task U_F5_TheFollowUpGuard_IsValidOnlyWhilePending_ForALiveCat()
    {
        var cat = Cat();
        var surgery = Surgery(cat, BookedAt);
        // Take the surgery to Discharged with two follow-ups through the real path.
        var plan = new[]
        {
            new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16)),
            new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.SutureRemoval, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 26)),
        };
        DischargedWithFollowUps(surgery, plan);

        var surgeries = new FakeAggregateRootRepository<SurgeryAggregate>();
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        surgeries.Seed(surgery);
        cats.Seed(cat);
        var guard = new SurgeryFollowUpGuard(surgeries, cats);

        Assert.True(await guard.IsStillValidAsync(CustomerId, surgery.Id, plan[0].Id, null));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, null, null)); // no follow-up named
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, Guid.NewGuid(), null)); // unknown follow-up

        surgery.CloseFollowUp(new FollowUpCloseDto(plan[0].Id, FollowUpStatus.Done, null, BookedAt, TestUserContext.Create(), Guid.NewGuid()));
        surgery.CloseFollowUp(new FollowUpCloseDto(plan[1].Id, FollowUpStatus.Cancelled, "Absorbable sutures", BookedAt, TestUserContext.Create(), Guid.NewGuid()));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, plan[0].Id, null));
        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, plan[1].Id, null));
    }

    [Fact]
    public async Task TheFollowUpGuard_FailsForADeceasedCat()
    {
        var cat = Cat();
        var surgery = Surgery(cat, BookedAt);
        var plan = new[] { new PlannedFollowUpInput(Guid.NewGuid(), SurgeryFollowUpKind.WoundCheck, new DateOnly(2026, 10, 15), new DateOnly(2026, 10, 14), new DateOnly(2026, 10, 16)) };
        DischargedWithFollowUps(surgery, plan);
        var surgeries = new FakeAggregateRootRepository<SurgeryAggregate>();
        var cats = new FakeAggregateRootRepository<CatAggregate>();
        surgeries.Seed(surgery);
        cats.Seed(cat);
        var guard = new SurgeryFollowUpGuard(surgeries, cats);
        Assert.True(await guard.IsStillValidAsync(CustomerId, surgery.Id, plan[0].Id, null));

        cat.MarkDeceased(new CatDeceasedDto(new DateOnly(2026, 10, 13), null, new DateOnly(2026, 10, 13), TestUserContext.Create(), Guid.NewGuid()));

        Assert.False(await guard.IsStillValidAsync(CustomerId, surgery.Id, plan[0].Id, null));
    }

    private static void DischargedWithFollowUps(SurgeryAggregate s, IReadOnlyList<PlannedFollowUpInput> followUps)
    {
        var user = TestUserContext.Create();
        s.CompleteChecklistItem(new ChecklistCompleteDto(PreOpChecklistItemKind.FastingInstructionsIssued, null, null, null, BookedAt, user, Guid.NewGuid()));
        s.WaiveChecklistItem(new ChecklistWaiveDto(PreOpChecklistItemKind.PreAnaestheticBloodWork, "Young healthy cat", true, BookedAt, user, Guid.NewGuid()));
        s.ApplyWeight(Guid.NewGuid(), new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 12), 1, user.UserId, BookedAt);
        s.RecordAsaGrade(new AsaGradeDto(AsaGrade.I, false, true, BookedAt, user, Guid.NewGuid()));
        s.Admit(new SurgeryAdmitDto(BookedAt, "Asia/Dhaka", 12, user, Guid.NewGuid()));
        s.Start(new SurgeryStartDto(true, null, false, BookedAt, user, Guid.NewGuid()));
        s.EndProcedure(new SurgeryEndProcedureDto(ProcedureOutcome.Completed, BookedAt.AddHours(1), "Asia/Dhaka", user, Guid.NewGuid()));
        s.SetRecoveryStatus(new RecoveryStatusDto(RecoveryStatus.ReadyForDischarge, null, BookedAt.AddHours(2), user, Guid.NewGuid()));
        s.Discharge(new SurgeryDischargeDto("Rest.", followUps, false, BookedAt.AddHours(3), user, Guid.NewGuid()));
    }
}
