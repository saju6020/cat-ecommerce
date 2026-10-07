using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-NTF-001 §16, Care reminders (F). I-F1, I-F3, I-F4, I-F7, I-F8 -- US-NTF-03.
/// TEST gate additions: I-F2 (window boundaries), I-F5 (transfer), I-F6 (renewal), U-F3
/// (deduplication key format) -- none of these had a test before this pass.</summary>
[Collection("Notifications")]
public sealed class CareReminderEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    [Fact]
    public async Task VaccinationDueIn14Days_SweepRequestsReminder_DeliveredWithCatNameVaccineDateAndLink()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Mitu");
        await SeedVaccinationDueInAsync(catId, dueInDays: 14); // default lead is 14 days

        await RunReminderSweepAsync();
        await RunDeliveryPassAsync();
        await RunDeliveryPassAsync();

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var items = log.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue).ToList();
        Assert.NotEmpty(items);

        var externalItem = items.First(i => i.GetProperty("channel").GetString() != "InApp");
        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{externalItem.GetProperty("id").GetGuid()}", Json);
        Assert.Contains("Mitu", detail.GetProperty("body").GetString());
        Assert.Contains("Rabies", detail.GetProperty("body").GetString());
        // HO-APT-001 §8.9 amendment: CatCareLink now points at the booking flow, not the cat's
        // portal page (category=Vaccination for a vaccination reminder).
        Assert.Contains($"/book?catId={catId}&category=Vaccination", detail.GetProperty("body").GetString());
    }

    [Fact]
    public async Task SweepRunTwice_StillOnlyOneRequest()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Rex");
        await SeedVaccinationDueInAsync(catId, dueInDays: 10);

        await RunReminderSweepAsync();
        await RunReminderSweepAsync();
        await RunDeliveryPassAsync();

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var externalItems = log.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue && i.GetProperty("channel").GetString() != "InApp")
            .ToList();

        Assert.Single(externalItems);
    }

    [Fact]
    public async Task DewormingDueIn7Days_RequestsDewormingReminder()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Whiskers");
        await SeedDewormingDueInAsync(catId, dueInDays: 7); // default lead is 7 days

        await RunReminderSweepAsync();
        await RunDeliveryPassAsync();

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.Contains(log.GetProperty("items").EnumerateArray(), i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.DewormingDue);
    }

    [Fact]
    public async Task CatMarkedDeceasedAfterRequest_BeforeDelivery_Cancelled()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Shadow");
        await SeedVaccinationDueInAsync(catId, dueInDays: 5);

        await RunReminderSweepAsync(); // creates the Pending request
        await MarkCatDeceasedAsync(catId);
        await RunDeliveryPassAsync(); // expand -- guard now fails

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);

        // The guard fails at expansion, so the REQUEST is suppressed and no NotificationAggregate
        // is ever created for it -- proving "nothing sent" the same way I-F7 does.
        Assert.DoesNotContain(log.GetProperty("items").EnumerateArray(), i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);
    }

    [Fact]
    public async Task DeceasedCatAtSweepTime_NoRequestAtAll()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Ghost");
        await SeedVaccinationDueInAsync(catId, dueInDays: 5);
        await MarkCatDeceasedAsync(catId);

        await RunReminderSweepAsync();
        await RunDeliveryPassAsync();

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);

        Assert.DoesNotContain(log.GetProperty("items").EnumerateArray(), i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);
    }

    [Fact]
    public async Task ChangingLeadDaysSetting_SweepPicksUpItemDueIn25Days()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Luna");
        await SeedVaccinationDueInAsync(catId, dueInDays: 25); // outside the default 14-day lead

        var change = await adminClient.PutAsJsonAsync("/api/admin/settings", new
        {
            changes = new[] { new { key = "notifications.vaccinationReminderLeadDays", value = "30" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        try
        {
            await RunReminderSweepAsync();
            await RunDeliveryPassAsync();

            var from = DateTimeOffset.UtcNow.AddMinutes(-5);
            var to = DateTimeOffset.UtcNow.AddMinutes(5);
            var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
            Assert.Contains(log.GetProperty("items").EnumerateArray(), i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);
        }
        finally
        {
            await adminClient.PutAsJsonAsync("/api/admin/settings", new
            {
                changes = new[] { new { key = "notifications.vaccinationReminderLeadDays", value = "14" } }
            }, Json);
        }
    }

    /// <summary>
    /// I-F2. §8.12: the window is <c>NextDueOn - leadDays &lt;= today &lt;= NextDueOn</c>. The
    /// default vaccination lead is 14 days, so: due in 14 days is IN (the far edge -- already
    /// covered by I-F1 above); due in 15 days is OUT (one day outside the lead); due TODAY is IN
    /// (the near edge); overdue by 1 day is OUT (OQ-5: "overdue items get no reminder").
    /// </summary>
    [Fact]
    public async Task ReminderWindowBoundaries_15DaysOutIsExcluded_DueTodayIsIncluded_OverdueByOneIsExcluded()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catTooEarly = await SeedCatAsync(customerId, "TooEarly");
        await SeedVaccinationDueInAsync(catTooEarly, dueInDays: 15); // one day outside the 14-day lead
        var catDueToday = await SeedCatAsync(customerId, "DueToday");
        await SeedVaccinationDueInAsync(catDueToday, dueInDays: 0);
        var catOverdue = await SeedCatAsync(customerId, "Overdue");
        await SeedVaccinationDueInAsync(catOverdue, dueInDays: -1);

        await RunReminderSweepAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var subjectIds = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.VaccinationDue)
            .Select(r => r.SubjectId)
            .ToListAsync();

        Assert.DoesNotContain(catTooEarly, subjectIds);
        Assert.Contains(catDueToday, subjectIds);
        Assert.DoesNotContain(catOverdue, subjectIds);
    }

    /// <summary>
    /// U-F3. §8.12's exact format: <c>care:{kind}:{catId:N}:{refId:N}:{ownerId:N}:{dueOn:yyyyMMdd}</c>
    /// -- the key that makes a missed sweep hour safe to repeat without ever double-sending (I-F2's
    /// sibling SweepRunTwice_StillOnlyOneRequest test proves the EFFECT; this proves the exact
    /// FORMAT the spec names, which nothing previously asserted on directly). Gate fix 2026-09-27
    /// (TEST gate MEDIUM, I-F5/A6): the owner id was added to the key -- see
    /// <see cref="TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner"/>.
    /// </summary>
    [Fact]
    public async Task DeduplicationKey_MatchesTheSpecifiedFormat()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Format");
        var vaccineId = await SeedVaccinationDueInAsync(catId, dueInDays: 10);
        var dueOn = (await BusinessTodayAsync()).AddDays(10);

        await RunReminderSweepAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var request = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .SingleAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.VaccinationDue);

        Assert.Equal($"care:vaccination:{catId:N}:{vaccineId:N}:{customerId:N}:{dueOn:yyyyMMdd}", request.DeduplicationKey);
    }

    /// <summary>
    /// I-F5, as specified: "Transfer the cat to another owner before delivery -&gt; Cancelled; the
    /// next sweep requests one for the new owner." The first half holds (proven below). The second
    /// half originally did NOT (TEST gate finding, MEDIUM, reported 2026-09-27): <c>CareReminderSweep</c>'s
    /// <c>DeduplicationKey</c> was <c>care:{kind}:{catId}:{refId}:{dueOn}</c> (§8.12) -- it did not
    /// vary by owner/recipient, and <c>RequestNotificationCommandHandler</c>'s dedup check
    /// (<c>requestRepository.ExistAsync(r =&gt; r.DeduplicationKey == ...)</c>) matches ANY row with
    /// that key regardless of status. The FIRST (now-Suppressed) request already consumed the key,
    /// so the second sweep's identical key was treated as "already requested" and nothing was ever
    /// created for the new owner -- for this exact due date, the new owner would silently never get
    /// a reminder. Fixed (same day) by including the owner id in the key (§8.12 amended) -- a
    /// transfer now computes a genuinely different key, so this test (unweakened from the version
    /// the TEST gate committed) now passes for real, not by coincidence.
    /// </summary>
    [Fact]
    public async Task TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner()
    {
        var (_, originalOwnerId, _) = await RegisterAndSignInCustomerAsync();
        var (_, newOwnerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(originalOwnerId, "Transferred");
        await SeedVaccinationDueInAsync(catId, dueInDays: 5);

        await RunReminderSweepAsync(); // creates the Pending request for the original owner
        await TransferCatOwnershipAsync(catId, newOwnerId);
        await RunDeliveryPassAsync(); // expand -- guard now fails for the original owner

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);

        var originalOwnerLog = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={originalOwnerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.DoesNotContain(originalOwnerLog.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);

        // The next sweep sees the SAME due vaccination, now owned by the new owner, and requests
        // (and delivers) a fresh reminder for them.
        await RunReminderSweepAsync();
        await RunDeliveryPassAsync();

        var newOwnerLog = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={newOwnerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.Contains(newOwnerLog.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);
    }

    /// <summary>
    /// TEST gate, second pass, 2026-09-27. The owner-id fix (§8.12 amended by the first-pass finding)
    /// makes a transfer to a NEW owner compute a genuinely different key
    /// (<see cref="TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner"/>),
    /// but a round trip -- original owner, then a second owner, then BACK to the original owner, all
    /// before either request is delivered -- computes the SAME key for the original owner's second
    /// request as its own first (now-Suppressed) request already consumed. §8.11's own dedup contract
    /// ("<c>RequestNotificationCommand</c> pre-checks and treats the index's <c>23505</c> as
    /// 'already requested' ... a duplicate can never fail a producer's business command") explicitly
    /// forbids a second row for the same key regardless of the first row's status -- so this is NOT a
    /// resurfacing of the fixed bug; it is the dedup guarantee working exactly as specified. This case
    /// proves the fix does not, as an unintended side effect, create a duplicate/second row for the
    /// original owner once they own the cat again -- the sweep must complete as a clean no-op, not
    /// throw, and the row count for the original owner's key must stay exactly one.
    /// </summary>
    [Fact]
    public async Task TransferBackToOriginalOwner_BeforeDelivery_OriginalOwnerIsRequestedAgain_WithoutDuplicates()
    {
        var (_, originalOwnerId, _) = await RegisterAndSignInCustomerAsync();
        var (_, otherOwnerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(originalOwnerId, "Boomerang");
        var vaccineId = await SeedVaccinationDueInAsync(catId, dueInDays: 5);
        var dueOn = (await BusinessTodayAsync()).AddDays(5);
        var originalOwnerKey = $"care:vaccination:{catId:N}:{vaccineId:N}:{originalOwnerId:N}:{dueOn:yyyyMMdd}";

        await RunReminderSweepAsync(); // Pending request #1 for the original owner, key = originalOwnerKey
        await TransferCatOwnershipAsync(catId, otherOwnerId);
        await RunDeliveryPassAsync(); // expand fails (owner mismatch) -- request #1 Suppressed, key consumed

        await RunReminderSweepAsync(); // Pending request #2 for the other owner, a DIFFERENT key
        await TransferCatOwnershipAsync(catId, originalOwnerId); // back to the original owner
        await RunDeliveryPassAsync(); // expand fails again (owner mismatch) -- request #2 Suppressed

        await RunReminderSweepAsync(); // the original owner owns the cat again (generation 2)
        await RunReminderSweepAsync(); // a repeated sweep must still be a no-op (dedup holds)

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var originalOwnerRequests = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.RecipientId == originalOwnerId && r.TemplateKey == NotificationTemplateCatalogue.VaccinationDue)
            .ToListAsync();

        // NTF-001 backlog row 1: the original request keeps the unsuffixed key, and the round trip
        // gets its own generation-2 request instead of being swallowed by the Suppressed one.
        Assert.Equal(2, originalOwnerRequests.Count);
        Assert.Single(originalOwnerRequests, r => r.DeduplicationKey == originalOwnerKey);
        Assert.Single(originalOwnerRequests, r => r.DeduplicationKey == originalOwnerKey + ":g2");
    }

    /// <summary>
    /// I-F6. A new vaccination for the SAME vaccine is recorded (moving NextDueOn a year out) after
    /// the sweep enqueues a reminder for the OLD due date but before it is delivered -- the guard
    /// must cancel the stale reminder, and it must not fire again until the NEW due date's own
    /// window opens (default lead 14 days -- the renewed due date is far outside it).
    /// </summary>
    [Fact]
    public async Task RecordNewVaccinationBeforeDelivery_Cancelled_NoNewReminderUntilTheNewWindow()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Renewed");
        var vaccineId = await SeedVaccinationDueInAsync(catId, dueInDays: 5);
        var newDueOn = (await BusinessTodayAsync()).AddDays(200);

        await RunReminderSweepAsync(); // creates the Pending request for the old due date
        await SeedAdditionalVaccinationAsync(catId, vaccineId, dueInDays: 200); // renewed -- NextDueOn moves ~6 months out, and AdministeredOn is still safely in the past
        await RunDeliveryPassAsync(); // expand -- guard now fails (stale due date no longer latest)

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.DoesNotContain(log.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);

        // The renewed due date is ~200 days out, far outside the default 14-day lead -- the next
        // sweep must not create a new reminder FOR THAT NEW DUE DATE yet (the stale, now-Suppressed
        // request for the OLD due date also has this same SubjectRef, so the check below is scoped
        // by the new due date specifically, not just the vaccine id).
        await RunReminderSweepAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var anyRequestForTheNewDueDate = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .AnyAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.VaccinationDue
                           && r.SubjectRef == vaccineId && r.SubjectDueOn == newDueOn);
        Assert.False(anyRequestForTheNewDueDate);
    }
}
