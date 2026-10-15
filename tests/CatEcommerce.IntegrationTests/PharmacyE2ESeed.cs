using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-PHM-002 TEST gate (2026-10-11): seeds clearly named dev-only clinical test data (owner-approved 2026-10-09) for the real-API Playwright run
/// <c>web/tests/e2e/pharmacy-real-api.spec.ts</c>. It does nothing unless <c>E2E_SEED_FILE</c> (output path) and <c>E2E_STAFF_PASSWORD</c> are set, so the
/// normal suite is unaffected. It runs only against a per-branch database (<see cref="PharmacyApiFactory"/> refuses dev and test) -- run it once on the fresh
/// database the API and web app are then pointed at. It uses only real endpoints, plus the repository path every area's tests use to seed staff.
/// </summary>
public sealed class PharmacyE2ESeed(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    [SkipUnlessSeedEnvironmentFact]
    public async Task SeedForRealApiRun()
    {
        var seedFile = Environment.GetEnvironmentVariable("E2E_SEED_FILE");
        var password = Environment.GetEnvironmentVariable("E2E_STAFF_PASSWORD");
        if (string.IsNullOrWhiteSpace(seedFile) || string.IsNullOrWhiteSpace(password)) return;

        // a controlled clock, as SeedClinicalWorldAsync does, so a service price can be effective "now" without waiting
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var api = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(clock);
            RealClockCookieExpiry.Apply(services);
        }));
        var (admin, context, _) = await SeedBranchWithAdminAssignedAsync(api, clock.GetUtcNow());

        async Task<(Guid Id, string Email)> StaffAsync(string key, string role, string fullName)
        {
            var email = $"e2e.{key}@purrcare.test";
            await using var scope = api.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var staff = new StaffAccountAggregate(Guid.NewGuid());
            staff.Create(new StaffAccountCreationDto(fullName, null, EmailAddress.Create(email), [role], new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
            staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);
            var assign = await admin.PutAsJsonAsync($"/api/admin/staff/{staff.Id}/branches", new { branchIds = new[] { context.BranchId }, primaryBranchId = context.BranchId }, Json);
            Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
            return (staff.Id, email);
        }

        await StaffAsync("admin", StaffRoles.Administrator, "E2E admin");
        var vetAcc = await StaffAsync("vet", StaffRoles.Vet, "E2E vet");
        await StaffAsync("vet2", StaffRoles.Vet, "E2E vet2");
        var nurseAcc = await StaffAsync("nurse", StaffRoles.Nurse, "E2E nurse");
        await StaffAsync("reception", StaffRoles.Reception, "E2E reception");

        var vet = await SignInStaffOnAsync(api, vetAcc.Email, password, context.BranchId);
        var nurse = await SignInStaffOnAsync(api, nurseAcc.Email, password, context.BranchId);

        var (_, customerId, mobile) = await RegisterAndSignInCustomerOnAsync(api);
        var mitu = await SeedCatAsync(customerId, "E2E Mitu");
        var penny = await SeedCatAsync(customerId, "E2E Penny");
        await RecordWeightAsync(nurse, mitu, 4.10m);
        await RecordWeightAsync(nurse, penny, 3.80m);
        await SetAllergiesAsync(vet, penny, "penicillin");
        var mituVisit = await OpenVisitAsync(nurse, await RegisterCheckedInWalkInAsync(admin, customerId, mitu, context.ServiceId));
        var pennyVisit = await OpenVisitAsync(nurse, await RegisterCheckedInWalkInAsync(admin, customerId, penny, context.ServiceId));

        var dispensary = await CreateLocationAsync(admin, "ClinicDispensary");
        var warehouse = await CreateLocationAsync(admin, "Warehouse");
        var quarantine = await CreateLocationAsync(admin, "Quarantine");

        var meloxSku = await CreateMedicineSkuAsync(admin, name: "E2E Meloxicam stock");
        var amoxSku = await CreateMedicineSkuAsync(admin, name: "E2E Amoxicillin stock");
        var predSku = await CreateMedicineSkuAsync(admin, name: "E2E Prednisolone stock");
        var bupreSku = await CreateMedicineSkuAsync(admin, name: "E2E Buprenorphine stock");
        var expdSku = await CreateMedicineSkuAsync(admin, name: "E2E Expired stock");
        var tramadolSku = await CreateMedicineSkuAsync(admin, name: "E2E Tramadol stock");
        var foodSku = await CreateMedicineSkuAsync(admin, prescriptionDiet: true, name: "E2E Urinary Support", priceEffectiveFrom: clock.GetUtcNow(), publish: true);

        var melox = await CreateMedicineAsync(admin, MedicineBody(code: "E2E-MELOX", name: "E2E Meloxicam", skuIds: [meloxSku.SkuId]));
        var amox = await CreateMedicineAsync(admin, MedicineBody(
            code: "E2E-AMOX", name: "E2E Amoxicillin", ingredients: ["amoxicillin"], classes: ["penicillin"], dosingBasis: "Fixed", skuIds: [amoxSku.SkuId],
            strength: new { amount = 50m, unit = "Mg", perQuantity = 1m, perUnit = "Millilitre" }));
        var pred = await CreateMedicineAsync(admin, MedicineBody(
            code: "E2E-PRED", name: "E2E Prednisolone", ingredients: ["prednisolone"], classes: ["corticosteroid"], dosingBasis: "Fixed", form: "Tablet", dispensingUnit: "Tablet",
            skuIds: [predSku.SkuId], strength: new { amount = 5m, unit = "Mg", perQuantity = 1m, perUnit = "Tablet" },
            interactions: [new { interactsWithMedicineId = (Guid?)melox, interactsWithTerm = (string?)null, severity = "Major", note = "Risk of gastric ulceration with NSAIDs." }]));
        var bupre = await CreateMedicineAsync(admin, MedicineBody(
            code: "E2E-BUPRE", name: "E2E Buprenorphine", ingredients: ["buprenorphine"], classes: ["opioid"], dosingBasis: "Fixed", skuIds: [bupreSku.SkuId],
            strength: new { amount = 0.3m, unit = "Mg", perQuantity = 1m, perUnit = "Millilitre" }));
        await SetControlledAsync(admin, bupre, true, "CD Schedule 3");
        var expd = await CreateMedicineAsync(admin, MedicineBody(
            code: "E2E-EXPD", name: "E2E Expired Drug", ingredients: ["expiredium"], classes: ["other"], dosingBasis: "Fixed", skuIds: [expdSku.SkuId],
            strength: new { amount = 10m, unit = "Mg", perQuantity = 1m, perUnit = "Millilitre" }));

        var meloxNear = await ReceiveAsync(admin, meloxSku.SkuId, dispensary, 400m, BranchToday.AddDays(90), "E2E-MELOX-NEAR");
        var meloxFar = await ReceiveAsync(admin, meloxSku.SkuId, dispensary, 400m, BranchToday.AddDays(200), "E2E-MELOX-FAR");
        var meloxOld = await ReceiveAsync(admin, meloxSku.SkuId, dispensary, 50m, BranchToday.AddDays(5), "E2E-MELOX-OLD");
        await SetBatchExpiryDirectlyAsync(meloxOld, new DateOnly(2026, 10, 1));
        var amoxBatch = await ReceiveAsync(admin, amoxSku.SkuId, dispensary, 300m, BranchToday.AddDays(120), "E2E-AMOX-1");
        var predBatch = await ReceiveAsync(admin, predSku.SkuId, dispensary, 300m, BranchToday.AddDays(120), "E2E-PRED-1");
        var bupreBatch = await ReceiveAsync(admin, bupreSku.SkuId, dispensary, 100m, BranchToday.AddDays(200), "E2E-BUPRE-1");
        var expdBatch = await ReceiveAsync(admin, expdSku.SkuId, dispensary, 25m, BranchToday.AddDays(5), "E2E-EXPD-OLD");
        await SetBatchExpiryDirectlyAsync(expdBatch, new DateOnly(2026, 10, 1));

        var seed = new
        {
            branchId = context.BranchId,
            serviceId = context.ServiceId,
            customer = new { id = customerId, mobile, password = "correcthorsebattery" },
            cats = new { mitu, penny },
            visits = new { mitu = mituVisit, penny = pennyVisit },
            meds = new { melox, amox, pred, bupre, expd },
            skus = new { melox = meloxSku.SkuId, amox = amoxSku.SkuId, pred = predSku.SkuId, bupre = bupreSku.SkuId, expd = expdSku.SkuId, tramadol = tramadolSku.SkuId, food = foodSku.SkuId },
            batches = new { meloxNear, meloxFar, meloxOld, amox = amoxBatch, pred = predBatch, bupre = bupreBatch, expd = expdBatch },
            locs = new { dispensary, warehouse, quarantine },
        };
        await File.WriteAllTextAsync(seedFile, JsonSerializer.Serialize(seed));
    }
}

/// <summary>A fact that is reported as SKIPPED, not passed, unless the seeding environment is present (backlog PHM 55).</summary>
public sealed class SkipUnlessSeedEnvironmentFactAttribute : FactAttribute
{
    public SkipUnlessSeedEnvironmentFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_SEED_FILE")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_STAFF_PASSWORD")))
            Skip = "Real-API seeder: set E2E_SEED_FILE and E2E_STAFF_PASSWORD (and CATECOMMERCE_TEST_DB to a per-branch database) to run it.";
    }
}
