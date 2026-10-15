using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 phase C -- prescribing (US-PHM-01, 02, 03; US-CLN-22; AC-18): I-C1 ... I-C11.</summary>
public sealed class PharmacyPrescribingEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    /// <summary>A prescription body. Meloxicam-like defaults: 0.2 ml of 1.5 mg/ml for 5 days, once daily.</summary>
    internal static Dictionary<string, object?> IssueBody(
        Guid visitId, Guid medicineId, decimal dose = 0.2m, string doseUnit = "Ml", string? fingerprint = null, string? allergyReason = null, string? doseNote = null,
        int durationDays = 5, decimal quantity = 10m, bool repeatable = false, int repeats = 0, DateOnly? validUntil = null, string route = "Oral") => new()
    {
        ["visitId"] = visitId, ["medicineId"] = medicineId, ["dose"] = new { amount = dose, unit = doseUnit }, ["route"] = route, ["frequency"] = "OnceDaily",
        ["durationDays"] = durationDays, ["quantity"] = quantity, ["instructions"] = "Give with food.", ["isRepeatable"] = repeatable, ["repeatsAuthorised"] = repeats,
        ["validUntil"] = validUntil, ["acknowledgedWarningsFingerprint"] = fingerprint, ["allergyAcknowledgementReason"] = allergyReason, ["doseAcknowledgementNote"] = doseNote
    };

    private static object CheckBody(Guid medicineId, decimal dose = 0.2m, string unit = "Ml") =>
        new { medicineId, dose = new { amount = dose, unit }, route = "Oral", frequency = "OnceDaily" };

    private async Task<(PharmacyWorld World, Guid MedicineId)> ReadyAsync(decimal weightKg = 4.0m, string[]? ingredients = null, string[]? classes = null, object[]? interactions = null)
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false);
        await RecordWeightAsync(world.Vet, world.CatId, weightKg);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(ingredients: ingredients, classes: classes, interactions: interactions));
        return (world, medicineId);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    // ── I-C1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C1_AVetIssuesWithinRange_AgainstAnOpenVisit_AndItIsLinkedToTheCatAndTheVisit()
    {
        var (world, medicineId) = await ReadyAsync();

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(world.VisitId, medicineId), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await JsonAsync(response);
        var prescriptionId = created.GetProperty("prescriptionId").GetGuid();
        Assert.Matches(new Regex(@"^RX-\d{4}-\d{6}$"), created.GetProperty("prescriptionNumber").GetString()!);

        var visitList = await JsonAsync(await world.Vet.GetAsync($"/api/staff/visits/{world.VisitId}/prescriptions"));
        var listed = Assert.Single(visitList.GetProperty("prescriptions").EnumerateArray());
        Assert.Equal(prescriptionId, listed.GetProperty("id").GetGuid());
        Assert.Equal("Issued", listed.GetProperty("status").GetString());
        Assert.True(listed.GetProperty("isActive").GetBoolean());
        Assert.Equal("0.20", listed.GetProperty("dose").GetProperty("amount").GetString());
        Assert.Equal("Ml", listed.GetProperty("dose").GetProperty("unit").GetString());
        Assert.Equal("Millilitre", listed.GetProperty("quantity").GetProperty("unit").GetString());

        var detail = await JsonAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{prescriptionId}"));
        Assert.Equal(world.CatId, detail.GetProperty("catId").GetGuid());
        Assert.Equal(world.VisitId, detail.GetProperty("visitId").GetGuid());
        Assert.Equal("Give with food.", detail.GetProperty("instructions").GetString());
        Assert.Equal("WithinRange", detail.GetProperty("safetyRecord").GetProperty("doseCheckOutcome").GetString());
        Assert.Equal("4.00", detail.GetProperty("safetyRecord").GetProperty("weightUsed").GetProperty("amount").GetString());
        Assert.Contains("Cancel", detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));

        var catList = await JsonAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/prescriptions?scope=active"));
        Assert.Equal(1, catList.GetProperty("totalCount").GetInt32());
    }

    // ── I-C2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C2_ReceptionNurseAndAdministrator_CannotPrescribe_AndEachDenialIsAudited()
    {
        var (world, medicineId) = await ReadyAsync();
        var body = IssueBody(world.VisitId, medicineId);

        foreach (var (client, actor) in new[]
                 {
                     (world.Reception, world.Clinic.ReceptionStaffId), (world.Nurse, world.Clinic.NurseStaffId), (world.Admin, world.Clinic.AdminStaffId)
                 })
        {
            var response = await client.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", body, Json);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(1, await CountDenialsAsync(actor, Actions.Prescriptions.Issue));
        }

        // The safety check is Vet-only too, and cancelling is part of prescribing.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions/safety-check", CheckBody(medicineId), Json)).StatusCode);

        var list = await JsonAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/prescriptions?scope=all"));
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    // ── I-C3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C3_AFinalisedVisit_AndAnotherCatsVisit_AreRefused()
    {
        var (world, medicineId) = await ReadyAsync();

        var otherCatId = await SeedCatAsync(world.CustomerId, "Other");
        var otherAppointment = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, otherCatId, world.Clinic.ServiceId);
        var otherVisit = await OpenVisitAsync(world.Nurse, otherAppointment);

        var mismatch = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(otherVisit, medicineId), Json);
        await AssertErrorAsync(mismatch, "VisitCatMismatch");

        await FinaliseVisitAsync(world.Clinic, world.VisitId);
        var finalised = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(world.VisitId, medicineId), Json);
        await AssertErrorAsync(finalised, "VisitNotOpen");

        await AssertErrorAsync(
            await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(Guid.NewGuid(), medicineId), Json), "VisitNotFound");
    }

    // ── I-C4 / I-C5 / I-C6 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C4_TheSafetyCheck_WarnsOfADoseAboveRange_ReturnsTheRangeInMgPerKgAndMg_AndWritesNothing()
    {
        var (world, medicineId) = await ReadyAsync();

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions/safety-check", CheckBody(medicineId, dose: 0.4m), Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assessment = await JsonAsync(response);
        Assert.Equal("Satisfied", assessment.GetProperty("weight").GetProperty("requirement").GetString());
        var check = assessment.GetProperty("doseCheck");
        Assert.Equal("AboveRange", check.GetProperty("outcome").GetString());
        Assert.Equal("0.60", check.GetProperty("doseMg").GetProperty("amount").GetString());
        Assert.Equal("0.15", check.GetProperty("doseMgPerKg").GetString());
        var range = check.GetProperty("expectedRange");
        Assert.Equal("0.05", range.GetProperty("minMgPerKg").GetString());
        Assert.Equal("0.10", range.GetProperty("maxMgPerKg").GetString());
        Assert.Equal("0.20", range.GetProperty("minMg").GetProperty("amount").GetString());
        Assert.Equal("0.40", range.GetProperty("maxMg").GetProperty("amount").GetString());

        var warning = Assert.Single(assessment.GetProperty("warnings").EnumerateArray(), w => w.GetProperty("kind").GetString() == "DoseAboveRange");
        Assert.True(warning.GetProperty("requiresAcknowledgement").GetBoolean());
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), assessment.GetProperty("fingerprint").GetString()!);
        Assert.Contains("dehydrated", assessment.GetProperty("contraindications").GetString());

        // A query writes nothing.
        var list = await JsonAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/prescriptions?scope=all"));
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task I_C5_IssuingAnOutOfRangeDose_NeedsTheFingerprint_ThenStoresWhatTheVetSawAndAcknowledged()
    {
        var (world, medicineId) = await ReadyAsync();
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        var refused = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, dose: 0.4m), Json);
        await AssertErrorAsync(refused, "PrescriptionWarningsNotAcknowledged");
        var refusal = await JsonAsync(refused);
        var fingerprint = refusal.GetProperty("assessment").GetProperty("fingerprint").GetString();
        Assert.Equal("AboveRange", refusal.GetProperty("assessment").GetProperty("doseCheck").GetProperty("outcome").GetString());

        var accepted = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, dose: 0.4m, fingerprint: fingerprint, doseNote: "Severe pain, reviewed"), Json);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        var id = (await JsonAsync(accepted)).GetProperty("prescriptionId").GetGuid();

        var detail = await JsonAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{id}"));
        var safety = detail.GetProperty("safetyRecord");
        Assert.Equal("AboveRange", safety.GetProperty("doseCheckOutcome").GetString());
        Assert.Equal("0.15", safety.GetProperty("doseMgPerKg").GetString());
        Assert.Equal("4.00", safety.GetProperty("weightUsed").GetProperty("amount").GetString());
        Assert.Equal("Severe pain, reviewed", safety.GetProperty("doseAcknowledgementNote").GetString());
        Assert.False(string.IsNullOrEmpty(safety.GetProperty("acknowledgedByName").GetString()));
        Assert.True(detail.GetProperty("hasAcknowledgedWarnings").GetBoolean());
        Assert.Contains(detail.GetProperty("warnings").EnumerateArray(), w => w.GetProperty("kind").GetString() == "DoseAboveRange");
    }

    [Fact]
    public async Task I_C6_AWeightRecordedBetweenTheCheckAndTheIssue_MakesTheOldFingerprint_409WithTheNewAssessment()
    {
        var (world, medicineId) = await ReadyAsync();
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        var check = await JsonAsync(await world.Vet.PostAsJsonAsync($"{url}/safety-check", CheckBody(medicineId, dose: 0.4m), Json));
        var oldFingerprint = check.GetProperty("fingerprint").GetString();

        await RecordWeightAsync(world.Vet, world.CatId, 5.0m);

        var response = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, dose: 0.4m, fingerprint: oldFingerprint), Json);

        await AssertErrorAsync(response, "PrescriptionWarningsChanged", HttpStatusCode.Conflict);
        var fresh = (await JsonAsync(response)).GetProperty("assessment");
        Assert.Equal("5.00", fresh.GetProperty("weight").GetProperty("latest").GetProperty("weight").GetProperty("amount").GetString());
        Assert.NotEqual(oldFingerprint, fresh.GetProperty("fingerprint").GetString());
    }

    // ── I-C7 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C7_NoWeightInThirtyDays_RefusesTheIssue_NamingTheLatestWeight_UntilOneIsRecorded()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody());
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        // No weight at all.
        var none = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId), Json);
        await AssertErrorAsync(none, "RecentWeightRequired");
        Assert.Equal("Missing", (await JsonAsync(none)).GetProperty("assessment").GetProperty("weight").GetProperty("requirement").GetString());

        // A weight 40 days old is stale, and the message names it and its date.
        var old = BranchToday.AddDays(-40);
        await RecordWeightAsync(world.Vet, world.CatId, 4.1m, old);
        var stale = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId), Json);
        await AssertErrorAsync(stale, "RecentWeightRequired");
        var text = await BodyAsync(stale);
        Assert.Contains("4.10 kg", text);
        Assert.Contains(old.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), text);
        Assert.Contains("40 days ago", text);

        // Recording a weight is one call; then the issue succeeds.
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId), Json)).StatusCode);
    }

    [Fact]
    public async Task I_C7b_AFixedDoseMedicine_NeedsNoWeight()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(dosingBasis: "Fixed", form: "Tablet", dispensingUnit: "Tablet",
            strength: new { amount = 50m, unit = "Mg", perQuantity = 1m, perUnit = "Tablet" }));

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(world.VisitId, medicineId, dose: 1m, doseUnit: "Tablet", quantity: 14m), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── I-C8 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C8_AnAllergyConflict_IsBlocking_AndNeedsTheFingerprintAndAReason()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false);
        await SetAllergiesAsync(world.Vet, world.CatId, "Penicillin (hives)");
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Amoxicillin " + Guid.NewGuid().ToString("N")[..6], ingredients: ["amoxicillin"], classes: ["penicillin", "antibiotic"], dosingBasis: "Fixed", form: "Tablet",
            dispensingUnit: "Tablet", strength: new { amount = 50m, unit = "Mg", perQuantity = 1m, perUnit = "Tablet" }));
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        var check = await JsonAsync(await world.Vet.PostAsJsonAsync($"{url}/safety-check", CheckBody(medicineId, dose: 1m, unit: "Tablet"), Json));
        var conflict = Assert.Single(check.GetProperty("warnings").EnumerateArray(), w => w.GetProperty("kind").GetString() == "AllergyConflict");
        Assert.True(conflict.GetProperty("blocking").GetBoolean());
        Assert.True(conflict.GetProperty("requiresAcknowledgement").GetBoolean());
        var fingerprint = check.GetProperty("fingerprint").GetString();

        // Not acknowledged at all, then acknowledged without a reason, then with one.
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, dose: 1m, doseUnit: "Tablet"), Json), "PrescriptionWarningsNotAcknowledged");
        await AssertErrorAsync(
            await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, dose: 1m, doseUnit: "Tablet", fingerprint: fingerprint), Json), "AllergyAcknowledgementReasonRequired");

        var accepted = await world.Vet.PostAsJsonAsync(url,
            IssueBody(world.VisitId, medicineId, dose: 1m, doseUnit: "Tablet", fingerprint: fingerprint, allergyReason: "Owner reports a different drug caused the hives"), Json);
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);

        var detail = await JsonAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{(await JsonAsync(accepted)).GetProperty("prescriptionId").GetGuid()}"));
        Assert.Contains(detail.GetProperty("warnings").EnumerateArray(), w => w.GetProperty("kind").GetString() == "AllergyConflict" && w.GetProperty("blocking").GetBoolean());
        Assert.Equal("Owner reports a different drug caused the hives", detail.GetProperty("safetyRecord").GetProperty("allergyAcknowledgementReason").GetString());
    }

    // ── I-C9 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C9_AnActiveMedicine_ListsAnInteractionWithTheNewOne_WithItsSeverityAndNote()
    {
        var (world, meloxicamId) = await ReadyAsync(
            interactions: [new { interactsWithMedicineId = (Guid?)null, interactsWithTerm = "corticosteroid", severity = "Major", note = "NSAID plus steroid risks ulceration." }]);
        var prednisolone = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Prednisolone " + Guid.NewGuid().ToString("N")[..6], ingredients: ["prednisolone"], classes: ["corticosteroid"], dosingBasis: "Fixed", form: "Tablet",
            dispensingUnit: "Tablet", strength: new { amount = 5m, unit = "Mg", perQuantity = 1m, perUnit = "Tablet" }));

        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(world.VisitId, meloxicamId), Json)).StatusCode);

        var check = await JsonAsync(await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions/safety-check", CheckBody(prednisolone, dose: 1m, unit: "Tablet"), Json));

        var interaction = Assert.Single(check.GetProperty("warnings").EnumerateArray(), w => w.GetProperty("kind").GetString() == "Interaction");
        Assert.Equal("Major", interaction.GetProperty("severity").GetString());
        Assert.True(interaction.GetProperty("requiresAcknowledgement").GetBoolean());
        Assert.Contains("ulceration", interaction.GetProperty("detail").GetString());
    }

    // ── I-C10 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C10_AcknowledgedWarnings_WriteABusinessEventAuditRow_JoinedToTheDataChangeByCorrelationId()
    {
        var (world, medicineId) = await ReadyAsync();
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";
        var fingerprint = (await JsonAsync(await world.Vet.PostAsJsonAsync($"{url}/safety-check", CheckBody(medicineId, dose: 0.4m), Json))).GetProperty("fingerprint").GetString();

        var created = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, dose: 0.4m, fingerprint: fingerprint), Json);
        var id = (await JsonAsync(created)).GetProperty("prescriptionId").GetGuid();

        var rows = await AuditRowsAsync("PrescriptionAggregate", id);
        var business = Assert.Single(rows, r => r.Action == "PrescriptionWarningsAcknowledged");
        Assert.Equal(AuditCategory.BusinessEvent, business.Category);
        Assert.NotNull(business.CorrelationId);
        Assert.Contains(rows, r => r.Category == AuditCategory.DataChange && r.CorrelationId == business.CorrelationId);
        Assert.DoesNotContain("Give with food", business.AfterValue ?? "");

        // A prescription issued with nothing to acknowledge writes none.
        var another = await CreateMedicineAsync(world.Admin, MedicineBody(ingredients: ["gabapentin"], classes: ["anticonvulsant"]));
        var quiet = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, another), Json);
        var quietId = (await JsonAsync(quiet)).GetProperty("prescriptionId").GetGuid();
        Assert.DoesNotContain(await AuditRowsAsync("PrescriptionAggregate", quietId), r => r.Action == "PrescriptionWarningsAcknowledged");
    }

    // ── I-C11 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_C11_ADeceasedCat_CannotBePrescribedFor()
    {
        var (world, medicineId) = await ReadyAsync();
        await MarkCatDeceasedAsync(world.Admin, world.CatId);

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(world.VisitId, medicineId), Json);

        await AssertErrorAsync(response, "CatNotTreatable");
    }

    // ── cancel, inactive medicine, validity and instants ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ACancelledPrescription_NeedsAReason_LeavesTheActiveList_AndOnlyAVetCanCancel()
    {
        var (world, medicineId) = await ReadyAsync();
        var created = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", IssueBody(world.VisitId, medicineId), Json);
        var id = (await JsonAsync(created)).GetProperty("prescriptionId").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{id}/cancel", new { reason = "" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{id}/cancel", new { reason = "Wrong cat" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{id}/cancel", new { reason = "Wrong cat" }, Json)).StatusCode);

        var detail = await JsonAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{id}"));
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
        Assert.False(detail.GetProperty("isActive").GetBoolean());
        Assert.Empty(detail.GetProperty("allowedActions").EnumerateArray());

        var active = await JsonAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/prescriptions?scope=active"));
        Assert.Equal(0, active.GetProperty("totalCount").GetInt32());
        var all = await JsonAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/prescriptions?scope=all"));
        Assert.Equal(1, all.GetProperty("totalCount").GetInt32());

        // A second cancel is an invalid transition naming both states.
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{id}/cancel", new { reason = "Again" }, Json), "InvalidPrescriptionTransition");
        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.PostAsJsonAsync($"/api/staff/prescriptions/{Guid.NewGuid()}/cancel", new { reason = "x" }, Json)).StatusCode);
    }

    [Fact]
    public async Task AnInactiveMedicine_AndADisallowedRoute_AreRefused_AndAnUnknownMedicineIsA400()
    {
        var (world, medicineId) = await ReadyAsync();
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, route: "Intravenous"), Json), "RouteNotAllowedForMedicine");
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, Guid.NewGuid()), Json), "FormularyMedicineNotFound");

        await world.Admin.PutAsJsonAsync($"/api/admin/formulary/{medicineId}/active", new { isActive = false }, Json);
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId), Json), "MedicineInactive");
    }

    [Fact]
    public async Task TheValidityRules_AreEnforced_ForRepeatableAndOrdinaryPrescriptions()
    {
        var (world, medicineId) = await ReadyAsync();
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        // A repeatable prescription needs its end date, within the maximum (180 days by default).
        var noDate = IssueBody(world.VisitId, medicineId, repeatable: true, repeats: 2);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Vet.PostAsJsonAsync(url, noDate, Json)).StatusCode);

        var tooLong = IssueBody(world.VisitId, medicineId, repeatable: true, repeats: 2, validUntil: BranchToday.AddDays(400));
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, tooLong, Json), "InvalidValidity");

        var good = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, medicineId, repeatable: true, repeats: 2, validUntil: BranchToday.AddDays(90)), Json);
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        var detail = await JsonAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{(await JsonAsync(good)).GetProperty("prescriptionId").GetGuid()}"));
        Assert.Equal(3, detail.GetProperty("suppliesAuthorised").GetInt32());

        // An ordinary prescription defaults its last dispensing day to 30 days after it starts.
        var another = await CreateMedicineAsync(world.Admin, MedicineBody(ingredients: ["gabapentin"], classes: ["anticonvulsant"]));
        var ordinary = await world.Vet.PostAsJsonAsync(url, IssueBody(world.VisitId, another), Json);
        var ordinaryDetail = await JsonAsync(await world.Vet.GetAsync($"/api/staff/prescriptions/{(await JsonAsync(ordinary)).GetProperty("prescriptionId").GetGuid()}"));
        var validUntil = DateOnly.Parse(ordinaryDetail.GetProperty("validUntil").GetString()!);
        Assert.InRange(validUntil.DayNumber - BranchToday.DayNumber, 29, 31);
    }

    [Fact]
    public async Task AClientSentInstantWithANonUtcOffset_IsA400Never500_AndTheDatesTheApiDoesAccept_AreDatesOnly()
    {
        var (world, medicineId) = await ReadyAsync();
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";

        // startsOn and validUntil are dates; an offset instant ("+06:00") is not one and must not reach the database as a malformed value.
        var body = IssueBody(world.VisitId, medicineId);
        body["startsOn"] = "2026-10-10T00:00:00+06:00";
        var response = await world.Vet.PostAsJsonAsync(url, body, Json);
        Assert.True((int)response.StatusCode is >= 400 and < 500, $"{(int)response.StatusCode}: {await BodyAsync(response)}");

        // A date-only value in a non-UTC-sounding form is fine -- the contract has no DateTimeOffset field at all.
        var contract = typeof(CatEcommerce.Api.Contracts.Prescription.PrescriptionFieldsRequest).Assembly.GetTypes()
            .Where(t => t.Namespace == "CatEcommerce.Api.Contracts.Prescription")
            .SelectMany(t => t.GetProperties())
            .Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?) || p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));
        Assert.Empty(contract);
    }
}
