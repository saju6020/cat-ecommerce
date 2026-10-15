using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 phase B -- the formulary (US-PHM-04 AC1): I-B1 ... I-B4, I-B6. I-B5 (the controlled flag on a medicine with register entries) is in
/// <see cref="PharmacyRegisterEndpointTests"/>, where register entries exist.</summary>
public sealed class PharmacyFormularyEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    [Fact]
    public async Task I_B1_AdminCreatesAMedicine_WithRangeInteractionsAndTwoLinkedSkus_AndReadsItBack()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var sku1 = await CreateMedicineSkuAsync(world.Admin);
        var sku2 = await CreateMedicineSkuAsync(world.Admin);
        var other = await CreateMedicineAsync(world.Admin, MedicineBody(ingredients: ["prednisolone"], classes: ["corticosteroid"], dosingBasis: "Fixed"));

        var code = NewMedicineCode();
        var body = MedicineBody(
            code: code, skuIds: [sku1.SkuId, sku2.SkuId], classes: ["nsaid", "oxicam"],
            interactions:
            [
                new { interactsWithMedicineId = (Guid?)other, interactsWithTerm = (string?)null, severity = "Major", note = "Do not combine with steroids." },
                new { interactsWithMedicineId = (Guid?)null, interactsWithTerm = "Aminoglycosides", severity = "Moderate", note = "Monitor kidney function." }
            ]);

        var medicineId = await CreateMedicineAsync(world.Admin, body);

        var detail = await ReadAsync(await world.Admin.GetAsync($"/api/staff/formulary/{medicineId}"));
        Assert.Equal(code, detail.GetProperty("code").GetString());
        Assert.Equal("nsaid", detail.GetProperty("drugClasses")[0].GetString());
        Assert.Equal("OralLiquid", detail.GetProperty("form").GetString());
        Assert.Equal("PerKg", detail.GetProperty("dosingBasis").GetString());
        Assert.Equal("0.05", detail.GetProperty("doseRange").GetProperty("minMgPerKg").GetString());
        Assert.Equal("1.50", detail.GetProperty("strength").GetProperty("amount").GetProperty("amount").GetString());
        Assert.Equal("Mg", detail.GetProperty("strength").GetProperty("amount").GetProperty("unit").GetString());
        Assert.Equal("1.5 mg/ml", detail.GetProperty("strength").GetProperty("text").GetString());
        Assert.False(detail.GetProperty("isControlled").GetBoolean());
        Assert.Equal(2, detail.GetProperty("linkedSkus").GetArrayLength());
        Assert.Equal(2, detail.GetProperty("interactions").GetArrayLength());

        var major = detail.GetProperty("interactions").EnumerateArray().Single(i => i.GetProperty("severity").GetString() == "Major");
        Assert.Equal(other, major.GetProperty("interactsWithMedicineId").GetGuid());
        Assert.False(string.IsNullOrEmpty(major.GetProperty("interactsWithMedicineName").GetString()));

        // The aminoglycoside term was stored normalised ("aminoglycoside"), the way matching reads it.
        var term = detail.GetProperty("interactions").EnumerateArray().Single(i => i.GetProperty("severity").GetString() == "Moderate");
        Assert.Equal("aminoglycoside", term.GetProperty("interactsWithTerm").GetString());

        // Marking it controlled is its own deliberate act (C3).
        await SetControlledAsync(world.Admin, medicineId);
        var controlled = await ReadAsync(await world.Admin.GetAsync($"/api/staff/formulary/{medicineId}"));
        Assert.True(controlled.GetProperty("isControlled").GetBoolean());
        Assert.Equal("CD Schedule 2", controlled.GetProperty("controlledSchedule").GetString());

        // It appears in the list, searchable by name and filterable by the flag.
        var list = await ReadAsync(await world.Admin.GetAsync($"/api/staff/formulary?search={code}&isControlled=true"));
        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.True(list.GetProperty("items")[0].GetProperty("isStocked").GetBoolean());
    }

    [Fact]
    public async Task I_B2_ANurseCannotCreateAMedicine_AndTheDenialIsAudited()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);

        var response = await world.Nurse.PostAsJsonAsync("/api/admin/formulary", MedicineBody(), Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, await CountDenialsAsync(world.Clinic.NurseStaffId, Actions.Formulary.Manage));

        // The same nurse may READ the formulary (Formulary.View).
        Assert.Equal(HttpStatusCode.OK, (await world.Nurse.GetAsync("/api/staff/formulary")).StatusCode);
    }

    [Fact]
    public async Task I_B3_ASkuAlreadyLinkedElsewhere_IsRefused()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var sku = await CreateMedicineSkuAsync(world.Admin);
        await CreateMedicineAsync(world.Admin, MedicineBody(skuIds: [sku.SkuId]));

        var second = await world.Admin.PostAsJsonAsync("/api/admin/formulary", MedicineBody(skuIds: [sku.SkuId]), Json);

        await AssertErrorAsync(second, "SkuAlreadyLinkedToMedicine");
    }

    [Fact]
    public async Task I_B3b_ADuplicateCode_AnUnknownSku_AndAnInactiveSku_AreRefused()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var code = NewMedicineCode();
        await CreateMedicineAsync(world.Admin, MedicineBody(code: code));

        await AssertErrorAsync(await world.Admin.PostAsJsonAsync("/api/admin/formulary", MedicineBody(code: code), Json), "FormularyCodeAlreadyExists");
        await AssertErrorAsync(await world.Admin.PostAsJsonAsync("/api/admin/formulary", MedicineBody(skuIds: [Guid.NewGuid()]), Json), "SkuNotFound");
    }

    [Fact]
    public async Task I_B3c_TheAggregateRules_ReachTheCaller()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);

        // A per-kg medicine needs a range (U-B1) and a fixed one must not have one.
        var noRange = new { code = NewMedicineCode(), name = "No range", activeIngredients = new[] { "x" }, drugClasses = Array.Empty<string>(), form = "Tablet",
            strength = new { amount = 5m, unit = "Mg", perQuantity = 1m, perUnit = "Tablet" }, defaultRoute = "Oral", allowedRoutes = new[] { "Oral" }, dosingBasis = "PerKg",
            contraindications = "", interactions = Array.Empty<object>(), dispensingUnit = "Tablet", linkedSkuIds = Array.Empty<Guid>() };
        await AssertErrorAsync(await world.Admin.PostAsJsonAsync("/api/admin/formulary", noRange, Json), "DoseRangeRequired");

        var rangeOnFixed = MedicineBody(dosingBasis: "Fixed");
        var fixedWithRange = JsonSerializer.SerializeToElement(rangeOnFixed, Json).Deserialize<Dictionary<string, object?>>(Json)!;
        fixedWithRange["doseRange"] = new { minMgPerKg = 0.1m, maxMgPerKg = 0.2m };
        await AssertErrorAsync(await world.Admin.PostAsJsonAsync("/api/admin/formulary", fixedWithRange, Json), "InvalidDoseRange");

        // An interaction with both targets (U-B4) and a range with min above max (U-B2).
        var both = MedicineBody(interactions: [new { interactsWithMedicineId = (Guid?)Guid.NewGuid(), interactsWithTerm = "x", severity = "Minor", note = "n" }]);
        await AssertErrorAsync(await world.Admin.PostAsJsonAsync("/api/admin/formulary", both, Json), "InvalidInteraction");

        var inverted = MedicineBody(doseRange: new { minMgPerKg = 0.2m, maxMgPerKg = 0.1m });
        await AssertErrorAsync(await world.Admin.PostAsJsonAsync("/api/admin/formulary", inverted, Json), "InvalidDoseRange");

        // Bad enumerations and bad shapes are 400s from the validator, not 500s.
        var badForm = MedicineBody(form: "Gel");
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Admin.PostAsJsonAsync("/api/admin/formulary", badForm, Json)).StatusCode);
        var badCode = MedicineBody(code: "lower case!");
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Admin.PostAsJsonAsync("/api/admin/formulary", badCode, Json)).StatusCode);
    }

    [Fact]
    public async Task I_B4_TwoAdminsUpdatingFromTheSameVersion_OneSucceeds_OneGets409()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var medicineId = await CreateMedicineAsync(world.Admin);
        var version = (await ReadAsync(await world.Admin.GetAsync($"/api/staff/formulary/{medicineId}"))).GetProperty("version").GetInt32();

        var first = await world.Admin.PutAsJsonAsync($"/api/admin/formulary/{medicineId}", MedicineBody(name: "Edited once", expectedVersion: version), Json);
        var second = await world.Admin.PutAsJsonAsync($"/api/admin/formulary/{medicineId}", MedicineBody(name: "Edited twice", expectedVersion: version), Json);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        await AssertErrorAsync(second, "StaleVersion", HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task I_B6_ASkuSearch_FindsAClinicOnlySku_AndNamesTheMedicineItIsLinkedTo()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var name = "Clinic only " + Guid.NewGuid().ToString("N")[..10];
        var sku = await CreateMedicineSkuAsync(world.Admin, name: name);

        var before = await ReadAsync(await world.Admin.GetAsync($"/api/admin/formulary/sku-search?term={name}"));
        var hit = Assert.Single(before.EnumerateArray());
        Assert.Equal(sku.SkuId, hit.GetProperty("skuId").GetGuid());
        Assert.True(hit.GetProperty("isClinicOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, hit.GetProperty("linkedMedicineId").ValueKind);

        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(skuIds: [sku.SkuId]));

        var after = await ReadAsync(await world.Admin.GetAsync($"/api/admin/formulary/sku-search?term={name}"));
        Assert.Equal(medicineId, Assert.Single(after.EnumerateArray()).GetProperty("linkedMedicineId").GetGuid());

        // Only the administrator manages the formulary: the picker is Formulary.Manage.
        Assert.Equal(HttpStatusCode.Forbidden, (await world.Vet.GetAsync($"/api/admin/formulary/sku-search?term={name}")).StatusCode);
    }

    [Fact]
    public async Task AnInactiveMedicine_StaysReadable_AndTheActiveFlagFiltersTheList()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false, openVisit: false);
        var code = NewMedicineCode();
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(code: code));

        var off = await world.Admin.PutAsJsonAsync($"/api/admin/formulary/{medicineId}/active", new { isActive = false }, Json);
        Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);

        var active = await ReadAsync(await world.Admin.GetAsync($"/api/staff/formulary?search={code}&isActive=true"));
        Assert.Equal(0, active.GetProperty("totalCount").GetInt32());
        var inactive = await ReadAsync(await world.Admin.GetAsync($"/api/staff/formulary?search={code}&isActive=false"));
        Assert.Equal(1, inactive.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.GetAsync($"/api/staff/formulary/{Guid.NewGuid()}")).StatusCode);
    }
}
