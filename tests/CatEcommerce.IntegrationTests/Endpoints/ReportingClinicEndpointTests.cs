using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 B -- I-B1 to I-B3: the clinic reports through HO-APT-001's and HO-CAT-001's own endpoints, one fresh branch per test (section 16.1 rule 1). The surgery register (I-B4)
/// is <see cref="ReportingSurgeryRegisterEndpointTests"/>, which needs the Surgery world.
/// </summary>
public sealed class ReportingClinicEndpointTests(ReportingApiFactory factory) : ReportingIntegrationTestBase(factory)
{
    private static readonly TimeSpan Dhaka = TimeSpan.FromHours(6);

    /// <summary>The shared fake clock only moves forward (effective-dated prices forbid back-dating). Put it in the 09:00-11:00 Dhaka window: the scenarios book up to seven hours ahead, so everything falls on one local day, inside the roster and outside quiet hours.</summary>
    private void EnsureDaytime()
    {
        var local = Factory.Clock.GetUtcNow().ToOffset(Dhaka);
        if (local.Hour is >= 9 and < 11) return;

        var target = local.Hour < 9 ? local.Date.AddHours(10) : local.Date.AddDays(1).AddHours(10);
        Factory.Clock.SetUtcNow(new DateTimeOffset(target, Dhaka).ToUniversalTime());
    }

    private static DateTimeOffset QuarterHour(DateTimeOffset value) => new(value.Year, value.Month, value.Day, value.Hour, value.Minute / 15 * 15, 0, value.Offset);

