using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-EMR-001 §16 Phase B -- I-B1 .. I-B7 and the open-visit list.</summary>
public sealed class VisitOpenAndVitalsEndpointTests(ConsultationApiFactory factory) : ConsultationIntegrationTestBase(factory)
{
    private static object FullVitals(string? weight = "4.2") => new
    {
        weightKg = decimal.Parse(weight!), temperatureC = 38.6m, heartRateBpm = 180, respiratoryRatePerMin = 30, bodyConditionScore = 5, painScore = 1
    };

    /// <summary>I-B1 -- a nurse opens a visit for a checked-in appointment; opening again returns the same id.</summary>
    [Fact]
    public async Task I_B1_NurseOpensAVisit_OpeningAgainReturnsTheSameVisit()
    {
        var world = await SeedClinicalWorldAsync();

        var first = await world.Nurse.PostAsJsonAsync("/api/staff/visits", new { appointmentId = world.AppointmentId }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var visitId = (await ReadAsync(first)).GetProperty("visitId").GetGuid();

        var second = await world.Vet.PostAsJsonAsync("/api/staff/visits", new { appointmentId = world.AppointmentId }, Json);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(visitId, (await ReadAsync(second)).GetProperty("visitId").GetGuid());

        var reference = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/appointments/{world.AppointmentId}/visit"));
        Assert.Equal(visitId, reference.GetProperty("visitId").GetGuid());
        Assert.Equal("Open", reference.GetProperty("status").GetString());

        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit where appointment_id = @a", ("a", world.AppointmentId)));
    }

    /// <summary>I-B2 -- two concurrent opens both succeed with the same id and leave one row.</summary>
    [Fact]
    public async Task I_B2_TwoConcurrentOpens_ReturnTheSameVisit_AndLeaveOneRow()
    {
        var world = await SeedClinicalWorldAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async i =>
        {
            var client = i % 2 == 0 ? world.Nurse : world.Vet;
            var response = await client.PostAsJsonAsync("/api/staff/visits", new { appointmentId = world.AppointmentId }, Json);
            Assert.True(response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await ReadAsync(response)).GetProperty("visitId").GetGuid();
        }));

