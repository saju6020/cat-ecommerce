using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 phase F -- repeats, renewal, the portal and the refill sweep (US-PHM-06, US-PHM-07, US-NTF-03 AC1; ADR-142, ADR-148, ADR-150): I-F1 ... I-F10.</summary>
public sealed class PharmacyRepeatEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    private sealed record Seeded(PharmacyWorld World, Guid MedicineId, Guid SkuId, Guid PrescriptionId, string PrescriptionNumber);

    /// <summary>A world with stocked, priced medicine and one issued prescription. A repeatable one is valid for <paramref name="validDays"/> days.</summary>
    private async Task<Seeded> SeedAsync(int repeats = 2, int validDays = 90, int durationDays = 5, decimal stock = 100m, PharmacyWorld? existing = null)
    {
        var world = existing ?? await SeedPharmacyWorldAsync();
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test repeat " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["meloxicam"], classes: ["nsaid"],
            dosingBasis: "Fixed", form: "OralLiquid", dispensingUnit: "Millilitre"));
        if (existing is null) await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, stock, BranchToday.AddDays(400), NewLotNumber());

        var (id, number) = await IssueAsync(world, medicineId, repeats, validDays, durationDays);
        return new Seeded(world, medicineId, sku.SkuId, id, number);
    }

    private static async Task<(Guid Id, string Number)> IssueAsync(PharmacyWorld world, Guid medicineId, int repeats, int validDays, int durationDays)
    {
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions",
            PharmacyPrescribingEndpointTests.IssueBody(
                world.VisitId, medicineId, dose: 2m, doseUnit: "Ml", quantity: 10m, durationDays: durationDays, repeatable: repeats > 0, repeats: repeats,
                validUntil: repeats > 0 ? BranchToday.AddDays(validDays) : null), Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var body = await ReadAsync(response);
        return (body.GetProperty("prescriptionId").GetGuid(), body.GetProperty("prescriptionNumber").GetString()!);
    }

    private static async Task<HttpResponseMessage> DispenseFromStockAsync(PharmacyWorld world, Seeded seeded, Guid? repeatRequestId = null)
    {
        var plan = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispense-plan"));
        var lines = plan.GetProperty("suggestedLines").EnumerateArray()
            .Select(l => (object)new { skuId = l.GetProperty("skuId").GetGuid(), batchId = l.GetProperty("batchId").GetGuid(), quantity = decimal.Parse(l.GetProperty("quantity").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture) })
            .ToArray();
        return await world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/dispensings",
            new { lines, fefoOverrideReason = (string?)null, repeatRequestId, witnessIdentifier = (string?)null, witnessPassword = (string?)null }, Json);
    }

    private static async Task<HttpResponseMessage> RequestRepeatAsync(HttpClient customer, Guid prescriptionId, Guid branchId, string? note = null) =>
        await customer.PostAsJsonAsync($"/api/customers/me/prescriptions/{prescriptionId}/repeat-requests", new { preferredBranchId = branchId, note }, Json);

    internal sealed record NotificationRequestRow(string TemplateKey, int Status, int? SuppressionReason, string Variables, string? DeduplicationKey, DateOnly? SubjectDueOn);

    internal static async Task<List<NotificationRequestRow>> RequestsAsync(string templateKey, Guid recipientId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "SELECT template_key, status, suppression_reason, variables::text, deduplication_key, subject_due_on FROM notify.notification_request WHERE template_key = @t AND recipient_id = @r ORDER BY requested_on",
            connection);
        command.Parameters.AddWithValue("t", templateKey);
        command.Parameters.AddWithValue("r", recipientId);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<NotificationRequestRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new NotificationRequestRow(
                reader.GetString(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetInt32(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : DateOnly.FromDateTime(reader.GetDateTime(5))));
        }

        return rows;
    }

    /// <summary>
    /// Time passing for a prescription WITHOUT moving the host clock: staff and customer sessions are validated against the same fake clock, so advancing it by days signs everyone
    /// out. Shifting the prescription's own dates back by <paramref name="days"/> is the same situation as seen by every rule that compares them with today.
    /// </summary>
    private static async Task AgeAsync(Guid prescriptionId, int days)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE pharmacy.prescription SET valid_until = valid_until - @d, starts_on = starts_on - @d, last_dispensed_on = last_dispensed_on - @d WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("d", days);
        command.Parameters.AddWithValue("id", prescriptionId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task RunRefillSweepAsync(PharmacyWorld world)
    {
        await using var scope = world.Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PharmacyWorkCycle>().RunRefillSweepAsync();
    }

    // ── I-F1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F1_TheOwnerRequestsARepeatInsideTheWindow_StaffDispenseItWithNoVisit_AndTheRequestIsDispensedAndTheOwnerIsTold()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        var requested = await RequestRepeatAsync(world.Customer, seeded.PrescriptionId, world.BranchId, "Running low");
        Assert.Equal(HttpStatusCode.Created, requested.StatusCode);
        var requestId = (await ReadAsync(requested)).GetProperty("repeatRequestId").GetGuid();

        // The staff view shows the open request; the repeat is dispensed with no visit at all (the visit was never an input).
        var before = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"));
        Assert.Equal("Requested", before.GetProperty("repeatRequests").EnumerateArray().Single().GetProperty("status").GetString());

        var dispensed = await DispenseFromStockAsync(world, seeded, requestId);
        Assert.True(dispensed.StatusCode == HttpStatusCode.Created, await BodyAsync(dispensed));

        var after = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"));
        var request = after.GetProperty("repeatRequests").EnumerateArray().Single();
        Assert.Equal("Dispensed", request.GetProperty("status").GetString());
        Assert.Equal(2, after.GetProperty("dispensings").GetArrayLength());

        var ready = await RequestsAsync(NotificationTemplateCatalogue.PharmacyRepeatReady, world.CustomerId);
        Assert.Single(ready);
        Assert.Contains("prescriptions", ready[0].Variables);

        // The owner can see it in the inbox once the notification worker has run.
        await RunNotificationDeliveryUntilAsync(world.Api, async db => (await RequestsAsync(NotificationTemplateCatalogue.PharmacyRepeatReady, world.CustomerId)).All(r => r.Status != NotificationRequestStatus.Pending.Value));
        Assert.Contains(await InboxTitlesAsync(world.Customer), t => t.Contains("repeat", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<List<string>> InboxTitlesAsync(HttpClient customer)
    {
        var inbox = await ReadAsync(await customer.GetAsync("/api/customers/me/inbox"));
        return inbox.GetProperty("items").EnumerateArray().Select(i => (i.GetProperty("title").GetString() ?? "") + " " + (i.GetProperty("body").GetString() ?? "")).ToList();
    }

    // ── I-F2 / I-F3 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F2_RequestingAfterValidUntil_IsRefused_WithTheDate_TheNewAuthorisationText_AndTheBookLink()
    {
        var seeded = await SeedAsync(validDays: 10);
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        await AgeAsync(seeded.PrescriptionId, 11);
        var refused = await RequestRepeatAsync(world.Customer, seeded.PrescriptionId, world.BranchId);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var text = await BodyAsync(refused);
        Assert.Contains(BusinessViolationCodes.PrescriptionValidityEnded, text);
        Assert.Contains(BranchToday.AddDays(-1).ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), text);
        Assert.Contains("vet", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bookLink", text);
    }

    [Fact]
    public async Task I_F3_AStaffMemberDispensingAfterValidUntilBeforeTheSweepRan_IsRefused()
    {
        var seeded = await SeedAsync(validDays: 10);
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        await AgeAsync(seeded.PrescriptionId, 11);

        // Nothing has expired it yet: the stored status is still Dispensed, and the rule still holds.
        await AssertErrorAsync(await DispenseFromStockAsync(world, seeded), BusinessViolationCodes.PrescriptionValidityEnded);
    }

    // ── I-F4 / I-F5 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F4_AVetRenewsAnExpiredPrescription_ItGetsANewNumber_TheOldOneIsLinked_AndTheOwnerCanRepeatTheNewOne()
    {
        var seeded = await SeedAsync(validDays: 10);
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);
        await AgeAsync(seeded.PrescriptionId, 11);

        var renewed = await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/renew", new
        {
            visitId = (Guid?)null, dose = new { amount = 2m, unit = "Ml" }, route = "Oral", frequency = "OnceDaily", durationDays = 5, quantity = 10m, instructions = "Give with food.",
            isRepeatable = true, repeatsAuthorised = 2, validUntil = BranchToday.AddDays(100)
        }, Json);

        Assert.True(renewed.StatusCode == HttpStatusCode.Created, await BodyAsync(renewed));
        var body = await ReadAsync(renewed);
        var newId = body.GetProperty("prescriptionId").GetGuid();
        Assert.NotEqual(seeded.PrescriptionNumber, body.GetProperty("prescriptionNumber").GetString());

        var old = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"));
        Assert.Equal(newId, old.GetProperty("renewedBy").GetProperty("id").GetGuid());
        var fresh = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{newId}"));
        Assert.True(fresh.GetProperty("isRemoteRenewal").GetBoolean());
        Assert.Equal(seeded.PrescriptionId, fresh.GetProperty("renewedFrom").GetProperty("id").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, fresh.GetProperty("safetyRecord").ValueKind); // the full assessment ran again

        // A second renewal of the same prescription is refused.
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/renew", new
        {
            visitId = (Guid?)null, dose = new { amount = 2m, unit = "Ml" }, route = "Oral", frequency = "OnceDaily", durationDays = 5, quantity = 10m, instructions = "Give with food.",
            isRepeatable = true, repeatsAuthorised = 2, validUntil = BranchToday.AddDays(100)
        }, Json), BusinessViolationCodes.PrescriptionAlreadyRenewed);

        // The owner repeats the NEW one once its first supply is out.
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded with { PrescriptionId = newId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RequestRepeatAsync(world.Customer, newId, world.BranchId)).StatusCode);
    }

    [Fact]
    public async Task I_F4b_ANurseCannotRenew()
    {
        var seeded = await SeedAsync();
        var response = await seeded.World.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/renew", new
        {
            visitId = (Guid?)null, dose = new { amount = 2m, unit = "Ml" }, route = "Oral", frequency = "OnceDaily", durationDays = 5, quantity = 10m, instructions = "x",
            isRepeatable = false, repeatsAuthorised = 0
        }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task I_F5_AVetDeclinesARequest_TheOwnerIsToldWithTheReasonText()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);
        var requestId = (await ReadAsync(await RequestRepeatAsync(world.Customer, seeded.PrescriptionId, world.BranchId))).GetProperty("repeatRequestId").GetGuid();

        const string reason = "Please bring Mitu in for a weight check first";
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/repeat-requests/{requestId}/decline", new { reason }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}/repeat-requests/{requestId}/decline", new { reason }, Json)).StatusCode);

        var declined = await RequestsAsync(NotificationTemplateCatalogue.PharmacyRepeatDeclined, world.CustomerId);
        var only = Assert.Single(declined);
        Assert.Contains(reason, only.Variables);

        // Declined means the owner may ask again.
        Assert.Equal(HttpStatusCode.Created, (await RequestRepeatAsync(world.Customer, seeded.PrescriptionId, world.BranchId)).StatusCode);
    }

    // ── I-F6 / I-F7 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F6_ThePortalListsActivePrescriptions_WithMedicineDoseFrequencyRemainingAndVet_CompletedButRunningIncluded_CancelledExcluded()
    {
        var world = await SeedPharmacyWorldAsync();
        var running = await SeedAsync(repeats: 0, existing: world);
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, running)).StatusCode); // the only supply: Completed, but the course is still running
        var cancelled = await SeedAsync(repeats: 0, existing: world);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{cancelled.PrescriptionId}/cancel", new { reason = "Wrong product" }, Json)).StatusCode);

        var active = await ReadAsync(await world.Customer.GetAsync("/api/customers/me/prescriptions?scope=active"));
        var rows = active.EnumerateArray().ToList();
        var mine = Assert.Single(rows, r => r.GetProperty("id").GetGuid() == running.PrescriptionId);
        Assert.DoesNotContain(rows, r => r.GetProperty("id").GetGuid() == cancelled.PrescriptionId);
        Assert.Equal(world.Clinic.CatId, mine.GetProperty("catId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty("medicine").GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty("frequency").GetString()));
        Assert.True(mine.GetProperty("remainingDays").GetInt32() > 0);
        Assert.False(string.IsNullOrWhiteSpace(mine.GetProperty("issuingVetName").GetString()));
        Assert.True(mine.GetProperty("isActive").GetBoolean());

        var past = await ReadAsync(await world.Customer.GetAsync("/api/customers/me/prescriptions?scope=past"));
        Assert.Contains(past.EnumerateArray(), r => r.GetProperty("id").GetGuid() == cancelled.PrescriptionId);

        // No batch number or safety record ever reaches the owner.
        var text = await BodyAsync(await world.Customer.GetAsync($"/api/customers/me/prescriptions/{running.PrescriptionId}"));
        Assert.DoesNotContain("lot", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("safetyRecord", text, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.BadRequest, (await world.Customer.GetAsync("/api/customers/me/prescriptions?scope=everything")).StatusCode);
    }

    [Fact]
    public async Task I_F7_AnotherCustomersPrescription_IsNotFound_OnDetailAndOnRepeatRequest()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        var (stranger, _, _) = await RegisterAndSignInCustomerOnAsync(world.Api);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/prescriptions/{seeded.PrescriptionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RequestRepeatAsync(stranger, seeded.PrescriptionId, world.BranchId)).StatusCode);
        // Withdrawing is idempotent and reveals nothing: someone else's prescription or request is simply "nothing to withdraw".
        Assert.Equal(HttpStatusCode.NoContent, (await stranger.DeleteAsync($"/api/customers/me/prescriptions/{seeded.PrescriptionId}/repeat-requests/{Guid.NewGuid()}")).StatusCode);

        var theirs = await ReadAsync(await stranger.GetAsync("/api/customers/me/prescriptions"));
        Assert.Empty(theirs.EnumerateArray());
    }

    // ── I-F8 / I-F9 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F8_TheRefillSweep_EnqueuesOneReminderEvenWhenRunTwice_WithTheRepeatLink()
    {
        var seeded = await SeedAsync(durationDays: 30, validDays: 120);
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        await AgeAsync(seeded.PrescriptionId, 27);
        await RunRefillSweepAsync(world);
        await RunRefillSweepAsync(world);

        var reminders = await RequestsAsync(NotificationTemplateCatalogue.PharmacyRefillDue, world.CustomerId);
        var only = Assert.Single(reminders);
        Assert.Contains("repeatLink", only.Variables);
        Assert.Contains(seeded.PrescriptionId.ToString("N"), only.DeduplicationKey!);
        Assert.Equal(BranchToday.AddDays(3), only.SubjectDueOn);
    }

    [Fact]
    public async Task I_F9_ARepeatDispensedBeforeTheReminderIsDelivered_CancelsTheReminder()
    {
        var seeded = await SeedAsync(durationDays: 30, validDays: 120);
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        await AgeAsync(seeded.PrescriptionId, 27);
        await RunRefillSweepAsync(world);
        Assert.Single(await RequestsAsync(NotificationTemplateCatalogue.PharmacyRefillDue, world.CustomerId));

        // The owner collects the repeat before the reminder went out.
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        await RunNotificationDeliveryUntilAsync(world.Api, _ => Task.FromResult(
            RequestsAsync(NotificationTemplateCatalogue.PharmacyRefillDue, world.CustomerId).GetAwaiter().GetResult().All(r => r.Status != NotificationRequestStatus.Pending.Value)));

        var request = Assert.Single(await RequestsAsync(NotificationTemplateCatalogue.PharmacyRefillDue, world.CustomerId));
        Assert.Equal(NotificationRequestStatus.Suppressed.Value, request.Status);
        Assert.Equal(NotificationSuppressionReason.SubjectNoLongerApplies.Value, request.SuppressionReason);
    }

    // ── I-F10 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F10_TheWorkerExpiryPass_RecordsThePastValidityPrescriptionAsExpired_AndTheOwnerCanNoLongerRequest()
    {
        var seeded = await SeedAsync(validDays: 10);
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await DispenseFromStockAsync(world, seeded)).StatusCode);

        await AgeAsync(seeded.PrescriptionId, 11);
        await RunPharmacyCycleAsync(world.Api);

        var detail = await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"));
        Assert.Equal("Expired", detail.GetProperty("status").GetString());

        var portal = await ReadAsync(await world.Customer.GetAsync($"/api/customers/me/prescriptions/{seeded.PrescriptionId}"));
        Assert.False(portal.GetProperty("repeat").GetProperty("canRequest").GetBoolean());

        // Running the pass again changes nothing (idempotent).
        await RunPharmacyCycleAsync(world.Api);
        Assert.Equal("Expired", (await ReadAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{seeded.PrescriptionId}"))).GetProperty("status").GetString());
    }
}