    private async Task<Guid> BookAsync(BillingContext context, ClinicSetup clinic, Guid customerId, Guid catId, DateTimeOffset startsOn)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/appointments", new
        { customerId, catId, serviceId = clinic.ServiceId, startsOn, performerId = clinic.VetStaffAccountId, roomId = clinic.RoomId }, Json));
        return (await ReadAsync(response)).GetProperty("appointmentId").GetGuid();
    }

    private async Task<Guid> WalkInAsync(BillingContext context, ClinicSetup clinic, Guid customerId, Guid catId, string triage = "Routine")
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/walk-ins",
            new { customerId, catId, serviceId = clinic.ServiceId, triagePriority = triage }, Json));
        return (await ReadAsync(response)).GetProperty("appointmentId").GetGuid();
    }

    // ── I-B1: US-RPT-01 AC1 -- the daily schedule ────────────────────────────────────────────────────────
    [Fact]
    public async Task I_B1_TheDailySchedule_ListsBookedWalkInNoShowAndCancelled_NeverAHold_NeverAnotherBranch()
    {
        EnsureDaytime();
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context);
        var other = await CreateBillingContextAsync();
        var otherClinic = await SeedClinicAsync(other);

        var customerId = await SeedCustomerAsync(context);
        async Task<Guid> NewCatAsync() => await SeedCatAsync(customerId);

        var start = QuarterHour(Now.AddHours(2));
        var noShow = await BookAsync(context, clinic, customerId, await NewCatAsync(), start);
        await BookAsync(context, clinic, customerId, await NewCatAsync(), start.AddHours(1));
        await BookAsync(context, clinic, customerId, await NewCatAsync(), start.AddHours(2));
        await BookAsync(context, clinic, customerId, await NewCatAsync(), start.AddHours(3));
        var cancelled = await BookAsync(context, clinic, customerId, await NewCatAsync(), start.AddHours(4));
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/appointments/{cancelled}/cancel", new { reason = "Owner called to cancel" }, Json));
        await WalkInAsync(context, clinic, customerId, await NewCatAsync());

        // An open hold at the same branch (a customer who never confirms) and one booking at another branch: neither may appear.
        var (customerClient, holdCustomerId) = await RegisterAndSignInCustomerAsync();
        var holdCat = await SeedCatAsync(holdCustomerId);
        await ExpectAsync(HttpStatusCode.Created, await customerClient.PostAsJsonAsync("/api/customers/me/appointment-holds",
            new { branchId = context.BranchId, serviceId = clinic.ServiceId, catId = holdCat, startsOn = start.AddHours(5), performerId = clinic.VetStaffAccountId }, Json));
        var otherCustomer = await SeedCustomerAsync(other);
        await BookAsync(other, otherClinic, otherCustomer, await SeedCatAsync(otherCustomer), start);

        // The no-show needs the grace period to have passed.
        Factory.Clock.Advance(TimeSpan.FromHours(2) + TimeSpan.FromMinutes(20));
        await ReSignInAsync(context);
        await ReSignInAsync(other);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsync($"/api/staff/appointments/{noShow}/no-show", null));

        var report = await RunReportAsync(context.Admin, ReportCatalogue.ClinicDailyScheduleKey, context);

        Assert.Equal(6, report.GetProperty("totalCount").GetInt32());
        var statuses = Rows(report).Select(r => r.GetProperty("status").GetString()).ToList();
        Assert.Equal(3, statuses.Count(s => s == "Booked"));
        Assert.Single(statuses, s => s == "NoShow");
        Assert.Single(statuses, s => s == "Cancelled");
        Assert.Single(statuses, s => s == "CheckedIn");
        Assert.DoesNotContain(statuses, s => s is "Held" or "HoldExpired" or "HoldReleased");
        Assert.Single(Rows(report), r => r.GetProperty("walkIn").GetBoolean());
        Assert.Equal("Booked 3 · Checked in 1 · Completed 0 · Cancelled 1 · No-show 1 · Walk-ins 1", report.GetProperty("totals").GetProperty("status").GetString());

        // The other branch sees only its own booking.
        var otherReport = await RunReportAsync(other.Admin, ReportCatalogue.ClinicDailyScheduleKey, other);
        Assert.Equal(1, otherReport.GetProperty("totalCount").GetInt32());
    }

    // ── I-B2: US-RPT-01 AC1 -- queue state ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_B2_TheQueueReport_HasTheSeededWaits_AndWaitingNowIsTodayOnly()
    {
        EnsureDaytime();
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context);
        var customerId = await SeedCustomerAsync(context);

        var first = await WalkInAsync(context, clinic, customerId, await SeedCatAsync(customerId));
        await WalkInAsync(context, clinic, customerId, await SeedCatAsync(customerId), "Urgent");
        var emergency = await WalkInAsync(context, clinic, customerId, await SeedCatAsync(customerId), "Emergency");
        var left = await WalkInAsync(context, clinic, customerId, await SeedCatAsync(customerId));

        Factory.Clock.Advance(TimeSpan.FromMinutes(35));
        await ReSignInAsync(context);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/appointments/{first}/start", new { performerId = clinic.VetStaffAccountId, roomId = clinic.RoomId }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/appointments/{left}/cancel", new { reason = "Owner left without being seen" }, Json));

        var report = await RunReportAsync(context.Admin, ReportCatalogue.ClinicQueuePerformanceKey, context, from: Today.AddDays(-1), to: Today);
        var rows = Rows(report);
        var today = rows.Single(r => r.GetProperty("localDate").GetString() == Today.ToString("yyyy-MM-dd"));
        var yesterday = rows.Single(r => r.GetProperty("localDate").GetString() == Today.AddDays(-1).ToString("yyyy-MM-dd"));

        Assert.Equal(2, today.GetProperty("walkInsRoutine").GetInt64());
        Assert.Equal(1, today.GetProperty("walkInsUrgent").GetInt64());
        Assert.Equal(1, today.GetProperty("walkInsEmergency").GetInt64());
        Assert.Equal(1, today.GetProperty("seen").GetInt64());
        Assert.Equal(35m, decimal.Parse(today.GetProperty("averageWaitMinutes").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(35, today.GetProperty("longestWaitMinutes").GetInt64());
        Assert.Equal(1, today.GetProperty("leftWithoutBeingSeen").GetInt64());
        Assert.Equal(2, today.GetProperty("waitingNow").GetInt64()); // the urgent and the emergency walk-in

        // Yesterday: "waiting now" is not recorded for a past day -- null, never 0.
        Assert.Equal(JsonValueKind.Null, yesterday.GetProperty("waitingNow").ValueKind);
        Assert.Equal(0, yesterday.GetProperty("seen").GetInt64());
        Assert.NotEqual(Guid.Empty, emergency);
    }

    // ── I-B3: US-RPT-01 AC1 -- the vaccination due list ──────────────────────────────────────────────────
    [Fact]
    public async Task I_B3_TheDueList_HasTheDueAndOverdueCats_WithContact_AndNotTheRenewedOrTheDeceased()
    {
        EnsureDaytime();
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context);

        var vaccine = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/vaccines",
            new { code = "RPT" + NewCode(), name = "Reporting vaccine " + NewCode(), isCore = true, validityMonths = 12 }, Json));
        var vaccineId = (await ReadAsync(vaccine)).GetProperty("vaccineId").GetGuid();

        async Task VaccinateAsync(Guid catId, DateOnly given) =>
            await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
            { vaccineId, batchReference = "B-" + NewCode(), batchExpiresOn = Today.AddYears(1), administeredOn = given, administeredByVetId = clinic.VetStaffAccountId }, Json));

        var customerId = await SeedCustomerAsync(context, "Due Owner");
        var due = await SeedCatAsync(customerId);
        var overdue = await SeedCatAsync(customerId);
        var renewed = await SeedCatAsync(customerId);
        var deceased = await SeedCatAsync(customerId);

        await VaccinateAsync(due, Today.AddMonths(-12).AddDays(10));
        await VaccinateAsync(overdue, Today.AddMonths(-12).AddDays(-20));
        await VaccinateAsync(renewed, Today.AddMonths(-24));
        await VaccinateAsync(renewed, Today.AddDays(-5));
        await VaccinateAsync(deceased, Today.AddMonths(-12).AddDays(10));
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/cats/{deceased}/deceased", new { deceasedOn = Today, note = "Reporting test" }, Json));

        // Cats are not branch-scoped: each belongs to the branch of its most recent appointment (ADR-292), so give every cat one here.
        var start = QuarterHour(Now.AddHours(1));
        foreach (var (cat, index) in new[] { due, overdue, renewed }.Select((c, i) => (c, i)))
            await BookAsync(context, clinic, customerId, cat, start.AddHours(index));

        var report = await RunReportAsync(context.Admin, ReportCatalogue.VaccinationDueListKey, context, from: Today, to: Today.AddDays(30));
        var rows = Rows(report);

        Assert.Equal(2, rows.Count);
        var dueRow = rows.Single(r => r.GetProperty("status").GetString() == "DueSoon");
        var overdueRow = rows.Single(r => r.GetProperty("status").GetString() == "Overdue");
        Assert.Equal(Today.AddMonths(-12).AddDays(10).AddMonths(12).ToString("yyyy-MM-dd"), dueRow.GetProperty("nextDueOn").GetString());
        Assert.Equal("Due Owner", dueRow.GetProperty("customerName").GetString());
        Assert.False(string.IsNullOrEmpty(dueRow.GetProperty("mobile").GetString()));
        Assert.True(dueRow.GetProperty("nextAppointmentBooked").GetBoolean());
        Assert.Equal(JsonValueKind.Null, dueRow.GetProperty("lastReminderSentOn").ValueKind);
        Assert.NotEqual(dueRow.GetProperty("catName").GetString(), overdueRow.GetProperty("catName").GetString());

        // Without the overdue cats only the due one is listed.
        var withoutOverdue = await RunReportAsync(context.Admin, ReportCatalogue.VaccinationDueListKey, context, "includeOverdue=false", Today, Today.AddDays(30));
        Assert.Single(Rows(withoutOverdue));

        // HO-NTF-001's sweep sends the reminder; the report then says when.
        await using (var scope = Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.NotificationWorkCycle>().RunReminderSweepAsync();

        await RunDeliveryPassesUntilAsync(async () =>
        {
            var again = Rows(await RunReportAsync(context.Admin, ReportCatalogue.VaccinationDueListKey, context, from: Today, to: Today.AddDays(30)));
            return again.Any(r => r.GetProperty("status").GetString() == "DueSoon" && r.GetProperty("lastReminderSentOn").ValueKind != JsonValueKind.Null);
        });
    }
}

