using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase C -- I-C1 .. I-C15: the note, the working copy, templates, finalisation, addenda,
/// the three layers of immutability and the ADR-133 guards.</summary>
public sealed class VisitNoteFinaliseEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private const string Assessment = "Mild otitis externa, left ear";
    private const string Plan = "Ear drops twice daily for seven days";

    private static async Task<int> VersionAsync(HttpClient client, Guid visitId) =>
        (await ReadAsync(await client.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();

    private static async Task<HttpResponseMessage> SaveNoteAsync(
        HttpClient vet, Guid visitId, int expectedVersion, IEnumerable<Guid> codeIds, string assessment = Assessment, string plan = Plan,
        Guid? templateId = null, int? templateVersion = null) =>
        await vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion, subjective = "Shaking head", objective = "Red ear canal", assessment, plan, dischargeInstructions = "Keep the ear dry.",
            problems = codeIds.Select(id => new { diagnosisCodeId = id, certainty = "Confirmed" }).ToArray(),
            appliedTemplateId = templateId, appliedTemplateVersion = templateVersion
        }, Json);

    /// <summary>An open visit with a saved note and one problem, ready to finalise.</summary>
    private async Task<(ClinicalWorld World, Guid VisitId, Guid CodeId)> ReadyToFinaliseAsync()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var codeId = await CreateDiagnosisCodeAsync(world.Vet);
        var save = await SaveNoteAsync(world.Vet, visitId, await VersionAsync(world.Vet, visitId), [codeId]);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        return (world, visitId, codeId);
    }

    private static async Task<HttpResponseMessage> FinaliseAsync(HttpClient client, Guid visitId) =>
        await client.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", new { expectedVersion = await VersionAsync(client, visitId) }, Json);

    /// <summary>I-C1 -- a vet saves S/O/A/P and two problems; the version moves and the visit stays open (US-CLN-12 AC1).</summary>
    [Fact]
    public async Task I_C1_VetSavesTheNoteAndTwoProblems_VersionIncrements_VisitStaysOpen()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var first = await CreateDiagnosisCodeAsync(world.Vet);
        var second = await CreateDiagnosisCodeAsync(world.Vet);
        var before = await VersionAsync(world.Vet, visitId);

        var save = await SaveNoteAsync(world.Vet, visitId, before, [first, second]);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await ReadAsync(save);
        Assert.True(saved.GetProperty("version").GetInt32() > before);

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal("Open", visit.GetProperty("status").GetString());
        Assert.Equal(Assessment, visit.GetProperty("note").GetProperty("assessment").GetString());
        Assert.Equal(2, visit.GetProperty("problems").GetArrayLength());
        Assert.Equal(saved.GetProperty("version").GetInt32(), visit.GetProperty("version").GetInt32());
        Assert.Contains("editNote", visit.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
        Assert.Contains("finalise", visit.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
    }

    /// <summary>I-C2 + I-C3 -- the working copy is returned to its author only, and only while newer than the saved note.</summary>
    [Fact]
    public async Task I_C2_C3_TheDraftIsReturnedToItsAuthorOnly_AndStopsWhenTheNoteIsSavedAfterwards()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var otherVet = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Vet);

        var put = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "typing in progress...", plan = "wip" }, Json);
        Assert.True(put.StatusCode == HttpStatusCode.NoContent, await put.Content.ReadAsStringAsync());
        world.Clock.Advance(TimeSpan.FromSeconds(10));
        var overwrite = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "typing in progress... more" }, Json);
        Assert.True(overwrite.StatusCode == HttpStatusCode.NoContent, await overwrite.Content.ReadAsStringAsync());

        var mine = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal("typing in progress... more", mine.GetProperty("draft").GetProperty("subjective").GetString());

        var theirs = await ReadAsync(await otherVet.Client.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("draft").ValueKind);

        // One working copy per (visit, author) -- overwritten, never duplicated.
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.consultation_draft where visit_id = @v", ("v", visitId)));

        // An explicit save later than the draft supersedes it.
        world.Clock.Advance(TimeSpan.FromSeconds(10));
        var code = await CreateDiagnosisCodeAsync(world.Vet);
        Assert.Equal(HttpStatusCode.OK, (await SaveNoteAsync(world.Vet, visitId, await VersionAsync(world.Vet, visitId), [code])).StatusCode);
        var after = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal(JsonValueKind.Null, after.GetProperty("draft").ValueKind);
    }

    /// <summary>The same author's two first draft writes racing on the unique (visit, author) index both succeed: the loser is re-dispatched
    /// once as an overwrite by the unique-constraint filter (§12.5); one row remains.</summary>
    [Fact]
    public async Task TheDraftFirstWriteRace_BothWritesSucceed_AndOneRowRemains()
    {
        var world = await SeedClinicalWorldAsync();

        // Two simultaneous FIRST writes by the same author, repeated on fresh visits until the race is actually hit. (Many writers
        // overwriting one row at the same instant is a different case: xmin makes that a 409, as for any aggregate.)
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var appointmentId = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, world.CatId, world.ServiceId);
            var visitId = await OpenVisitAsync(world.Nurse, appointmentId);

            var results = await Task.WhenAll(
                world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "first" }, Json),
                world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "second" }, Json));

            Assert.All(results, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));
            Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.consultation_draft where visit_id = @v", ("v", visitId)));
        }
    }

    /// <summary>I-C4 -- a service's template is offered until the first save, and its id and version are recorded (US-CLN-20).</summary>
    [Fact]
    public async Task I_C4_TheTemplateIsOfferedForPreFill_UntilTheFirstSave_AndIsRecordedOnTheNote()
    {
        var world = await SeedClinicalWorldAsync();
        var created = await world.Vet.PostAsJsonAsync("/api/staff/note-templates", new { serviceId = world.ServiceId, name = "Wellness", subjective = "History: ", plan = "Plan: " }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var templateId = (await ReadAsync(created)).GetProperty("noteTemplateId").GetGuid();

        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        var template = visit.GetProperty("template");
        Assert.Equal(templateId, template.GetProperty("id").GetGuid());
        Assert.Equal(1, template.GetProperty("version").GetInt32());
        Assert.Equal("History: ", template.GetProperty("subjective").GetString());

        var code = await CreateDiagnosisCodeAsync(world.Vet);
        Assert.Equal(HttpStatusCode.OK, (await SaveNoteAsync(world.Vet, visitId, visit.GetProperty("version").GetInt32(), [code], templateId: templateId, templateVersion: 1)).StatusCode);

        var saved = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal(JsonValueKind.Null, saved.GetProperty("template").ValueKind);
        Assert.Equal(templateId, saved.GetProperty("note").GetProperty("appliedTemplateId").GetGuid());
        Assert.Equal(1, saved.GetProperty("note").GetProperty("appliedTemplateVersion").GetInt32());
    }

    /// <summary>I-C5 -- two vets saving with the same expectedVersion: one 200, one 409 (US-CLN-12 AC1).</summary>
    [Fact]
    public async Task I_C5_TwoVetsSaveWithTheSameExpectedVersion_OneWins_OneConflicts()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var otherVet = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Vet);
        var code = await CreateDiagnosisCodeAsync(world.Vet);
        var version = await VersionAsync(world.Vet, visitId);

        var results = await Task.WhenAll(
            SaveNoteAsync(world.Vet, visitId, version, [code], assessment: "Vet one"),
            SaveNoteAsync(otherVet.Client, visitId, version, [code], assessment: "Vet two"));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }

    /// <summary>I-C6 -- receptionist, nurse and administrator are each refused finalisation, each audited once (US-CLN-22, AC-18).</summary>
    [Fact]
    public async Task I_C6_ReceptionNurseAndAdministrator_AreRefusedFinalisation_AndAuditedAsDenied()
    {
        var (world, visitId, _) = await ReadyToFinaliseAsync();
        var version = await VersionAsync(world.Vet, visitId);
        var body = new { expectedVersion = version };

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", body, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Admin.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise", body, Json)).StatusCode);

        Assert.Equal(1, await CountDenialsAsync(world.ReceptionStaffId, Actions.Visits.Finalise));
        Assert.Equal(1, await CountDenialsAsync(world.NurseStaffId, Actions.Visits.Finalise));
        Assert.Equal(1, await CountDenialsAsync(world.AdminStaffId, Actions.Visits.Finalise));

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal("Open", visit.GetProperty("status").GetString());
    }

    /// <summary>I-C7 -- no problem: ProblemRequired; with the system code SYS-NAD a well visit finalises (ADR-123).</summary>
    [Fact]
    public async Task I_C7_FinaliseWithoutAProblem_IsRefused_WithTheSystemCode_ItSucceeds()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var noProblem = await SaveNoteAsync(world.Vet, visitId, await VersionAsync(world.Vet, visitId), []);
        Assert.Equal(HttpStatusCode.OK, noProblem.StatusCode);

        var refused = await FinaliseAsync(world.Vet, visitId);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.ProblemRequired, await refused.Content.ReadAsStringAsync());

        var saved = await SaveNoteAsync(world.Vet, visitId, await VersionAsync(world.Vet, visitId), [DiagnosisCodeSeed.RoutineNoAbnormality.Id]);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal("Finalised", visit.GetProperty("status").GetString());
        Assert.Equal("SYS-NAD", visit.GetProperty("problems")[0].GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.String, visit.GetProperty("finalisedOn").ValueKind);
    }

    /// <summary>I-C8 -- after finalisation every write is VisitFinalised, and nothing changed (US-CLN-17 AC1, AC3).</summary>
    [Fact]
    public async Task I_C8_AfterFinalisation_NoteVitalsAndDraftWritesAreRefused_NothingChanges()
    {
        var (world, visitId, codeId) = await ReadyToFinaliseAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);
        var version = await VersionAsync(world.Vet, visitId);

        var note = await SaveNoteAsync(world.Vet, visitId, version, [codeId], assessment: "Changed afterwards");
        Assert.Equal(HttpStatusCode.BadRequest, note.StatusCode);
        Assert.Contains(BusinessViolationCodes.VisitFinalised, await note.Content.ReadAsStringAsync());

        var vitals = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38.5m }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, vitals.StatusCode);
        Assert.Contains(BusinessViolationCodes.VisitFinalised, await vitals.Content.ReadAsStringAsync());

        var draft = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "late" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, draft.StatusCode);

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal(Assessment, visit.GetProperty("note").GetProperty("assessment").GetString());
        Assert.Equal(0, visit.GetProperty("vitals").GetArrayLength());
        Assert.Equal(version, visit.GetProperty("version").GetInt32());
        Assert.Equal(["addAddendum"], visit.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()!).ToArray()); // only the addendum remains
    }

    /// <summary>I-C9 -- the database refuses what the aggregate and the API already refuse (ADR-118): UPDATE, DELETE, INSERT and
    /// TRUNCATE on a finalised visit and its addenda raise CR001, and the rows are unchanged. Real triggers; never disabled.</summary>
    [Fact]
    public async Task I_C9_TheDatabaseRefusesEveryChangeToAFinalisedVisitAndItsAddenda()
    {
        var (world, visitId, _) = await ReadyToFinaliseAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "Weight was 4.1 kg.", reason = "typo" }, Json)).StatusCode);

        var problemId = await ScalarAsync<Guid>("select \"Id\" from clinic.visit_problem where visit_id = @v limit 1", ("v", visitId));
        var addendumId = await ScalarAsync<Guid>("select \"Id\" from clinic.visit_addendum where visit_id = @v limit 1", ("v", visitId));

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit set assessment = 'x' where \"Id\" = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit set status = 1 where \"Id\" = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit where \"Id\" = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_problem where \"Id\" = @p", ("p", problemId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_problem set name = 'x' where \"Id\" = @p", ("p", problemId)));
        Assert.Equal("CR001", await SqlStateOfAsync("""
            insert into clinic.visit_vitals ("Id", measured_on, temperature_c, confirmed_out_of_range, recorded_by, recorded_on, visit_id)
            values (gen_random_uuid(), now(), 38.5, '{}', gen_random_uuid(), now(), @v)
            """, ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_addendum where \"Id\" = @a", ("a", addendumId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_addendum set text = 'changed' where \"Id\" = @a", ("a", addendumId)));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.visit_addendum"));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.visit cascade"));

        // The rows are exactly as finalised.
        Assert.Equal(Assessment, await ScalarAsync<string>("select assessment from clinic.visit where \"Id\" = @v", ("v", visitId)));
        Assert.Equal("Weight was 4.1 kg.", await ScalarAsync<string>("select text from clinic.visit_addendum where \"Id\" = @a", ("a", addendumId)));
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_problem where visit_id = @v", ("v", visitId)));
    }

    /// <summary>The same triggers also refuse a delete while the visit is still open: a clinical row is never deleted (C-ALL-4).</summary>
    [Fact]
    public async Task AClinicalRowIsNeverDeleted_EvenOnAnOpenVisit()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38.5m }, Json);

        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_vitals where visit_id = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit where \"Id\" = @v", ("v", visitId)));
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_vitals where visit_id = @v", ("v", visitId)));
    }

    /// <summary>I-C10 -- an addendum is recorded beneath the unchanged original, with author and time (US-CLN-17 AC2).</summary>
    [Fact]
    public async Task I_C10_AnAddendumIsListedBeneathTheUnchangedOriginal_WithAuthorAndTime()
    {
        var (world, visitId, _) = await ReadyToFinaliseAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);

        var added = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "Owner phoned: ear is much better.", reason = "follow-up call" }, Json);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"));
        Assert.Equal(Assessment, visit.GetProperty("note").GetProperty("assessment").GetString());
        var addendum = Assert.Single(visit.GetProperty("addenda").EnumerateArray());
        Assert.Equal("Owner phoned: ear is much better.", addendum.GetProperty("text").GetString());
        Assert.False(string.IsNullOrWhiteSpace(addendum.GetProperty("author").GetString()));
        Assert.Equal(JsonValueKind.String, addendum.GetProperty("addedOn").ValueKind);
        Assert.Contains("addAddendum", visit.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));

        // Reception, nurse and administrator cannot add addenda (Visits.AddAddendum is Vet).
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "x" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Admin.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "x" }, Json)).StatusCode);
    }

    /// <summary>I-C11 -- an addendum on an open visit is refused with the way forward.</summary>
    [Fact]
    public async Task I_C11_AnAddendumOnAnOpenVisit_IsRefused()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        var response = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "Too early" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.AddendumRequiresFinalisedVisit, body);
        Assert.Contains("edit the note instead", body);
    }

    /// <summary>I-C12 -- the appointment cannot complete while its visit is open; after finalising it can (ADR-133).</summary>
    [Fact]
    public async Task I_C12_CompleteIsRefusedWhileTheVisitIsOpen_AndSucceedsAfterFinalising()
    {
        var (world, visitId, _) = await ReadyToFinaliseAsync();
        var start = await world.Admin.PostAsJsonAsync($"/api/staff/appointments/{world.AppointmentId}/start", new { performerId = world.VetStaffId, roomId = world.Context.RoomId }, Json);
        Assert.True(start.StatusCode == HttpStatusCode.NoContent, await start.Content.ReadAsStringAsync());

        var refused = await world.Admin.PostAsync($"/api/staff/appointments/{world.AppointmentId}/complete", null);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.VisitNotFinalised, await refused.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Admin.PostAsync($"/api/staff/appointments/{world.AppointmentId}/complete", null)).StatusCode);
    }

    /// <summary>I-C13 -- a staff cancel of the checked-in appointment is refused once a visit exists (ADR-133).</summary>
    [Fact]
    public async Task I_C13_StaffCancelOfACheckedInAppointmentWithAVisit_IsRefused()
    {
        var world = await SeedClinicalWorldAsync();
        await OpenVisitAsync(world.Nurse, world.AppointmentId);

        var response = await world.Admin.PostAsJsonAsync($"/api/staff/appointments/{world.AppointmentId}/cancel", new { reason = "mistake" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.VisitInProgress, await response.Content.ReadAsStringAsync());
    }

    /// <summary>I-C14 -- finalising produces the owner's in-app message: a portal link and no clinical text (ADR-130).</summary>
    [Fact]
    public async Task I_C14_FinalisingNotifiesTheOwner_WithAPortalLink_AndNoClinicalText()
    {
        var (world, visitId, _) = await ReadyToFinaliseAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);

        await RunNotificationDeliveryUntilAsync(world.Api, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.VisitSummaryAvailable && n.Channel == NotificationChannel.InApp));

        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var message = await db.Set<NotificationAggregate>().AsNoTracking().SingleAsync(n =>
            n.RecipientId == world.CustomerId && n.TemplateKey == NotificationTemplateCatalogue.VisitSummaryAvailable && n.Channel == NotificationChannel.InApp);

        Assert.Contains($"/portal/visits/{visitId}", message.LinkPath ?? "");
        Assert.Contains("Mitu", message.Body);
        foreach (var clinical in new[] { Assessment, Plan, "otitis", "Shaking head", "Ear drops" })
        {
            Assert.DoesNotContain(clinical, message.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(clinical, message.Title ?? "", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>I-C15 -- the audit trail: DataChange rows for the visit's saves, none for drafts, BusinessEvent rows for finalise and addendum (AC-20).</summary>
    [Fact]
    public async Task I_C15_AuditRows_DataChangeForVisitSaves_NoneForDrafts_BusinessEventsForFinaliseAndAddendum()
    {
        var (world, visitId, _) = await ReadyToFinaliseAsync();
        await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "keystrokes" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, (await FinaliseAsync(world.Vet, visitId)).StatusCode);
        var addendum = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/addenda", new { text = "Late note." }, Json);
        var addendumId = (await ReadAsync(addendum)).GetProperty("addendumId").GetGuid();

        Assert.True(await CountAuditRowsAsync(AuditCategory.DataChange, "VisitAggregate", visitId) >= 2); // open, note save, finalise
        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.BusinessEvent, "VisitAggregate", visitId));
        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.BusinessEvent, "VisitAddendumAggregate", addendumId));

        var draftId = await ScalarAsync<Guid>("select \"Id\" from clinic.consultation_draft where visit_id = @v limit 1", ("v", visitId));
        Assert.Equal(0, await CountAuditRowsAsync(AuditCategory.DataChange, "ConsultationDraftAggregate", draftId));
    }
}
