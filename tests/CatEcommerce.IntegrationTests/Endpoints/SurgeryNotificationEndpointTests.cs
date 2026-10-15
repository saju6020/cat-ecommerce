using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SRG-001 §8.10 (ADR-169) -- the surgery messages, driven through the real HTTP endpoints and HO-NTF-001's real delivery pass under the fake clock. Until a provider exists (HO-NTF-001 OQ-1)
/// the in-app copy is the delivered one, so that is what these assert. <c>[Collection("Notifications")]</c>: a class that drives the delivery pass against the shared notification tables
/// must never run beside another that does (see <c>AppointmentReminderFlowTests</c>).
/// </summary>
[Collection("Notifications")]
public sealed class SurgeryNotificationEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static async Task<NotificationRequestAggregate[]> RequestsAsync(SurgeryWorld world, string templateKey, Guid? subjectId = null)
    {
        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        return await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.TemplateKey == templateKey && r.RecipientId == world.CustomerId && (subjectId == null || r.SubjectId == subjectId))
            .ToArrayAsync();
    }

    private static Task RunUntilAsync(SurgeryWorld world, Func<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext, Task<bool>> done) =>
        RunNotificationDeliveryUntilAsync(world.Api, done);

    private static Task<bool> InAppSentAsync(CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext db, Guid customerId, string templateKey) =>
        db.Set<NotificationAggregate>().AsNoTracking().AnyAsync(n => n.RecipientId == customerId && n.TemplateKey == templateKey && n.Channel == NotificationChannel.InApp);

    /// <summary>I-B8 (delivery half) -- after the delivery pass the customer holds an in-app copy of the booked message, with the details link and the fasting instructions.</summary>
    [Fact]
    public async Task I_B8_TheBookedMessage_ReachesTheCustomerInApp()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 3, 10));

        await RunUntilAsync(world, db => InAppSentAsync(db, world.CustomerId, NotificationTemplateCatalogue.SurgeryBooked));

        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var copy = await db.Set<NotificationAggregate>().AsNoTracking()
            .FirstAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.SurgeryBooked && n.Channel == NotificationChannel.InApp);
        Assert.Contains("No food from midnight", copy.Body);
        Assert.Contains($"/portal/surgeries/{surgeryId}", copy.LinkPath ?? "");
    }

    /// <summary>I-B9 -- reschedule after the pre-op reminder was queued: the old reminder is suppressed by its guard (the stamp changed) and never sent, the new one is queued for the new time,
    /// and the rescheduled message is delivered.</summary>
    [Fact]
    public async Task I_B9_Reschedule_SuppressesTheOldReminder_QueuesANewOne_AndSendsTheRescheduledMessage()
    {
        var world = await SeedSurgeryWorldAsync();
        var first = LocalStart(world, 3, 10);
        var second = LocalStart(world, 3, 14);
        var surgeryId = await BookAsync(world, first);
        var version = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("version").GetInt32();

        var oldReminder = Assert.Single(await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryPreOpReminder, surgeryId));
        Assert.Equal(first.AddHours(-18), oldReminder.SendAfter);

        var moved = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/reschedule",
            new { expectedVersion = version, startsOn = second, theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.True(moved.StatusCode == HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());

        var reminders = await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryPreOpReminder, surgeryId);
        Assert.Equal(2, reminders.Length);
        var newReminder = Assert.Single(reminders, r => r.Id != oldReminder.Id);
        Assert.Equal(second.AddHours(-18), newReminder.SendAfter);
        Assert.NotEqual(oldReminder.SubjectStamp, newReminder.SubjectStamp);

        // Past the OLD reminder's send time (but before the new one's): the guard fails it.
        world.Clock.SetUtcNow(oldReminder.SendAfter!.Value.AddMinutes(5));
        await RunUntilAsync(world, async db =>
            await db.Set<NotificationRequestAggregate>().AsNoTracking().AnyAsync(r => r.Id == oldReminder.Id && r.Status != NotificationRequestStatus.Pending)
            && await InAppSentAsync(db, world.CustomerId, NotificationTemplateCatalogue.SurgeryRescheduled));

        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        Assert.Equal(NotificationRequestStatus.Suppressed, (await db.Set<NotificationRequestAggregate>().AsNoTracking().SingleAsync(r => r.Id == oldReminder.Id)).Status);
        Assert.Equal(NotificationRequestStatus.Pending, (await db.Set<NotificationRequestAggregate>().AsNoTracking().SingleAsync(r => r.Id == newReminder.Id)).Status);
        Assert.False(await db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.SurgeryPreOpReminder));
    }

    /// <summary>I-B10 (message half) -- cancel: the cancellation is delivered and the still-pending reminder is never sent.</summary>
    [Fact]
    public async Task I_B10_Cancel_SendsTheCancellation_AndTheReminderIsNeverSent()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 3, 10);
        var surgeryId = await BookAsync(world, start);
        var reminder = Assert.Single(await RequestsAsync(world, NotificationTemplateCatalogue.SurgeryPreOpReminder, surgeryId));

        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/cancel", new { reason = "Owner called to cancel" }, Json)).StatusCode);

        world.Clock.SetUtcNow(reminder.SendAfter!.Value.AddMinutes(5));
        await RunUntilAsync(world, async db =>
            await db.Set<NotificationRequestAggregate>().AsNoTracking().AnyAsync(r => r.Id == reminder.Id && r.Status != NotificationRequestStatus.Pending)
            && await InAppSentAsync(db, world.CustomerId, NotificationTemplateCatalogue.SurgeryCancelled));

        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        Assert.Equal(NotificationRequestStatus.Suppressed, (await db.Set<NotificationRequestAggregate>().AsNoTracking().SingleAsync(r => r.Id == reminder.Id)).Status);
        Assert.False(await db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.SurgeryPreOpReminder));
    }
}
