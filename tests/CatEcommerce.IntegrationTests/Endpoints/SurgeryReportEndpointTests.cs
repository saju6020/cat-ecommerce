using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SRG-001 phase E -- US-SRG-05 (the surgical report, immutable once finalised, addenda) and US-SRG-06 (neuter status set from a completed spay or neuter through the Cat context).</summary>
public sealed class SurgeryReportEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static object ReportBody(SurgeryWorld world, int? expectedVersion = null, string suture = "4-0 PDS", bool complications = false, string? complicationText = null, string findings = "Normal ovaries") => new
    {
        expectedVersion, procedurePerformed = "Ovariohysterectomy", findings, technique = "Midline approach", sutureMaterial = suture, hadComplications = complications,
        complications = complicationText, surgeonId = world.VetId, assistantId = world.AssistantId
    };

    private static async Task<int> SaveDraftAsync(HttpClient client, Guid surgeryId, object body)
    {
        var response = await client.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", body, Json);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("version").GetInt32();
    }

    private static async Task<JsonElement> ReportAsync(HttpClient client, Guid surgeryId)
    {
        var response = await client.GetAsync($"/api/staff/surgeries/{surgeryId}/report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static Task<HttpResponseMessage> EndAsync(HttpClient client, Guid surgeryId, string outcome = "Completed") =>
        client.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome }, Json);

    private static async Task<JsonElement> CatAsync(HttpClient client, Guid catId) => await client.GetFromJsonAsync<JsonElement>($"/api/cats/{catId}", Json);

    // ── US-SRG-06 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-E1 -- ending a spay Completed sets the cat's neuter status and the surgery's LOCAL date automatically; the audit trail names the vet (US-SRG-06).</summary>
    [Fact]
    public async Task I_E1_ACompletedSpay_SetsTheNeuterStatus_AndDate_AutomaticallyAndAudited()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, appointmentId) = await InTheatreAsync(world);
        Assert.NotEqual("Neutered", (await CatAsync(world.Admin, world.CatId)).GetProperty("neuterStatus").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await EndAsync(world.Vet, surgeryId)).StatusCode);

        var cat = await CatAsync(world.Admin, world.CatId);
        Assert.Equal("Neutered", cat.GetProperty("neuterStatus").GetString());
        Assert.Equal(LocalToday(world).ToString("yyyy-MM-dd"), cat.GetProperty("neuteredOn").GetString());

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Recovery", detail.GetProperty("status").GetString());
        Assert.Equal("Completed", detail.GetProperty("outcome").GetString());
        Assert.Equal("Recovering", detail.GetProperty("recoveryStatus").GetString());
        Assert.True(detail.GetProperty("neuterStatusApplied").GetBoolean());
        Assert.Equal(AppointmentStatus.Completed.Value, await ScalarAsync<int>("SELECT status FROM clinic.appointment WHERE \"Id\" = @a", ("a", appointmentId)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM clinic.appointment_staff_time WHERE appointment_id = @a AND occupies", ("a", appointmentId))); // the slot is freed

        // The cat's own audit trail records the change with the vet as the actor (HO-CAT-001's interceptor).
        Assert.True(await CountAsync("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @c AND \"ActorUserId\" = @v AND \"Action\" ILIKE '%Neuter%'", ("c", world.CatId), ("v", world.VetId)) >= 1
            || await CountAsync("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @c AND \"ActorUserId\" = @v", ("c", world.CatId), ("v", world.VetId)) >= 1);
    }

    /// <summary>U-E2 (integration) -- an abandoned spay and a completed dental leave the cat's status alone.</summary>
    [Fact]
    public async Task AnAbandonedSpay_AndACompletedDental_DoNotTouchTheCat()
    {
        var world = await SeedSurgeryWorldAsync();
        var (abandoned, _) = await InTheatreAsync(world);
        Assert.Equal(HttpStatusCode.NoContent, (await EndAsync(world.Vet, abandoned, "Abandoned")).StatusCode);

        var cat = await CatAsync(world.Admin, world.CatId);
        Assert.NotEqual("Neutered", cat.GetProperty("neuterStatus").GetString());
        var detail = await GetSurgeryAsync(world.Vet, abandoned);
        Assert.Equal("Abandoned", detail.GetProperty("outcome").GetString());
        Assert.Equal("Recovery", detail.GetProperty("status").GetString());

        var other = await SeedSurgeryWorldAsync();
        var (dental, _) = await InTheatreAsync(other, other.DentalServiceId);
        Assert.Equal(HttpStatusCode.NoContent, (await EndAsync(other.Vet, dental)).StatusCode);
        Assert.NotEqual("Neutered", (await CatAsync(other.Admin, other.CatId)).GetProperty("neuterStatus").GetString());
        Assert.Equal(JsonValueKind.Null, (await GetSurgeryAsync(other.Vet, dental)).GetProperty("neuterStatusApplied").ValueKind);
    }

    /// <summary>Ending needs a vet; only an InTheatre surgery can end; the outcome must be known.</summary>
    [Fact]
    public async Task EndProcedure_IsAVetsAct_FromInTheatre_WithAKnownOutcome()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        Assert.Equal(HttpStatusCode.Forbidden, (await EndAsync(world.Nurse, surgeryId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await EndAsync(world.Admin, surgeryId)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EndAsync(world.Vet, surgeryId, "Postponed")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await EndAsync(world.Vet, surgeryId)).StatusCode);

        var again = await EndAsync(world.Vet, surgeryId);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidSurgeryTransition, await again.Content.ReadAsStringAsync());

        var scheduled = await BookAsync(world, LocalStart(world, 1, 12), catId: await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Later"));
        var early = await EndAsync(world.Vet, scheduled);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Equal("A surgery that is scheduled cannot be ended.", (await ErrorsOfAsync(early)).Single().GetProperty("errorMessage").GetString());
    }

    /// <summary>I-E5 -- the cat is marked deceased before the procedure ends: the handler does NOT fail the request (the surgery is durable), writes a NeuterStatusNotApplied audit row, and the
    /// repair command refuses plainly with CatIsDeceased.</summary>
    [Fact]
    public async Task I_E5_ADeceasedCat_IsNotAnError_TheNeuterStatusIsNotApplied_AndTheRepairRefusesPlainly()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await MarkCatDeceasedAsync(world.Admin, world.CatId);

        Assert.Equal(HttpStatusCode.NoContent, (await EndAsync(world.Vet, surgeryId)).StatusCode);

        Assert.Equal("Recovery", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("status").GetString());
        Assert.NotEqual("Neutered", (await CatAsync(world.Admin, world.CatId)).GetProperty("neuterStatus").GetString());
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @s AND \"Action\" = 'NeuterStatusNotApplied' AND \"Reason\" = 'CatDeceased'", ("s", surgeryId)));

        var repair = await world.Vet.PostAsync($"/api/staff/surgeries/{surgeryId}/apply-sterilisation-outcome", null);
        Assert.Equal(HttpStatusCode.BadRequest, repair.StatusCode);
        Assert.Equal([BusinessViolationCodes.CatIsDeceased], await CodesOfAsync(repair));
    }

    /// <summary>The repair command is idempotent, a vet's, and only for a completed sterilisation.</summary>
    [Fact]
    public async Task TheRepairCommand_IsIdempotent_AVetsAct_AndOnlyForACompletedSterilisation()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        Assert.Equal("Neutered", (await CatAsync(world.Admin, world.CatId)).GetProperty("neuterStatus").GetString());
        var url = $"/api/staff/surgeries/{surgeryId}/apply-sterilisation-outcome";

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsync(url, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsync(url, null)).StatusCode);

        var other = await SeedSurgeryWorldAsync();
        var (dental, _) = await InTheatreAsync(other, other.DentalServiceId);
        await EndAsync(other.Vet, dental);
        Assert.Equal(HttpStatusCode.BadRequest, (await other.Vet.PostAsync($"/api/staff/surgeries/{dental}/apply-sterilisation-outcome", null)).StatusCode);

        // Un-neuter the cat's record behind the handler's back (a manual correction of the record), then repair.
        var repairable = await SeedSurgeryWorldAsync();
        var (spay, _) = await InTheatreAsync(repairable);
        await EndAsync(repairable.Vet, spay);
        var cleared = await repairable.Vet.PutAsJsonAsync($"/api/cats/{repairable.CatId}/neuter-status", new { neuterStatus = "Intact" }, Json);
        Assert.True(cleared.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, await cleared.Content.ReadAsStringAsync());
        Assert.False((await GetSurgeryAsync(repairable.Vet, spay)).GetProperty("neuterStatusApplied").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await repairable.Vet.PostAsync($"/api/staff/surgeries/{spay}/apply-sterilisation-outcome", null)).StatusCode);
        Assert.Equal("Neutered", (await CatAsync(repairable.Admin, repairable.CatId)).GetProperty("neuterStatus").GetString());
        Assert.True((await GetSurgeryAsync(repairable.Vet, spay)).GetProperty("neuterStatusApplied").GetBoolean());
    }

    // ── US-SRG-05 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-E2 -- draft, finalise, then PUT: 409 ReportNotEditable; an addendum is 201; both are visible (US-SRG-05). I-E4: the report attaches to the cat and the visit.</summary>
    [Fact]
    public async Task I_E2_ADraftIsEditable_AFinalisedReportIsNot_AndAddendaFollow()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        var visitId = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("visitId").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.GetAsync($"/api/staff/surgeries/{surgeryId}/report")).StatusCode);

        var v1 = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world));
        var v2 = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world, v1, findings: "Normal ovaries, no free fluid"));
        Assert.True(v2 > v1);

        var stale = await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", ReportBody(world, v1), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var draft = await ReportAsync(world.Reception, surgeryId);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal(world.CatId, draft.GetProperty("catId").GetGuid());
        Assert.Equal(visitId, draft.GetProperty("visitId").GetGuid());
        Assert.Equal("Normal ovaries, no free fluid", draft.GetProperty("findings").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = v2 }, Json)).StatusCode);

        var afterFinalise = await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", ReportBody(world, v2 + 1, findings: "Rewritten"), Json);
        Assert.Equal(HttpStatusCode.Conflict, afterFinalise.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReportNotEditable, await afterFinalise.Content.ReadAsStringAsync());

        var addendum = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/addenda", new { text = "Owner informed of the findings by phone." }, Json);
        Assert.Equal(HttpStatusCode.Created, addendum.StatusCode);

        var finalised = await ReportAsync(world.Nurse, surgeryId);
        Assert.Equal("Finalised", finalised.GetProperty("status").GetString());
        Assert.Equal("Normal ovaries, no free fluid", finalised.GetProperty("findings").GetString());
        Assert.Equal(world.VetId, finalised.GetProperty("finalisedBy").GetGuid());
        var added = Assert.Single(finalised.GetProperty("addenda").EnumerateArray());
        Assert.Equal("Owner informed of the findings by phone.", added.GetProperty("text").GetString());
        Assert.Equal(world.VetId, added.GetProperty("addedBy").GetGuid());
        Assert.Equal("Finalised", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("reportStatus").GetString());
    }

    /// <summary>The report can be written only once the procedure has ended, only by a vet, and finalisation names the missing fields.</summary>
    [Fact]
    public async Task TheReport_NeedsAnEndedProcedure_AVet_AndEveryRequiredField()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        var tooEarly = await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", ReportBody(world), Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidSurgeryTransition, await tooEarly.Content.ReadAsStringAsync());

        await EndAsync(world.Vet, surgeryId);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", ReportBody(world), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", ReportBody(world), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Admin.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", ReportBody(world), Json)).StatusCode);

        var version = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world, suture: ""));
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = version }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = version }, Json)).StatusCode);

        var incomplete = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = version }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Equal([BusinessViolationCodes.ReportIncomplete], await CodesOfAsync(incomplete));
        Assert.Contains("suture material", await incomplete.Content.ReadAsStringAsync());

        var v2 = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world, version, complications: true, complicationText: null));
        var noComplications = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = v2 }, Json);
        Assert.Contains("complications", await noComplications.Content.ReadAsStringAsync());

        var v3 = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world, v2, complications: true, complicationText: "Mild bleeding, controlled"));
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = v3 }, Json)).StatusCode);

        // Finalised twice, an addendum to a draft, and an addendum by a nurse.
        var currentVersion = (await ReportAsync(world.Vet, surgeryId)).GetProperty("version").GetInt32();
        Assert.Contains(BusinessViolationCodes.ReportAlreadyFinalised, await (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = currentVersion }, Json)).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/addenda", new { text = "x" }, Json)).StatusCode);

        var other = await SeedSurgeryWorldAsync();
        var (otherSurgery, _) = await InTheatreAsync(other);
        await EndAsync(other.Vet, otherSurgery);
        await SaveDraftAsync(other.Vet, otherSurgery, ReportBody(other));
        var onDraft = await other.Vet.PostAsJsonAsync($"/api/staff/surgeries/{otherSurgery}/report/addenda", new { text = "Too early for an addendum" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, onDraft.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReportNotFinalised, await onDraft.Content.ReadAsStringAsync());
    }

    /// <summary>I-E3 -- a raw UPDATE of a finalised report row, inside a rolled-back transaction, is refused by the trigger with P0001 'immutable:'; so are DELETE and an addendum UPDATE; and a DRAFT can
    /// still be updated (finalising itself is an update of a draft).</summary>
    [Fact]
    public async Task I_E3_TheTriggers_RefuseAnyChangeToAFinalisedReport_AndItsAddenda()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        var version = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world));

        // A draft row may be updated (the rolled-back probe proves the trigger lets it through).
        await using (var connection = await OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("UPDATE clinic.surgical_report SET findings = 'draft edit' WHERE \"Id\" = @s", connection, transaction);
            command.Parameters.AddWithValue("s", surgeryId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
            await transaction.RollbackAsync();
        }

        await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = version }, Json);
        var addendum = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/addenda", new { text = "Suture count confirmed by two people." }, Json);
        var addendumId = (await addendum.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("addendumId").GetGuid();

        foreach (var (statement, parameter) in new[]
        {
            ("UPDATE clinic.surgical_report SET findings = 'tampered' WHERE \"Id\" = @p", surgeryId),
            ("DELETE FROM clinic.surgical_report WHERE \"Id\" = @p", surgeryId),
            ("UPDATE clinic.surgical_report_addendum SET text = 'tampered' WHERE id = @p", addendumId),
            ("DELETE FROM clinic.surgical_report_addendum WHERE id = @p", addendumId),
        })
        {
            await using var connection = await OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(statement, connection, transaction);
            command.Parameters.AddWithValue("p", parameter);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal("P0001", ex.SqlState);
            Assert.StartsWith("immutable:", ex.MessageText, StringComparison.Ordinal);
            await transaction.RollbackAsync();
        }

        Assert.Equal("Normal ovaries", await ScalarAsync<string>("SELECT findings FROM clinic.surgical_report WHERE \"Id\" = @s", ("s", surgeryId)));
    }

    /// <summary>U-E6 (integration) -- an addendum adds a row and does not touch the report row: its version is unchanged (the trigger would fire on a root update).</summary>
    [Fact]
    public async Task U_E6_AnAddendum_DoesNotTouchTheReportRow()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        var version = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world));
        await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = version }, Json);
        var before = await ScalarAsync<int>("SELECT \"Version\" FROM clinic.surgical_report WHERE \"Id\" = @s", ("s", surgeryId));
        var xminBefore = await ScalarAsync<long>("SELECT xmin::text::bigint FROM clinic.surgical_report WHERE \"Id\" = @s", ("s", surgeryId));

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/addenda", new { text = $"Addendum {i}" }, Json)).StatusCode);

        Assert.Equal(before, await ScalarAsync<int>("SELECT \"Version\" FROM clinic.surgical_report WHERE \"Id\" = @s", ("s", surgeryId)));
        Assert.Equal(xminBefore, await ScalarAsync<long>("SELECT xmin::text::bigint FROM clinic.surgical_report WHERE \"Id\" = @s", ("s", surgeryId)));
        Assert.Equal(3, await ScalarAsync<long>("SELECT count(*) FROM clinic.surgical_report_addendum WHERE report_id = @s", ("s", surgeryId)));
    }

    /// <summary>OQ-10 -- a surgery whose procedure has ended but whose report is not finalised shows on the reportOutstanding list, and leaves it when finalised. Reading the report is a clinical access.</summary>
    [Fact]
    public async Task ReportOutstanding_ListsEndedSurgeriesWithoutAFinalisedReport_AndReadingTheReportIsLogged()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await EndAsync(world.Vet, surgeryId);
        var date = LocalToday(world).ToString("yyyy-MM-dd");

        var outstanding = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries?from={date}&to={date}&reportOutstanding=true", Json);
        var row = Assert.Single(outstanding.GetProperty("items").EnumerateArray(), s => s.GetProperty("id").GetGuid() == surgeryId);
        Assert.True(row.GetProperty("reportOutstanding").GetBoolean());

        var version = await SaveDraftAsync(world.Vet, surgeryId, ReportBody(world));
        await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report/finalise", new { expectedVersion = version }, Json);

        var after = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries?from={date}&to={date}&reportOutstanding=true", Json);
        Assert.DoesNotContain(after.GetProperty("items").EnumerateArray(), s => s.GetProperty("id").GetGuid() == surgeryId);

        var before = await CountAccessRowsAsync(world.NurseId);
        await ReportAsync(world.Nurse, surgeryId);
        Assert.Equal(before + 1, await CountAccessRowsAsync(world.NurseId));

        var other = await SeedSurgeryWorldAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/surgeries/{surgeryId}/report")).StatusCode);
    }
}