/// <summary>HO-RPT-001 section 16 B, I-B4 -- the surgery register, driven through HO-SRG-001's own endpoints (the Surgery world: theatre, vet, nurse, services).</summary>
public sealed class ReportingSurgeryRegisterEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    [Fact]
    public async Task I_B4_TheRegister_ListsASurgeryWithComplications_AndOneWithAConsentOverride_WithSurgeonOutcomeAndDrugEntryCount()
    {
        var world = await SeedSurgeryWorldAsync();

        // Surgery 1: ends, with a finalised report that records a complication.
        var (first, _) = await InTheatreAsync(world, world.SpayServiceId, hour: 10);
        await Ok204Async(await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{first}/end-procedure", new { outcome = "Completed" }, Json));
        var draft = await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{first}/report", new
        {
            expectedVersion = (int?)null, procedurePerformed = "Ovariohysterectomy", findings = "Normal ovaries", technique = "Midline approach", sutureMaterial = "4-0 PDS",
            hadComplications = true, complications = "Mild bleeding, controlled", surgeonId = world.VetId, assistantId = world.AssistantId
        }, Json);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);

        // Surgery 2: no consent on record -- a vet starts it with an override and a reason.
        var cat2 = await SeedCatOfAsync(world.CustomerId, CatEcommerce.Shared.Common.Enums.CatSex.Male);
        var (second, _) = await ReadyToStartAsync(world, world.NeuterServiceId, cat2, consent: false, hour: 12);
        await Ok204Async(await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{second}/start-with-consent-override", new { reason = "An emergency, the owner cannot be reached" }, Json));

        var date = LocalToday(world);
        var response = await world.Vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}?from={date.AddDays(-1):yyyy-MM-dd}&to={date.AddDays(1):yyyy-MM-dd}&branchId={world.BranchId}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var report = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var rows = report.GetProperty("rows").EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);
        var withComplications = rows.Single(r => r.GetProperty("hadComplications").GetBoolean());
        Assert.Equal("Mild bleeding, controlled", withComplications.GetProperty("complicationsSummary").GetString());
        Assert.Equal("Spay", withComplications.GetProperty("procedureCategory").GetString());
        Assert.Equal("Completed", withComplications.GetProperty("outcome").GetString());
        Assert.False(string.IsNullOrWhiteSpace(withComplications.GetProperty("surgeonName").GetString()));

        var overridden = rows.Single(r => r.GetProperty("consentOverride").GetBoolean());
        Assert.Equal("Neuter", overridden.GetProperty("procedureCategory").GetString());

        // A clinical report is Clinical-only: reception cannot open it.
        var reception = await world.Reception.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}?from={date:yyyy-MM-dd}&to={date:yyyy-MM-dd}&branchId={world.BranchId}");
        Assert.Equal(HttpStatusCode.Forbidden, reception.StatusCode);
    }
}
