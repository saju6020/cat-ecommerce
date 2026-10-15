using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase G -- I-G1 .. I-G6 and the I-C8 cases that need endpoints built here (photo and follow-up on a
/// finalised visit): the discharge summary (staff and portal, JSON and PDF), the portal's ownership rules and the follow-up.</summary>
public sealed class DischargeSummaryAndFollowUpEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private const string HomeCare = "Keep the ear dry and give the drops twice a day.";

    /// <summary>The notification pass works through the <i>whole</i> shared database oldest request first, in batches. A test that jumps its
    /// clock forward to a reminder's send time makes every other test's leftover request due as well, so a request stamped "now" can sit behind
    /// thousands of them and never be reached. These tests therefore start their clock 30 days back: their requests are the oldest in the
    /// database and are expanded first, whatever else the suite has left behind.</summary>
    private static DateTimeOffset OlderThanEveryOtherTestsRequests => DateTimeOffset.UtcNow.AddDays(-30);

    private async Task<(ClinicalWorld World, Guid VisitId)> FinalisedVisitAsync(DateTimeOffset? start = null)
    {
        var world = await SeedClinicalWorldAsync(start);
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        Assert.Equal(HttpStatusCode.Created, (await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { weightKg = 4.2m }, Json)).StatusCode);

        var codeId = await CreateDiagnosisCodeAsync(world.Vet);
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        var saved = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Otitis externa", plan = "Ear drops", dischargeInstructions = HomeCare,
            problems = new[] { new { diagnosisCodeId = codeId, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var today = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime);
        var setFollowUp = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Reminder", dueOn = today.AddDays(14) }, Json);
        Assert.True(setFollowUp.StatusCode == HttpStatusCode.NoContent, await setFollowUp.Content.ReadAsStringAsync());

        var finalised = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise",
            new { expectedVersion = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, finalised.StatusCode);
        return (world, visitId);
    }

    /// <summary>I-G1 -- staff and owner read the summary; the PDF prints (US-CLN-16).</summary>
    [Fact]
    public async Task I_G1_StaffAndOwnerReadTheSummary_AndThePdfIsAvailable()
    {
        var (world, visitId) = await FinalisedVisitAsync();

        foreach (var (client, route) in new[] { (world.Vet, $"/api/staff/visits/{visitId}/discharge-summary"), (world.Customer, $"/api/customers/me/visits/{visitId}") })
        {
            var response = await client.GetAsync(route);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            var summary = await ReadAsync(response);

            Assert.Equal("Mitu", summary.GetProperty("catName").GetString());
            Assert.Equal(HomeCare, summary.GetProperty("homeCareInstructions").GetString());
            Assert.Equal(1, summary.GetProperty("problems").GetArrayLength());
            Assert.Equal("4.20", summary.GetProperty("weightAtVisit").GetProperty("value").GetString());
            Assert.Equal("kg", summary.GetProperty("weightAtVisit").GetProperty("unit").GetString());
            Assert.Equal("Reminder", summary.GetProperty("followUp").GetProperty("kind").GetString());
            Assert.False(summary.GetProperty("prescriptionsAvailable").GetBoolean()); // until HO-PHM-001 registers a contributor
            Assert.False(string.IsNullOrWhiteSpace(summary.GetProperty("attendingVet").GetString()));
            Assert.Equal(0, summary.GetProperty("corrections").GetArrayLength());
        }

        foreach (var (client, route) in new[] { (world.Nurse, $"/api/staff/visits/{visitId}/discharge-summary.pdf"), (world.Customer, $"/api/customers/me/visits/{visitId}/discharge-summary.pdf") })
        {
            var pdf = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
            Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
            Assert.Contains("no-store", pdf.Headers.CacheControl!.ToString());
            var bytes = await pdf.Content.ReadAsByteArrayAsync();
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
        }

        // Reception is not a clinical reader (OQ-5).
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync($"/api/staff/visits/{visitId}/discharge-summary")).StatusCode);

        // A correction made after finalisation appears under "Corrections" -- the summary is derived, never stored.
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "Weight was 4.1 kg." }, Json)).StatusCode);
        var corrected = await ReadAsync(await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}"));
        Assert.Equal("Weight was 4.1 kg.", Assert.Single(corrected.GetProperty("corrections").EnumerateArray()).GetProperty("text").GetString());

        // The owner's list shows finalised visits only, and their lab results route answers.
        var list = await ReadAsync(await world.Customer.GetAsync($"/api/customers/me/cats/{world.CatId}/visits"));
        var listed = Assert.Single(list.EnumerateArray());
        Assert.Equal(visitId, listed.GetProperty("id").GetGuid());
        Assert.Equal(DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime).AddDays(14), DateOnly.Parse(listed.GetProperty("followUpOn").GetString()!));   // additive, owner decision 2026-10-10
        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/customers/me/cats/{world.CatId}/lab-results")).StatusCode);
    }

    /// <summary>I-G2 -- an open visit has no summary: <c>404</c> for staff and owner alike (a draft is not a summary).</summary>
    [Fact]
    public async Task I_G2_AnOpenVisitHasNoSummary_ForStaffOrOwner()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.GetAsync($"/api/staff/visits/{visitId}/discharge-summary")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.GetAsync($"/api/staff/visits/{visitId}/discharge-summary.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}/discharge-summary.pdf")).StatusCode);

        var list = await ReadAsync(await world.Customer.GetAsync($"/api/customers/me/cats/{world.CatId}/visits"));
        Assert.Equal(0, list.GetArrayLength());
    }

    /// <summary>I-G3 -- another customer's visit is a 404, and so is the previous owner's after an ownership transfer (FR-CAT-8).</summary>
    [Fact]
    public async Task I_G3_OnlyTheCurrentOwnerReadsTheirCatsVisit()
    {
        var (world, visitId) = await FinalisedVisitAsync();
        var (stranger, strangerId, _) = await RegisterAndSignInCustomerOnAsync(world.Api);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/visits/{visitId}/discharge-summary.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/cats/{world.CatId}/visits")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/cats/{world.CatId}/lab-results")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);

        await using (var scope = world.Api.Services.CreateAsyncScope())
        {
            var cats = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
            var cat = await cats.GetByIdAsync(world.CatId);
            cat.TransferOwnership(new CatOwnershipTransferDto(strangerId, "sold", new UserContext(world.AdminStaffId, Guid.NewGuid()), Guid.NewGuid()));
            await cats.UpdateAsync(cat, EventPublishOption.InMemoryPublishOnly);
        }

        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/customers/me/cats/{world.CatId}/visits")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await stranger.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);

        // Staff are not customers: the portal routes refuse them.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Vet.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);
    }

    /// <summary>The portal reads are access-logged with the customer as the actor (NFR-SEC-8).</summary>
    [Fact]
    public async Task ThePortalReadsAreAccessLogged_WithTheCustomerAsActor()
    {
        var (world, visitId) = await FinalisedVisitAsync();
        var before = await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.DischargeSummary), visitId, world.CustomerId);

        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/customers/me/visits/{visitId}")).StatusCode);

        Assert.Equal(before + 1, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.DischargeSummary), visitId, world.CustomerId));
    }

    private async Task<List<NotificationRequestAggregate>> FollowUpRequestsAsync(ClinicalWorld world, Guid visitId)
    {
        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        return await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.TemplateKey == NotificationTemplateCatalogue.VisitFollowUpDue && r.SubjectId == visitId).OrderBy(r => r.RequestedOn).ToListAsync();
    }

    /// <summary>I-G4 -- a reminder 10 days out is held until its lead time and then delivered once (US-CLN-18).</summary>
    [Fact]
    public async Task I_G4_AReminderTenDaysOut_IsHeldUntilItsLeadTime_ThenDeliveredOnce()
    {
        var world = await SeedClinicalWorldAsync(OlderThanEveryOtherTestsRequests);
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var dueOn = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime).AddDays(10);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Reminder", dueOn }, Json)).StatusCode);

        var request = Assert.Single(await FollowUpRequestsAsync(world, visitId));
        Assert.Equal(world.CustomerId, request.RecipientId);
        Assert.True(request.SendAfter > world.Clock.GetUtcNow().AddDays(5), "The reminder is held back to its lead time, not sent now.");

        world.Clock.SetUtcNow(request.SendAfter!.Value.AddMinutes(5));
        await RunNotificationDeliveryUntilAsync(world.Api, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.VisitFollowUpDue && n.Channel == NotificationChannel.InApp));

        await RunNotificationDeliveryPassAsync(world.Api);
        await RunNotificationDeliveryPassAsync(world.Api);
        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var inApp = await db.Set<NotificationAggregate>().AsNoTracking().Where(n =>
            n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.VisitFollowUpDue && n.Channel == NotificationChannel.InApp).ToListAsync();
        var message = Assert.Single(inApp);
        Assert.Contains("Mitu", message.Body);
        Assert.DoesNotContain("otitis", message.Body, StringComparison.OrdinalIgnoreCase); // a follow-up reason is never sent
    }

    /// <summary>I-G5 -- changing the follow-up stands the earlier reminder down; so does the owner booking before it sends.</summary>
    [Fact]
    public async Task I_G5_AChangedFollowUp_OrAnOwnerBooking_StandsTheReminderDown()
    {
        var world = await SeedClinicalWorldAsync(OlderThanEveryOtherTestsRequests);
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var today = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Reminder", dueOn = today.AddDays(10) }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Reminder", dueOn = today.AddDays(12) }, Json)).StatusCode);
        var requests = await FollowUpRequestsAsync(world, visitId);
        Assert.Equal(2, requests.Count);
        var (first, second) = (requests[0], requests[1]);

        world.Clock.SetUtcNow(second.SendAfter!.Value.AddMinutes(5));
        await RunNotificationDeliveryUntilAsync(world.Api, async db => !await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .AnyAsync(r => r.SubjectId == visitId && r.TemplateKey == NotificationTemplateCatalogue.VisitFollowUpDue && r.Status == NotificationRequestStatus.Pending));

        var after = await FollowUpRequestsAsync(world, visitId);
        Assert.Equal(NotificationRequestStatus.Suppressed, after.Single(r => r.Id == first.Id).Status);
        Assert.Equal(NotificationSuppressionReason.SubjectNoLongerApplies, after.Single(r => r.Id == first.Id).SuppressionReason);
        Assert.NotEqual(NotificationRequestStatus.Suppressed, after.Single(r => r.Id == second.Id).Status);

        // The owner books before a reminder is sent: it stands down.
        var other = await SeedClinicalWorldAsync(OlderThanEveryOtherTestsRequests);
        var otherVisit = await OpenVisitAsync(other.Nurse, other.AppointmentId);
        var otherToday = DateOnly.FromDateTime(other.Clock.GetUtcNow().UtcDateTime);
        Assert.Equal(HttpStatusCode.NoContent, (await other.Vet.PutAsJsonAsync($"/api/staff/visits/{otherVisit}/follow-up", new { kind = "Reminder", dueOn = otherToday.AddDays(10) }, Json)).StatusCode);
        var reminder = Assert.Single(await FollowUpRequestsAsync(other, otherVisit));

        var now = other.Clock.GetUtcNow();
        var startsOn = new DateTimeOffset(now.Year, now.Month, now.Day, 4, 0, 0, TimeSpan.Zero).AddDays(9);
        await BookConfirmedAppointmentAsync(other.Customer, other.Context, other.CatId, startsOn);

        other.Clock.SetUtcNow(reminder.SendAfter!.Value.AddMinutes(5));
        await RunNotificationDeliveryUntilAsync(other.Api, async db => !await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .AnyAsync(r => r.SubjectId == otherVisit && r.TemplateKey == NotificationTemplateCatalogue.VisitFollowUpDue && r.Status == NotificationRequestStatus.Pending));

        var final = Assert.Single(await FollowUpRequestsAsync(other, otherVisit));
        Assert.Equal(NotificationRequestStatus.Suppressed, final.Status);
        Assert.Equal(NotificationSuppressionReason.SubjectNoLongerApplies, final.SuppressionReason);
    }

    /// <summary>I-G6 -- a follow-up linked to an appointment booked through HO-APT-001 is accepted; another cat's appointment is refused.</summary>
    [Fact]
    public async Task I_G6_AFollowUpLinkedToABookedAppointment_IsAccepted_AnotherCatsIsRefused()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var now = world.Clock.GetUtcNow();
        var tomorrow = new DateTimeOffset(now.Year, now.Month, now.Day, 4, 0, 0, TimeSpan.Zero).AddDays(7);

        var booked = await BookConfirmedAppointmentAsync(world.Customer, world.Context, world.CatId, tomorrow);
        var dueOn = DateOnly.FromDateTime(tomorrow.UtcDateTime);
        var linked = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Appointment", dueOn, appointmentId = booked }, Json);
        Assert.True(linked.StatusCode == HttpStatusCode.NoContent, await linked.Content.ReadAsStringAsync());

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal(booked, visit.GetProperty("followUp").GetProperty("appointmentId").GetGuid());
        Assert.Empty(await FollowUpRequestsAsync(world, visitId)); // a booked follow-up queues no reminder

        var otherCat = await SeedCatAsync(world.CustomerId, "Other cat");
        var otherBooked = await BookConfirmedAppointmentAsync(world.Customer, world.Context, otherCat, tomorrow.AddHours(2));
        var refused = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Appointment", dueOn, appointmentId = otherBooked }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.FollowUpAppointmentInvalid, await refused.Content.ReadAsStringAsync());

        var past = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Reminder", dueOn = DateOnly.FromDateTime(now.UtcDateTime) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Contains(BusinessViolationCodes.FollowUpDateInPast, await past.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.DeleteAsync($"/api/staff/visits/{visitId}/follow-up")).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("followUp").ValueKind);
    }

    /// <summary>I-C8 (photo and follow-up) -- after finalisation they are refused as well (US-CLN-17 AC1, AC3).</summary>
    [Fact]
    public async Task I_C8_AfterFinalisation_APhotoOrAFollowUpIsRefused()
    {
        var (world, visitId) = await FinalisedVisitAsync();
        var today = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime);

        var followUp = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/follow-up", new { kind = "Reminder", dueOn = today.AddDays(30) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, followUp.StatusCode);
        Assert.Contains(BusinessViolationCodes.VisitFinalised, await followUp.Content.ReadAsStringAsync());

        var clear = await world.Vet.DeleteAsync($"/api/staff/visits/{visitId}/follow-up");
        Assert.Equal(HttpStatusCode.BadRequest, clear.StatusCode);

        using var form = new MultipartFormDataContent("emr-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE]);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(part, "file", "late.png");
        var upload = await world.Vet.PostAsync("/api/staff/files?purpose=ClinicalPhoto", form);
        var fileId = (await ReadAsync(upload)).GetProperty("fileId").GetGuid();

        var photo = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/photos", new { fileId, takenOn = today }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, photo.StatusCode);
        Assert.Contains(BusinessViolationCodes.VisitFinalised, await photo.Content.ReadAsStringAsync());

        // The follow-up set before finalising is frozen with the visit.
        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal("Reminder", visit.GetProperty("followUp").GetProperty("kind").GetString());
        Assert.Equal(0, visit.GetProperty("photos").GetArrayLength());
    }
}
