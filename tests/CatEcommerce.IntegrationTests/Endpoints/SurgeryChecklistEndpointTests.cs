using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SRG-001 phase C -- US-SRG-02 (the five-item checklist, outstanding items named on the day list) and US-SRG-03 (consent re-read at start; the vet-only, audited
/// emergency override). Weight and consent complete from the cat's and HO-EMR-001's own events; nothing here talks to the checklist directly for those.</summary>
public sealed class SurgeryChecklistEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static JsonElement Item(JsonElement detail, string kind) =>
        detail.GetProperty("checklist").EnumerateArray().Single(i => i.GetProperty("kind").GetString() == kind);

    private static string[] Outstanding(JsonElement summaryParent) =>
        summaryParent.GetProperty("outstandingItems").EnumerateArray().Select(i => i.GetString()!).ToArray();

    // ── US-SRG-02 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-C1 -- a new surgery shows its five outstanding items BY NAME on the day list (US-SRG-02 AC1, AC2).</summary>
    [Fact]
    public async Task I_C1_ANewSurgery_ShowsFiveOutstandingItems_ByName_OnTheDayList()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 1, 10);
        var surgeryId = await BookAsync(world, start);
        var date = DateOnly.FromDateTime(start.ToOffset(TimeSpan.FromHours(6)).DateTime).ToString("yyyy-MM-dd");

        var day = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries/day?date={date}", Json);
        var row = Assert.Single(day.GetProperty("surgeries").EnumerateArray(), s => s.GetProperty("surgeryId").GetGuid() == surgeryId);

        var checklist = row.GetProperty("checklist");
        Assert.False(checklist.GetProperty("isComplete").GetBoolean());
        Assert.Equal(["FastingInstructionsIssued", "PreAnaestheticBloodWork", "WeightRecorded", "ConsentSigned", "AsaGradeAssessed"], Outstanding(checklist));
        Assert.False(row.GetProperty("consentRecorded").GetBoolean());
        Assert.Equal("Scheduled", row.GetProperty("status").GetString());
        Assert.Equal(world.TheatreId, row.GetProperty("theatre").GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("theatre").GetProperty("name").GetString()));
        Assert.Equal(world.VetId, row.GetProperty("surgeon").GetProperty("id").GetGuid());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("cat").GetProperty("name").GetString()));
        Assert.Equal("Asia/Dhaka", day.GetProperty("branchTimeZoneId").GetString());

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal(5, detail.GetProperty("checklist").GetArrayLength());
        Assert.All(detail.GetProperty("checklist").EnumerateArray(), i => Assert.Equal("Outstanding", i.GetProperty("status").GetString()));
    }

    /// <summary>I-C1 (rest) -- the manual items: a nurse completes the fasting note once; consent and the ASA grade cannot be completed by hand; the blood work is the only waivable item, and
    /// only by a vet; the ASA grade is a vet's.</summary>
    [Fact]
    public async Task TheManualItems_AreCompletedByNurseOrVet_AndTheRolesAreEnforcedAtTheApi()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 1, 10));
        string Url(string kind, string verb) => $"/api/staff/surgeries/{surgeryId}/checklist/{kind}/{verb}";

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync(Url("FastingInstructionsIssued", "complete"), new { }, Json)).StatusCode);
        await Ok204Async(await world.Nurse.PostAsJsonAsync(Url("FastingInstructionsIssued", "complete"), new { note = "Told the owner by phone" }, Json));

        var twice = await world.Nurse.PostAsJsonAsync(Url("FastingInstructionsIssued", "complete"), new { }, Json);
        Assert.Contains(BusinessViolationCodes.ChecklistItemAlreadyDone, await twice.Content.ReadAsStringAsync());

        foreach (var kind in new[] { "ConsentSigned", "AsaGradeAssessed" })
        {
            var byHand = await world.Vet.PostAsJsonAsync(Url(kind, "complete"), new { }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, byHand.StatusCode);
            Assert.Contains(BusinessViolationCodes.ChecklistItemNotManual, await byHand.Content.ReadAsStringAsync());
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await world.Nurse.PostAsJsonAsync(Url("NotAnItem", "complete"), new { }, Json)).StatusCode);

        // Waiving: a nurse is refused at the door; a vet may waive the blood work but nothing else; consent never.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync(Url("PreAnaestheticBloodWork", "waive"), new { reason = "Young healthy cat" }, Json)).StatusCode);
        foreach (var kind in new[] { "ConsentSigned", "WeightRecorded", "FastingInstructionsIssued" })
            Assert.Contains(BusinessViolationCodes.ChecklistItemNotWaivable, await (await world.Vet.PostAsJsonAsync(Url(kind, "waive"), new { reason = "Because I say so" }, Json)).Content.ReadAsStringAsync());
        await Ok204Async(await world.Vet.PostAsJsonAsync(Url("PreAnaestheticBloodWork", "waive"), new { reason = "Young healthy cat" }, Json));

        // The ASA grade: vet only; grade must be I to V.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/asa-grade", new { grade = "II", isEmergency = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/asa-grade", new { grade = "VI", isEmergency = false }, Json)).StatusCode);
        await Ok204Async(await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/asa-grade", new { grade = "III", isEmergency = true }, Json));

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("III", detail.GetProperty("asaGrade").GetString());
        Assert.True(detail.GetProperty("asaIsEmergency").GetBoolean());
        Assert.Equal("Done", Item(detail, "FastingInstructionsIssued").GetProperty("status").GetString());
        Assert.Equal(world.NurseId, Item(detail, "FastingInstructionsIssued").GetProperty("completedBy").GetGuid());
        Assert.Equal("Waived", Item(detail, "PreAnaestheticBloodWork").GetProperty("status").GetString());
        Assert.Equal("Young healthy cat", Item(detail, "PreAnaestheticBloodWork").GetProperty("waiverReason").GetString());
        Assert.Equal(["WeightRecorded", "ConsentSigned"], Outstanding(detail.GetProperty("checklistSummary")));
    }

    /// <summary>I-C2 -- a weight recorded through HO-CAT-001's own endpoint completes the weight item (with the record id) when it falls inside the window; one two days earlier does not.</summary>
    [Fact]
    public async Task I_C2_ARecordedWeight_CompletesTheWeightItem_ThroughTheCatEvent()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 0, 10));

        await RecordWeightAsync(world.Nurse, world.CatId, 3.2m, LocalToday(world).AddDays(-2));
        Assert.Equal("Outstanding", Item(await GetSurgeryAsync(world.Vet, surgeryId), "WeightRecorded").GetProperty("status").GetString());

        var response = await world.Nurse.PostAsJsonAsync($"/api/cats/{world.CatId}/weights", new { kilograms = 3.4m, recordedOn = LocalToday(world) }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var weightId = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("weightRecordId").GetGuid();

        var item = Item(await GetSurgeryAsync(world.Vet, surgeryId), "WeightRecorded");
        Assert.Equal("Done", item.GetProperty("status").GetString());
        Assert.Equal(weightId, item.GetProperty("referenceId").GetGuid());
        Assert.Equal(3.4m, (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("latestWeight").GetProperty("kilograms").GetDecimal());
    }

    [Fact]
    public async Task AWeightRecordId_GivenByHand_IsVerifiedAgainstTheCatAndTheWindow()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 0, 10));
        string Url() => $"/api/staff/surgeries/{surgeryId}/checklist/WeightRecorded/complete";

        var noId = await world.Nurse.PostAsJsonAsync(Url(), new { }, Json);
        Assert.Contains(BusinessViolationCodes.WeightRecordNotValidForSurgery, await noId.Content.ReadAsStringAsync());

        var old = await world.Nurse.PostAsJsonAsync($"/api/cats/{world.CatId}/weights", new { kilograms = 3.0m, recordedOn = LocalToday(world).AddDays(-3) }, Json);
        var oldId = (await old.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("weightRecordId").GetGuid();
        var tooOld = await world.Nurse.PostAsJsonAsync(Url(), new { weightRecordId = oldId }, Json);
        Assert.Contains(BusinessViolationCodes.WeightRecordNotValidForSurgery, await tooOld.Content.ReadAsStringAsync());

        var otherCat = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Other");
        var theirs = await world.Nurse.PostAsJsonAsync($"/api/cats/{otherCat}/weights", new { kilograms = 4.0m, recordedOn = LocalToday(world) }, Json);
        var theirsId = (await theirs.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("weightRecordId").GetGuid();
        var wrongCat = await world.Nurse.PostAsJsonAsync(Url(), new { weightRecordId = theirsId }, Json);
        Assert.Contains(BusinessViolationCodes.WeightRecordNotValidForSurgery, await wrongCat.Content.ReadAsStringAsync());
    }

    /// <summary>I-C3 -- consent recorded through HO-EMR-001's endpoint completes the consent item once BOTH required kinds are on record; withdrawing one reverts it.</summary>
    [Fact]
    public async Task I_C3_ConsentRecorded_CompletesTheConsentItem_AndWithdrawingItRevertsIt()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 1, 10));
        var appointmentId = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("appointmentId").GetGuid();

        var surgeryConsent = await RecordConsentAsync(world, appointmentId, "Surgery");
        var half = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Outstanding", Item(half, "ConsentSigned").GetProperty("status").GetString()); // anaesthesia consent is still missing
        Assert.False(half.GetProperty("consentRecorded").GetBoolean());

        var anaesthesiaConsent = await RecordConsentAsync(world, appointmentId, "Anaesthesia");
        var both = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Done", Item(both, "ConsentSigned").GetProperty("status").GetString());
        Assert.True(both.GetProperty("consentRecorded").GetBoolean());
        Assert.Contains(Item(both, "ConsentSigned").GetProperty("referenceId").GetGuid(), new[] { surgeryConsent, anaesthesiaConsent }); // one of the two records that together cover both kinds

        var withdraw = await world.Reception.PostAsJsonAsync($"/api/staff/consents/{surgeryConsent}/withdraw", new { reason = "The owner changed their mind" }, Json);
        Assert.True(withdraw.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, await withdraw.Content.ReadAsStringAsync());

        var after = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Outstanding", Item(after, "ConsentSigned").GetProperty("status").GetString());
        Assert.False(after.GetProperty("consentRecorded").GetBoolean());
    }

    // ── US-SRG-03 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-C4 -- US-SRG-03 AC1: admit, then start without consent: 400 ConsentNotRecorded, and the surgery is still PreOp.</summary>
    [Fact]
    public async Task I_C4_StartWithoutConsent_IsRefused_AndTheSurgeryStaysPreOp()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await ReadyToStartAsync(world, consent: false);

        var start = await world.Nurse.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null);

        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Equal([BusinessViolationCodes.ConsentNotRecorded], await CodesOfAsync(start));
        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("PreOp", detail.GetProperty("status").GetString());
        Assert.Null(detail.GetProperty("procedureStartedOn").ValueKind == JsonValueKind.Null ? null : "started");
        Assert.Contains(detail.GetProperty("startBlockers").EnumerateArray(), b => b.GetProperty("code").GetString() == BusinessViolationCodes.ConsentNotRecorded);
    }

    [Fact]
    public async Task OneOfTheTwoConsents_IsNotEnoughToStart()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, appointmentId) = await ReadyToStartAsync(world, consent: false);
        await RecordConsentAsync(world, appointmentId, "Surgery");

        var start = await world.Nurse.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null);

        Assert.Equal([BusinessViolationCodes.ConsentNotRecorded], await CodesOfAsync(start));
    }

    /// <summary>The ordinary start, with consent on record and every item done: InTheatre. The start route cannot carry an override.</summary>
    [Fact]
    public async Task Start_WithConsentOnRecord_AndEveryItemDone_GoesInTheatre()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await ReadyToStartAsync(world);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null)).StatusCode);

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("InTheatre", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("consentOverride").ValueKind);
        Assert.Equal(["PreOp", "InTheatre"], detail.GetProperty("statusHistory").EnumerateArray().Skip(1).Select(h => h.GetProperty("to").GetString()).ToArray());
        Assert.Contains("PreOp", detail.GetProperty("statusHistory").EnumerateArray().Select(h => h.GetProperty("to").GetString()));
    }

    /// <summary>U-C6 (integration) -- consent recorded but an item outstanding: PreOpChecklistIncomplete naming it, and the surgery stays PreOp.</summary>
    [Fact]
    public async Task Start_WithAnOutstandingItem_NamesIt()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 0, 10));
        var appointmentId = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("appointmentId").GetGuid();
        await Ok204Async(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/checklist/FastingInstructionsIssued/complete", new { }, Json));
        await Ok204Async(await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/checklist/PreAnaestheticBloodWork/waive", new { reason = "Young healthy cat" }, Json));
        await Ok204Async(await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/asa-grade", new { grade = "I", isEmergency = false }, Json));
        await RecordBothConsentsAsync(world, appointmentId);
        await AdmitAsync(world, surgeryId);

        var start = await world.Nurse.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null);

        Assert.Equal(HttpStatusCode.BadRequest, start.StatusCode);
        Assert.Equal([BusinessViolationCodes.PreOpChecklistIncomplete], await CodesOfAsync(start));
        Assert.Contains("weight recorded", await start.Content.ReadAsStringAsync());
    }

    /// <summary>I-C5 -- a nurse calling the override route is refused at the door (403); an override sent to the ordinary start route does not exist.</summary>
    [Fact]
    public async Task I_C5_ANurse_CannotUseTheOverrideRoute()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await ReadyToStartAsync(world, consent: false);

        var asNurse = await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = "An emergency, the owner cannot be reached" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, asNurse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = "An emergency, the owner cannot be reached" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Admin.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = "An emergency, the owner cannot be reached" }, Json)).StatusCode);

        Assert.Equal("PreOp", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("status").GetString());
        // The refusal is itself audited (HO-ACC-001's AuthorizationDenied row).
        Assert.True(await CountDenialsAsync(world.NurseId, Actions.Surgery.OverrideConsent) >= 1);
    }

    /// <summary>I-C6 -- US-SRG-03 AC2: a vet overrides with a reason: 204, InTheatre, the detail shows the override with the vet and the reason, and a BusinessEvent audit row
    /// SurgeryConsentOverridden names the actor and the reason. A reason under ten characters is refused first.</summary>
    [Fact]
    public async Task I_C6_AVetOverride_StartsTheSurgery_IsOnTheRecord_AndInTheAuditLog()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await ReadyToStartAsync(world, consent: false);
        const string Reason = "Cat is bleeding internally, owner unreachable";

        var tooShort = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = "no time" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal([BusinessViolationCodes.ConsentOverrideReasonRequired], await CodesOfAsync(tooShort));
        Assert.Equal("PreOp", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("status").GetString());

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = Reason }, Json);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("InTheatre", detail.GetProperty("status").GetString());
        var recorded = detail.GetProperty("consentOverride");
        Assert.Equal(world.VetId, recorded.GetProperty("overriddenBy").GetGuid());
        Assert.Equal(Reason, recorded.GetProperty("reason").GetString());
        Assert.False(string.IsNullOrEmpty(recorded.GetProperty("overriddenByName").GetString()));

        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.BusinessEvent, "Surgery", surgeryId, world.VetId));
        Assert.Equal(Reason, await ScalarAsync<string>("SELECT \"Reason\" FROM audit.audit_entry WHERE \"EntityId\" = @e AND \"Action\" = 'SurgeryConsentOverridden'", ("e", surgeryId)));
    }

    /// <summary>§8.7 -- an override sent while consent IS on record is ignored (and the response says so): the surgery starts as an ordinary one and nothing is stored or audited.</summary>
    [Fact]
    public async Task AnOverride_SentWhileConsentIsOnRecord_IsIgnored()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await ReadyToStartAsync(world);

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = "Not actually needed here" }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("overrideIgnored").GetBoolean());
        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("InTheatre", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("consentOverride").ValueKind);
        Assert.Equal(0, await CountAuditRowsAsync(AuditCategory.BusinessEvent, "Surgery", surgeryId, world.VetId));
    }

    /// <summary>The override does not bypass the OTHER checklist items.</summary>
    [Fact]
    public async Task TheOverride_DoesNotBypassTheRestOfTheChecklist()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 0, 10));
        await AdmitAsync(world, surgeryId);

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/start-with-consent-override", new { reason = "Emergency with the owner unreachable" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([BusinessViolationCodes.PreOpChecklistIncomplete], await CodesOfAsync(response));
        Assert.Equal(JsonValueKind.Null, (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("consentOverride").ValueKind);
    }

    /// <summary>I-C7 (the parts that exist in this phase) -- a receptionist attempting start, the ASA grade, or a checklist item is refused (AC-18 pattern).</summary>
    [Fact]
    public async Task I_C7_AReceptionist_CannotStart_AssessRisk_OrCompleteChecklistItems()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await ReadyToStartAsync(world);

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/asa-grade", new { grade = "I", isEmergency = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/checklist/FastingInstructionsIssued/complete", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Admin.PostAsync($"/api/staff/surgeries/{surgeryId}/start", null)).StatusCode); // administrators hold no clinical write
    }

    /// <summary>Admission rules: not before the early-admission window (the world clock is 08:00; tomorrow's 10:00 surgery is outside it), and only from Scheduled.</summary>
    [Fact]
    public async Task Admit_TomorrowsSurgery_IsRefusedToday_AndTodaysIsAllowedOnce()
    {
        var world = await SeedSurgeryWorldAsync();
        var tomorrow = await BookAsync(world, LocalStart(world, 1, 10));
        var today = await BookAsync(world, LocalStart(world, 0, 10), catId: await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Today"), serviceId: world.DentalServiceId);

        var tooEarly = await world.Reception.PostAsync($"/api/staff/surgeries/{tomorrow}/admit", null);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Equal([BusinessViolationCodes.AdmissionNotToday], await CodesOfAsync(tooEarly));

        await AdmitAsync(world, today);
        var detail = await GetSurgeryAsync(world.Vet, today);
        Assert.Equal("PreOp", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, detail.GetProperty("admittedOn").ValueKind);

        var again = await world.Reception.PostAsync($"/api/staff/surgeries/{today}/admit", null);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidSurgeryTransition, await again.Content.ReadAsStringAsync());
        Assert.Equal(["Scheduled", "PreOp"], detail.GetProperty("statusHistory").EnumerateArray().Select(h => h.GetProperty("to").GetString()).ToArray());
        Assert.Equal(["Cancel", "Start"], detail.GetProperty("allowedTransitions").EnumerateArray().Select(t => t.GetString()!).Order().ToArray());
    }

    /// <summary>NFR-SEC-8 -- reading a surgery is a clinical-record access and is logged.</summary>
    [Fact]
    public async Task ReadingASurgery_WritesAClinicalAccessRow()
    {
        var world = await SeedSurgeryWorldAsync();
        var surgeryId = await BookAsync(world, LocalStart(world, 1, 10));
        var before = await CountAccessRowsAsync(world.NurseId);

        await GetSurgeryAsync(world.Nurse, surgeryId);

        Assert.Equal(before + 1, await CountAccessRowsAsync(world.NurseId));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @e AND \"Action\" = 'ClinicalRecordRead'", ("e", surgeryId)));
    }
}
