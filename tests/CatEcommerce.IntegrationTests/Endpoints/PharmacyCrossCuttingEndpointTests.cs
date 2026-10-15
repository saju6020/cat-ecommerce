using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-PHM-001 §16 cross-cutting cases: I-A1 (schema), I-A2 (host), I-X1 (logs), I-X2 (response bodies), I-X3 (AC-4 end to end).</summary>
public sealed class PharmacyCrossCuttingEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    // ── I-A1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A1_TheMigrations_CreateThePharmacySchema_ItsTables_BothSequences_AndTheRegisterTriggers()
    {
        string[] tables =
        [
            "formulary_medicine", "formulary_interaction", "formulary_medicine_sku", "prescription", "prescription_warning", "repeat_request", "dispensing", "dispensing_line",
            "cd_register_entry", "cd_register_seal", "diet_authorisation"
        ];

        foreach (var table in tables)
            Assert.Equal(1L, await ScalarAsync<long>("SELECT count(*) FROM information_schema.tables WHERE table_schema = 'pharmacy' AND table_name = @t", ("t", table)));

        Assert.Equal(2L, await ScalarAsync<long>("SELECT count(*) FROM information_schema.sequences WHERE sequence_schema = 'pharmacy'"));

        // The register is append-only at the database too: UPDATE and DELETE triggers on the entries and on the seals (the fourth layer-3 guard is TRUNCATE).
        var triggers = await ScalarAsync<long>(
            "SELECT count(*) FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace " +
            "WHERE n.nspname = 'pharmacy' AND c.relname IN ('cd_register_entry', 'cd_register_seal') AND NOT t.tgisinternal AND t.tgenabled <> 'D'");
        Assert.True(triggers >= 4, $"expected at least four enabled register triggers, found {triggers}");

        // Every filtered unique index exists with the predicate written from the enumeration.
        Assert.Equal($"(status = {DietAuthorisationStatus.Active.Value})",
            (await ScalarAsync<string>("SELECT pg_get_expr(i.indpred, i.indrelid) FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = 'ix_diet_authorisation_active'")));
        Assert.Equal($"(status = {DispensingStatus.Dispensed.Value})",
            (await ScalarAsync<string>("SELECT pg_get_expr(i.indpred, i.indrelid) FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = 'ix_dispensing_supply'")));
    }

    // ── I-A2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void I_A2_EveryPharmacyHandlerValidatorQueryAndPort_ResolvesFromARequestScope()
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var assemblies = new[]
        {
            typeof(CatEcommerce.Application.Commands.Prescription.AuthoriseDietCommand).Assembly,
            typeof(CatEcommerce.Read.Queries.Prescription.GetCatDietAuthorisationsQuery).Assembly,
        };

        var handlerShapes = new[]
        {
            typeof(Platform.Infrastructure.Core.Commands.ICommandHandlerAsync<>), typeof(Platform.Infrastructure.Core.Queries.IQueryHandlerAsync<,>),
            typeof(FluentValidation.IValidator<>), typeof(Platform.Infrastructure.Core.Events.IEventHandlerAsync<>)
        };

        var resolved = 0;
        foreach (var type in assemblies.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false } && (t.Namespace ?? "").Contains(".Prescription") || (t.Namespace ?? "").EndsWith(".Validators") && t.Name.Contains("Diet")))
        {
            foreach (var service in type.GetInterfaces().Where(i => i.IsGenericType && handlerShapes.Contains(i.GetGenericTypeDefinition())))
            {
                var instance = provider.GetServices(service).ToList();
                Assert.Contains(instance, i => i!.GetType() == type);
                resolved++;
            }
        }

        Assert.True(resolved > 40, $"only {resolved} pharmacy registrations were found - the scan is out of date");

        foreach (var port in new[]
        {
            typeof(CatEcommerce.Domain.DomainServices.IDietAuthorisationGate), typeof(CatEcommerce.Domain.DomainServices.IControlledDrugRegister),
            typeof(CatEcommerce.Domain.DomainServices.IControlledDrugDirectory), typeof(CatEcommerce.Domain.DomainServices.IStaffWitnessVerifier),
            typeof(CatEcommerce.Domain.DomainServices.IFormularyReader), typeof(CatEcommerce.Domain.DomainServices.ICatalogueReader),
            typeof(CatEcommerce.Domain.DomainServices.ISkuPriceReader), typeof(CatEcommerce.Domain.DomainServices.IPharmacyClock),
            typeof(CatEcommerce.Domain.DomainServices.ISafetyInputLoader), typeof(CatEcommerce.Domain.DomainServices.IPrescriptionNumberAllocator),
            typeof(CatEcommerce.Domain.DomainServices.IRegisterChain), typeof(CatEcommerce.Domain.DomainServices.IPharmacyRecordFinder),
            typeof(CatEcommerce.Api.Background.PharmacyWorkCycle)
        })
            Assert.NotNull(provider.GetRequiredService(port));

        // The real gate replaced the fail-closed fake.
        Assert.IsType<CatEcommerce.Infrastructure.Pharmacy.DietAuthorisationGate>(provider.GetRequiredService<CatEcommerce.Domain.DomainServices.IDietAuthorisationGate>());
        Assert.Contains(provider.GetServices<CatEcommerce.Domain.DomainServices.INotificationSubjectGuard>(), g => g.Kind == NotificationSubjectKind.PrescriptionRefillDue);
        Assert.Contains(provider.GetServices<CatEcommerce.Domain.DomainServices.IRetentionReviewSource>(), s => s.SourceKey == "pharmacy-clinical");
    }

    // ── I-X1 / I-X2: one journey, two assertions ────────────────────────────────────────────────

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

    private sealed class Journey(List<string> bodies)
    {
        public async Task<JsonElement> GetAsync(HttpClient client, string url)
        {
            var response = await client.GetAsync(url);
            var text = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, $"{url} -> {(int)response.StatusCode}: {text}");
            bodies.Add(text);
            return JsonDocument.Parse(text).RootElement.Clone();
        }

        public async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
        {
            var response = await send();
            var text = await response.Content.ReadAsStringAsync();
            bodies.Add(text);
            response.Content = new StringContent(text, System.Text.Encoding.UTF8, "application/json");
            return response;
        }
    }

    private static IEnumerable<string> PropertyNames(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    yield return property.Name;
                    foreach (var inner in PropertyNames(property.Value)) yield return inner;
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                foreach (var inner in PropertyNames(item)) yield return inner;
                break;
        }
    }

    [Fact]
    public async Task I_X1_X2_ATrailOfEveryKindOfRequest_LogsNoSecretOrClinicalText_AndReturnsNoPasswordOrHashField()
    {
        var token = Guid.NewGuid().ToString("N")[..12];
        var instructionsText = $"Instructions-{token}";
        var allergenText = $"allergen{token}";
        var notesText = $"DietNotes-{token}";
        var wrongPassword = $"wrong-{token}-pw";
        var logs = new CapturingLoggerProvider();
        var bodies = new List<string>();
        var journey = new Journey(bodies);

        var world = await SeedPharmacyWorldAsync(configureServices: services => services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs)));
        var witness = await AddStaffAsync(world.Api, world.Admin, world.BranchId, StaffRoles.Nurse);

        var sku = await CreateMedicineSkuAsync(world.Admin, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(
            name: "Test ketamine " + token, skuIds: [sku.SkuId], ingredients: ["ketamine"], classes: ["anaesthetic"], dosingBasis: "Fixed", form: "Injection",
            dispensingUnit: "Millilitre", strength: new { amount = 10m, unit = "Mg", perQuantity = 1m, perUnit = "Millilitre" }));
        await SetControlledAsync(world.Admin, medicineId);
        var batchId = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 20m, BranchToday.AddDays(200), NewLotNumber());
        var host = PortHostFor(world, world.Clinic.VetStaffId);
        var receipt = await EnlistAsync(host, RegisterDto(world.BranchId, medicineId, ControlledDrugEntryKind.Receipt, 20m, world.Clinic.VetStaffId, skuId: sku.SkuId, batchId: batchId));
        await SetAllergiesAsync(world.Vet, world.CatId, allergenText);

        // A wrong witness password, then a prescription with distinctive instructions, dispensed under a verified witness.
        var url = $"/api/staff/controlled-drugs/register/{receipt[0]}/corrections";
        await AssertErrorAsync(await journey.SendAsync(() => world.Vet.PostAsJsonAsync(url,
            new { quantityDelta = 0m, reason = "Check", witnessIdentifier = witness.Mobile, witnessPassword = wrongPassword }, Json)), "WitnessNotVerified");

        var issueBody = PharmacyPrescribingEndpointTests.IssueBody(world.VisitId, medicineId, dose: 2m, doseUnit: "Ml", quantity: 5m);
        issueBody["instructions"] = instructionsText;
        var issued = await journey.SendAsync(() => world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", issueBody, Json));
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var prescriptionId = (await ReadAsync(issued)).GetProperty("prescriptionId").GetGuid();

        var dispensed = await journey.SendAsync(() => world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{prescriptionId}/dispensings",
            new { lines = new[] { new { skuId = sku.SkuId, batchId, quantity = 5m } }, witnessIdentifier = witness.Mobile, witnessPassword = witness.Password }, Json));
        Assert.True(dispensed.StatusCode == HttpStatusCode.Created, await BodyAsync(dispensed));
        var dispensingId = (await ReadAsync(dispensed)).GetProperty("dispensingId").GetGuid();

        // A diet authorisation with distinctive notes.
        var diet = await CreateMedicineSkuAsync(world.Admin, price: 50m, prescriptionDiet: true, clinicOnly: false, publish: true, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var authorised = await journey.SendAsync(() => world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/diet-authorisations",
            new { visitId = world.VisitId, productId = diet.ProductId, validFrom = BranchToday, validTo = BranchToday.AddDays(30), notes = notesText }, Json));
        Assert.Equal(HttpStatusCode.Created, authorised.StatusCode);

        // Seal the register so the chain fields exist, then read everything a person can read.
        world.Clinic.Clock.Advance(TimeSpan.FromSeconds(ControlledDrugRules.SealSettleSeconds + 5));
        await RunPharmacyCycleAsync(world.Api);

        await journey.GetAsync(world.Vet, "/api/staff/formulary");
        await journey.GetAsync(world.Vet, $"/api/staff/formulary/{medicineId}");
        await journey.GetAsync(world.Vet, $"/api/staff/cats/{world.CatId}/prescriptions?scope=all");
        await journey.GetAsync(world.Vet, $"/api/staff/prescriptions/{prescriptionId}");
        await journey.GetAsync(world.Vet, $"/api/staff/visits/{world.VisitId}/prescriptions");
        await journey.GetAsync(world.Vet, $"/api/staff/cats/{world.CatId}/diet-authorisations?scope=all");
        await journey.GetAsync(world.Nurse, $"/api/staff/dispensings/{dispensingId}");
        await journey.GetAsync(world.Nurse, "/api/staff/pharmacy/queue");
        await journey.GetAsync(world.Vet, $"/api/staff/controlled-drugs/register?medicineId={medicineId}");
        var entries = await journey.GetAsync(world.Vet, $"/api/staff/controlled-drugs/register?medicineId={medicineId}");
        foreach (var item in entries.GetProperty("items").EnumerateArray())
            await journey.GetAsync(world.Vet, $"/api/staff/controlled-drugs/register/{item.GetProperty("id").GetGuid()}");
        await journey.GetAsync(world.Vet, "/api/staff/controlled-drugs/balances");
        await journey.GetAsync(world.Vet, "/api/staff/controlled-drugs/verification");
        await journey.GetAsync(world.Customer, "/api/customers/me/prescriptions?scope=active");
        await journey.GetAsync(world.Customer, $"/api/customers/me/prescriptions/{prescriptionId}");
        await journey.GetAsync(world.Customer, "/api/customers/me/diet-authorisations");

        // I-X2: no field named like a password or a hash, except the entry hash of a register read.
        var offending = bodies.Where(b => b.TrimStart().StartsWith('{') || b.TrimStart().StartsWith('['))
            .SelectMany(b => PropertyNames(JsonDocument.Parse(b).RootElement))
            .Where(name => Regex.IsMatch(name, "password|hash", RegexOptions.IgnoreCase) && name != "entryHash")
            .Distinct().ToList();
        Assert.True(offending.Count == 0, "Response fields that look like secrets: " + string.Join(", ", offending));
        Assert.Contains(bodies, b => b.Contains("\"entryHash\""));

        // ...and no body ever echoes the witness password.
        Assert.DoesNotContain(bodies, b => b.Contains(witness.Password) || b.Contains(wrongPassword));

        // I-X1: nothing the host logged - at any level - contains a secret, free clinical text or the owner's name.
        var lines = logs.Lines.ToList();
        Assert.NotEmpty(lines);
        foreach (var secret in new[] { witness.Password, wrongPassword, instructionsText, allergenText, notesText })
            Assert.DoesNotContain(lines, l => l.Contains(secret, StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("Test Customer", StringComparison.Ordinal));
    }

    // ── I-X3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_X3_Ac4EndToEnd_OutOfRangeAndAllergyWarned_Dispensed_DeductedLabelled_AndTheOwnerSeesItInThePortal()
    {
        var world = await SeedPharmacyWorldAsync();
        var sku = await CreateMedicineSkuAsync(world.Admin, priceEffectiveFrom: world.Clinic.Clock.GetUtcNow());
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(name: "Test meloxicam " + Guid.NewGuid().ToString("N")[..6], skuIds: [sku.SkuId]));
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        await SetAllergiesAsync(world.Vet, world.CatId, "Meloxicam");
        var batchId = await ReceiveAsync(world.Admin, sku.SkuId, world.DispensaryId, 10m, BranchToday.AddDays(120), NewLotNumber());

        // Step 1 - the safety check warns of the out-of-range dose AND the allergy conflict.
        var url = $"/api/staff/cats/{world.CatId}/prescriptions";
        var check = await ReadAsync(await world.Vet.PostAsJsonAsync($"{url}/safety-check",
            new { medicineId, dose = new { amount = 0.4m, unit = "Ml" }, route = "Oral", frequency = "OnceDaily" }, Json));
        var kinds = check.GetProperty("warnings").EnumerateArray().Select(w => w.GetProperty("kind").GetString()).ToList();
        Assert.Contains("DoseAboveRange", kinds);
        Assert.Contains("AllergyConflict", kinds);
        var fingerprint = check.GetProperty("fingerprint").GetString();

        // Step 2 - it cannot be issued unacknowledged; acknowledged with the fingerprint and a reason, it is.
        await AssertErrorAsync(await world.Vet.PostAsJsonAsync(url, PharmacyPrescribingEndpointTests.IssueBody(world.VisitId, medicineId, dose: 0.4m, doseUnit: "Ml", quantity: 10m), Json),
            "PrescriptionWarningsNotAcknowledged");
        var issued = await world.Vet.PostAsJsonAsync(url, PharmacyPrescribingEndpointTests.IssueBody(
            world.VisitId, medicineId, dose: 0.4m, doseUnit: "Ml", quantity: 10m, fingerprint: fingerprint, allergyReason: "Owner confirms the earlier reaction was to a different drug",
            doseNote: "Short course, reviewed"), Json);
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);
        var prescriptionId = (await ReadAsync(issued)).GetProperty("prescriptionId").GetGuid();

        // Step 3 - dispensed: the batch is deducted and the label is produced.
        var dispensed = await world.Nurse.PostAsJsonAsync($"/api/staff/prescriptions/{prescriptionId}/dispensings",
            new { lines = new[] { new { skuId = sku.SkuId, batchId, quantity = 10m } } }, Json);
        Assert.Equal(HttpStatusCode.Created, dispensed.StatusCode);
        var label = (await ReadAsync(dispensed)).GetProperty("label");
        Assert.Equal("Give 0.4 ml by mouth once daily for 5 days", label.GetProperty("doseLine").GetString());
        Assert.Equal(0m, await OnHandAsync(batchId, world.DispensaryId));

        // Step 4 - the owner sees the prescription in the portal.
        var portal = await ReadAsync(await world.Customer.GetAsync("/api/customers/me/prescriptions"));
        var mine = Assert.Single(portal.EnumerateArray(), p => p.GetProperty("id").GetGuid() == prescriptionId);
        Assert.Equal(world.CatId, mine.GetProperty("catId").GetGuid());
        Assert.Equal("Completed", mine.GetProperty("status").GetString()); // the only supply is out; the course is still running, so it is listed as active
    }
}
