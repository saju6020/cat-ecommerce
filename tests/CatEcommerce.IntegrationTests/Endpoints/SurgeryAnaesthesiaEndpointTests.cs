using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SRG-001 phase D -- the anaesthesia log (US-SRG-04) and controlled drugs (US-SRG-08, BR-3, BR-7). The ports are the REAL ones: HO-IMP-001's stock ledger, HO-PHM-001's controlled-drug register and
/// witness verification (a witness types their OWN identifier and password into the request). Log rows are never cleaned up -- the trigger refuses it -- so every assertion filters by the test's own surgery.
/// </summary>
public sealed class SurgeryAnaesthesiaEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private sealed record Drug(Guid MedicineId, Guid SkuId, Guid BatchId, string Lot);

    private sealed record Witness(Guid StaffId, string Mobile, string Password);

    /// <summary>A formulary injectable linked to a stocked clinic SKU, with one batch at the clinic dispensary. <paramref name="controlled"/> flags it in the register.</summary>
    private async Task<Drug> SeedDrugAsync(SurgeryWorld world, bool controlled, decimal onHand = 10m, DateOnly? expires = null, string name = "Test anaesthetic")
    {
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: $"{name} {Guid.NewGuid().ToString("N")[..6]}", skuIds: [sku.SkuId], ingredients: [controlled ? "ketamine" : "propofol"], classes: [controlled ? "anaesthetic" : "sedative"],
            dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Vial", routes: ["Intravenous"]));
        if (controlled) await SetControlledAsync(world.Admin, medicineId);

        var lot = NewLotNumber();
        var batchId = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, onHand, expires ?? BranchToday.AddDays(300), lot);
        return new Drug(medicineId, sku.SkuId, batchId, lot);
    }

    private async Task<Witness> AddWitnessAsync(SurgeryWorld world)
    {
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);
        return new Witness(witness.StaffId, witness.Mobile, witness.Password);
    }

    private static object DrugBody(
        SurgeryWorld world, Guid medicineId, DateTimeOffset? occurredOn = null, Guid? administeredBy = null, Guid? batchId = null, decimal? drawn = null, decimal? wasted = null,
        Witness? witness = null, string? witnessPassword = null, string? fefoReason = null, decimal dose = 0.5m) => new
    {
        medicineId, doseAmount = dose, doseUnit = "Ml", route = "Intravenous", occurredOn = occurredOn ?? world.Clock.GetUtcNow().AddMinutes(-5), administeredById = administeredBy ?? world.VetId,
        batchId, quantityDrawn = drawn, quantityWasted = wasted, witnessIdentifier = witness?.Mobile, witnessPassword = witnessPassword ?? witness?.Password, fefoOverrideReason = fefoReason
    };

    private static Task<HttpResponseMessage> LogDrugAsync(HttpClient client, Guid surgeryId, object body) =>
        client.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/drugs", body, Json);

    private static async Task<Guid> EntryIdAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("entryId").GetGuid();
    }

    private static async Task<JsonElement> LogAsync(HttpClient client, Guid surgeryId)
    {
        var response = await client.GetAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static Task<int> LogRowsAsync(Guid surgeryId) => CountAsync("SELECT count(*) FROM clinic.anaesthesia_log_entry WHERE surgery_id = @s", ("s", surgeryId));

    private static Task<int> LedgerRowsAsync(Guid surgeryId) => CountAsync("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @s", ("s", surgeryId));

    private static Task<int> RegisterRowsAsync(Guid surgeryId) => CountAsync("SELECT count(*) FROM pharmacy.cd_register_entry WHERE source_id = @s", ("s", surgeryId));

    // ── US-SRG-04 ───────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-D1 -- premedication, vitals x3, induction and a drug appear as ONE timeline in order, each attributed and timestamped (US-SRG-04 AC1, AC2).</summary>
    [Fact]
    public async Task I_D1_DrugsVitalsAndMilestones_AreOneTimeline_InOrder_EachAttributed()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: false);
        var now = world.Clock.GetUtcNow();
        string Url(string leaf) => $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{leaf}";

        await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Url("milestones"), new { milestone = "Premedication", occurredOn = now.AddMinutes(-30), note = "Acepromazine" }, Json));
        await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Url("vitals"), new { occurredOn = now.AddMinutes(-25), heartRateBpm = 140, respiratoryRatePerMin = 24, spO2Percent = 98, temperatureCelsius = 38.4m }, Json));
        await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Url("milestones"), new { milestone = "Induction", occurredOn = now.AddMinutes(-20) }, Json));
        await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Url("vitals"), new { occurredOn = now.AddMinutes(-15), heartRateBpm = 120, spO2Percent = 99 }, Json));
        var drugEntry = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, now.AddMinutes(-18), batchId: drug.BatchId, drawn: 2m, wasted: 0m)));
        await EntryIdAsync(await world.Vet.PostAsJsonAsync(Url("vitals"), new { occurredOn = now.AddMinutes(-10), heartRateBpm = 110, etCo2MmHg = 38 }, Json));

        var log = await LogAsync(world.Reception, surgeryId);
        var entries = log.GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(6, entries.Count);
        Assert.Equal(["Milestone", "Vitals", "Milestone", "Drug", "Vitals", "Vitals"], entries.Select(e => e.GetProperty("kind").GetString()).ToArray());
        Assert.Equal(entries.Select(e => e.GetProperty("occurredOn").GetDateTimeOffset()).Order().ToArray(), entries.Select(e => e.GetProperty("occurredOn").GetDateTimeOffset()).ToArray());
        Assert.True(log.GetProperty("isOpen").GetBoolean());
        Assert.Equal("Asia/Dhaka", log.GetProperty("branchTimeZoneId").GetString());

        Assert.All(entries, e => Assert.NotEqual(Guid.Empty, e.GetProperty("loggedBy").GetGuid()));
        Assert.All(entries, e => Assert.Equal(now, e.GetProperty("loggedOn").GetDateTimeOffset()));
        Assert.Equal(world.NurseId, entries[0].GetProperty("loggedBy").GetGuid());
        var drugRow = entries.Single(e => e.GetProperty("id").GetGuid() == drugEntry);
        Assert.Equal(world.VetId, drugRow.GetProperty("loggedBy").GetGuid());
        Assert.Equal(drug.Lot, drugRow.GetProperty("values").GetProperty("batchNumber").GetString());
        Assert.Equal(0.5m, drugRow.GetProperty("values").GetProperty("doseAmount").GetDecimal());
        Assert.Equal("Intravenous", drugRow.GetProperty("values").GetProperty("route").GetString());
        Assert.Equal("Premedication", entries[0].GetProperty("values").GetProperty("milestone").GetString());
        Assert.Equal(140, entries[1].GetProperty("values").GetProperty("heartRateBpm").GetInt32());
    }

    /// <summary>U-D2 / U-D3 -- an entry cannot be dated in the future or more than an hour before the cat was admitted; vitals need a value, implausible ones need a confirmation (stored).</summary>
    [Fact]
    public async Task EntriesAreBounded_InTime_AndVitalsAreChecked_ForPlausibility()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var now = world.Clock.GetUtcNow();
        string Vitals() => $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals";

        var future = await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddMinutes(5), heartRateBpm = 120 }, Json);
        Assert.Contains(BusinessViolationCodes.InvalidAnaesthesiaEntry, await future.Content.ReadAsStringAsync());
        var tooEarly = await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddHours(-1).AddMinutes(-1), heartRateBpm = 120 }, Json);
        Assert.Contains(BusinessViolationCodes.InvalidAnaesthesiaEntry, await tooEarly.Content.ReadAsStringAsync());
        await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddMinutes(1), heartRateBpm = 120 }, Json)); // inside the 2-minute tolerance

        var none = await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddMinutes(-1), note = "nothing measured" }, Json);
        Assert.Contains(BusinessViolationCodes.VitalsRequired, await none.Content.ReadAsStringAsync());

        var implausible = await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddMinutes(-1), heartRateBpm = 300 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, implausible.StatusCode);
        Assert.Contains(BusinessViolationCodes.ImplausibleVitalValue, await implausible.Content.ReadAsStringAsync());

        var confirmedId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddMinutes(-1), heartRateBpm = 300, confirmImplausible = true }, Json));
        var stored = (await LogAsync(world.Vet, surgeryId)).GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == confirmedId);
        Assert.True(stored.GetProperty("values").GetProperty("implausibleConfirmed").GetBoolean());

        var precision = await world.Nurse.PostAsJsonAsync(Vitals(), new { occurredOn = now.AddMinutes(-1), temperatureCelsius = 37.25m }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, precision.StatusCode);
    }

    /// <summary>U-D10 -- the log is open only while the surgery is PreOp, InTheatre or Recovery.</summary>
    [Fact]
    public async Task TheLog_IsClosed_ForAScheduledOrCancelledSurgery()
    {
        var world = await SeedSurgeryWorldAsync();
        var scheduled = await BookAsync(world, LocalStart(world, 1, 10));
        var body = new { occurredOn = world.Clock.GetUtcNow().AddMinutes(-1), heartRateBpm = 120 };

        var closed = await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{scheduled}/anaesthesia-log/vitals", body, Json);
        Assert.Equal(HttpStatusCode.BadRequest, closed.StatusCode);
        Assert.Equal([BusinessViolationCodes.AnaesthesiaLogClosed], await CodesOfAsync(closed));
        Assert.False((await LogAsync(world.Vet, scheduled)).GetProperty("isOpen").GetBoolean());

        await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{scheduled}/cancel", new { reason = "Owner called" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{scheduled}/anaesthesia-log/vitals", body, Json)).StatusCode);
        Assert.Equal(0, await LogRowsAsync(scheduled));
    }

    [Fact]
    public async Task TheRolesAreEnforced_AtTheApi_ForLoggingAndReading()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: false);
        var now = world.Clock.GetUtcNow();
        var vitals = new { occurredOn = now.AddMinutes(-2), heartRateBpm = 120 };
        string Url(string leaf) => $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{leaf}";

        Assert.Equal(HttpStatusCode.Forbidden, (await LogDrugAsync(world.Nurse, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m))).StatusCode); // drugs are a vet's
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync(Url("vitals"), vitals, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Admin.PostAsJsonAsync(Url("vitals"), vitals, Json)).StatusCode); // administrators hold no clinical write
        Assert.Equal(HttpStatusCode.Forbidden, (await LogDrugAsync(world.Admin, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await world.Nurse.PostAsJsonAsync(Url("vitals"), vitals, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Reception.GetAsync(Url(""))).StatusCode);
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
    }

    /// <summary>NFR-SEC-8 -- reading the log is a clinical-record access; another branch gets a 404.</summary>
    [Fact]
    public async Task ReadingTheLog_IsLogged_AndAnotherBranchIsNotFound()
    {
        var world = await SeedSurgeryWorldAsync();
        var other = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var before = await CountAccessRowsAsync(world.NurseId);

        await LogAsync(world.Nurse, surgeryId);

        Assert.Equal(before + 1, await CountAccessRowsAsync(world.NurseId));
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals", new { occurredOn = DateTimeOffset.UtcNow, heartRateBpm = 120 }, Json)).StatusCode);
    }

    // ── stock (US-SRG-08 AC3) ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A stocked medicine names its batch and quantity; the stock leaves THAT batch in the same save (reference = the surgery, carrying the cat and owner); a non-FEFO choice needs a reason.</summary>
    [Fact]
    public async Task ANonControlledDrug_TakesStockFromTheNamedBatch_AndAnOutOfFefoChoiceNeedsAReason()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 90m, priceEffectiveFrom: world.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(name: "Two batch " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], dosingBasis: "Fixed", form: "Injection",
            dispensingUnit: "Vial", routes: ["Intravenous"]));
        var early = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 5m, BranchToday.AddDays(100), NewLotNumber());
        var late = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 5m, BranchToday.AddDays(300), NewLotNumber());

        var noBatch = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, medicineId));
        Assert.Equal([BusinessViolationCodes.BatchRequired], await CodesOfAsync(noBatch));

        var otherSku = await CreateMedicineSkuAsync(world.Admin, price: 50m, priceEffectiveFrom: world.Clock.GetUtcNow());
        var foreign = await ReceiveAsync(world.Admin, otherSku.SkuId, world.DispensaryId, 5m);
        Assert.Equal([BusinessViolationCodes.BatchSkuMismatch], await CodesOfAsync(await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, medicineId, batchId: foreign, drawn: 1m))));

        var notFefo = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, medicineId, batchId: late, drawn: 1m));
        Assert.Equal(HttpStatusCode.BadRequest, notFefo.StatusCode);
        Assert.Contains("AllocationOverrideReasonRequired", await notFefo.Content.ReadAsStringAsync());
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));

        var entryId = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, medicineId, batchId: late, drawn: 1m, fefoReason: "The early batch is on the other trolley")));
        Assert.Equal(4m, await OnHandAsync(late, world.DispensaryId));
        Assert.Equal(5m, await OnHandAsync(early, world.DispensaryId));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @s AND reference_line_id = @e AND batch_id = @b AND quantity_delta = -1 AND cat_id = @c AND customer_id = @u",
            ("s", surgeryId), ("e", entryId), ("b", late), ("c", world.CatId), ("u", world.CustomerId)));

        var follow = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, medicineId, batchId: early, drawn: 2m)));
        Assert.Equal(3m, await OnHandAsync(early, world.DispensaryId));
        Assert.NotEqual(entryId, follow);
        Assert.Equal(0, await RegisterRowsAsync(surgeryId)); // not controlled: no register entry
    }

    [Fact]
    public async Task AnUnknownOrInactiveMedicine_IsRefused_AndAMedicineWithoutAStockedSku_DeductsNothing()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        var unknown = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, Guid.NewGuid()));
        Assert.Equal([BusinessViolationCodes.MedicineNotFound], await CodesOfAsync(unknown));

        var oxygen = await CreateMedicineAsync(world.Admin, MedicineBody(name: "Medical oxygen " + Guid.NewGuid().ToString("N")[..6], dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Millilitre", routes: ["Inhaled"]));
        await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, oxygen, dose: 2m)));
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
        Assert.Equal(1, await LogRowsAsync(surgeryId));
    }

    /// <summary>I-D4 / U-D6 -- an expired batch is refused with NO override (BR-3, FR-IMP-9); nothing is written.</summary>
    [Fact]
    public async Task I_D4_AnExpiredBatch_IsRefused_AndNothingIsWritten()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        await SetBatchExpiryDirectlyAsync(drug.BatchId, LocalToday(world).AddDays(-1));

        var response = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: witness, fefoReason: "Use it anyway, this is an emergency"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([BusinessViolationCodes.BatchNotUsable], await CodesOfAsync(response));
        Assert.Equal(0, await LogRowsAsync(surgeryId));
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
        Assert.Equal(0, await RegisterRowsAsync(surgeryId));
    }

    /// <summary>U-D7 -- on-hand below the quantity: the drug HAS been given, so the entry is saved with the shortfall flag (and a reorder signal), not refused.</summary>
    [Fact]
    public async Task U_D7_AStockShortfall_IsRecorded_NotRefused()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: false, onHand: 1m);

        var response = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 3m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("stockShortfall").GetBoolean());
        Assert.Equal(1m, await OnHandAsync(drug.BatchId, world.DispensaryId)); // nothing was taken: there was not enough
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
        var entry = (await LogAsync(world.Vet, surgeryId)).GetProperty("entries").EnumerateArray().Single();
        Assert.True(entry.GetProperty("values").GetProperty("stockShortfall").GetBoolean());
        Assert.Equal(3m, entry.GetProperty("values").GetProperty("quantityDrawn").GetDecimal());
    }

    // ── US-SRG-08: controlled drugs ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>I-D2 -- a controlled drug with a real witness who re-enters their own identifier and password: 201; ONE log entry, ONE ledger entry for that batch, and the register entries naming
    /// vet, cat, dose, wastage and witness (US-SRG-08 AC1, AC3); a BusinessEvent audit row names the vet and the witness.</summary>
    [Fact]
    public async Task I_D2_AControlledDrug_WritesTheEntry_TheLedgerRow_AndTheWitnessedRegisterEntries_Together()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);

        var response = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 3m, wasted: 1m, witness: witness));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var entryId = body.GetProperty("entryId").GetGuid();
        var registerEntryId = body.GetProperty("registerEntryId").GetGuid();
        Assert.False(body.GetProperty("stockShortfall").GetBoolean());

        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(1, await LedgerRowsAsync(surgeryId));
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @s AND batch_id = @b AND quantity_delta = -3", ("s", surgeryId), ("b", drug.BatchId)));

        // The register: the administered quantity (drawn minus wasted) and the wastage, each naming the vet, the cat, the batch and the witness.
        Assert.Equal(2, await RegisterRowsAsync(surgeryId));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM pharmacy.cd_register_entry WHERE \"Id\" = @r AND source_id = @s AND actor_id = @a AND witness_id = @w AND cat_id = @c AND batch_id = @b AND quantity_delta = -2 AND source_type = @t",
            ("r", registerEntryId), ("s", surgeryId), ("a", world.VetId), ("w", witness.StaffId), ("c", world.CatId), ("b", drug.BatchId), ("t", ControlledDrugSourceType.Surgery.Value)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pharmacy.cd_register_entry WHERE source_id = @s AND quantity_delta = -1 AND wastage_quantity = 1 AND witness_id = @w",
            ("s", surgeryId), ("w", witness.StaffId)));

        var row = (await LogAsync(world.Vet, surgeryId)).GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == entryId).GetProperty("values");
        Assert.True(row.GetProperty("isControlled").GetBoolean());
        Assert.Equal(witness.StaffId, row.GetProperty("witnessId").GetGuid());
        Assert.Equal(registerEntryId, row.GetProperty("registerEntryId").GetGuid());
        Assert.Equal(1m, row.GetProperty("quantityWasted").GetDecimal());

        Assert.Equal(1, await CountAuditRowsAsync(AuditCategory.BusinessEvent, "AnaesthesiaLogEntryAggregate", entryId, world.VetId));
        var after = await ScalarAsync<string>("SELECT \"AfterValue\" FROM audit.audit_entry WHERE \"EntityId\" = @e AND \"Action\" = 'ControlledDrugAdministered'", ("e", entryId));
        Assert.Contains($"WitnessId={witness.StaffId}", after);
        Assert.Contains($"AdministeringVetId={world.VetId}", after);
    }

    /// <summary>I-D3 / U-D4 -- every refusal of a controlled drug leaves NOTHING behind (no log row, no ledger row, no register row): a bad witness, a missing witness, the witness being the administering vet,
    /// a nurse as administering vet, missing wastage.</summary>
    [Fact]
    public async Task I_D3_EveryControlledDrugRefusal_LeavesNothingBehind()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var vet2 = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Vet);

        async Task AssertRefusedAsync(object body, string code, HttpClient? caller = null)
        {
            var response = await LogDrugAsync(caller ?? world.Vet, surgeryId, body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(code, await response.Content.ReadAsStringAsync());
            Assert.Equal(0, await LogRowsAsync(surgeryId));
            Assert.Equal(0, await LedgerRowsAsync(surgeryId));
            Assert.Equal(0, await RegisterRowsAsync(surgeryId));
        }

        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 0m), BusinessViolationCodes.WitnessRequired);
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: witness, witnessPassword: "not-their-password"), BusinessViolationCodes.WitnessNotVerified);
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: null, witness: witness), BusinessViolationCodes.WastageRequired);
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, administeredBy: world.NurseId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: witness), BusinessViolationCodes.AdministeringVetRequired);
        // The caller cannot witness their own entry (the verifier refuses the acting person), and the witness cannot be the administering vet.
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: new Witness(world.VetId, world.VetMobile, world.VetPassword)), BusinessViolationCodes.WitnessNotVerified);
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, administeredBy: vet2.StaffId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: new Witness(vet2.StaffId, vet2.Mobile, vet2.Password)),
            BusinessViolationCodes.WitnessSameAsAdministrator);
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 2m, witness: witness), "wasted", caller: world.Vet);
        await AssertRefusedAsync(DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1.234m, wasted: 0m, witness: witness), BusinessViolationCodes.InvalidQuantity);

        Assert.Equal(10m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>I-D5 -- there is no PUT, PATCH or DELETE on any log route (405), and no entry changed.</summary>
    [Fact]
    public async Task I_D5_NoLogRouteAcceptsPutPatchOrDelete()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var entryId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals",
            new { occurredOn = world.Clock.GetUtcNow().AddMinutes(-2), heartRateBpm = 120 }, Json));
        var before = await ScalarAsync<int?>("SELECT heart_rate_bpm FROM clinic.anaesthesia_log_entry WHERE \"Id\" = @e", ("e", entryId));

        var routes = new[]
        {
            $"/api/staff/surgeries/{surgeryId}/anaesthesia-log", $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals", $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/drugs",
            $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/milestones", $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{entryId}/corrections",
        };
        foreach (var route in routes)
        {
            foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
            {
                var response = await world.Vet.SendAsync(new HttpRequestMessage(method, route) { Content = JsonContent.Create(new { heartRateBpm = 1 }) });
                Assert.True(response.StatusCode == HttpStatusCode.MethodNotAllowed, $"{method} {route} -> {(int)response.StatusCode}");
            }
        }

        // The entry itself has no route of its own to mutate.
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            var response = await world.Vet.SendAsync(new HttpRequestMessage(method, $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{entryId}") { Content = JsonContent.Create(new { }) });
            Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{method} entry -> {(int)response.StatusCode}");
        }

        Assert.Equal(before, await ScalarAsync<int?>("SELECT heart_rate_bpm FROM clinic.anaesthesia_log_entry WHERE \"Id\" = @e", ("e", entryId)));
        Assert.Equal(1, await LogRowsAsync(surgeryId));
    }

    /// <summary>I-D6 / F31 -- the trigger: a raw UPDATE, DELETE or TRUNCATE of a log row, inside a rolled-back transaction, is refused with P0001 'immutable:'.</summary>
    [Fact]
    public async Task I_D6_TheTrigger_RefusesUpdateDeleteAndTruncate_OfALogRow()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var entryId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals",
            new { occurredOn = world.Clock.GetUtcNow().AddMinutes(-2), heartRateBpm = 120 }, Json));

        foreach (var statement in new[]
        {
            "UPDATE clinic.anaesthesia_log_entry SET heart_rate_bpm = 1 WHERE \"Id\" = @e",
            "DELETE FROM clinic.anaesthesia_log_entry WHERE \"Id\" = @e",
            "TRUNCATE clinic.anaesthesia_log_entry",
        })
        {
            await using var connection = await OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using var command = new NpgsqlCommand(statement, connection, transaction);
            command.Parameters.AddWithValue("e", entryId);

            var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

            Assert.Equal("P0001", ex.SqlState);
            Assert.StartsWith("immutable:", ex.MessageText, StringComparison.Ordinal);
            await transaction.RollbackAsync();
        }

        Assert.Equal(120, await ScalarAsync<int?>("SELECT heart_rate_bpm FROM clinic.anaesthesia_log_entry WHERE \"Id\" = @e", ("e", entryId)));
    }

    /// <summary>I-D7 -- the register row written in I-D2 cannot be updated either: HO-PHM-001's own table rule refuses it (US-SRG-08 AC2).</summary>
    [Fact]
    public async Task I_D7_TheRegisterRow_CannotBeUpdated_AtTheTable()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var response = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: witness));
        var registerEntryId = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("registerEntryId").GetGuid();

        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("UPDATE pharmacy.cd_register_entry SET quantity_delta = -0.01 WHERE \"Id\" = @r", connection, transaction);
        command.Parameters.AddWithValue("r", registerEntryId);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.False(string.IsNullOrEmpty(ex.SqlState));
        await transaction.RollbackAsync();
        Assert.Equal(-1.00m, await ScalarAsync<decimal>("SELECT quantity_delta FROM pharmacy.cd_register_entry WHERE \"Id\" = @r", ("r", registerEntryId)));
    }

    /// <summary>I-D8 / U-D8 / U-D9 -- correct a controlled dose from 0.5 to 0.4 ml: a NEW linked log entry (needing a NEW witness), a stock RETURN of 0.1, a NEW register entry linked to the original;
    /// the originals untouched; the timeline marks the corrected entry and shows the effective values (US-SRG-08 AC2, US-SRG-04 AC2).</summary>
    [Fact]
    public async Task I_D8_ACorrection_IsANewLinkedEntry_WithAStockReturn_AndALinkedRegisterEntry()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var original = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 3m, wasted: 0m, witness: witness, dose: 0.5m));
        var originalBody = await original.Content.ReadFromJsonAsync<JsonElement>(Json);
        var originalEntry = originalBody.GetProperty("entryId").GetGuid();
        var originalRegister = originalBody.GetProperty("registerEntryId").GetGuid();
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
        var originalRegisterDelta = await ScalarAsync<decimal>("SELECT quantity_delta FROM pharmacy.cd_register_entry WHERE \"Id\" = @r", ("r", originalRegister));

        object Correction(Witness? w) => new
        {
            reason = "Drew up less than first recorded", occurredOn = world.Clock.GetUtcNow().AddMinutes(-3),
            drug = new { doseAmount = 0.4m, doseUnit = "Ml", route = "Intravenous", administeredById = world.VetId, quantityDrawn = 2m, quantityWasted = 0m },
            witnessIdentifier = w?.Mobile, witnessPassword = w?.Password
        };
        string Url() => $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{originalEntry}/corrections";

        // A new witness is required for the correction.
        var noWitness = await world.Vet.PostAsJsonAsync(Url(), Correction(null), Json);
        Assert.Contains(BusinessViolationCodes.WitnessRequired, await noWitness.Content.ReadAsStringAsync());
        Assert.Equal(1, await LogRowsAsync(surgeryId));

        // A nurse may not correct a drug.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync(Url(), Correction(witness), Json)).StatusCode);

        var corrected = await world.Vet.PostAsJsonAsync(Url(), Correction(witness), Json);
        var correctionId = await EntryIdAsync(corrected);

        // Stock: a RETURN of 0.1 (into quarantine -- Inventory's judgement what happens next); the original deduction untouched.
        Assert.Equal(2, await LedgerRowsAsync(surgeryId));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @s AND batch_id = @b AND quantity_delta = 1 AND kind = @k",
            ("s", surgeryId), ("b", drug.BatchId), ("k", MovementKind.Return.Value)));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @s AND batch_id = @b AND quantity_delta = -3", ("s", surgeryId), ("b", drug.BatchId)));

        // Register: a NEW Correction entry linked to the original; the original unchanged.
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pharmacy.cd_register_entry WHERE source_id = @s AND corrects_entry_id = @o AND kind = @k AND quantity_delta = 1.00 AND witness_id = @w",
            ("s", surgeryId), ("o", originalRegister), ("k", ControlledDrugEntryKind.Correction.Value), ("w", witness.StaffId)));
        Assert.Equal(originalRegisterDelta, await ScalarAsync<decimal>("SELECT quantity_delta FROM pharmacy.cd_register_entry WHERE \"Id\" = @r", ("r", originalRegister)));

        // The log: both entries remain; the first is marked corrected and the timeline shows the effective values.
        Assert.Equal(2, await LogRowsAsync(surgeryId));
        var entries = (await LogAsync(world.Vet, surgeryId)).GetProperty("entries").EnumerateArray().ToList();
        var first = entries.Single(e => e.GetProperty("id").GetGuid() == originalEntry);
        var second = entries.Single(e => e.GetProperty("id").GetGuid() == correctionId);
        Assert.True(first.GetProperty("isCorrected").GetBoolean());
        Assert.Equal(correctionId, first.GetProperty("correctedByEntryId").GetGuid());
        Assert.Equal(3m, first.GetProperty("values").GetProperty("quantityDrawn").GetDecimal());
        Assert.Equal(2m, first.GetProperty("effective").GetProperty("quantityDrawn").GetDecimal());
        Assert.Equal("Correction", second.GetProperty("kind").GetString());
        Assert.Equal(originalEntry, second.GetProperty("correctsEntryId").GetGuid());
        Assert.Equal("Drug", second.GetProperty("correctedKind").GetString());
        Assert.False(second.GetProperty("isCorrected").GetBoolean());

        // The original is no longer the latest of its chain; a second correction must target the correction.
        var again = await world.Vet.PostAsJsonAsync(Url(), Correction(witness), Json);
        Assert.Contains(BusinessViolationCodes.CorrectionTargetNotFound, await again.Content.ReadAsStringAsync());
        var viaChain = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{correctionId}/corrections", new
        {
            reason = "Drew up even less in the end", occurredOn = world.Clock.GetUtcNow().AddMinutes(-1),
            drug = new { doseAmount = 0.3m, doseUnit = "Ml", route = "Intravenous", administeredById = world.VetId, quantityDrawn = 1m, quantityWasted = 0m },
            witnessIdentifier = witness.Mobile, witnessPassword = witness.Password
        }, Json);
        Assert.Equal(HttpStatusCode.Created, viaChain.StatusCode);
        var chain = (await LogAsync(world.Vet, surgeryId)).GetProperty("entries").EnumerateArray().Single(e => e.GetProperty("id").GetGuid() == originalEntry);
        Assert.Equal(1m, chain.GetProperty("effective").GetProperty("quantityDrawn").GetDecimal()); // the leaf of the chain
    }

    /// <summary>A vitals correction is a new linked entry too; a milestone correction likewise; the kind must match; a stranger entry is not found.</summary>
    [Fact]
    public async Task ACorrection_OfVitalsOrAMilestone_IsANewLinkedEntry_AndTheKindMustMatch()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var now = world.Clock.GetUtcNow();
        var vitalsId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals", new { occurredOn = now.AddMinutes(-4), heartRateBpm = 210 }, Json));
        var milestoneId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/milestones", new { milestone = "Induction", occurredOn = now.AddMinutes(-3) }, Json));
        string Url(Guid id) => $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{id}/corrections";

        var wrongKind = await world.Nurse.PostAsJsonAsync(Url(vitalsId), new { reason = "Typed the wrong milestone", occurredOn = now.AddMinutes(-2), milestone = new { milestone = "Extubation" } }, Json);
        Assert.Contains(BusinessViolationCodes.InvalidAnaesthesiaEntry, await wrongKind.Content.ReadAsStringAsync());

        var shortReason = await world.Nurse.PostAsJsonAsync(Url(vitalsId), new { reason = "typo", occurredOn = now.AddMinutes(-2), vitals = new { heartRateBpm = 120 } }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, shortReason.StatusCode);

        var vitals = await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Url(vitalsId), new { reason = "Typed 210 instead of 120", occurredOn = now.AddMinutes(-2), vitals = new { heartRateBpm = 120 } }, Json));
        var milestone = await EntryIdAsync(await world.Nurse.PostAsJsonAsync(Url(milestoneId), new { reason = "It was the intubation, not the induction", occurredOn = now.AddMinutes(-1), milestone = new { milestone = "Intubation" } }, Json));

        var entries = (await LogAsync(world.Vet, surgeryId)).GetProperty("entries").EnumerateArray().ToList();
        Assert.Equal(120, entries.Single(e => e.GetProperty("id").GetGuid() == vitalsId).GetProperty("effective").GetProperty("heartRateBpm").GetInt32());
        Assert.Equal("Intubation", entries.Single(e => e.GetProperty("id").GetGuid() == milestoneId).GetProperty("effective").GetProperty("milestone").GetString());
        Assert.Equal(4, entries.Count);
        Assert.Equal(new[] { vitals, milestone }.Order().ToArray(), entries.Where(e => e.GetProperty("kind").GetString() == "Correction").Select(e => e.GetProperty("id").GetGuid()).Order().ToArray());

        var stranger = await world.Nurse.PostAsJsonAsync(Url(Guid.NewGuid()), new { reason = "Typed 210 instead of 120", occurredOn = now, vitals = new { heartRateBpm = 120 } }, Json);
        Assert.Contains(BusinessViolationCodes.CorrectionTargetNotFound, await stranger.Content.ReadAsStringAsync());
    }

    /// <summary>OQ-11 -- the log shows the cat's latest weight and the dose in mg/kg, for information only.</summary>
    [Fact]
    public async Task TheLog_ShowsTheLatestWeight_AndTheDoseInMgPerKg()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world); // the checklist recorded 3.4 kg
        var oxygen = await CreateMedicineAsync(world.Admin, MedicineBody(name: "Dose per kg " + Guid.NewGuid().ToString("N")[..6], dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Millilitre", routes: ["Intravenous"]));
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/drugs", new
        {
            medicineId = oxygen, doseAmount = 3.4m, doseUnit = "Mg", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId
        }, Json);
        await EntryIdAsync(response);

        var log = await LogAsync(world.Vet, surgeryId);

        Assert.Equal(3.4m, log.GetProperty("latestWeight").GetProperty("kilograms").GetDecimal());
        Assert.Equal(1.0m, log.GetProperty("entries").EnumerateArray().Single().GetProperty("effectiveDoseMgPerKg").GetDecimal());
    }
    // ── gate fix round (CODE_REVIEW rows 16-18) ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Row 16 -- the correction link is UNIQUE in the database: one correction per entry, so two simultaneous corrections cannot fork the chain.</summary>
    [Fact]
    public async Task TheCorrectionLink_IsAUniqueIndex()
    {
        await SeedSurgeryWorldAsync();

        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM pg_indexes WHERE schemaname = 'clinic' AND indexname = 'ix_anaesthesia_log_entry_corrects' AND indexdef LIKE 'CREATE UNIQUE INDEX%' AND indexdef LIKE '%corrects_entry_id IS NOT NULL%'"));
    }

    /// <summary>Row 16 -- two corrections of ONE entry sent at the same moment: exactly one is recorded, the other is refused (400 from the pre-check or 409 from the index), and the chain does not fork.</summary>
    [Fact]
    public async Task TwoSimultaneousCorrections_OfOneEntry_RecordExactlyOne()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var now = world.Clock.GetUtcNow();
        var vitalsId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals", new { occurredOn = now.AddMinutes(-4), heartRateBpm = 210 }, Json));
        var url = $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{vitalsId}/corrections";

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            world.Nurse.PostAsJsonAsync(url, new { reason = $"Typed 210 instead of 120 ({i})", occurredOn = now.AddMinutes(-2), vitals = new { heartRateBpm = 120 + i } }, Json)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
            Assert.True(refused.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict, $"{(int)refused.StatusCode}: {await refused.Content.ReadAsStringAsync()}");

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.anaesthesia_log_entry WHERE surgery_id = @s AND corrects_entry_id = @e", ("s", surgeryId), ("e", vitalsId)));
    }

    /// <summary>Row 16 -- when the index (not the pre-check) catches the loser, the answer is a clear 409 naming the entry.</summary>
    [Fact]
    public async Task ARacedCorrection_CaughtByTheIndex_IsA409()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var now = world.Clock.GetUtcNow();
        var vitalsId = await EntryIdAsync(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/vitals", new { occurredOn = now.AddMinutes(-4), heartRateBpm = 210 }, Json));
        var url = $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{vitalsId}/corrections";

        // A hammer of simultaneous requests: whichever lose the race at the index must answer 409 with the dedicated code, never a 500.
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(i =>
            world.Nurse.PostAsJsonAsync(url, new { reason = $"Typed 210 instead of 120 ({i})", occurredOn = now.AddMinutes(-2), vitals = new { heartRateBpm = 100 + i } }, Json)));

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Contains(BusinessViolationCodes.AnaesthesiaEntryAlreadyCorrected, await conflict.Content.ReadAsStringAsync());

        // TEST gate: the loop above passes with no 409 at all, so also pin the outcome -- one winner, every other answer a refusal, one correction row.
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created), r => Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.anaesthesia_log_entry WHERE surgery_id = @s AND corrects_entry_id = @e", ("s", surgeryId), ("e", vitalsId)));
    }

    /// <summary>Row 17 -- a retry of a drug entry with the same client-generated entry id returns the FIRST result: one entry, one deduction, one register entry.</summary>
    [Fact]
    public async Task ARepeatedDrugEntry_WithTheSameEntryId_IsLoggedDeductedAndRegisteredOnce()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var entryId = Guid.NewGuid();
        object Body() => new
        {
            entryId, medicineId = drug.MedicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId = drug.BatchId, quantityDrawn = 3m, quantityWasted = 1m, witnessIdentifier = witness.Mobile, witnessPassword = witness.Password
        };

        var first = await LogDrugAsync(world.Vet, surgeryId, Body());
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(entryId, firstBody.GetProperty("entryId").GetGuid());

        // The double click / retry: same id, same everything -- the first result comes back and nothing else is written.
        var retry = await LogDrugAsync(world.Vet, surgeryId, Body());
        var retryBody = await retry.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(entryId, retryBody.GetProperty("entryId").GetGuid());
        Assert.Equal(firstBody.GetProperty("registerEntryId").GetGuid(), retryBody.GetProperty("registerEntryId").GetGuid());

        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(1, await LedgerRowsAsync(surgeryId));
        Assert.Equal(2, await RegisterRowsAsync(surgeryId)); // the administered quantity and the wastage, once each
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>Row 17 -- a burst of simultaneous identical requests (a frantic double click) still writes one entry; the losers get the first result or a 409, never a 500 or a second deduction.</summary>
    [Fact]
    public async Task SimultaneousDrugEntries_WithTheSameEntryId_WriteOnce()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: false);
        var entryId = Guid.NewGuid();
        object Body() => new
        {
            entryId, medicineId = drug.MedicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId = drug.BatchId, quantityDrawn = 1m
        };

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => LogDrugAsync(world.Vet, surgeryId, Body())));

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict, $"{(int)r.StatusCode}"));
        // Backlog 41 -- a PK-race loser is the same vet re-sending: its 409 says "retry the same id", never "use a new id".
        foreach (var loser in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Equal([BusinessViolationCodes.AnaesthesiaEntryRaced], await CodesOfAsync(loser));
        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(1, await LedgerRowsAsync(surgeryId));
        Assert.Equal(9m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>Backlog 42 -- the same entry id with a different medicine, quantity or dose is refused with 409, not silently answered with the first entry.</summary>
    [Fact]
    public async Task ASameIdRepeat_WithADifferentBody_IsRefused_AndWritesNothingMore()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: false);
        var entryId = Guid.NewGuid();
        object Body(decimal drawn, decimal dose) => new
        {
            entryId, medicineId = drug.MedicineId, doseAmount = dose, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId = drug.BatchId, quantityDrawn = drawn
        };

        Assert.Equal(HttpStatusCode.Created, (await LogDrugAsync(world.Vet, surgeryId, Body(1m, 0.5m))).StatusCode);

        foreach (var changed in new[] { Body(2m, 0.5m), Body(1m, 0.9m) })
        {
            var mismatch = await LogDrugAsync(world.Vet, surgeryId, changed);
            Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
            Assert.Equal([BusinessViolationCodes.AnaesthesiaEntryIdMismatch], await CodesOfAsync(mismatch));
        }

        Assert.Equal(HttpStatusCode.Created, (await LogDrugAsync(world.Vet, surgeryId, Body(1m, 0.5m))).StatusCode); // the identical body still replays
        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(9m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>Row 17 -- an entry id already used by another surgery's record is refused; it is not a replay, and it does not reveal the record.</summary>
    [Fact]
    public async Task AnEntryId_UsedOnAnotherSurgery_IsRefused()
    {
        var world = await SeedSurgeryWorldAsync();
        var (first, _) = await InTheatreAsync(world);
        var (second, _) = await InTheatreAsync(world, hour: 14);
        var drug = await SeedDrugAsync(world, controlled: false);
        var entryId = Guid.NewGuid();
        object Body() => new
        {
            entryId, medicineId = drug.MedicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId = drug.BatchId, quantityDrawn = 1m
        };

        Assert.Equal(HttpStatusCode.Created, (await LogDrugAsync(world.Vet, first, Body())).StatusCode);
        var clash = await LogDrugAsync(world.Vet, second, Body());

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal([BusinessViolationCodes.DuplicateAnaesthesiaEntry], await CodesOfAsync(clash));
        Assert.Equal(0, await LogRowsAsync(second));
        Assert.Equal(9m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>Row 18 -- a stocked drug dated out of the window is refused BEFORE any ledger or register row is enlisted: nothing is written, and nothing is left tracked in the request.</summary>
    [Fact]
    public async Task AStockedDrug_WithAnOutOfWindowOccurredOn_IsRefused_BeforeAnyStockOrRegisterRowIsEnlisted()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var stocked = await SeedDrugAsync(world, controlled: false);
        var controlled = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var future = world.Clock.GetUtcNow().AddDays(1);
        var longBefore = world.Clock.GetUtcNow().AddDays(-3);

        foreach (var when in new[] { future, longBefore })
        {
            var plain = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, stocked.MedicineId, occurredOn: when, batchId: stocked.BatchId, drawn: 1m));
            Assert.Equal([BusinessViolationCodes.InvalidAnaesthesiaEntry], await CodesOfAsync(plain));

            var witnessed = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, controlled.MedicineId, occurredOn: when, batchId: controlled.BatchId, drawn: 2m, wasted: 0m, witness: witness));
            Assert.Equal([BusinessViolationCodes.InvalidAnaesthesiaEntry], await CodesOfAsync(witnessed));
        }

        Assert.Equal(0, await LogRowsAsync(surgeryId));
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
        Assert.Equal(0, await RegisterRowsAsync(surgeryId));
        Assert.Equal(10m, await OnHandAsync(stocked.BatchId, world.DispensaryId));
        Assert.Equal(10m, await OnHandAsync(controlled.BatchId, world.DispensaryId));
    }

    // ── TEST gate (2026-10-11) ────────────────────────────────────────────────────────────────────────────────────────────

    private static object ControlledBody(SurgeryWorld world, Drug drug, Witness witness, Guid? entryId = null, decimal drawn = 3m, decimal wasted = 1m) => new
    {
        entryId, medicineId = drug.MedicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
        batchId = drug.BatchId, quantityDrawn = drawn, quantityWasted = wasted, witnessIdentifier = witness.Mobile, witnessPassword = witness.Password
    };

    /// <summary>T-02 -- the PK race on a CONTROLLED drug: the losers had enlisted a ledger row and register rows in their own scope; none of them may survive. One log row, one ledger row, the two register
    /// rows of the winner, stock deducted once.</summary>
    [Fact]
    public async Task ControlledDrug_PkRace_LosersRollBackTheirLedgerAndRegisterRows()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var entryId = Guid.NewGuid();

        using var gate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 6).Select(_ => Task.Run(async () => { gate.Wait(); return await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, drug, witness, entryId)); })).ToList();
        gate.Set();
        var responses = await Task.WhenAll(tasks);

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var other in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            Assert.Equal(HttpStatusCode.Conflict, other.StatusCode);
            Assert.Equal([BusinessViolationCodes.AnaesthesiaEntryRaced], await CodesOfAsync(other));
        }

        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(1, await LedgerRowsAsync(surgeryId));
        Assert.Equal(2, await RegisterRowsAsync(surgeryId)); // administered (-2) and wastage (-1), once
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>T-04 -- simultaneous corrections of one controlled entry: exactly one wins, and the stock return and the register Correction exist once, not once per loser.</summary>
    [Fact]
    public async Task SimultaneousCorrections_OfAControlledDrug_ReturnStockOnce_AndWriteOneRegisterCorrection()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var original = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, drug, witness, drawn: 3m, wasted: 0m)));
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
        var url = $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{original}/corrections";

        using var gate = new ManualResetEventSlim(false);
        var tasks = Enumerable.Range(0, 6).Select(i => Task.Run(async () =>
        {
            gate.Wait();
            return await world.Vet.PostAsJsonAsync(url, new
            {
                reason = $"Drew up less than first recorded ({i})", occurredOn = world.Clock.GetUtcNow().AddMinutes(-3),
                drug = new { doseAmount = 0.4m, doseUnit = "Ml", route = "Intravenous", administeredById = world.VetId, quantityDrawn = 2m, quantityWasted = 0m },
                witnessIdentifier = witness.Mobile, witnessPassword = witness.Password
            }, Json);
        })).ToList();
        gate.Set();
        var responses = await Task.WhenAll(tasks);

        Assert.DoesNotContain(responses, r => (int)r.StatusCode >= 500);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            var codes = await CodesOfAsync(loser);
            Assert.True(codes.SequenceEqual([BusinessViolationCodes.AnaesthesiaEntryAlreadyCorrected]) || codes.SequenceEqual([BusinessViolationCodes.CorrectionTargetNotFound]), string.Join(",", codes));
        }

        Assert.Equal(1, await CountAsync("SELECT count(*) FROM clinic.anaesthesia_log_entry WHERE surgery_id = @s AND corrects_entry_id = @e", ("s", surgeryId), ("e", original)));
        Assert.Equal(2, await LogRowsAsync(surgeryId));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @s AND kind = @k", ("s", surgeryId), ("k", MovementKind.Return.Value)));
        Assert.Equal(2, await LedgerRowsAsync(surgeryId)); // the deduction and ONE return
        // A returned unit goes to quarantine (I-D8), so on-hand stays 7; what proves "returned once" is the single Return row of exactly +1.
        Assert.Equal(1m, await ScalarAsync<decimal>("SELECT COALESCE(SUM(quantity_delta), 0) FROM inventory.stock_ledger_entry WHERE reference_id = @s AND kind = @k", ("s", surgeryId), ("k", MovementKind.Return.Value)));
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
        Assert.Equal(1, await CountAsync("SELECT count(*) FROM pharmacy.cd_register_entry WHERE source_id = @s AND kind = @k", ("s", surgeryId), ("k", ControlledDrugEntryKind.Correction.Value)));
    }

    /// <summary>T-10 -- the entry id is held by ANOTHER VET's entry on the SAME surgery: not a replay, a 409 Duplicate, nothing written, and no hint of the first entry.</summary>
    [Fact]
    public async Task AnEntryId_HeldByAnotherVetOnTheSameSurgery_IsRefused_NotReplayed()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: false);
        var vet2 = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Vet);
        var entryId = Guid.NewGuid();
        object Body() => new
        {
            entryId, medicineId = drug.MedicineId, doseAmount = 0.5m, doseUnit = "Ml", route = "Intravenous", occurredOn = world.Clock.GetUtcNow().AddMinutes(-5), administeredById = world.VetId,
            batchId = drug.BatchId, quantityDrawn = 1m
        };

        Assert.Equal(HttpStatusCode.Created, (await LogDrugAsync(world.Vet, surgeryId, Body())).StatusCode);
        var clash = await LogDrugAsync(vet2.Client, surgeryId, Body());

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal([BusinessViolationCodes.DuplicateAnaesthesiaEntry], await CodesOfAsync(clash));
        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(1, await LedgerRowsAsync(surgeryId));
        Assert.Equal(9m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }

    /// <summary>T-14 -- DryRun: a dose the entry refuses (four decimals, zero, negative) is refused for a stocked and for a controlled drug with no ledger, register or log row.</summary>
    [Fact]
    public async Task InvalidDrugEntries_LeaveNoLogLedgerOrRegisterRow()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var stocked = await SeedDrugAsync(world, controlled: false);
        var controlled = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);

        foreach (var dose in new[] { 0.1234m, 0m, -1m })
        {
            var plain = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, stocked.MedicineId, batchId: stocked.BatchId, drawn: 1m, dose: dose));
            Assert.Equal(HttpStatusCode.BadRequest, plain.StatusCode);
            var witnessed = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, controlled.MedicineId, batchId: controlled.BatchId, drawn: 2m, wasted: 0m, witness: witness, dose: dose));
            Assert.Equal(HttpStatusCode.BadRequest, witnessed.StatusCode);
        }

        Assert.Equal(0, await LogRowsAsync(surgeryId));
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
        Assert.Equal(0, await RegisterRowsAsync(surgeryId));
        Assert.Equal(10m, await OnHandAsync(stocked.BatchId, world.DispensaryId));
        Assert.Equal(10m, await OnHandAsync(controlled.BatchId, world.DispensaryId));
    }

    /// <summary>T-15 -- DryRun on the CORRECTION path: a correction the entry refuses (bad dose) must not leave a stock return or a register Correction behind, for a stocked and a controlled drug; the
    /// corrected quantity here would otherwise have returned stock.</summary>
    [Fact]
    public async Task AnInvalidCorrection_LeavesNoStockReturnOrRegisterCorrection()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var stocked = await SeedDrugAsync(world, controlled: false);
        var controlled = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var plainEntry = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, stocked.MedicineId, batchId: stocked.BatchId, drawn: 3m)));
        var controlledEntry = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, controlled, witness, drawn: 3m, wasted: 0m)));
        var ledgerBefore = await LedgerRowsAsync(surgeryId);
        var registerBefore = await RegisterRowsAsync(surgeryId);
        var logBefore = await LogRowsAsync(surgeryId);

        foreach (var (target, w) in new[] { (plainEntry, (Witness?)null), (controlledEntry, witness) })
        {
            var response = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{target}/corrections", new
            {
                reason = "Drew up less than first recorded", occurredOn = world.Clock.GetUtcNow().AddMinutes(-3),
                drug = new { doseAmount = 0.1234m, doseUnit = "Ml", route = "Intravenous", administeredById = world.VetId, quantityDrawn = 1m, quantityWasted = 0m },
                witnessIdentifier = w?.Mobile, witnessPassword = w?.Password
            }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Equal(ledgerBefore, await LedgerRowsAsync(surgeryId));
        Assert.Equal(registerBefore, await RegisterRowsAsync(surgeryId));
        Assert.Equal(logBefore, await LogRowsAsync(surgeryId));
        Assert.Equal(7m, await OnHandAsync(stocked.BatchId, world.DispensaryId));
        Assert.Equal(7m, await OnHandAsync(controlled.BatchId, world.DispensaryId));
    }

    /// <summary>T-22 -- who can witness: a Reception or Administrator account is refused with the generic WitnessNotVerified even with the right password; another vet is accepted.</summary>
    [Fact]
    public async Task OnlyAVetOrNurse_CanWitness()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);

        foreach (var role in new[] { StaffRoles.Reception, StaffRoles.Administrator })
        {
            var member = await AddStaffAsync(world.Api, world.Admin, world.BranchId, role);
            var refused = await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, drug, new Witness(member.StaffId, member.Mobile, member.Password)));
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal([BusinessViolationCodes.WitnessNotVerified], await CodesOfAsync(refused));
        }

        Assert.Equal(0, await LogRowsAsync(surgeryId));
        Assert.Equal(0, await RegisterRowsAsync(surgeryId));
        Assert.Equal(10m, await OnHandAsync(drug.BatchId, world.DispensaryId));

        var vet2 = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Vet);
        Assert.Equal(HttpStatusCode.Created, (await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, drug, new Witness(vet2.StaffId, vet2.Mobile, vet2.Password)))).StatusCode);
    }

    /// <summary>T-23 -- three wrong witness passwords on the SURGERY route count toward the witness account's lockout like sign-in failures: the fourth attempt, with the RIGHT password, is refused, and nothing is written.</summary>
    [Fact]
    public async Task WrongWitnessPasswords_OnTheSurgeryRoute_LockTheWitnessAccount()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var wrong = await LogDrugAsync(world.Vet, surgeryId, DrugBody(world, drug.MedicineId, batchId: drug.BatchId, drawn: 1m, wasted: 0m, witness: witness, witnessPassword: "not-their-password-" + attempt));
            Assert.Equal([BusinessViolationCodes.WitnessNotVerified], await CodesOfAsync(wrong));
        }

        var locked = await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, drug, witness, drawn: 1m, wasted: 0m));
        Assert.Equal([BusinessViolationCodes.WitnessNotVerified], await CodesOfAsync(locked));
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClientOn(world.Api).PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = witness.Mobile, password = witness.Password }, Json)).StatusCode);
        Assert.True(await CountAsync("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @w AND \"Action\" = 'WitnessVerificationFailed'", ("w", witness.StaffId)) >= 3);
        Assert.Equal(0, await LogRowsAsync(surgeryId));
        Assert.Equal(0, await LedgerRowsAsync(surgeryId));
        Assert.Equal(0, await RegisterRowsAsync(surgeryId));
    }

    /// <summary>T-24 -- on the CORRECTION route the caller cannot witness their own correction, and the witness cannot be the administering vet; nothing is written.</summary>
    [Fact]
    public async Task ACorrection_CannotBeWitnessedByTheCaller_OrByTheAdministeringVet()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        var drug = await SeedDrugAsync(world, controlled: true);
        var witness = await AddWitnessAsync(world);
        var vet2 = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Vet);
        var original = await EntryIdAsync(await LogDrugAsync(world.Vet, surgeryId, ControlledBody(world, drug, witness, drawn: 3m, wasted: 0m)));
        var url = $"/api/staff/surgeries/{surgeryId}/anaesthesia-log/{original}/corrections";
        var ledger = await LedgerRowsAsync(surgeryId);
        var register = await RegisterRowsAsync(surgeryId);

        object Correction(Guid administeredBy, string mobile, string password) => new
        {
            reason = "Drew up less than first recorded", occurredOn = world.Clock.GetUtcNow().AddMinutes(-3),
            drug = new { doseAmount = 0.4m, doseUnit = "Ml", route = "Intravenous", administeredById = administeredBy, quantityDrawn = 2m, quantityWasted = 0m },
            witnessIdentifier = mobile, witnessPassword = password
        };

        var self = await world.Vet.PostAsJsonAsync(url, Correction(world.VetId, world.VetMobile, world.VetPassword), Json);
        Assert.Equal([BusinessViolationCodes.WitnessNotVerified], await CodesOfAsync(self));
        var sameAsAdministrator = await world.Vet.PostAsJsonAsync(url, Correction(vet2.StaffId, vet2.Mobile, vet2.Password), Json);
        Assert.Equal([BusinessViolationCodes.WitnessSameAsAdministrator], await CodesOfAsync(sameAsAdministrator));

        Assert.Equal(1, await LogRowsAsync(surgeryId));
        Assert.Equal(ledger, await LedgerRowsAsync(surgeryId));
        Assert.Equal(register, await RegisterRowsAsync(surgeryId));
        Assert.Equal(7m, await OnHandAsync(drug.BatchId, world.DispensaryId));
    }
}
