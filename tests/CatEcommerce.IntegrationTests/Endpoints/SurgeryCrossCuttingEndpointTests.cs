using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SRG-001 section 16 cross-cutting cases: I-A1 (schema), I-X1 (AC-3, a spay end to end) and I-X2 (nothing sensitive reaches a log). The retention source is registered here too.</summary>
[Collection("Notifications")]
public sealed class SurgeryCrossCuttingEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    // ── I-A1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A1_EverySurgeryTable_TrigggerAndIndex_Exists()
    {
        string[] tables =
        [
            "surgery", "surgery_checklist_item", "surgery_follow_up", "surgery_status_change", "surgery_recovery_change", "anaesthesia_log_entry", "surgical_report",
            "surgical_report_addendum", "surgery_protocol", "surgery_protocol_follow_up", "surgery_campaign", "surgery_campaign_price", "appointment_staff_time"
        ];
        foreach (var table in tables)
            Assert.Equal(1, await CountAsync("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'clinic' AND table_name = @t", ("t", table)));

        // The three immutability guards: anaesthesia log, surgical report and its addenda -- each has a row trigger and a TRUNCATE trigger.
        foreach (var (table, expected) in new[] { ("anaesthesia_log_entry", 2), ("surgical_report", 2), ("surgical_report_addendum", 2) })
        {
            var triggers = await CountAsync(
                "SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace " +
                "WHERE n.nspname = 'clinic' AND c.relname = @t AND NOT t.tgisinternal AND t.tgenabled <> 'D'", ("t", table));
            Assert.True(triggers >= expected, $"{table}: expected at least {expected} enabled triggers, found {triggers}");
        }

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pg_constraint WHERE conname = 'ex_appointment_staff_time' AND contype = 'x'"));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pg_indexes WHERE schemaname = 'clinic' AND indexname = 'ix_surgery_campaign_code' AND indexdef ILIKE '%UNIQUE%'"));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pg_indexes WHERE schemaname = 'clinic' AND indexname = 'ix_surgery_campaign_starts_on' AND indexdef ILIKE '%campaign_id IS NOT NULL%'"));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pg_indexes WHERE schemaname = 'clinic' AND indexname = 'ix_surgery_appointment_id' AND indexdef ILIKE '%UNIQUE%'"));
    }

    [Fact]
    public async Task TheRetentionSource_IsRegistered_AndCountsOnlyCatsPastTheirRetention()
    {
        var world = await SeedSurgeryWorldAsync();
        await using var scope = world.Api.Services.CreateAsyncScope();
        var sources = scope.ServiceProvider.GetServices<CatEcommerce.Domain.DomainServices.IRetentionReviewSource>().ToList();
        var source = Assert.Single(sources, s => s.SourceKey == "surgery-clinical");

        // A recent surgery is nowhere near retention.
        await BookAsync(world, LocalStart(world, 2, 10));
        var today = DateOnly.FromDateTime(world.Clock.GetUtcNow().UtcDateTime);
        var counts = await source.CountPastRetentionAsync(today, CancellationToken.None);
        Assert.DoesNotContain(counts, c => c.Count > 0 && c.OldestAnchor >= today.AddYears(-1));

        // Looked at from far in the future, the same records are flagged for review -- and only flagged: nothing is deleted.
        var future = await source.CountPastRetentionAsync(today.AddYears(40), CancellationToken.None);
        var surgeries = Assert.Single(future, c => c.RecordKind == "surgery");
        Assert.Equal(RecordClass.Clinical, surgeries.RecordClass);
        Assert.True(surgeries.Count >= 1);
        Assert.True(await CountAsync("SELECT count(*) FROM clinic.surgery WHERE cat_id = @c", ("c", world.CatId)) >= 1);
    }

    // ── the journey ─────────────────────────────────────────────────────────────────────────────

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                owner.Lines.Enqueue(formatter(state, exception) + (exception is null ? "" : " " + exception));
                if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                    foreach (var pair in pairs) owner.Lines.Enqueue($"{pair.Key}={pair.Value}");
            }
        }
    }

    private sealed record JourneyResult(Guid SurgeryId, Guid AppointmentId, Guid FollowUpWoundCheckId, Guid RegisterBatchId, string WitnessPassword);

    /// <summary>Every step of AC-3 through the real endpoints, with a distinctive token in every piece of free text so a log scan can find it.</summary>
    private async Task<JourneyResult> SpayJourneyAsync(SurgeryWorld world, string token)
    {
        string Url(Guid id, string leaf) => $"/api/staff/surgeries/{id}/{leaf}";

        var surgeryId = await BookAsync(world, LocalStart(world, 0, 10));
        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        var appointmentId = detail.GetProperty("appointmentId").GetGuid();

        // Pre-op: fasting (nurse), bloods waived (vet), weight (the cat endpoint), ASA (vet), consent (both kinds), admit.
        await Ok204Async(await world.Nurse.PostAsJsonAsync(Url(surgeryId, "checklist/FastingInstructionsIssued/complete"), new { note = $"Fasting-{token}" }, Json));
        await Ok204Async(await world.Vet.PostAsJsonAsync(Url(surgeryId, "checklist/PreAnaestheticBloodWork/waive"), new { reason = $"Waiver-{token}" }, Json));
        await RecordWeightAsync(world.Nurse, world.CatId, 3.4m, LocalToday(world));
        await Ok204Async(await world.Vet.PutAsJsonAsync(Url(surgeryId, "asa-grade"), new { grade = "I", isEmergency = false }, Json));
        await RecordBothConsentsAsync(world, appointmentId);
        await AdmitAsync(world, surgeryId);
        await Ok204Async(await world.Nurse.PostAsync(Url(surgeryId, "start"), null));

        // A controlled drug with a witness: the stock, the log entry and the register entries in one save.
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: $"Test ketamine {token}", skuIds: [sku.SkuId], ingredients: ["ketamine"], classes: ["anaesthetic"], dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Vial", routes: ["Intravenous"]));
        await SetControlledAsync(world.Admin, medicineId);
        var batchId = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(300), NewLotNumber());
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);

        var wrong = await world.Vet.PostAsJsonAsync(Url(surgeryId, "anaesthesia-log/drugs"), new
        {
            medicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId, quantityDrawn = 1m, quantityWasted = 0m, witnessIdentifier = witness.Mobile, witnessPassword = $"wrong-{token}-pw"
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var drug = await world.Vet.PostAsJsonAsync(Url(surgeryId, "anaesthesia-log/drugs"), new
        {
            medicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId, quantityDrawn = 1m, quantityWasted = 0m, witnessIdentifier = witness.Mobile, witnessPassword = witness.Password
        }, Json);
        Assert.True(drug.StatusCode == HttpStatusCode.Created, await drug.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, (await world.Nurse.PostAsJsonAsync(Url(surgeryId, "anaesthesia-log/vitals"),
            new { occurredOn = world.Clock.GetUtcNow().AddMinutes(-2), heartRateBpm = 130, spO2Percent = 98 }, Json)).StatusCode);

        // End, report, finalise, addendum.
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync(Url(surgeryId, "end-procedure"), new { outcome = "Completed" }, Json)).StatusCode);
        var draft = await world.Vet.PutAsJsonAsync(Url(surgeryId, "report"), new
        {
            procedurePerformed = "Ovariohysterectomy", findings = $"Findings-{token}", technique = "Midline approach", sutureMaterial = "4-0 PDS", hadComplications = false,
            surgeonId = world.VetId, assistantId = world.AssistantId
        }, Json);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var version = (await draft.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("version").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync(Url(surgeryId, "report/finalise"), new { expectedVersion = version }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await world.Vet.PostAsJsonAsync(Url(surgeryId, "report/addenda"), new { text = $"Addendum-{token}" }, Json)).StatusCode);

        // Recovery and discharge.
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PutAsJsonAsync(Url(surgeryId, "recovery-status"), new { status = "Stable", note = $"Recovery-{token}" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PutAsJsonAsync(Url(surgeryId, "recovery-status"), new { status = "ReadyForDischarge" }, Json)).StatusCode);
        var discharged = await world.Nurse.PostAsJsonAsync(Url(surgeryId, "discharge"), new { postOpInstructions = $"Instructions-{token}" }, Json);
        Assert.True(discharged.StatusCode == HttpStatusCode.OK, await discharged.Content.ReadAsStringAsync());
        var woundCheck = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("followUps")[0].GetProperty("id").GetGuid();

        return new JourneyResult(surgeryId, appointmentId, woundCheck, batchId, witness.Password);
    }

    /// <summary>I-X1 -- AC-3: a spay end to end. Every step succeeded (the journey asserts each); the neuter status is set; the register entry cannot be edited; the follow-up reminders are queued;
    /// every transition names its actor.</summary>
    [Fact]
    public async Task I_X1_Ac3_ASpayEndToEnd()
    {
        var world = await SeedSurgeryWorldAsync();
        var journey = await SpayJourneyAsync(world, Guid.NewGuid().ToString("N")[..10]);
        var surgeryId = journey.SurgeryId;

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal("Discharged", detail.GetProperty("status").GetString());
        Assert.Equal("Finalised", detail.GetProperty("reportStatus").GetString());
        Assert.True(detail.GetProperty("neuterStatusApplied").GetBoolean());

        var cat = await world.Admin.GetFromJsonAsync<JsonElement>($"/api/cats/{world.CatId}", Json);
        Assert.Equal("Neutered", cat.GetProperty("neuterStatus").GetString());

        // The status history names the actor of every transition.
        var history = detail.GetProperty("statusHistory").EnumerateArray().ToList();
        Assert.Equal(["Scheduled", "PreOp", "InTheatre", "Recovery", "Discharged"], history.Select(h => h.GetProperty("to").GetString()).ToArray());
        Assert.All(history, h => Assert.NotEqual(Guid.Empty, h.GetProperty("changedBy").GetGuid()));
        Assert.Equal(world.VetId, history.Single(h => h.GetProperty("to").GetString() == "Recovery").GetProperty("changedBy").GetGuid());
        Assert.Equal(world.NurseId, history.Single(h => h.GetProperty("to").GetString() == "Discharged").GetProperty("changedBy").GetGuid());

        // The controlled-drug register entry exists and cannot be edited (the database refuses).
        var registerId = await ScalarAsync<Guid>("SELECT \"Id\" FROM pharmacy.cd_register_entry WHERE source_id = @s ORDER BY 1 LIMIT 1", ("s", surgeryId));
        await using (var connection = await OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand("UPDATE pharmacy.cd_register_entry SET quantity_delta = 99 WHERE \"Id\" = @i", connection, transaction);
            command.Parameters.AddWithValue("i", registerId);
            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.False(string.IsNullOrEmpty(ex.SqlState)); // the register is append-only; PHM raises its own SQLSTATE
            await transaction.RollbackAsync();
        }

        // The post-op message and both follow-up reminders are queued for the owner.
        await using var scope = world.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var requests = await db.Set<NotificationRequestAggregate>().AsNoTracking().Where(r => r.RecipientId == world.CustomerId).ToListAsync();
        Assert.Single(requests, r => r.TemplateKey == NotificationTemplateCatalogue.SurgeryPostOpInstructions);
        Assert.Equal(2, requests.Count(r => r.TemplateKey == NotificationTemplateCatalogue.SurgeryFollowUpDue));

        // The audit log carries the clinical reads and the controlled-drug administration with the vet as actor (AC-20).
        Assert.True(await CountAsync("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @s AND \"ActorUserId\" = @v", ("s", surgeryId), ("v", world.VetId)) >= 1);
    }

    /// <summary>I-X2 -- the same journey, plus an emergency consent override and a cancellation, with a logger capturing every level: no override reason, instruction, report or addendum text,
    /// waiver, note, cancellation reason, witness password or customer name appears in any log line.</summary>
    [Fact]
    public async Task I_X2_NothingSensitive_ReachesAnyLog()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        var logs = new CapturingLoggerProvider();
        var world = await SeedSurgeryWorldAsync(configureServices: services => services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs)));

        var journey = await SpayJourneyAsync(world, token);

        // An emergency start without consent (vet only) on a second cat, then a cancellation with a reason.
        var emergencyCat = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Emergency");
        var (emergency, _) = await ReadyToStartAsync(world, catId: emergencyCat, consent: false, hour: 14);
        var overrideResponse = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{emergency}/start-with-consent-override", new { reason = $"Override-{token}" }, Json);
        Assert.True(overrideResponse.StatusCode == HttpStatusCode.NoContent, await overrideResponse.Content.ReadAsStringAsync());

        var cancelCat = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Cancelled");
        var toCancel = await BookAsync(world, LocalStart(world, 3, 10), catId: cancelCat);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{toCancel}/cancel", new { reason = $"Cancel-{token}" }, Json)).StatusCode);

        // Read everything a person can read.
        await world.Vet.GetAsync($"/api/staff/surgeries/{journey.SurgeryId}");
        await world.Vet.GetAsync($"/api/staff/surgeries/{journey.SurgeryId}/anaesthesia-log");
        await world.Vet.GetAsync($"/api/staff/surgeries/{journey.SurgeryId}/report");
        await world.Reception.GetAsync("/api/staff/surgeries/recovery-board");
        await world.Customer.GetAsync($"/api/customers/me/surgeries/{journey.SurgeryId}");

        var customerName = (await GetSurgeryAsync(world.Vet, journey.SurgeryId)).GetProperty("customerName").GetString()!;
        var lines = logs.Lines.ToList();
        Assert.NotEmpty(lines);
        foreach (var secret in new[]
        {
            $"Fasting-{token}", $"Waiver-{token}", $"Findings-{token}", $"Addendum-{token}", $"Recovery-{token}", $"Instructions-{token}", $"Override-{token}", $"Cancel-{token}",
            $"wrong-{token}-pw", journey.WitnessPassword
        })
            Assert.DoesNotContain(lines, l => l.Contains(secret, StringComparison.Ordinal));

        Assert.False(string.IsNullOrWhiteSpace(customerName));
        Assert.DoesNotContain(lines, l => l.Contains(customerName, StringComparison.Ordinal));
    }
}
