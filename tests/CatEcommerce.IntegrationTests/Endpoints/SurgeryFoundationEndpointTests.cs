using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Infrastructure.Persistence.Migrations;
using CatEcommerce.Shared.Common.Constants;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Queries;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SRG-001 phase A -- I-A1..I-A3, F31, the settings bounds (U-A7), protocols (C1) and the HO-APT-001 amendment's first consequences (G6).</summary>
public sealed class SurgeryFoundationEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    /// <summary>I-A1 -- the migrations on an empty database: the tables, the staff-time exclusion constraint (<c>contype = 'x'</c>) and the surgery columns on the appointment.</summary>
    [Fact]
    public async Task I_A1_TheStaffTimeTable_TheExclusionConstraint_AndTheAppointmentColumns_Exist()
    {
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'clinic' AND table_name = 'appointment_staff_time'"));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'clinic' AND table_name = 'surgery_protocol'"));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pg_constraint WHERE conname = 'ex_appointment_staff_time' AND contype = 'x'"));
        Assert.Equal(2, await CountAsync("SELECT count(*) FROM information_schema.columns WHERE table_schema = 'clinic' AND table_name = 'appointment' AND column_name IN ('surgery_id','assistant_id')"));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pg_indexes WHERE schemaname = 'clinic' AND indexname = 'ix_surgery_protocol_service_id'"));
    }

    /// <summary>I-A2 -- the host starts with scope validation on (every factory in this suite does), and every Surgery command has its handler and validator, every
    /// Surgery query its handler -- registered, resolvable in a scope, and the validator registered as <c>IValidator&lt;T&gt;</c> (rule 21).</summary>
    [Fact]
    public async Task I_A2_EverySurgeryCommandAndQuery_Resolves_InAScope()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var application = typeof(UpsertSurgeryProtocolCommand).Assembly;

        var commands = application.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "CatEcommerce.Application.Commands.Surgery" && typeof(Command).IsAssignableFrom(t)).ToList();
        Assert.NotEmpty(commands);
        foreach (var command in commands)
        {
            Assert.NotNull(provider.GetService(typeof(ICommandHandlerAsync<>).MakeGenericType(command)));
            Assert.NotNull(provider.GetService(typeof(IValidator<>).MakeGenericType(command)));
        }

        var read = typeof(CatEcommerce.Read.Queries.Surgery.GetSurgeryProtocolsQuery).Assembly;
        var queries = read.GetTypes().Where(t => t.IsClass && !t.IsAbstract && t.Namespace == "CatEcommerce.Read.Queries.Surgery").ToList();
        Assert.NotEmpty(queries);
        foreach (var query in queries)
        {
            var response = query.BaseType!.GetGenericArguments()[0];
            Assert.NotNull(provider.GetService(typeof(IQueryHandlerAsync<,>).MakeGenericType(query, response)));
        }
    }

    /// <summary>I-A3 -- the migration's backfill, run over a table emptied of the rows an appointment of this test's branch had: occupying appointments get a
    /// Performer row, others (cancelled, walk-ins) none.</summary>
    [Fact]
    public async Task I_A3_TheBackfill_CreatesStaffTimeRows_ForOccupyingAppointmentsOnly()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 2);

        var booked = await BookOrdinaryAppointmentAsync(world, start);
        var cancelled = await BookOrdinaryAppointmentAsync(world, start.AddHours(2));
        var cancel = await world.Admin.PostAsJsonAsync($"/api/staff/appointments/{cancelled}/cancel", new { reason = "Backfill probe" }, Json);
        Assert.True(cancel.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var walkIn = world.Pharmacy.Clinic.AppointmentId; // the world's checked-in walk-in: never occupies

        // Empty the derived rows of this branch's appointments (they are derived data; the appointment is the record), then replay the migration statement.
        await ExecuteAsync("DELETE FROM clinic.appointment_staff_time WHERE appointment_id IN (SELECT \"Id\" FROM clinic.appointment WHERE branch_id = @b)", ("b", world.BranchId));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM clinic.appointment_staff_time t JOIN clinic.appointment a ON a.\"Id\" = t.appointment_id WHERE a.branch_id = @b", ("b", world.BranchId)));

        await ExecuteAsync(AddAppointmentStaffTime.BackfillSql.Replace("WHERE performer_id IS NOT NULL", "WHERE branch_id = '" + world.BranchId + "' AND performer_id IS NOT NULL", StringComparison.Ordinal));

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.appointment_staff_time WHERE appointment_id = @a AND occupies", ("a", booked)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM clinic.appointment_staff_time WHERE appointment_id = @a", ("a", cancelled)));
        Assert.Equal(0, await CountAsync("SELECT count(*) FROM clinic.appointment_staff_time WHERE appointment_id = @a", ("a", walkIn)));
    }

    /// <summary>The exclusion constraint itself: two occupying rows for one staff account with overlapping time are refused by PostgreSQL (23P01), in a rolled-back transaction.</summary>
    [Fact]
    public async Task TheStaffTimeConstraint_RefusesAnOverlap_OfOneStaffAccount_AndAllowsAbuttingTimes()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 2);
        var first = await BookOrdinaryAppointmentAsync(world, start);
        var second = await BookOrdinaryAppointmentAsync(world, start.AddHours(3));

        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        // Move the second appointment's staff-time row onto the first one's time directly: the constraint, not the application, must refuse it.
        var state = await ExecuteInTransactionAsync(connection, transaction,
            "UPDATE clinic.appointment_staff_time SET starts_on = (SELECT starts_on FROM clinic.appointment_staff_time WHERE appointment_id = @first), ends_on = (SELECT ends_on FROM clinic.appointment_staff_time WHERE appointment_id = @first) WHERE appointment_id = @second",
            ("first", first), ("second", second));
        Assert.Equal("23P01", state);

        await transaction.RollbackAsync();
    }

    /// <summary>F31 (§7) -- a BEFORE UPDATE/DELETE row trigger that raises P0001 surfaces through Npgsql as a PostgresException with SqlState "P0001" and the trigger message,
    /// which the filter maps when it starts with "immutable:". Verified on a scratch table inside a rolled-back transaction (the real triggers land in phases D and E, whose tests
    /// repeat the proof through EF's DbUpdateException).</summary>
    [Theory]
    [InlineData("UPDATE f31_probe SET v = 'b' WHERE id = 1")]
    [InlineData("DELETE FROM f31_probe WHERE id = 1")]
    public async Task F31_ATriggerRaisingP0001_ReachesNpgsql_WithTheStatedSqlStateAndPrefix(string statement)
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var ddl = new NpgsqlCommand("""
            CREATE TEMP TABLE f31_probe (id int PRIMARY KEY, v text);
            CREATE FUNCTION pg_temp.f31_refuse() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'immutable: probe rows cannot be changed' USING ERRCODE = 'P0001';
            END $$;
            CREATE TRIGGER trg_f31_probe BEFORE UPDATE OR DELETE ON f31_probe FOR EACH ROW EXECUTE FUNCTION pg_temp.f31_refuse();
            INSERT INTO f31_probe VALUES (1, 'a');
            """, connection, transaction))
            await ddl.ExecuteNonQueryAsync();

        await using var command = new NpgsqlCommand(statement, connection, transaction);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal("P0001", ex.SqlState);
        Assert.StartsWith("immutable:", ex.MessageText, StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    /// <summary>U-A7 (integration) -- the registry enforces each key's bounds: an out-of-range value is refused and nothing in the batch applies.</summary>
    [Theory]
    [InlineData(SettingKeys.SurgeryAdmissionLeadMinutes, "721")]
    [InlineData(SettingKeys.SurgeryEarlyAdmissionHours, "25")]
    [InlineData(SettingKeys.SurgeryPreOpReminderHours, "73")]
    [InlineData(SettingKeys.SurgeryWeightValidityDays, "8")]
    [InlineData(SettingKeys.SurgeryFollowUpReminderLeadDays, "-1")]
    public async Task U_A7_ASurgerySettingOutsideItsBounds_IsRefused(string key, string value)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.PutAsJsonAsync("/api/admin/settings", new { changes = new[] { new { key, value } } }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── protocols (C1) ────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static object ProtocolBody(object[]? followUps = null, int fastingHours = 8) => new
    {
        fastingInstructions = "No food from midnight; water is fine until you leave home.",
        fastingHours,
        postOpInstructions = "Keep her warm and quiet. Call us if the wound opens.",
        followUps = followUps ?? [new { kind = "WoundCheck", dayOffset = 3, windowFromDay = 2, windowToDay = 4 }]
    };

    [Fact]
    public async Task Protocols_AVetSavesOne_ItReplacesTheDefaults_AndAListShowsIt()
    {
        var world = await SeedSurgeryWorldAsync();

        var before = await world.Vet.GetFromJsonAsync<JsonElement>("/api/staff/surgery-protocols", Json);
        var row = before.EnumerateArray().Single(p => p.GetProperty("serviceId").GetGuid() == world.SpayServiceId);
        Assert.True(row.GetProperty("isDefault").GetBoolean());
        Assert.Equal(2, row.GetProperty("followUps").GetArrayLength());

        var save = await world.Vet.PutAsJsonAsync($"/api/staff/surgery-protocols/{world.SpayServiceId}", ProtocolBody(), Json);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saveAgain = await world.Vet.PutAsJsonAsync($"/api/staff/surgery-protocols/{world.SpayServiceId}", ProtocolBody(fastingHours: 10), Json);
        Assert.Equal(HttpStatusCode.OK, saveAgain.StatusCode);

        var after = await world.Admin.GetFromJsonAsync<JsonElement>("/api/staff/surgery-protocols", Json);
        var updated = after.EnumerateArray().Single(p => p.GetProperty("serviceId").GetGuid() == world.SpayServiceId);
        Assert.False(updated.GetProperty("isDefault").GetBoolean());
        Assert.Equal(10, updated.GetProperty("fastingHours").GetInt32());
        Assert.Equal(1, updated.GetProperty("followUps").GetArrayLength());
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.surgery_protocol WHERE service_id = @s", ("s", world.SpayServiceId)));
    }

    [Fact]
    public async Task Protocols_ANurseCannotSave_AndAnOffsetOutsideItsWindowIsRefused_AndSoIsANonSurgicalService()
    {
        var world = await SeedSurgeryWorldAsync();

        var asNurse = await world.Nurse.PutAsJsonAsync($"/api/staff/surgery-protocols/{world.SpayServiceId}", ProtocolBody(), Json);
        Assert.Equal(HttpStatusCode.Forbidden, asNurse.StatusCode);

        var outside = await world.Vet.PutAsJsonAsync($"/api/staff/surgery-protocols/{world.SpayServiceId}",
            ProtocolBody([new { kind = "WoundCheck", dayOffset = 9, windowFromDay = 2, windowToDay = 4 }]), Json);
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Contains(BusinessViolationCodes.FollowUpOutsideWindow, await outside.Content.ReadAsStringAsync());

        var consultation = world.Pharmacy.Clinic.ServiceId; // a Consultation-room service
        var notSurgical = await world.Vet.PutAsJsonAsync($"/api/staff/surgery-protocols/{consultation}", ProtocolBody(), Json);
        Assert.Equal(HttpStatusCode.BadRequest, notSurgical.StatusCode);
        Assert.Contains(BusinessViolationCodes.ServiceNotSurgical, await notSurgical.Content.ReadAsStringAsync());
    }

    // ── HO-APT-001 amendment ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>G6 / I-B7 (first half) -- ADR-172: a staff booking of a theatre service through HO-APT-001 is refused ServiceNotBookable.</summary>
    [Fact]
    public async Task G6_AnOrdinaryStaffBooking_OfATheatreService_IsRefused()
    {
        var world = await SeedSurgeryWorldAsync();

        var response = await world.Admin.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId = world.CustomerId, catId = world.CatId, serviceId = world.SpayServiceId, startsOn = LocalStart(world, 2), performerId = world.VetId, roomId = world.TheatreId
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.ServiceNotBookable, await response.Content.ReadAsStringAsync());
    }

    // ── helpers ────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> BookOrdinaryAppointmentAsync(SurgeryWorld world, DateTimeOffset startsOn)
    {
        var response = await world.Admin.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId = world.CustomerId, catId = world.CatId, serviceId = world.Pharmacy.Clinic.ServiceId, startsOn, performerId = world.VetId
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();
    }

    private static async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a statement inside the given transaction and returns the SQLSTATE it fails with (null when it succeeds).</summary>
    private static async Task<string?> ExecuteInTransactionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        try
        {
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
    }
}
