using CatEcommerce.Application.CommandHandlers.Clinic;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Fakes.Administration;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Clinic;

/// <summary>
/// HO-APT-001 §16 U-D1 -- ACCEPTANCE gate fix (2026-09-28, A13). <c>ClinicNotifications.EnqueueReminders</c>
/// enqueues a reminder only while its own lead time has not yet passed at booking/reschedule time
/// (§8.9). Default settings: 24h and 2h before.
/// </summary>
public sealed class ClinicNotificationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);

    private sealed class CapturingNotificationOutbox : INotificationOutbox
    {
        public List<NotificationRequestDto> Enqueued { get; } = [];
        public void Enqueue(NotificationRequestDto request) => Enqueued.Add(request);
    }

    private sealed class StubLinkBuilder : INotificationLinkBuilder
    {
        public string PasswordResetLink(string token) => "";
        public string CatCareLink(Guid catId, string category) => "";
        public string UnsubscribeLink(string token) => "";
        public string OneClickUnsubscribeUrl(string token) => "";
        public string AppointmentLink(Guid appointmentId) => $"https://portal.example.test/portal/appointments/{appointmentId}";
        public string BookingLink() => "https://portal.example.test/book";
        public string ProductLink(string slug, Guid skuId) => $"https://portal.example.test/shop/products/{slug}?sku={skuId}";
        public string VisitSummaryLink(Guid visitId) => $"https://portal.example.test/portal/visits/{visitId}";
        public string InvoiceLink(Guid invoiceId) => "";
        public string ReceiptLink(Guid paymentId) => "";
        public string OrderLink(Guid orderId) => "";
        public string PrescriptionLink(Guid prescriptionId) => "";
        public string RepeatRequestLink(Guid prescriptionId) => "";
        public string DietAuthorisationsLink() => "";
    }

    private static ServiceSnapshot Snapshot() => new("Consultation", ServiceCategory.Consultation, 30, new Money(500m, "BDT"));

    private static AppointmentAggregate BookedAppointmentStartingIn(TimeSpan leadTime)
    {
        var startsOn = Now.Add(leadTime);
        var appointment = new AppointmentAggregate(Guid.NewGuid());
        appointment.PlaceHold(new SlotHoldDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Snapshot(),
            Guid.NewGuid(), Guid.NewGuid(), startsOn, startsOn.AddMinutes(30), BookingChannel.Online, null,
            Now.AddMinutes(10), TestUserContext.Create(), Guid.NewGuid()));
        appointment.Confirm(new AppointmentConfirmDto(Now, "Annual check", TestUserContext.Create(), Guid.NewGuid()));
        return appointment;
    }

    private static ClinicNotifications.MessageContext Context() => new("Mitu", "Test Branch", "1 Road, Dhaka", "Dr Rahman", "Asia/Dhaka");

    /// <summary><see cref="InMemoryBusinessSettings"/> returns 0 for any unmapped key (it is not
    /// fail-closed -- see its own doc comment) -- SettingDefinitions' catalogue defaults (24h/2h)
    /// must be supplied explicitly here.</summary>
    private static InMemoryBusinessSettings SettingsWithDefaultReminderHours() => new(new Dictionary<string, object>
    {
        [SettingKeys.BookingFirstReminderHours] = 24,
        [SettingKeys.BookingSecondReminderHours] = 2,
    });

    [Theory]
    [InlineData(30, 2)] // 30h ahead: both the 24h and the 2h reminder still have time to fire.
    [InlineData(3, 1)]  // 3h ahead: the 24h reminder's lead time has already passed; only the 2h one is enqueued.
    [InlineData(1.5, 0)] // 90 min ahead: both lead times have already passed.
    public void EnqueueReminders_LeadTimeAlreadyPassed_OnlyStillFutureRemindersAreEnqueued(double hoursAhead, int expectedCount)
    {
        var appointment = BookedAppointmentStartingIn(TimeSpan.FromHours(hoursAhead));
        var outbox = new CapturingNotificationOutbox();
        var settings = SettingsWithDefaultReminderHours();

        ClinicNotifications.EnqueueReminders(outbox, new StubLinkBuilder(), settings, appointment, Context(), Now, Guid.NewGuid(), TestUserContext.Create());

        Assert.Equal(expectedCount, outbox.Enqueued.Count);
        Assert.All(outbox.Enqueued, r => Assert.Equal(NotificationTemplateCatalogue.AppointmentReminder, r.TemplateKey));
        Assert.All(outbox.Enqueued, r => Assert.Equal(appointment.ScheduleStamp, r.SubjectStamp));
        Assert.All(outbox.Enqueued, r => Assert.True(r.SendAfter > Now, "A reminder must never be enqueued with a SendAfter already in the past."));
    }

    [Fact]
    public void EnqueueReminders_30HoursAhead_SendAfterMatchesEachLeadTime()
    {
        var appointment = BookedAppointmentStartingIn(TimeSpan.FromHours(30));
        var outbox = new CapturingNotificationOutbox();
        var settings = SettingsWithDefaultReminderHours();

        ClinicNotifications.EnqueueReminders(outbox, new StubLinkBuilder(), settings, appointment, Context(), Now, Guid.NewGuid(), TestUserContext.Create());

        var startsOn = appointment.StartsOn!.Value;
        Assert.Contains(outbox.Enqueued, r => r.SendAfter == startsOn.AddHours(-24));
        Assert.Contains(outbox.Enqueued, r => r.SendAfter == startsOn.AddHours(-2));
    }
}
