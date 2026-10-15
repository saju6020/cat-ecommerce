using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 phase G -- diet authorisations and the gate (US-PHM-08, BR-2, BR-6; ADR-149, ADR-152): I-G1 ... I-G7.</summary>
public sealed class PharmacyDietEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    private sealed record DietWorld(PharmacyWorld World, MedicineSku Diet);

    private async Task<DietWorld> SeedAsync(bool openVisit = true)
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: openVisit);
        var diet = await CreateMedicineSkuAsync(world.Admin, price: 50m, prescriptionDiet: true, clinicOnly: false, publish: true, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow(), name: "Renal diet " + Guid.NewGuid().ToString("N")[..6]);
        return new DietWorld(world, diet);
    }

    private static object Body(Guid visitId, Guid productId, DateOnly? from = null, DateOnly? to = null, string? notes = null) =>
        new { visitId, productId, validFrom = from ?? BranchToday, validTo = to ?? BranchToday.AddDays(30), notes };

    private static Task<HttpResponseMessage> AuthoriseAsync(HttpClient client, Guid catId, object body) =>
        client.PostAsJsonAsync($"/api/staff/cats/{catId}/diet-authorisations", body, Json);

    private static async Task<DietGateResult> GateAsync(PharmacyWorld world, Guid customerId, Guid catId, Guid skuId)
    {
        await using var scope = world.Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDietAuthorisationGate>().CheckAsync(customerId, catId, skuId, CancellationToken.None);
    }

    private static async Task<int> StoredStatusAsync(Guid authorisationId) =>
        (await ScalarAsync<int>("SELECT status FROM pharmacy.diet_authorisation WHERE \"Id\" = @id", ("id", authorisationId)));

    private static async Task AgeAsync(Guid authorisationId, int days)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("UPDATE pharmacy.diet_authorisation SET valid_from = valid_from - @d, valid_to = valid_to - @d WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("d", days);
        command.Parameters.AddWithValue("id", authorisationId);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    // ── I-G1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G1_AVetAuthorisesAPrescriptionDietFromAnOpenVisit_TheGateSaysAuthorised_AndTheOwnerIsTold()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        Assert.Equal(DietGateOutcome.NoAuthorisation, (await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Outcome);

        var response = await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId, notes: "Kidney support"));

        Assert.True(response.StatusCode == HttpStatusCode.Created, await BodyAsync(response));
        var authorisationId = (await ReadAsync(response)).GetProperty("authorisationId").GetGuid();

        var gate = await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId);
        Assert.Equal(DietGateOutcome.Authorised, gate.Outcome);
        Assert.Equal(authorisationId, gate.AuthorisationId);
        Assert.Equal(BranchToday.AddDays(30), gate.ValidTo);

        var requests = await PharmacyRepeatEndpointTests.RequestsAsync(NotificationTemplateCatalogue.PharmacyDietAuthorised, world.CustomerId);
        var only = Assert.Single(requests);
        Assert.Contains("authorisationsLink", only.Variables);
        Assert.DoesNotContain("Kidney", only.Variables); // no clinical detail in the message

        var listed = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/diet-authorisations"));
        var row = Assert.Single(listed.EnumerateArray());
        Assert.Equal("Active", row.GetProperty("status").GetString());
        Assert.Equal(world.VisitId, row.GetProperty("visitId").GetGuid());

        var visit = await ReadAsync(await world.Vet.GetAsync($"/api/staff/visits/{world.VisitId}/prescriptions"));
        Assert.Single(visit.GetProperty("dietAuthorisations").EnumerateArray());

        var portal = await ReadAsync(await world.Customer.GetAsync("/api/customers/me/diet-authorisations"));
        var mine = Assert.Single(portal.EnumerateArray());
        Assert.Equal(seeded.Diet.ProductId, mine.GetProperty("productId").GetGuid());
        Assert.Equal(world.CatId, mine.GetProperty("catId").GetGuid());

        // The product picker offers prescription products only.
        var products = await ReadAsync(await world.Vet.GetAsync($"/api/staff/diet-products?search={Uri.EscapeDataString(seeded.Diet.Name)}"));
        Assert.Contains(products.EnumerateArray(), p => p.GetProperty("productId").GetGuid() == seeded.Diet.ProductId);
    }

    // ── I-G2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G2_TheGateIsPerCatAndPerOwner_AnotherCatOfTheOwnerIsNotAuthorised_AnotherCustomerIsCatNotOwned()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        Assert.Equal(HttpStatusCode.Created, (await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId))).StatusCode);

        var otherCat = await SeedCatAsync(world.CustomerId, "Second cat");
        Assert.Equal(DietGateOutcome.NoAuthorisation, (await GateAsync(world, world.CustomerId, otherCat, seeded.Diet.SkuId)).Outcome);

        var (_, strangerId, _) = await RegisterAndSignInCustomerOnAsync(world.Api);
        Assert.Equal(DietGateOutcome.CatNotOwned, (await GateAsync(world, strangerId, world.CatId, seeded.Diet.SkuId)).Outcome);

        // A food that needs no prescription is "not required" whoever asks.
        var plain = await CreateMedicineSkuAsync(world.Admin, price: 5m, prescriptionDiet: false, clinicOnly: false, publish: true, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        Assert.Equal(DietGateOutcome.NotRequired, (await GateAsync(world, world.CustomerId, world.CatId, plain.SkuId)).Outcome);

        // The set-based form answers for every SKU in one call, with every requested SKU as a key.
        await using var scope = world.Api.Services.CreateAsyncScope();
        var many = await scope.ServiceProvider.GetRequiredService<IDietAuthorisationGate>()
            .CheckManyAsync(world.CustomerId, world.CatId, [seeded.Diet.SkuId, plain.SkuId, Guid.NewGuid()], CancellationToken.None);
        Assert.Equal(3, many.Count);
        Assert.Equal(DietGateOutcome.Authorised, many[seeded.Diet.SkuId].Outcome);
        Assert.Equal(DietGateOutcome.NotRequired, many[plain.SkuId].Outcome);

        var cats = await scope.ServiceProvider.GetRequiredService<IDietAuthorisationGate>().GetAuthorisedCatsAsync(world.CustomerId, seeded.Diet.SkuId, CancellationToken.None);
        Assert.Equal(world.CatId, Assert.Single(cats).CatId);
    }

    [Fact]
    public async Task I_G2b_ADeceasedCatIsNotSold_ARevokedAuthorisationNoLongerOpensTheGate()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var id = (await ReadAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId)))).GetProperty("authorisationId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/diet-authorisations/{id}/revoke", new { reason = "Diagnosis changed" }, Json)).StatusCode);
        Assert.Equal(DietGateOutcome.NoAuthorisation, (await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Outcome);

        var again = await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId));
        Assert.Equal(HttpStatusCode.Created, again.StatusCode); // the revoked one no longer blocks a new authorisation

        await MarkCatDeceasedAsync(world.Admin, world.CatId);
        Assert.Equal(DietGateOutcome.CatDeceased, (await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Outcome);
    }

    /// <summary>U-G4 (US-PHM-08 AC2) -- whatever the outcome, the gate's message is owner-facing and carries no diagnosis, dose or vet notes. The notes, the visit's diagnosis
    /// and a dose-like text are planted, then every outcome's message is checked.</summary>
    [Fact]
    public async Task U_G4_TheGateMessages_CarryNoDiagnosis_NoDose_AndNoVetNotes()
    {
        const string secretNotes = "CKD stage 3 creatinine 280 give 2.5 ml twice daily";
        var seeded = await SeedAsync();
        var world = seeded.World;
        var forbidden = new[] { secretNotes, "CKD", "creatinine", "2.5 ml", "mg", DiagnosisCodeSeed.RoutineNoAbnormality.Name };

        var messages = new List<string>
        {
            (await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Message // NoAuthorisation
        };

        var id = (await ReadAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId, to: BranchToday.AddDays(1), notes: secretNotes)))).GetProperty("authorisationId").GetGuid();
        messages.Add((await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Message); // Authorised

        var (_, strangerId, _) = await RegisterAndSignInCustomerOnAsync(world.Api);
        messages.Add((await GateAsync(world, strangerId, world.CatId, seeded.Diet.SkuId)).Message); // CatNotOwned

        await AgeAsync(id, 3);
        var expired = await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId);
        Assert.Equal(DietGateOutcome.AuthorisationExpired, expired.Outcome);
        messages.Add(expired.Message);

        await MarkCatDeceasedAsync(world.Admin, world.CatId);
        var deceased = await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId);
        Assert.Equal(DietGateOutcome.CatDeceased, deceased.Outcome);
        messages.Add(deceased.Message);

        Assert.All(messages, message =>
        {
            foreach (var word in forbidden) Assert.DoesNotContain(word, message, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(messages, m => m.Length > 0); // the check is not vacuous
    }

    // ── I-G3 / I-G4 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G3_OnceValidToHasPassed_TheGateLocksTheProduct_WithoutTheWorkerHavingRun()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var id = (await ReadAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId, to: BranchToday.AddDays(1))))).GetProperty("authorisationId").GetGuid();
        Assert.Equal(DietGateOutcome.Authorised, (await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Outcome);

        await AgeAsync(id, 3); // three days pass; the worker has not run

        var gate = await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId);
        Assert.Equal(DietGateOutcome.AuthorisationExpired, gate.Outcome);
        Assert.Equal(DietAuthorisationStatus.Active.Value, await StoredStatusAsync(id)); // the stored status is still Active: the gate decided from the dates
        Assert.Contains("ended on", gate.Message);

        // The staff and portal reads show the EFFECTIVE status too.
        var listed = await ReadAsync(await world.Vet.GetAsync($"/api/staff/cats/{world.CatId}/diet-authorisations?scope=all"));
        Assert.Equal("Expired", listed.EnumerateArray().Single().GetProperty("status").GetString());
        Assert.Empty((await ReadAsync(await world.Customer.GetAsync("/api/customers/me/diet-authorisations"))).EnumerateArray());
        Assert.Single((await ReadAsync(await world.Customer.GetAsync("/api/customers/me/diet-authorisations?scope=past"))).EnumerateArray());
    }

    [Fact]
    public async Task I_G4_TheWorkerRecordsTheExpiry_RaisesTheEvent_AndTellsTheOwnerOnce()
    {
        var captured = new ConcurrentBag<DietAuthorisationExpiredEvent>();
        var seeded = await SeedAsync();
        var world = seeded.World;
        var host = world.Api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IEventHandlerAsync<DietAuthorisationExpiredEvent>>(_ => new CapturingHandler(captured))));

        var id = (await ReadAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId, to: BranchToday.AddDays(1))))).GetProperty("authorisationId").GetGuid();
        await AgeAsync(id, 3);

        await RunPharmacyCycleAsync(host);

        Assert.Equal(DietAuthorisationStatus.Expired.Value, await StoredStatusAsync(id));
        var raised = Assert.Single(captured, e => e.AuthorisationId == id);
        Assert.Equal(world.CatId, raised.CatId);

        var notices = await PharmacyRepeatEndpointTests.RequestsAsync(NotificationTemplateCatalogue.PharmacyDietAuthorisationExpired, world.CustomerId);
        var notice = Assert.Single(notices);
        Assert.Contains("bookLink", notice.Variables);

        await RunNotificationDeliveryUntilAsync(world.Api, _ => Task.FromResult(
            PharmacyRepeatEndpointTests.RequestsAsync(NotificationTemplateCatalogue.PharmacyDietAuthorisationExpired, world.CustomerId).GetAwaiter().GetResult()
                .All(r => r.Status != NotificationRequestStatus.Pending.Value)));
        var inbox = await ReadAsync(await world.Customer.GetAsync("/api/customers/me/inbox"));
        Assert.Contains(inbox.GetProperty("items").EnumerateArray(), i => (i.GetProperty("body").GetString() ?? "").Contains(seeded.Diet.Name));

        // Idempotent: a second pass records nothing more and tells nobody twice.
        await RunPharmacyCycleAsync(host);
        Assert.Single(captured, e => e.AuthorisationId == id);
        Assert.Single(await PharmacyRepeatEndpointTests.RequestsAsync(NotificationTemplateCatalogue.PharmacyDietAuthorisationExpired, world.CustomerId));

        // A renewal after a short lapse re-activates it (within 30 days); the gate says yes again.
        var extend = await world.Vet.PostAsJsonAsync($"/api/staff/diet-authorisations/{id}/extend", new { validTo = BranchToday.AddDays(60) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, extend.StatusCode);
        Assert.Equal(DietGateOutcome.Authorised, (await GateAsync(world, world.CustomerId, world.CatId, seeded.Diet.SkuId)).Outcome);
    }

    private sealed class CapturingHandler(ConcurrentBag<DietAuthorisationExpiredEvent> captured) : IEventHandlerAsync<DietAuthorisationExpiredEvent>
    {
        public Task HandleAsync(DietAuthorisationExpiredEvent @event)
        {
            captured.Add(@event);
            return Task.CompletedTask;
        }
    }

    // ── I-G5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G5_AProductThatNeedsNoPrescription_AndADuplicateActiveAuthorisation_AreRefused()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var plain = await CreateMedicineSkuAsync(world.Admin, price: 5m, prescriptionDiet: false, clinicOnly: false, publish: true, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());

        await AssertErrorAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, plain.ProductId)), BusinessViolationCodes.ProductNotPrescriptionDiet);
        await AssertErrorAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, Guid.NewGuid())), BusinessViolationCodes.ProductNotFound);

        Assert.Equal(HttpStatusCode.Created, (await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId))).StatusCode);
        await AssertErrorAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId)), BusinessViolationCodes.DietAuthorisationAlreadyActive);

        // Validity: more than a year, or ending before it starts.
        var other = await CreateMedicineSkuAsync(world.Admin, price: 50m, prescriptionDiet: true, clinicOnly: false, publish: true, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        await AssertErrorAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, other.ProductId, to: BranchToday.AddDays(400))), BusinessViolationCodes.InvalidDietValidity);
        Assert.Equal(HttpStatusCode.BadRequest, (await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, other.ProductId, from: BranchToday.AddDays(5), to: BranchToday.AddDays(2)))).StatusCode);

        Assert.Equal(1, await ScalarAsync<long>("SELECT COUNT(*) FROM pharmacy.diet_authorisation WHERE cat_id = @c AND product_id = @p", ("c", world.CatId), ("p", seeded.Diet.ProductId)));
    }

    [Fact]
    public async Task I_G5b_ConcurrentDuplicates_LeaveExactlyOneRow_AndTheLoserSeesTheSameBadRequest()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId))));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var loser in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
            await AssertErrorAsync(loser, BusinessViolationCodes.DietAuthorisationAlreadyActive);
        Assert.Equal(1, await ScalarAsync<long>("SELECT COUNT(*) FROM pharmacy.diet_authorisation WHERE cat_id = @c AND product_id = @p", ("c", world.CatId), ("p", seeded.Diet.ProductId)));
    }

    [Fact]
    public async Task I_G5c_TheVisitMustBeOpen_AndBelongToTheCat()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;
        var otherCat = await SeedCatAsync(world.CustomerId, "Visitor");

        await AssertErrorAsync(await AuthoriseAsync(world.Vet, otherCat, Body(world.VisitId, seeded.Diet.ProductId)), BusinessViolationCodes.VisitCatMismatch);
        await AssertErrorAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(Guid.NewGuid(), seeded.Diet.ProductId)), BusinessViolationCodes.VisitNotFound);

        await FinaliseVisitAsync(world.Clinic, world.VisitId);
        await AssertErrorAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId)), BusinessViolationCodes.VisitNotOpen);
    }

    // ── I-G6 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G6_OnlyAVetMayAuthoriseExtendOrRevoke_TheRefusalIsAForbidden_AndAuditable()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;

        Assert.Equal(HttpStatusCode.Forbidden, (await AuthoriseAsync(world.Nurse, world.CatId, Body(world.VisitId, seeded.Diet.ProductId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthoriseAsync(world.Admin, world.CatId, Body(world.VisitId, seeded.Diet.ProductId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await AuthoriseAsync(world.Reception, world.CatId, Body(world.VisitId, seeded.Diet.ProductId))).StatusCode);
        Assert.Equal(0, await ScalarAsync<long>("SELECT COUNT(*) FROM pharmacy.diet_authorisation WHERE cat_id = @c", ("c", world.CatId)));

        var id = (await ReadAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId)))).GetProperty("authorisationId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/diet-authorisations/{id}/extend", new { validTo = BranchToday.AddDays(60) }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.PostAsJsonAsync($"/api/staff/diet-authorisations/{id}/revoke", new { reason = "x" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Nurse.GetAsync($"/api/staff/diet-products")).StatusCode);

        // Reading is open to the clinic team, not to a customer session.
        Assert.Equal(HttpStatusCode.OK, (await world.Nurse.GetAsync($"/api/staff/cats/{world.CatId}/diet-authorisations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Customer.GetAsync($"/api/staff/cats/{world.CatId}/diet-authorisations")).StatusCode);

        // Revoke needs a reason; extend must move the date forward.
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Vet.PostAsJsonAsync($"/api/staff/diet-authorisations/{id}/revoke", new { reason = "" }, Json)).StatusCode);
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync($"/api/staff/diet-authorisations/{id}/extend", new { validTo = BranchToday.AddDays(5) }, Json), BusinessViolationCodes.InvalidDietValidity);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Vet.PostAsJsonAsync($"/api/staff/diet-authorisations/{Guid.NewGuid()}/revoke", new { reason = "x" }, Json)).StatusCode);
    }

    // ── I-G7 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G7_MergingTheOwner_MovesPrescriptionsDispensingsAndAuthorisations_ButNeverTheRegisterEntries()
    {
        var seeded = await SeedAsync();
        var world = seeded.World;

        // A prescription with a dispensing, a diet authorisation, and a register entry naming the duplicate.
        var sku = await CreateMedicineSkuAsync(world.Admin, price: 120m, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var locationId = await CreateLocationAsync(world.Admin, "ClinicDispensary");
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test merge " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId], ingredients: ["meloxicam"], classes: ["nsaid"], dosingBasis: "Fixed", form: "OralLiquid", dispensingUnit: "Millilitre"));
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        var batchId = await ReceiveAsync(world.Admin, sku.SkuId, locationId, 50m, BranchToday.AddDays(300), NewLotNumber());

        var issued = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions",
            PharmacyPrescribingEndpointTests.IssueBody(world.VisitId, medicineId, dose: 2m, doseUnit: "Ml", quantity: 10m), Json);
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var prescriptionId = (await ReadAsync(issued)).GetProperty("prescriptionId").GetGuid();
        var dispensed = await world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{prescriptionId}/dispensings",
            new { lines = new[] { new { skuId = sku.SkuId, batchId, quantity = 10m } } }, Json);
        Assert.Equal(HttpStatusCode.Created, dispensed.StatusCode);
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();

        var authorisationId = (await ReadAsync(await AuthoriseAsync(world.Vet, world.CatId, Body(world.VisitId, seeded.Diet.ProductId)))).GetProperty("authorisationId").GetGuid();

        var host = PortHostFor(world, world.Clinic.VetStaffId);
        var medicine = await CreateMedicineAsync(world.Admin, MedicineBody());
        var entryIds = await EnlistAsync(host, RegisterDto(world.BranchId, medicine, ControlledDrugEntryKind.Receipt, 5m, world.Clinic.VetStaffId, catId: world.CatId, customerId: world.CustomerId));
        var entryId = Assert.Single(entryIds);

        var (survivorClient, survivorId, _) = await RegisterAndSignInCustomerOnAsync(world.Api);
        var shared = NewMobile();
        foreach (var client in new[] { world.Customer, survivorClient })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/customers/me", new { fullName = "Merged Customer", additionalPhoneNumbers = new[] { shared } }, Json)).StatusCode);
        var merge = await world.Admin.PostAsJsonAsync($"/api/staff/customers/{world.CustomerId}/merge", new { survivingCustomerId = survivorId, reason = "Same person, confirmed by phone" }, Json);
        Assert.True(merge.StatusCode == HttpStatusCode.OK, await BodyAsync(merge));

        Assert.Equal(survivorId, await ScalarAsync<Guid>("SELECT customer_id FROM pharmacy.prescription WHERE \"Id\" = @id", ("id", prescriptionId)));
        Assert.Equal(survivorId, await ScalarAsync<Guid>("SELECT customer_id FROM pharmacy.dispensing WHERE \"Id\" = @id", ("id", dispensingId)));
        Assert.Equal(survivorId, await ScalarAsync<Guid>("SELECT customer_id FROM pharmacy.diet_authorisation WHERE \"Id\" = @id", ("id", authorisationId)));

        // The register entry is append-only: it still names the duplicate; the read follows the merge to the survivor's name.
        Assert.Equal(world.CustomerId, await ScalarAsync<Guid>("SELECT customer_id FROM pharmacy.cd_register_entry WHERE \"Id\" = @id", ("id", entryId)));
        var entry = await ReadAsync(await world.Vet.GetAsync($"/api/staff/controlled-drugs/register/{entryId}"));
        var survivorName = await ScalarAsync<string>("SELECT full_name FROM account.customer_account WHERE \"Id\" = @id", ("id", survivorId));
        Assert.Equal(survivorName, entry.GetProperty("entry").GetProperty("customerName").GetString());

        // Delivering the event again changes nothing (idempotent): the rows are still the survivor's.
        Assert.Equal(survivorId, await ScalarAsync<Guid>("SELECT customer_id FROM pharmacy.prescription WHERE \"Id\" = @id", ("id", prescriptionId)));
    }
}
