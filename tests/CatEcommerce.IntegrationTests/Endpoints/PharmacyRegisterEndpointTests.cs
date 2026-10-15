using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Infrastructure.Auditing;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Pharmacy;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.QueryHandlers.Prescription;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 phase D -- the controlled-drug register and the witness (FR-PHM-7, C-2, BR-7, ADR-144 ... ADR-146): I-A3, I-B5, I-D1 ... I-D8.</summary>
public sealed class PharmacyRegisterEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    /// <summary>A world with a controlled medicine linked to a clinic-only SKU, ten units of one batch in the dispensary and a Receipt entry for them in the register.</summary>
    private sealed record ControlledWorld(PharmacyWorld World, Guid MedicineId, Guid SkuId, Guid BatchId, Guid ReceiptEntryId);

    private async Task<ControlledWorld> SeedControlledAsync(decimal quantity = 10m)
    {
        var world = await SeedPharmacyWorldAsync(openVisit: false);
        var sku = await CreateMedicineSkuAsync(world.Admin);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test ketamine " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["ketamine"], classes: ["anaesthetic"], dosingBasis: "Fixed",
            form: "Injection", dispensingUnit: "Millilitre", strength: new { amount = 10m, unit = "Mg", perQuantity = 1m, perUnit = "Millilitre" }));
        await SetControlledAsync(world.Admin, medicineId);

        var batchId = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, quantity);
        var host = PortHostFor(world, world.Clinic.VetStaffId);
        var receipt = await EnlistAsync(host, RegisterDto(world.BranchId, medicineId, ControlledDrugEntryKind.Receipt, quantity, world.Clinic.VetStaffId, skuId: sku.SkuId, batchId: batchId));
        return new ControlledWorld(world, medicineId, sku.SkuId, batchId, receipt[0]);
    }

    private async Task<(Guid StaffId, string Mobile, string Password)> WitnessAsync(PharmacyWorld world, string role = StaffRoles.Nurse)
    {
        var staff = await AddStaffAsync(world.Api, world.Admin, world.BranchId, role);
        return (staff.StaffId, staff.Mobile, staff.Password);
    }

    private static object Correction(decimal delta, string reason, string identifier, string password, string? notes = null, string? doseText = null) => new
    {
        quantityDelta = delta, reason, witnessIdentifier = identifier, witnessPassword = password, notes, correctedDoseText = doseText
    };

    // ── I-A3: the V1 spike ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A3_TwoEntriesSaved_ReadBackWithDistinctIncreasingEntryNumbers()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var medicineId = Guid.NewGuid();
        var host = PortHostFor(world, world.Clinic.VetStaffId);

        await EnlistAsync(host,
            RegisterDto(world.BranchId, medicineId, ControlledDrugEntryKind.Receipt, 5m, world.Clinic.VetStaffId),
            RegisterDto(world.BranchId, medicineId, ControlledDrugEntryKind.Receipt, 7m, world.Clinic.VetStaffId));

        var page = await ReadAsync(await world.Admin.GetAsync($"/api/staff/controlled-drugs/register?medicineId={medicineId}"));
        var numbers = page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("entryNumber").GetInt64()).ToList();
        Assert.Equal(2, numbers.Count);
        Assert.True(numbers[0] > numbers[1] && numbers[1] > 0);
    }

    // ── I-D1 / I-D2: three layers of append-only ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D1_UpdateDeleteAndTruncate_OnTheEntriesAndTheSeals_AreRefusedByTheDatabase()
    {
        var seeded = await SeedControlledAsync();
        await RunSealedPassAsync(seeded.World);

        foreach (var table in new[] { "pharmacy.cd_register_entry", "pharmacy.cd_register_seal" })
        {
            var id = seeded.ReceiptEntryId;
            Assert.Equal("PH001", await RefusedInTransactionAsync($"UPDATE {table} SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = @id", ("id", id)));
            Assert.Equal("PH001", await RefusedInTransactionAsync($"DELETE FROM {table} WHERE \"Id\" = @id", ("id", id)));
            Assert.Equal("PH001", await RefusedInTransactionAsync($"TRUNCATE {table}"));
        }

        // The entry is still there, unchanged.
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE \"Id\" = @id", ("id", seeded.ReceiptEntryId)));
        Assert.Equal(4L, await ScalarAsync<long>(
            "SELECT count(*) FROM pg_trigger WHERE tgrelid IN ('pharmacy.cd_register_entry'::regclass, 'pharmacy.cd_register_seal'::regclass) AND NOT tgisinternal"));
    }

    /// <summary>Runs a statement inside a transaction that is ALWAYS rolled back, so even a refusal that did not happen could not change the register.</summary>
    private static async Task<string?> RefusedInTransactionAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            await command.ExecuteNonQueryAsync();
            return null;
        }
        catch (PostgresException ex)
        {
            return ex.SqlState;
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    public async Task I_D2_ModifyingOrDeletingALoadedEntryOrSeal_ThrowsBeforeAnySqlIsSent()
    {
        var seeded = await SeedControlledAsync();
        await RunSealedPassAsync(seeded.World);

        await using var scope = seeded.World.Api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var entry = await db.ControlledDrugRegisterEntries.SingleAsync(e => e.Id == seeded.ReceiptEntryId);
        db.Entry(entry).Property(nameof(ControlledDrugRegisterEntryAggregate.Notes)).CurrentValue = "tampered";
        await Assert.ThrowsAsync<ControlledDrugRegisterImmutableException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var again = await db.ControlledDrugRegisterEntries.SingleAsync(e => e.Id == seeded.ReceiptEntryId);
        db.ControlledDrugRegisterEntries.Remove(again);
        await Assert.ThrowsAsync<ControlledDrugRegisterImmutableException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        var seal = await db.ControlledDrugRegisterSeals.SingleAsync(s => s.Id == seeded.ReceiptEntryId);
        db.Entry(seal).Property(nameof(ControlledDrugRegisterSealAggregate.EntryHash)).CurrentValue = new string('a', 64);
        await Assert.ThrowsAsync<ControlledDrugRegisterImmutableException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        Assert.Null((await db.ControlledDrugRegisterEntries.AsNoTracking().SingleAsync(e => e.Id == seeded.ReceiptEntryId)).Notes);
    }

    // ── I-D3 / I-D4: corrections ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D3_AVetCorrectsAnEntry_WithAVerifiedWitness_ANewLinkedEntryAndAnInventoryAdjustmentInOneTransaction()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var witness = await WitnessAsync(world);
        var before = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}"));

        var response = await world.Vet.PostAsJsonAsync(
            $"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections", Correction(-1m, "Miscounted at receipt", witness.Mobile, witness.Password, notes: "Recount"), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync(response);
        var newId = created.GetProperty("entryId").GetGuid();
        Assert.True(created.GetProperty("entryNumber").GetInt64() > before.GetProperty("entry").GetProperty("entryNumber").GetInt64());

        // The original is exactly as it was, and now lists its correction.
        var original = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}"));
        Assert.Equal(before.GetProperty("entry").GetProperty("quantityDelta").GetProperty("amount").GetString(), original.GetProperty("entry").GetProperty("quantityDelta").GetProperty("amount").GetString());
        Assert.Equal("Receipt", original.GetProperty("entry").GetProperty("kind").GetString());
        var correction = Assert.Single(original.GetProperty("corrections").EnumerateArray());
        Assert.Equal(newId, correction.GetProperty("id").GetGuid());
        Assert.Equal("Correction", correction.GetProperty("kind").GetString());
        Assert.Equal("-1.00", correction.GetProperty("quantityDelta").GetProperty("amount").GetString());
        Assert.Equal(original.GetProperty("entry").GetProperty("entryNumber").GetInt64(), correction.GetProperty("correctsEntryNumber").GetInt64());
        Assert.False(string.IsNullOrEmpty(correction.GetProperty("witnessName").GetString()));

        // Register and stock moved together: nine in the dispensary, one ledger row referencing the correction entry.
        Assert.Equal(9m, await OnHandAsync(seeded.BatchId, world.DispensaryId));
        Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM inventory.stock_ledger_entry WHERE reference_id = @id", ("id", newId)));

        // The witness's success is audited, with the source.
        Assert.Contains((await AuditRowsAsync("StaffAccount", witness.StaffId)), r => r.Action == "WitnessVerified" && r.AfterValue!.Contains("Correction"));
    }

    [Fact]
    public async Task I_D3b_AZeroQuantityCorrection_MovesNoStock_AndAWitnessIsRequiredAndGeneric()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var witness = await WitnessAsync(world);

        var fixedText = await world.Vet.PostAsJsonAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections",
            Correction(0m, "Typo in the dose text", witness.Mobile, witness.Password, doseText: "0.5 ml"), Json);
        Assert.Equal(HttpStatusCode.Created, fixedText.StatusCode);
        Assert.Equal(10m, await OnHandAsync(seeded.BatchId, world.DispensaryId));

        // Missing witness fields are a shape error; a wrong correction target is a mismatch; an unknown entry is a 404.
        var noWitness = await world.Vet.PostAsJsonAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections",
            new { quantityDelta = 0m, reason = "x reason", witnessIdentifier = "", witnessPassword = "" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noWitness.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.PostAsJsonAsync($"/api/staff/controlled-drugs/register/{Guid.NewGuid()}/corrections",
            Correction(0m, "reason", witness.Mobile, witness.Password), Json)).StatusCode);

        // The vet cannot witness their own correction, and a receptionist is not a witness role: the SAME generic refusal.
        var self = await world.Vet.PostAsJsonAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections",
            Correction(0m, "Self witness", world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword), Json);
        var reception = await WitnessAsync(world, StaffRoles.Reception);
        var receptionist = await world.Vet.PostAsJsonAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections",
            Correction(0m, "Reception witness", reception.Mobile, reception.Password), Json);
        await AssertErrorAsync(self, "WitnessNotVerified");
        await AssertErrorAsync(receptionist, "WitnessNotVerified");
        Assert.Equal(await BodyAsync(self), await BodyAsync(receptionist));
    }

    [Fact]
    public async Task I_D4_AnAdministrator_CannotCorrectTheRegister_AndTheDenialIsAudited()
    {
        var seeded = await SeedControlledAsync();
        var witness = await WitnessAsync(seeded.World);

        var response = await seeded.World.Admin.PostAsJsonAsync(
            $"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections", Correction(0m, "Admin attempt", witness.Mobile, witness.Password), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await CountDenialsAsync(seeded.World.Clinic.AdminStaffId, Actions.ControlledDrugs.Correct));

        // ...but an administrator and a nurse may READ it; reception may not.
        Assert.Equal(HttpStatusCode.OK, (await seeded.World.Admin.GetAsync("/api/staff/controlled-drugs/register")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await seeded.World.Nurse.GetAsync("/api/staff/controlled-drugs/register")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await seeded.World.Reception.GetAsync("/api/staff/controlled-drugs/register")).StatusCode);
    }

    // ── I-D5: the witness is a person ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D5_AWrongWitnessPassword_ThreeTimes_IsGenericAudited_AndLocksTheWitnessAccount()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var witness = await WitnessAsync(world);
        var url = $"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections";

        string? first = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var response = await world.Vet.PostAsJsonAsync(url, Correction(0m, "Wrong password " + attempt, witness.Mobile, "not-the-password"), Json);
            await AssertErrorAsync(response, "WitnessNotVerified");
            first ??= await BodyAsync(response);
            Assert.Equal(first, await BodyAsync(response));
        }

        // The failures counted toward the witness account's lockout exactly as sign-in failures do: even the RIGHT password is refused now, and a sign-in is 401.
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, Correction(0m, "Right password but locked", witness.Mobile, witness.Password), Json), "WitnessNotVerified");
        var signIn = await CreateClientOn(world.Api).PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = witness.Mobile, password = witness.Password }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, signIn.StatusCode);

        // Audited with the MASKED identifier -- never the raw one, never a password.
        var failures = (await AuditRowsAsync("StaffAccount", witness.StaffId)).Where(r => r.Action == "WitnessVerificationFailed").ToList();
        Assert.True(failures.Count >= 3);
        Assert.All(failures, r =>
        {
            Assert.DoesNotContain(witness.Mobile, r.AfterValue!);
            Assert.DoesNotContain("not-the-password", r.AfterValue!);
            Assert.Contains("***", r.AfterValue!);
        });

        // No correction was recorded.
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE corrects_entry_id = @id", ("id", seeded.ReceiptEntryId)));
    }

    [Fact]
    public async Task I_W1_EveryWitnessFailure_HasTheSameResultShape_WhateverTheReason()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var url = $"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections";

        var good = await WitnessAsync(world);
        var locked = await WitnessAsync(world);
        var reception = await WitnessAsync(world, StaffRoles.Reception);
        for (var i = 0; i < 3; i++)
            await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, Correction(0m, "Lock " + i, locked.Mobile, "not-the-password"), Json), "WitnessNotVerified");

        var attempts = new Dictionary<string, (string Identifier, string Password)>
        {
            ["unknown identifier"] = ("01999999999", "whatever-password"),
            ["unparseable identifier"] = ("not an identifier", "whatever-password"),
            ["wrong password"] = (good.Mobile, "not-the-password"),
            ["locked account, right password"] = (locked.Mobile, locked.Password),
            ["no witness role"] = (reception.Mobile, reception.Password),
            ["the person acting"] = (world.Clinic.Context.VetMobile, world.Clinic.Context.VetPassword),
        };

        var bodies = new Dictionary<string, string>();
        foreach (var (reason, credentials) in attempts)
        {
            var response = await world.Vet.PostAsJsonAsync(url, Correction(0m, "Witness " + reason, credentials.Identifier, credentials.Password), Json);
            await AssertErrorAsync(response, "WitnessNotVerified");
            bodies[reason] = await BodyAsync(response);
        }

        Assert.Single(bodies.Values.Distinct());
        Assert.DoesNotContain("password", bodies.Values.First(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_entry WHERE corrects_entry_id = @id", ("id", seeded.ReceiptEntryId)));
    }

    [Fact]
    public async Task I_W2_AWrongPassword_CountsTowardTheWitnessAccountsThrottle_AndIsAuditedWithTheMaskedIdentifier()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var witness = await WitnessAsync(world);
        var url = $"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections";

        var before = await ScalarAsync<long>("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @id AND \"Action\" = 'WitnessVerificationFailed'", ("id", witness.StaffId));
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, Correction(0m, "One wrong password", witness.Mobile, "not-the-password"), Json), "WitnessNotVerified");

        var after = await ScalarAsync<long>("SELECT count(*) FROM audit.audit_entry WHERE \"EntityId\" = @id AND \"Action\" = 'WitnessVerificationFailed'", ("id", witness.StaffId));
        Assert.Equal(before + 1, after);

        // The throttle counter moved: two more wrong passwords lock the account (three in all), and a fourth with the RIGHT password is still refused.
        for (var i = 0; i < 2; i++)
            await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, Correction(0m, "More " + i, witness.Mobile, "not-the-password"), Json), "WitnessNotVerified");
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, Correction(0m, "Right but locked", witness.Mobile, witness.Password), Json), "WitnessNotVerified");

        var rows = (await AuditRowsAsync("StaffAccount", witness.StaffId)).Where(r => r.Action == "WitnessVerificationFailed").ToList();
        Assert.All(rows, r => Assert.DoesNotContain(witness.Mobile, r.AfterValue!));
    }

    [Fact]
    public async Task TheWitnessEndpoints_AreRateLimited()
    {
        var tight = Factory.WithTightPharmacyRateLimits();
        var seeded = await SeedControlledAsync();
        var vet = await SignInStaffOnAsync(tight, seeded.World.Clinic.Context.VetMobile, seeded.World.Clinic.Context.VetPassword, seeded.World.BranchId);
        var url = $"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections";

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
            statuses.Add((await vet.PostAsJsonAsync(url, Correction(0m, "Rate limit " + i, "01700000000", "nope"), Json)).StatusCode);

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    // ── I-D6 / I-D7: the hash chain ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Advances the world's clock past the settle delay and runs the worker's sealing pass.</summary>
    private static async Task RunSealedPassAsync(PharmacyWorld world)
    {
        world.Clinic.Clock.Advance(TimeSpan.FromSeconds(ControlledDrugRules.SealSettleSeconds + 5));
        await RunPharmacyCycleAsync(world.Api);
    }

    [Fact]
    public async Task I_D6_TheWorkerSealsEveryEntry_AndTheVerificationReportsAnIntactChain()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var host = PortHostFor(world, world.Clinic.VetStaffId);
        var witnessId = world.Clinic.NurseStaffId;
        await EnlistAsync(host,
            RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Dispensed, -1m, world.Clinic.VetStaffId, witnessId, seeded.SkuId, seeded.BatchId, catId: world.CatId, customerId: world.CustomerId),
            RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Wastage, -0.5m, world.Clinic.VetStaffId, witnessId, seeded.SkuId, seeded.BatchId));

        // Before the settle delay nothing is sealed (an in-flight transaction with a lower number must not be skipped).
        await RunPharmacyCycleAsync(world.Api);
        var early = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/verification"));
        Assert.Equal(0, early.GetProperty("entriesChecked").GetInt32());

        await RunSealedPassAsync(world);

        var verification = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/verification"));
        Assert.True(verification.GetProperty("isIntact").GetBoolean());
        Assert.Equal(3, verification.GetProperty("entriesChecked").GetInt32());
        Assert.Empty(verification.GetProperty("staleUnsealed").EnumerateArray());

        var page = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register?medicineId={seeded.MedicineId}"));
        Assert.All(page.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("isSealed").GetBoolean()));

        // Positions are 1..3 per branch and each seal chains from the previous hash. The API shows the position and the entry hash only (I-X2); the previous hash is read from the table.
        var shown = new List<(long Position, string Hash)>();
        foreach (var item in page.GetProperty("items").EnumerateArray())
        {
            var detail = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register/{item.GetProperty("id").GetGuid()}"));
            var seal = detail.GetProperty("seal");
            Assert.False(seal.TryGetProperty("previousHash", out _));
            shown.Add((seal.GetProperty("chainPosition").GetInt64(), seal.GetProperty("entryHash").GetString()!));
        }

        var stored = new List<(long Position, string Previous, string Hash)>();
        await using (var connection = await OpenConnectionAsync())
        await using (var command = new Npgsql.NpgsqlCommand("SELECT chain_position, previous_hash, entry_hash FROM pharmacy.cd_register_seal WHERE branch_id = @b ORDER BY chain_position", connection))
        {
            command.Parameters.AddWithValue("b", world.BranchId);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) stored.Add((reader.GetInt64(0), reader.GetString(1).Trim(), reader.GetString(2).Trim()));
        }

        var ordered = shown.OrderBy(p => p.Position).ToList();
        Assert.Equal([1L, 2L, 3L], ordered.Select(p => p.Position));
        Assert.Equal(stored.Select(s => (s.Position, s.Hash)), ordered);
        Assert.Equal(ControlledDrugRules.ZeroHash, stored[0].Previous);
        Assert.Equal(stored[0].Hash, stored[1].Previous);
        Assert.Equal(stored[1].Hash, stored[2].Previous);

        // A second pass has nothing to do (idempotent).
        await RunPharmacyCycleAsync(world.Api);
        Assert.Equal(3, (await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/verification"))).GetProperty("entriesChecked").GetInt32());
    }

    [Fact]
    public async Task I_D6b_AnEntryUnsealedForTooLong_IsListedAsAFinding()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;

        world.Clinic.Clock.Advance(TimeSpan.FromMinutes(ControlledDrugRules.UnsealedAlertMinutes + 1));

        var verification = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/verification"));
        Assert.True(verification.GetProperty("isIntact").GetBoolean());
        Assert.Contains(verification.GetProperty("staleUnsealed").EnumerateArray(), e => e.GetProperty("entryNumber").GetInt64() > 0);
    }

    [Fact]
    public async Task I_D7_ABreakAroundAllThreeLayers_IsDetected_AtTheFirstBrokenPosition_AndLeavesNoTriggerDisabled()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var host = PortHostFor(world, world.Clinic.VetStaffId);
        var second = (await EnlistAsync(host, RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Receipt, 3m, world.Clinic.VetStaffId, skuId: seeded.SkuId, batchId: seeded.BatchId)))[0];
        await EnlistAsync(host, RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Receipt, 4m, world.Clinic.VetStaffId, skuId: seeded.SkuId, batchId: seeded.BatchId));
        await RunSealedPassAsync(world);

        var secondPosition = await ScalarAsync<long>("SELECT chain_position FROM pharmacy.cd_register_seal WHERE \"Id\" = @id", ("id", second));
        var originalQuantity = await ScalarAsync<decimal>("SELECT quantity_delta FROM pharmacy.cd_register_entry WHERE \"Id\" = @id", ("id", second));

        // Test-only technique (§16 I-D7): disable the trigger INSIDE a transaction, alter a quantity, verify INSIDE the same transaction, roll back.
        await using (var connection = await OpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var disable = new NpgsqlCommand("ALTER TABLE pharmacy.cd_register_entry DISABLE TRIGGER tg_cd_register_entry_immutable", connection, transaction))
                await disable.ExecuteNonQueryAsync();
            await using (var alter = new NpgsqlCommand("UPDATE pharmacy.cd_register_entry SET quantity_delta = quantity_delta + 1 WHERE \"Id\" = @id", connection, transaction))
            {
                alter.Parameters.AddWithValue("id", second);
                Assert.Equal(1, await alter.ExecuteNonQueryAsync());
            }

            var options = new DbContextOptionsBuilder<CatEcommerceDbContext>().UseNpgsql(connection).Options;
            await using var db = new CatEcommerceDbContext(options);
            await db.Database.UseTransactionAsync(transaction);

            var handler = new VerifyControlledDrugRegisterQueryHandler(db, TimeProvider.System);
            var verdict = (await handler.HandleAsync(new VerifyControlledDrugRegisterQuery
            { BranchId = world.BranchId, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid() })).Result;

            Assert.False(verdict.IsIntact);
            Assert.Equal(secondPosition, verdict.FirstBrokenPosition);

            await transaction.RollbackAsync();
        }

        // After the rollback the trigger is enabled and the data is unchanged -- and the chain is intact again.
        Assert.Equal("O", await ScalarAsync<string>("SELECT tgenabled::text FROM pg_trigger WHERE tgname = 'tg_cd_register_entry_immutable'"));
        Assert.Equal(originalQuantity, await ScalarAsync<decimal>("SELECT quantity_delta FROM pharmacy.cd_register_entry WHERE \"Id\" = @id", ("id", second)));
        Assert.True((await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/verification"))).GetProperty("isIntact").GetBoolean());
    }

    // ── I-D8: export ────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_D8_TheExport_CarriesRunningBalanceAndHashes_GuardsFormulaCells_IsNoStore_AndIsAudited()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var witness = await WitnessAsync(world);
        var correction = await world.Vet.PostAsJsonAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}/corrections",
            Correction(-1m, "Formula in notes", witness.Mobile, witness.Password, notes: "=SUM(1)"), Json);
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        await RunSealedPassAsync(world);

        var response = await world.Vet.GetAsync($"/api/staff/controlled-drugs/register/export?medicineId={seeded.MedicineId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.Contains("\"'=SUM(1)\"", csv);
        Assert.DoesNotContain("\"=SUM(1)\"", csv);
        Assert.Contains("RunningBalance", lines[0]);
        Assert.Contains("\"10\"", lines[1].Replace("10.00", "10"));
        Assert.Matches("[0-9a-f]{64}", lines[1]);

        var audit = await AuditRowsAsync("ControlledDrugRegister", world.BranchId);
        Assert.Contains(audit, r => r.Action == "ControlledDrugRegisterExported" && r.AfterValue!.Contains("Rows=2"));

        // Reception cannot export.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.GetAsync("/api/staff/controlled-drugs/register/export")).StatusCode);
    }

    // ── I-B5: the controlled flag on a medicine with register entries ──────────────────────────────────────────────────────────

    [Fact]
    public async Task I_B5_ClearingTheControlledFlag_OnAMedicineWithRegisterEntries_NeedsAReason_AndIsAudited()
    {
        var seeded = await SeedControlledAsync();
        var admin = seeded.World.Admin;

        var withoutReason = await admin.PutAsJsonAsync($"/api/admin/formulary/{seeded.MedicineId}/controlled", new { isControlled = false }, Json);
        await AssertErrorAsync(withoutReason, "ControlledFlagChangeReasonRequired");

        var withReason = await admin.PutAsJsonAsync($"/api/admin/formulary/{seeded.MedicineId}/controlled", new { isControlled = false, reason = "Reclassified by the regulator" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, withReason.StatusCode);

        var rows = await AuditRowsAsync("FormularyMedicineAggregate", seeded.MedicineId);
        var change = rows.Where(r => r.Category == AuditCategory.DataChange && r.AfterValue is not null && r.AfterValue.Contains("IsControlled")).ToList();
        Assert.NotEmpty(change);
        Assert.Contains(change, r => r.BeforeValue is not null && r.BeforeValue.Contains("true", StringComparison.OrdinalIgnoreCase) && r.AfterValue!.Contains("false", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(rows, r => r.Category == AuditCategory.BusinessEvent && r.Action == "FormularyControlledFlagChanged");

        // A medicine that has NO register entries may be cleared without a reason.
        var fresh = await CreateMedicineAsync(admin, MedicineBody());
        await SetControlledAsync(admin, fresh);
        var plain = await admin.PutAsJsonAsync($"/api/admin/formulary/{fresh}/controlled", new { isControlled = false }, Json);
        Assert.Equal(HttpStatusCode.NoContent, plain.StatusCode);
    }

    // ── reads: the register page, balances, another branch ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheRegisterPage_ShowsRunningBalances_Filters_AndAnotherBranchSeesNothing()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var host = PortHostFor(world, world.Clinic.VetStaffId);
        await EnlistAsync(host, RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Dispensed, -2m, world.Clinic.VetStaffId, world.Clinic.NurseStaffId, seeded.SkuId, seeded.BatchId,
            catId: world.CatId, customerId: world.CustomerId, doseText: "0.5 ml"));

        var page = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register?medicineId={seeded.MedicineId}"));
        var items = page.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("Dispensed", items[0].GetProperty("kind").GetString());
        Assert.Equal("8.00", items[0].GetProperty("runningBalance").GetProperty("amount").GetString());
        Assert.Equal("Millilitre", items[0].GetProperty("runningBalance").GetProperty("unit").GetString());
        Assert.Equal("10.00", items[1].GetProperty("runningBalance").GetProperty("amount").GetString());
        Assert.Equal("Mitu", items[0].GetProperty("catName").GetString());

        var onlyDispensed = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register?medicineId={seeded.MedicineId}&kind=Dispensed"));
        Assert.Equal(1, onlyDispensed.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Vet.GetAsync("/api/staff/controlled-drugs/register?kind=Nonsense")).StatusCode);

        // Another branch's register is a separate register: its staff see none of these entries, and the entry itself is a 404.
        var other = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var otherPage = await ReadAsync(await other.Vet.GetAsync($"/api/staff/controlled-drugs/register?medicineId={seeded.MedicineId}"));
        Assert.Equal(0, otherPage.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await other.Vet.GetAsync($"/api/staff/controlled-drugs/register/{seeded.ReceiptEntryId}")).StatusCode);
    }

    [Fact]
    public async Task TheBalances_ComparePerBatchRegisterWithStock_AndReportAMismatchWithoutCorrectingIt()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;

        var agreed = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/balances"));
        var row = Assert.Single(agreed.EnumerateArray(), b => b.GetProperty("medicineId").GetGuid() == seeded.MedicineId);
        Assert.Equal("10.00", row.GetProperty("registerBalance").GetProperty("amount").GetString());
        Assert.Equal("10.00", row.GetProperty("stockBalance").GetProperty("amount").GetString());
        Assert.False(row.GetProperty("isMismatch").GetBoolean());

        // An Inventory-only adjustment (no register entry) opens a difference -- reported, never auto-corrected.
        var adjust = await world.Admin.PostAsJsonAsync("/api/staff/inventory/adjustments", new
        {
            locationId = world.DispensaryId,
            lines = new[] { new { skuId = seeded.SkuId, batchId = seeded.BatchId, quantityDelta = -1m } },
            reason = "Damage", note = "Spillage not yet registered"
        }, Json);
        Assert.True(adjust.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, await BodyAsync(adjust));

        var mismatched = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/balances"));
        var after = Assert.Single(mismatched.EnumerateArray(), b => b.GetProperty("medicineId").GetGuid() == seeded.MedicineId);
        Assert.True(after.GetProperty("isMismatch").GetBoolean());
        Assert.Equal("10.00", after.GetProperty("registerBalance").GetProperty("amount").GetString());
        Assert.Equal("9.00", after.GetProperty("stockBalance").GetProperty("amount").GetString());
    }

    // ── CODE_REVIEW row 2: two sealing passes racing for the same positions ─────────────────────────────────────────────────────

    /// <summary>Wraps the real chain and holds both passes at a barrier after they have READ the same unsealed entries and tip, so both try to append the same positions.</summary>
    private sealed class RacingChain(IRegisterChain inner, Barrier barrier, System.Collections.Concurrent.ConcurrentBag<bool> outcomes) : IRegisterChain
    {
        public Task<RegisterSealTip?> GetTipAsync(Guid branchId, CancellationToken ct) => inner.GetTipAsync(branchId, ct);

        public async Task<IReadOnlyList<ControlledDrugCanonicalFields>> GetUnsealedAsync(Guid branchId, DateTimeOffset recordedBefore, int take, CancellationToken ct)
        {
            var unsealed = await inner.GetUnsealedAsync(branchId, recordedBefore, take, ct);
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(30), ct), "Both sealing passes should have read the unsealed entries.");
            return unsealed;
        }

        public async Task<bool> TryAppendAsync(IReadOnlyList<ControlledDrugRegisterSealAggregate> seals, CancellationToken ct)
        {
            var written = await inner.TryAppendAsync(seals, ct);
            outcomes.Add(written);
            return written;
        }
    }

    [Fact]
    public async Task I_D6c_TwoSealingPassesRacingForTheSamePositions_ExactlyOneWrites_TheLoserWritesNothing_AndTheChainStaysIntact()
    {
        var seeded = await SeedControlledAsync();
        var world = seeded.World;
        var witnessId = world.Clinic.NurseStaffId;
        await EnlistAsync(PortHostFor(world, world.Clinic.VetStaffId),
            RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Dispensed, -1m, world.Clinic.VetStaffId, witnessId, seeded.SkuId, seeded.BatchId, catId: world.CatId, customerId: world.CustomerId),
            RegisterDto(world.BranchId, seeded.MedicineId, ControlledDrugEntryKind.Wastage, -0.5m, world.Clinic.VetStaffId, witnessId, seeded.SkuId, seeded.BatchId));
        world.Clinic.Clock.Advance(TimeSpan.FromSeconds(ControlledDrugRules.SealSettleSeconds + 5));

        var outcomes = new System.Collections.Concurrent.ConcurrentBag<bool>();
        using var barrier = new Barrier(2);
        var racing = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IRegisterChain>(sp => new RacingChain(ActivatorUtilities.CreateInstance<EfRegisterChain>(sp), barrier, outcomes))));

        async Task PassAsync()
        {
            await using var scope = racing.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PharmacyWorkCycle>().RunSealPassAsync(onlyBranchId: world.BranchId);
        }

        await Task.WhenAll(Task.Run(PassAsync), Task.Run(PassAsync));

        // One pass won; the other hit the unique violation, detached its seals and wrote nothing.
        Assert.Equal([false, true], outcomes.OrderBy(o => o));
        Assert.Equal(3L, await ScalarAsync<long>("SELECT count(*) FROM pharmacy.cd_register_seal WHERE branch_id = @b", ("b", world.BranchId)));
        Assert.Equal(3L, await ScalarAsync<long>("SELECT count(DISTINCT chain_position) FROM pharmacy.cd_register_seal WHERE branch_id = @b", ("b", world.BranchId)));

        var verification = await ReadAsync(await world.Vet.GetAsync("/api/staff/controlled-drugs/verification"));
        Assert.True(verification.GetProperty("isIntact").GetBoolean());
        Assert.Equal(3, verification.GetProperty("entriesChecked").GetInt32());
    }
}
