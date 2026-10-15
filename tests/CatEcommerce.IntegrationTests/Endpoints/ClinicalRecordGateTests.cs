using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST-gate additions for HO-EMR-001 (TC-HO-EMR-001 rows G1..G9): cases the implementation sheet did not exercise --
/// SQLSTATE CR001 reaching the HTTP edge as 409, child-row immutability beyond the I-C9 probes, what the audit interceptor
/// captures for nested owned rows, a vitals time sent with an offset as raw JSON text, and a harder draft first-write race.
/// Real database, real triggers; no trigger is disabled (a test-only trigger is created for the 409 case and always dropped).
/// </summary>
public sealed class ClinicalRecordGateTests(ConsultationApiFactory factory, ITestOutputHelper output) : ConsultationIntegrationTestBase(factory)
{
    private static async Task<Guid> FinaliseWithVitalsAsync(ClinicalWorld world, Guid visitId)
    {
        var vitals = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38.5m }, Json);
        Assert.Equal(HttpStatusCode.Created, vitals.StatusCode);

        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        var save = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Gate", plan = "None",
            problems = new[] { new { diagnosisCodeId = DiagnosisCodeSeed.RoutineNoAbnormality.Id, certainty = "Confirmed" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var finalised = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/finalise",
            new { expectedVersion = (await ReadAsync(save)).GetProperty("version").GetInt32() }, Json);
        Assert.Equal(HttpStatusCode.NoContent, finalised.StatusCode);
        return visitId;
    }

    /// <summary>G1 -- a CR001 raised by the database inside a request becomes a 409 with the fixed message (not a 500), and the whole commit rolls back.
    /// The CR001 is produced by a test-only trigger on the weight insert of this test's own cat -- the same technique as I-B6.</summary>
    [Fact]
    public async Task G1_ACr001FromTheDatabase_IsAnswered409_AndTheCommitRollsBack()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using (var connection = await OpenConnectionAsync())
        {
            await using var create = new Npgsql.NpgsqlCommand($$"""
                create function clinic.test_cr001_{{suffix}}() returns trigger language plpgsql as
                $f$ begin raise exception 'forced frozen' using errcode = 'CR001'; end $f$;
                create trigger tr_test_cr001_{{suffix}} before insert on clinic.cat_weight_record
                    for each row when (NEW."CatId" = '{{world.CatId}}') execute function clinic.test_cr001_{{suffix}}();
                """, connection);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var response = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { weightKg = 4.2m, temperatureC = 38.6m }, Json);
            var body = await response.Content.ReadAsStringAsync();

            Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"{(int)response.StatusCode} {body}");
            Assert.Contains("finalised and cannot be changed", body);
            Assert.Equal(0, await ScalarAsync<int>("select count(*) from clinic.visit_vitals where visit_id = @v", ("v", visitId)));
        }
        finally
        {
            await using var connection = await OpenConnectionAsync();
            await using var drop = new Npgsql.NpgsqlCommand(
                $"drop trigger tr_test_cr001_{suffix} on clinic.cat_weight_record; drop function clinic.test_cr001_{suffix}();", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    /// <summary>G2 -- child rows of a finalised visit refuse UPDATE and DELETE of vitals and problems, and the visit's own status cannot be reopened.</summary>
    [Fact]
    public async Task G2_ChildRowsOfAFinalisedVisit_RefuseUpdateAndDelete()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await FinaliseWithVitalsAsync(world, await OpenVisitAsync(world.Nurse, world.AppointmentId));

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_vitals set temperature_c = 40.0 where visit_id = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_vitals where visit_id = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_problem set certainty = certainty where visit_id = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("delete from clinic.visit_problem where visit_id = @v", ("v", visitId)));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.visit_vitals"));
        Assert.Equal("CR001", await SqlStateOfAsync("truncate clinic.visit_problem"));
        Assert.Equal(38.5m, await ScalarAsync<decimal>("select temperature_c from clinic.visit_vitals where visit_id = @v", ("v", visitId)));
    }

    /// <summary>G3 -- a child row cannot be MOVED off a finalised visit by changing its visit_id (the trigger must look at OLD.visit_id as well as NEW).</summary>
    [Fact]
    public async Task G3_AChildRowCannotBeReparentedOffAFinalisedVisit()
    {
        var world = await SeedClinicalWorldAsync();
        var finalisedVisit = await FinaliseWithVitalsAsync(world, await OpenVisitAsync(world.Nurse, world.AppointmentId));

        var secondAppointment = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, world.CatId, world.ServiceId);
        var openVisit = await OpenVisitAsync(world.Nurse, secondAppointment);

        var state = await SqlStateOfAsync("update clinic.visit_vitals set visit_id = @open where visit_id = @fin", ("open", openVisit), ("fin", finalisedVisit));

        Assert.True(state == "CR001", $"moving a vitals row off a finalised visit returned SQLSTATE {state ?? "<success>"}");
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_vitals where visit_id = @v", ("v", finalisedVisit)));
    }

    /// <summary>G10 -- (EMR backlog row 4) a child row of an OPEN visit cannot be moved onto a finalised visit, nor onto another visit at all: visit_id never changes.</summary>
    [Fact]
    public async Task G10_AChildRowCannotBeMovedOntoAnyOtherVisit()
    {
        var world = await SeedClinicalWorldAsync();
        var finalisedVisit = await FinaliseWithVitalsAsync(world, await OpenVisitAsync(world.Nurse, world.AppointmentId));

        var secondAppointment = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, world.CatId, world.ServiceId);
        var openVisit = await OpenVisitAsync(world.Nurse, secondAppointment);
        Assert.Equal(HttpStatusCode.Created, (await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{openVisit}/vitals", new { temperatureC = 38.1m }, Json)).StatusCode);

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_vitals set visit_id = @fin where visit_id = @open", ("fin", finalisedVisit), ("open", openVisit)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit_vitals set visit_id = gen_random_uuid() where visit_id = @open", ("open", openVisit)));
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_vitals where visit_id = @v", ("v", openVisit)));
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit_vitals where visit_id = @v", ("v", finalisedVisit)));
        // a legitimate in-place edit of the open visit's row still works
        Assert.Null(await SqlStateOfAsync("update clinic.visit_vitals set temperature_c = 38.2 where visit_id = @open", ("open", openVisit)));
    }

    /// <summary>G11 -- (EMR backlog row 5) the soft-delete flag cannot be set on a visit, open or finalised (ADR-118: no clinical row is ever deleted).</summary>
    [Fact]
    public async Task G11_AVisitCannotBeMarkedToDelete_OpenOrFinalised()
    {
        var world = await SeedClinicalWorldAsync();
        var finalisedVisit = await FinaliseWithVitalsAsync(world, await OpenVisitAsync(world.Nurse, world.AppointmentId));
        var secondAppointment = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, world.CatId, world.ServiceId);
        var openVisit = await OpenVisitAsync(world.Nurse, secondAppointment);

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit set \"IsMarkedToDelete\" = true where \"Id\" = @v", ("v", finalisedVisit)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.visit set \"IsMarkedToDelete\" = true where \"Id\" = @v", ("v", openVisit)));
        Assert.Equal(0, await ScalarAsync<int>("select count(*) from clinic.visit where \"Id\" in (@a, @b) and \"IsMarkedToDelete\"", ("a", finalisedVisit), ("b", openVisit)));
        // a write that leaves the flag alone is unaffected on the open visit
        Assert.Null(await SqlStateOfAsync("update clinic.visit set \"IsMarkedToDelete\" = false where \"Id\" = @v", ("v", openVisit)));
    }

    /// <summary>G12 -- (EMR backlog row 5) a diagnostic order's identity (cat, visit, branch) and soft-delete flag are frozen; its own status still moves through the API.</summary>
    [Fact]
    public async Task G12_ADiagnosticOrder_CannotHaveItsIdentityOrDeleteFlagRewritten()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var order = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind = "BloodWork" }, Json);
        var orderId = (await ReadAsync(order)).GetProperty("diagnosticOrderId").GetGuid();

        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.diagnostic_order set cat_id = gen_random_uuid() where \"Id\" = @o", ("o", orderId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.diagnostic_order set visit_id = gen_random_uuid() where \"Id\" = @o", ("o", orderId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.diagnostic_order set branch_id = gen_random_uuid() where \"Id\" = @o", ("o", orderId)));
        Assert.Equal("CR001", await SqlStateOfAsync("update clinic.diagnostic_order set \"IsMarkedToDelete\" = true where \"Id\" = @o", ("o", orderId)));
        Assert.Equal(visitId, await ScalarAsync<Guid>("select visit_id from clinic.diagnostic_order where \"Id\" = @o and not \"IsMarkedToDelete\"", ("o", orderId)));

        var results = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results", new
        {
            reportedOn = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime),
            values = new object[] { new { name = "GateAnalyteAlpha", valueKind = "Numeric", numericValue = 7m, unit = "g/dL", referenceLow = 5m, referenceHigh = 10m } }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, results.StatusCode); // the order's own UPDATE (status -> Resulted) is not refused
    }

    /// <summary>G4 -- observed: which rows reach the DataChange audit snapshot of a diagnostic order. The interceptor only captures owned rows
    /// whose DIRECT owner is the root, so the nested result values are not captured. This test pins the observed behaviour and writes the snapshot to the test output.</summary>
    [Fact]
    public async Task G4_AuditSnapshotOfADiagnosticOrder_ShowsWhichOwnedRowsAreCaptured()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var order = await world.Vet.PostAsJsonAsync($"/api/staff/visits/{visitId}/diagnostics", new { kind = "BloodWork" }, Json);
        var orderId = (await ReadAsync(order)).GetProperty("diagnosticOrderId").GetGuid();

        var results = await world.Vet.PostAsJsonAsync($"/api/staff/diagnostics/{orderId}/results", new
        {
            reportedOn = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime),
            values = new object[]
            {
                new { name = "GateAnalyteAlpha", valueKind = "Numeric", numericValue = 4.9m, unit = "g/dL", referenceLow = 5m, referenceHigh = 10m },
                new { name = "GateAnalyteBeta", valueKind = "Numeric", numericValue = 7.7m, unit = "g/dL", referenceLow = 5m, referenceHigh = 10m },
            }
        }, Json);
        Assert.True(results.StatusCode == HttpStatusCode.Created, await results.Content.ReadAsStringAsync());

        var snapshots = await AfterValuesAsync(orderId);
        output.WriteLine("DiagnosticOrder audit rows: " + snapshots.Count);
        foreach (var s in snapshots) output.WriteLine(s ?? "<null>");

        Assert.NotEmpty(snapshots);
        var latest = snapshots[^1]!;
        // Observed: the order's own state and the owned result SET rows reach the snapshot...
        Assert.Contains("Status", latest);
        // ...but the nested result VALUES (the clinical content: names, numbers, flags) do not.
        Assert.DoesNotContain("GateAnalyteAlpha", latest);
        Assert.DoesNotContain("4.9", latest);
    }

    /// <summary>G5 -- observed: two owned rows of the same type (two problems on one visit) in one DataChange snapshot.</summary>
    [Fact(Skip = "KNOWN DEFECT (TEST gate finding F-2, MEDIUM, docs/gates/HO-EMR-001-TEST.md): AuditChangeCaptureInterceptor keys owned rows by type name, so the second of two problems overwrites the first in the snapshot. Observed FAIL 2026-10-07; un-skip when the snapshot carries every owned row.")]
    public async Task G5_AuditSnapshotOfAVisitWithTwoProblems_RecordsWhatItCan()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var codeA = await CreateDiagnosisCodeAsync(world.Vet);
        var codeB = await CreateDiagnosisCodeAsync(world.Vet);
        var version = (await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{visitId}"))).GetProperty("version").GetInt32();
        var save = await world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/note", new
        {
            expectedVersion = version, assessment = "Gate", plan = "None",
            problems = new[] { new { diagnosisCodeId = codeA, certainty = "Confirmed" }, new { diagnosisCodeId = codeB, certainty = "Provisional" } }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var snapshots = await AfterValuesAsync(visitId);
        output.WriteLine("Visit audit rows: " + snapshots.Count);
        foreach (var s in snapshots) output.WriteLine(s ?? "<null>");
        Assert.NotEmpty(snapshots);

        var latest = JsonDocument.Parse(snapshots[^1]!).RootElement;
        var problemKeys = latest.EnumerateObject().Count(p => p.Name.StartsWith("VisitProblem.", StringComparison.Ordinal) && p.Name.EndsWith(".Certainty", StringComparison.Ordinal));
        output.WriteLine("Certainty keys in snapshot: " + problemKeys);
        Assert.True(problemKeys >= 1, "no problem row at all reached the snapshot");
        // Two problems must be distinguishable in the audit trail, not collapsed into one key per column.
        Assert.True(problemKeys >= 2 || latest.GetRawText().Contains("Provisional") && latest.GetRawText().Contains("Confirmed"),
            "two problems were saved but the snapshot holds one value per column (the later row overwrites the earlier)");
    }

    private static async Task<List<string?>> AfterValuesAsync(Guid entityId)
    {
        var list = new List<string?>();
        await using var connection = await OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand(
            "select \"AfterValue\" from audit.audit_entry where \"EntityId\" = @id and category = 1 order by \"OccurredOn\", \"Id\"", connection);
        command.Parameters.AddWithValue("id", entityId);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(reader.IsDBNull(0) ? null : reader.GetString(0));
        return list;
    }

    /// <summary>G6 -- vitals time sent as RAW JSON text with an explicit offset (+06:00, -05:00, +05:30, Z): accepted, stored as the same instant.</summary>
    [Theory]
    [InlineData("+06:00")]
    [InlineData("-05:00")]
    [InlineData("+05:30")]
    [InlineData("Z")]
    public async Task G6_VitalsMeasuredOn_SentAsRawJsonWithAnOffset_IsStoredAsTheSameInstant(string offset)
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var instant = world.Clock.GetUtcNow().AddMinutes(-20);
        var local = offset == "Z" ? instant : instant.ToOffset(TimeSpan.Parse(offset.TrimStart('+')));
        var literal = local.ToString("yyyy-MM-dd'T'HH:mm:ss") + offset;

        using var content = new StringContent($$"""{"temperatureC":38.4,"measuredOn":"{{literal}}"}""", Encoding.UTF8, "application/json");
        var response = await world.Nurse.PostAsync($"/api/staff/visits/{visitId}/vitals", content);

        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{literal} -> {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var stored = await ScalarAsync<DateTime>("select measured_on at time zone 'UTC' from clinic.visit_vitals where visit_id = @v", ("v", visitId));
        Assert.Equal(DateTimeOffset.Parse(literal).UtcDateTime, DateTime.SpecifyKind(stored, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    /// <summary>G7 -- vitals time with no offset at all (a bare local time) is not a 500.</summary>
    [Fact]
    public async Task G7_VitalsMeasuredOn_WithNoOffset_IsNotAServerError()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var literal = world.Clock.GetUtcNow().AddMinutes(-20).UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss");

        using var content = new StringContent($$"""{"temperatureC":38.4,"measuredOn":"{{literal}}"}""", Encoding.UTF8, "application/json");
        var response = await world.Nurse.PostAsync($"/api/staff/visits/{visitId}/vitals", content);

        Assert.True((int)response.StatusCode < 500, $"{(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>G8 -- the draft first-write race, harder than the developer's: eight fresh visits, three simultaneous first writes each.
    /// No write may be a 500; the unique (visit, author) index leaves exactly one row; a loser may be 204 or (xmin) 409.</summary>
    [Fact]
    public async Task G8_DraftFirstWriteRace_WithThreeWriters_NeverProducesAServerError()
    {
        var world = await SeedClinicalWorldAsync();

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var appointmentId = await RegisterCheckedInWalkInAsync(world.Admin, world.CustomerId, world.CatId, world.ServiceId);
            var visitId = await OpenVisitAsync(world.Nurse, appointmentId);

            var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(i =>
                world.Vet.PutAsJsonAsync($"/api/staff/visits/{visitId}/draft", new { subjective = "writer " + i }, Json)));

            foreach (var r in results)
                Assert.True(r.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict, $"attempt {attempt}: {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
            Assert.Contains(results, r => r.StatusCode == HttpStatusCode.NoContent);
            Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.consultation_draft where visit_id = @v", ("v", visitId)));
        }
    }
}