        Assert.Single(results.Distinct());
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.visit where appointment_id = @a", ("a", world.AppointmentId)));
    }

    /// <summary>I-B3 -- a booked (not checked-in) appointment cannot have a visit opened.</summary>
    [Fact]
    public async Task I_B3_OpenForABookedAppointment_IsRefused()
    {
        var world = await SeedClinicalWorldAsync();
        var now = world.Clock.GetUtcNow();
        var startsOn = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddHours(4); // on the 15-minute grid
        var bookedId = await BookConfirmedAppointmentAsync(world.Customer, world.Context, world.CatId, startsOn);

        var response = await world.Nurse.PostAsJsonAsync("/api/staff/visits", new { appointmentId = bookedId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.AppointmentNotReadyForVisit, await response.Content.ReadAsStringAsync());
    }

    /// <summary>I-B4 -- vitals with units; the weight joins the cat's history under the entry's own id (US-CLN-11 AC1, UI-8).</summary>
    [Fact]
    public async Task I_B4_NurseRecordsVitals_TheWeightReachesTheCatsHistory_WithTheEntryId()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        var recorded = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", FullVitals(), Json);
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
        var entryId = (await ReadAsync(recorded)).GetProperty("vitalsEntryId").GetGuid();

        // The cat's weight history: newest point is 4.20 kg, stored under the vitals entry's id.
        var weights = await ReadAsync(await world.Admin.GetAsync($"/api/cats/{world.CatId}/weights"));
        var points = weights.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(points, p => p.GetProperty("kilograms").GetDecimal() == 4.2m);
        Assert.Equal(1, await ScalarAsync<int>("select count(*) from clinic.cat_weight_record where \"Id\" = @id and \"CatId\" = @c", ("id", entryId), ("c", world.CatId)));

        // Every sign travels with its unit; no bare number.
        var visit = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/visits/{visitId}"));
        var vitals = Assert.Single(visit.GetProperty("vitals").EnumerateArray());
        Assert.Equal("4.20", vitals.GetProperty("weight").GetProperty("value").GetString());
        Assert.Equal("kg", vitals.GetProperty("weight").GetProperty("unit").GetString());
        Assert.Equal("38.6", vitals.GetProperty("temperature").GetProperty("value").GetString());
        Assert.Equal("°C", vitals.GetProperty("temperature").GetProperty("unit").GetString());
        Assert.Equal("bpm", vitals.GetProperty("heartRate").GetProperty("unit").GetString());
        Assert.Equal("breaths/min", vitals.GetProperty("respiratoryRate").GetProperty("unit").GetString());
        Assert.Equal("/9", vitals.GetProperty("bodyConditionScore").GetProperty("unit").GetString());
        Assert.Equal("/10", vitals.GetProperty("painScore").GetProperty("unit").GetString());
        Assert.Equal("Asia/Dhaka", visit.GetProperty("branchTimeZoneId").GetString());
        Assert.Equal("Open", visit.GetProperty("status").GetString());
    }

    /// <summary>I-B5 -- 42.5 degrees warns with the range, saves when confirmed and stores the confirmation (US-CLN-11 AC2).</summary>
    [Fact]
    public async Task I_B5_PlausibleRangeWarning_ThenConfirmed_AndAbsurdIsRefusedOutright()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        var warned = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 42.5m }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, warned.StatusCode);
        var body = await ReadAsync(warned);
        Assert.Equal(BusinessViolationCodes.VitalsOutsidePlausibleRange, body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
        var sign = Assert.Single(body.GetProperty("outOfRange").EnumerateArray());
        Assert.Equal("Temperature", sign.GetProperty("sign").GetString());
        Assert.Equal("42.5", sign.GetProperty("value").GetString());
        Assert.Equal("35.0", sign.GetProperty("plausibleMin").GetString());
        Assert.Equal("41.5", sign.GetProperty("plausibleMax").GetString());

        var confirmed = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 42.5m, confirmedOutOfRange = new[] { "Temperature" } }, Json);
        Assert.Equal(HttpStatusCode.Created, confirmed.StatusCode);

        var visit = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/visits/{visitId}"));
        var entry = Assert.Single(visit.GetProperty("vitals").EnumerateArray());
        Assert.Equal("Temperature", Assert.Single(entry.GetProperty("confirmedOutOfRange").EnumerateArray()).GetString());

        var absurd = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 46.0m, confirmedOutOfRange = new[] { "Temperature" } }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, absurd.StatusCode);
        Assert.Contains(BusinessViolationCodes.VitalSignOutOfRange, await absurd.Content.ReadAsStringAsync());

        var none = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { }, Json);
        Assert.Contains(BusinessViolationCodes.VitalsRequired, await none.Content.ReadAsStringAsync());

        var rules = await ReadAsync(await world.Nurse.GetAsync("/api/staff/clinical/vital-sign-rules"));
        Assert.Equal(6, rules.GetArrayLength());
        Assert.Equal("41.5", rules.EnumerateArray().Single(r => r.GetProperty("sign").GetString() == "Temperature").GetProperty("plausibleMax").GetString());
    }

    /// <summary>I-B6 -- a weight insert that fails inside the commit leaves neither the vitals nor the weight (ADR-121 atomicity).
    /// The failure is a real database error: a trigger that refuses a weight insert for this test's own cat only.</summary>
    [Fact]
    public async Task I_B6_AWeightInsertThatFails_RollsBackTheVitalsToo()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using (var connection = await OpenConnectionAsync())
        {
            await using var create = new Npgsql.NpgsqlCommand($$"""
                create function clinic.test_block_weight_{{suffix}}() returns trigger language plpgsql as
                $f$ begin raise exception 'forced weight failure' using errcode = 'P0001'; end $f$;
                create trigger tr_test_block_weight_{{suffix}} before insert on clinic.cat_weight_record
                    for each row when (NEW."CatId" = '{{world.CatId}}') execute function clinic.test_block_weight_{{suffix}}();
                """, connection);
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var response = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", FullVitals(), Json);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

            Assert.Equal(0, await ScalarAsync<int>("select count(*) from clinic.visit_vitals where visit_id = @v", ("v", visitId)));
            Assert.Equal(0, await ScalarAsync<int>("select count(*) from clinic.cat_weight_record where \"CatId\" = @c", ("c", world.CatId)));
        }
        finally
        {
            await using var connection = await OpenConnectionAsync();
            await using var drop = new Npgsql.NpgsqlCommand(
                $"drop trigger tr_test_block_weight_{suffix} on clinic.cat_weight_record; drop function clinic.test_block_weight_{suffix}();", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }

    /// <summary>A client time with a local offset is accepted (stored as the same instant in UTC), not a 500 from timestamptz.</summary>
    [Fact]
    public async Task MeasuredOn_WithALocalOffset_IsAccepted_AndStoredInUtc()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        var measuredOn = world.Clock.GetUtcNow().AddMinutes(-30).ToOffset(TimeSpan.FromHours(6));

        var response = await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38.5m, measuredOn }, Json);

        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var visit = await ReadAsync(await world.Nurse.GetAsync($"/api/staff/visits/{visitId}"));
        var stored = visit.GetProperty("vitals")[0].GetProperty("measuredOn").GetDateTimeOffset();
        // timestamptz keeps microseconds; the fake clock carries 100 ns ticks.
        Assert.Equal(measuredOn.ToUniversalTime(), stored.ToUniversalTime(), TimeSpan.FromMilliseconds(1));
    }

    /// <summary>I-B7 -- reception holds no clinical-notes access (Appendix D).</summary>
    [Fact]
    public async Task I_B7_ReceptionCannotReadOrOpenAVisit()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync($"/api/staff/visits/{visitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync("/api/staff/visits", new { appointmentId = world.AppointmentId }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync("/api/staff/visits")).StatusCode);
    }

    /// <summary>The open list shows this branch's open visits with their flags; another branch never sees it.</summary>
    [Fact]
    public async Task OpenVisitList_ShowsTheBranchsVisits_WithHasVitals_AndAnotherBranchSeesNothing()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);
        await world.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38.5m }, Json);

        var list = await ReadAsync(await world.Nurse.GetAsync("/api/staff/visits?status=Open"));
        var item = list.EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == visitId);
        Assert.True(item.GetProperty("hasVitals").GetBoolean());
        Assert.False(item.GetProperty("hasNote").GetBoolean());
        Assert.Equal("Mitu", item.GetProperty("cat").GetProperty("name").GetString());
        Assert.False(item.GetProperty("cat").GetProperty("isHighRisk").GetBoolean());
        Assert.Equal(world.CatId, item.GetProperty("cat").GetProperty("id").GetGuid());

        // A second world is a second branch: its nurse cannot read this visit (404, never 403) or list it.
        var other = await SeedClinicalWorldAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Nurse.GetAsync($"/api/staff/visits/{visitId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Nurse.PostAsJsonAsync($"/api/staff/visits/{visitId}/vitals", new { temperatureC = 38m }, Json)).StatusCode);
        var otherList = await ReadAsync(await other.Nurse.GetAsync("/api/staff/visits"));
        Assert.DoesNotContain(otherList.EnumerateArray(), i => i.GetProperty("id").GetGuid() == visitId);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Nurse.GetAsync("/api/staff/visits?status=Nonsense")).StatusCode);
    }

    /// <summary>A visit read is a clinical read: exactly one access row, with the actor, the record and the IP-less test path (§8.11).</summary>
    [Fact]
    public async Task EveryVisitReadWritesOneClinicalRecordAccessRow()
    {
        var world = await SeedClinicalWorldAsync();
        var visitId = await OpenVisitAsync(world.Nurse, world.AppointmentId);

        var before = await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.Visit), visitId);
        var firstRead = await world.Nurse.GetAsync($"/api/staff/visits/{visitId}");
        Assert.Equal(HttpStatusCode.OK, firstRead.StatusCode);
        Assert.Contains("no-store", firstRead.Headers.CacheControl!.ToString()); // personal data is never cached
        Assert.Equal(HttpStatusCode.OK, (await world.Vet.GetAsync($"/api/staff/visits/{visitId}")).StatusCode);
        Assert.Equal(before + 2, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.Visit), visitId));

        // A refused read (wrong role) is not a read: the authorisation filter runs first and nothing is logged.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync($"/api/staff/visits/{visitId}")).StatusCode);
        Assert.Equal(before + 2, await CountAuditRowsAsync(AuditCategory.ClinicalRecordAccess, nameof(ClinicalRecordKind.Visit), visitId));
    }
}
