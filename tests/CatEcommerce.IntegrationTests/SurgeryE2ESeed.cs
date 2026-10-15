using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// Integrator (SRG backlog row 89, 2026-10-14): seeds clearly named dev-only surgery test data for the real-API Playwright run
/// <c>web/tests/e2e/surgery-real-api.spec.ts</c>, so the HO-SRG-002 TEST gate's run can be repeated from the repo. Modelled on
/// <see cref="PharmacyE2ESeed"/>: it is SKIPPED unless <c>E2E_SEED_FILE</c> (output path) and <c>E2E_STAFF_PASSWORD</c> are set, so the normal
/// suite is unaffected, and it runs only against a per-branch database (<see cref="SurgeryApiFactory"/> refuses dev and test) -- run it once,
/// filtered to this class, on the fresh database the API and web app are then pointed at. It seeds a branch with a Theatre room, rostered vets and
/// a rostered assistant, a nurse and reception, a customer and cat, priced spay/neuter/dental services with a spay protocol, both consent forms, and
/// a controlled and a non-controlled drug with batches, using only real endpoints plus the repository path every area's tests use to seed staff.
/// The run also needs a raised sign-in rate limit and an absolute https <c>PublicSite:WebBaseUrl</c> (SRG rows 85 and 97).
/// </summary>
public sealed class SurgeryE2ESeed(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    [SkipUnlessSeedEnvironmentFact]
    public async Task SeedForRealApiRun()
    {
        var seedFile = Environment.GetEnvironmentVariable("E2E_SEED_FILE");
        var password = Environment.GetEnvironmentVariable("E2E_STAFF_PASSWORD");
        if (string.IsNullOrWhiteSpace(seedFile) || string.IsNullOrWhiteSpace(password)) return;

        var world = await SeedSurgeryWorldAsync();
        var admin = world.Admin;

        async Task<(Guid Id, string Email)> StaffAsync(string key, string role, string fullName, bool roster)
        {
            var email = $"e2e.{key}@purrcare.test";
            await using var scope = world.Api.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var staff = new StaffAccountAggregate(Guid.NewGuid());
            staff.Create(new StaffAccountCreationDto(fullName, null, EmailAddress.Create(email), [role], new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);
            staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);
            var assign = await admin.PutAsJsonAsync($"/api/admin/staff/{staff.Id}/branches", new { branchIds = new[] { world.BranchId }, primaryBranchId = world.BranchId }, Json);
            Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());
            if (roster) await RosterAllWeekAsync(admin, world.BranchId, staff.Id);
            return (staff.Id, email);
        }

        var adm = await StaffAsync("admin", StaffRoles.Administrator, "E2E Admin", false);
        var vet = await StaffAsync("vet", StaffRoles.Vet, "E2E Vet", true);
        var vet2 = await StaffAsync("vet2", StaffRoles.Vet, "E2E Vet Two", true);
        var nurse = await StaffAsync("nurse", StaffRoles.Nurse, "E2E Nurse", false);
        var assistant = await StaffAsync("assistant", StaffRoles.Nurse, "E2E Assistant", true);
        var reception = await StaffAsync("reception", StaffRoles.Reception, "E2E Reception", false);

        await EnsureConsentFormPublishedAsync(admin, "Surgery");
        await EnsureConsentFormPublishedAsync(admin, "Anaesthesia");

        var protocol = await world.Vet.PutAsJsonAsync($"/api/staff/surgery-protocols/{world.SpayServiceId}", new
        {
            fastingInstructions = "No food from midnight; water is fine until you leave home.",
            fastingHours = 8,
            postOpInstructions = "Keep her warm and quiet. Call us if the wound opens.",
            followUps = new object[]
            {
                new { kind = "WoundCheck", dayOffset = 3, windowFromDay = 2, windowToDay = 4 },
                new { kind = "SutureRemoval", dayOffset = 10, windowFromDay = 9, windowToDay = 12 }
            }
        }, Json);
        Assert.True(protocol.StatusCode == HttpStatusCode.OK, await protocol.Content.ReadAsStringAsync());

        async Task<object> DrugAsync(string name, bool controlled, string[] lots, decimal onHand, int[] expiryDays)
        {
            var sku = await CreateMedicineSkuAsync(admin, price: 120m, priceEffectiveFrom: world.Clock.GetUtcNow(), name: name + " stock");
            var medicineId = await CreateMedicineAsync(admin, MedicineBody(
                name: name, skuIds: [sku.SkuId], ingredients: [controlled ? "ketamine" : "propofol"], classes: [controlled ? "anaesthetic" : "sedative"],
                dosingBasis: "Fixed", form: "Injection", dispensingUnit: "Vial", routes: ["Intravenous"]));
            if (controlled) await SetControlledAsync(admin, medicineId);
            var batches = new List<object>();
            for (var i = 0; i < lots.Length; i++)
            {
                var batchId = await ReceiveAsync(admin, sku.SkuId, world.DispensaryId, onHand, BranchToday.AddDays(expiryDays[i]), lots[i]);
                batches.Add(new { batchId, lot = lots[i], onHand });
            }
            return new { medicineId, skuId = sku.SkuId, name, batches };
        }

        var ketamine = await DrugAsync("E2E Ketamine", true, ["E2E-KET-NEAR", "E2E-KET-FAR"], 20m, [90, 200]);
        var propofol = await DrugAsync("E2E Propofol", false, ["E2E-PRO-1"], 20m, [120]);

        var seed = new
        {
            branchId = world.BranchId,
            theatreId = world.TheatreId,
            serviceIds = new { spay = world.SpayServiceId, neuter = world.NeuterServiceId, dental = world.DentalServiceId },
            customer = new { id = world.CustomerId, mobile = world.Pharmacy.Clinic.CustomerMobile, password = "correcthorsebattery" },
            catId = world.CatId,
            // ValueTuple fields do not serialize, so each staff member is written as a named object
            staff = new[] { ("admin", adm), ("vet", vet), ("vet2", vet2), ("nurse", nurse), ("assistant", assistant), ("reception", reception) }
                .ToDictionary(s => s.Item1, s => new { id = s.Item2.Id, email = s.Item2.Email }),
            worldVet = new { id = world.VetId, mobile = world.VetMobile },
            dispensaryId = world.DispensaryId,
            ketamine,
            propofol
        };
        await File.WriteAllTextAsync(seedFile, JsonSerializer.Serialize(seed));
    }

    /// <summary>
    /// RA-N8's one direct write (SRG row 89): moves one surgery's <c>admitted_on</c> back so an anaesthesia entry at 23:55 on a past day is allowed.
    /// SKIPPED unless <c>SRG_E2E_BACKDATE_ID</c> (the surgery id) and <c>SRG_E2E_BACKDATE_TO</c> (an ISO-8601 instant) are set; per-branch database only.
    /// </summary>
    [SkipUnlessSurgeryBackdateEnvironmentFact]
    public async Task BackdateAdmissionForRealApiRun()
    {
        var id = Environment.GetEnvironmentVariable("SRG_E2E_BACKDATE_ID");
        var to = Environment.GetEnvironmentVariable("SRG_E2E_BACKDATE_TO");
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(to)) return;

        await using var connection = await OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand("UPDATE clinic.surgery SET admitted_on = @t WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("t", DateTimeOffset.Parse(to, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("id", Guid.Parse(id));
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }
}

/// <summary>A fact that is reported as SKIPPED, not passed, unless the surgery backdate environment is present (SRG row 89).</summary>
public sealed class SkipUnlessSurgeryBackdateEnvironmentFactAttribute : FactAttribute
{
    public SkipUnlessSurgeryBackdateEnvironmentFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SRG_E2E_BACKDATE_ID")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SRG_E2E_BACKDATE_TO")))
            Skip = "Real-API surgery backdate: set SRG_E2E_BACKDATE_ID and SRG_E2E_BACKDATE_TO (and CATECOMMERCE_TEST_DB to a per-branch database) to run it.";
    }
}
