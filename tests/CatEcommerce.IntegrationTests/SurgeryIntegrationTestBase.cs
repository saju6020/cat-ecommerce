using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-SRG-001 §16.1 -- every test creates its own branch (through the real HO-ADM-001 endpoints) with a Theatre room, a rostered vet surgeon, a rostered nurse
/// assistant, a priced spay and a priced neuter service, a customer and a cat -- so no test depends on another's data and none disables a trigger.
/// The vet is the branch's seeded surgeon; the clock is a <see cref="FakeTimeProvider"/> so a "tomorrow at 09:00" start and the pre-op/follow-up reminder
/// times are exact.
/// </summary>
public abstract class SurgeryIntegrationTestBase(SurgeryApiFactory factory) : PharmacyIntegrationTestBase(factory), IClassFixture<SurgeryApiFactory>
{
    protected new SurgeryApiFactory Factory => factory;

    protected sealed record SurgeryWorld(
        PharmacyWorld Pharmacy,
        Guid TheatreId,
        Guid AssistantId,
        string AssistantMobile,
        string AssistantPassword,
        HttpClient Assistant,
        Guid SpayServiceId,
        Guid NeuterServiceId,
        Guid DentalServiceId)
    {
        public HttpClient Admin => Pharmacy.Admin;
        public HttpClient Vet => Pharmacy.Vet;
        public HttpClient Nurse => Pharmacy.Nurse;
        public HttpClient Reception => Pharmacy.Reception;
        public HttpClient Customer => Pharmacy.Customer;
        public Guid CatId => Pharmacy.CatId;
        public Guid BranchId => Pharmacy.BranchId;
        public Guid CustomerId => Pharmacy.CustomerId;
        public Guid VetId => Pharmacy.Clinic.VetStaffId;
        public Guid NurseId => Pharmacy.Clinic.NurseStaffId;
        public Guid DispensaryId => Pharmacy.DispensaryId;
        public FakeTimeProvider Clock => Pharmacy.Clinic.Clock;
        public WebApplicationFactory<Program> Api => Pharmacy.Api;
        public string VetMobile => Pharmacy.Clinic.Context.VetMobile;
        public string VetPassword => Pharmacy.Clinic.Context.VetPassword;
    }

    /// <summary>The standard world: theatre, vet surgeon (the branch's), nurse assistant, spay + neuter + dental services (the dental one is a theatre
    /// procedure that is NOT a sterilisation).</summary>
    protected async Task<SurgeryWorld> SeedSurgeryWorldAsync(DateTimeOffset? start = null, Action<IServiceCollection>? configureServices = null)
    {
        // The world clock is FAKE and starts at 02:00 UTC (08:00 Dhaka) on today's UTC date: "10:00 today" is 2 h ahead, "10:00 tomorrow" is outside the
        // 12-hour early-admission window, and a weight recorded today (the real UTC date the cat endpoints validate against) is inside the pre-op weight window --
        // whatever time of day the suite actually runs at.
        var pharmacy = await SeedPharmacyWorldAsync(start ?? new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero).AddHours(2), withLocations: true, openVisit: false, configureServices);
        var admin = pharmacy.Admin;
        var branchId = pharmacy.BranchId;

        var theatreId = await CreateRoomAsync(admin, branchId, "Theatre");

        var (assistantClient, assistantId, mobile, password) = await AddStaffAsync(pharmacy.Api, admin, branchId, StaffRoles.Nurse);
        await RosterAllWeekAsync(admin, branchId, assistantId);

        var taxClassId = await CreateTaxClassAsync(admin);
        var now = pharmacy.Clinic.Clock.GetUtcNow();
        var spay = await CreateTheatreServiceAsync(admin, taxClassId, "Spay", 60, now);
        var neuter = await CreateTheatreServiceAsync(admin, taxClassId, "Neuter", 45, now);
        var dental = await CreateTheatreServiceAsync(admin, taxClassId, "Dental", 30, now);

        return new SurgeryWorld(pharmacy, theatreId, assistantId, mobile, password, assistantClient, spay, neuter, dental);
    }

    protected static async Task RosterAllWeekAsync(HttpClient admin, Guid branchId, Guid staffId)
    {
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var slots = days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray();
        var response = await admin.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{staffId}/pattern", new { slots }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    protected static async Task<Guid> CreateTaxClassAsync(HttpClient admin)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode(), name = "Standard " + NewCode() }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();
    }

    /// <summary>A priced Vet-qualified theatre service. <paramref name="price"/> is effective from <paramref name="now"/>.</summary>
    protected static async Task<Guid> CreateTheatreServiceAsync(HttpClient admin, Guid taxClassId, string category, int minutes, DateTimeOffset now, decimal price = 5000m)
    {
        var create = await admin.PostAsJsonAsync("/api/admin/services", new
        {
            code = NewCode(), name = $"{category} {NewCode()}", description = "Theatre procedure", category, durationMinutes = minutes,
            qualifiedRoleIds = new[] { StaffRoles.Vet }, requiredRoomKind = "Theatre", taxClassId, isBookableOnline = false
        }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var serviceId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();

        var priced = await admin.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices", new { amount = price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), currencyCode = "BDT", effectiveFrom = now }, Json);
        Assert.True(priced.StatusCode == HttpStatusCode.Created, await priced.Content.ReadAsStringAsync());
        return serviceId;
    }

    /// <summary>A start at the given branch-local time (Asia/Dhaka, +06:00 -- the seeded branch zone) <paramref name="daysAhead"/> local days after the world clock, always on the
    /// 15-minute grid and well inside the 00:00-23:45 rosters, whatever time of day the suite runs at.</summary>
    protected static DateTimeOffset LocalStart(SurgeryWorld world, int daysAhead, int hour = 10, int minute = 0) => LocalStart(world.Clock, daysAhead, hour, minute);

    protected static DateTimeOffset LocalStart(FakeTimeProvider clock, int daysAhead, int hour = 10, int minute = 0)
    {
        var offset = TimeSpan.FromHours(6);
        var localToday = clock.GetUtcNow().ToOffset(offset).Date;
        return new DateTimeOffset(localToday.AddDays(daysAhead).AddHours(hour).AddMinutes(minute), offset);
    }

    // ── booking and the world's usual moves ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>The world's local date (the fake clock's, in the branch zone).</summary>
    protected static DateOnly LocalToday(SurgeryWorld world) => DateOnly.FromDateTime(world.Clock.GetUtcNow().ToOffset(TimeSpan.FromHours(6)).DateTime);

    protected static object BookBody(
        SurgeryWorld world, DateTimeOffset startsOn, Guid? serviceId = null, Guid? theatreId = null, Guid? surgeonId = null, Guid? assistantId = null, Guid? catId = null,
        Guid? customerId = null, bool confirmNeuterDiscrepancy = false, Guid? campaignId = null, string? eligibilityNote = null) => new
    {
        customerId = customerId ?? world.CustomerId, catId = catId ?? world.CatId, serviceId = serviceId ?? world.SpayServiceId, startsOn,
        theatreId = theatreId ?? world.TheatreId, surgeonId = surgeonId ?? world.VetId, assistantId = assistantId ?? world.AssistantId,
        confirmNeuterStatusDiscrepancy = confirmNeuterDiscrepancy, campaignId, campaignEligibilityNote = eligibilityNote
    };

    protected static Task<HttpResponseMessage> TryBookAsync(HttpClient client, object body) => client.PostAsJsonAsync("/api/staff/surgeries", body, Json);

    /// <summary>Books a surgery and returns its id; fails the test with the body when it is refused.</summary>
    protected async Task<Guid> BookAsync(SurgeryWorld world, DateTimeOffset startsOn, Guid? serviceId = null, Guid? catId = null, Guid? customerId = null, HttpClient? client = null)
    {
        var response = await TryBookAsync(client ?? world.Reception, BookBody(world, startsOn, serviceId, catId: catId, customerId: customerId));
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("surgeryId").GetGuid();
    }

    protected static async Task<JsonElement> GetSurgeryAsync(HttpClient client, Guid surgeryId)
    {
        var response = await client.GetAsync($"/api/staff/surgeries/{surgeryId}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    protected static async Task<string> ErrorCodesAsync(HttpResponseMessage response) => await response.Content.ReadAsStringAsync();

    /// <summary>The error codes of a 400 body.</summary>
    protected static async Task<string[]> CodesOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("errorCode").GetString()!).ToArray();
    }

    protected static async Task<JsonElement[]> ErrorsOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("errors").EnumerateArray().ToArray();
    }

    protected static async Task Ok204Async(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>A cat of the given sex and neuter status for the customer (registered through the aggregate, like the other suites).</summary>
    protected async Task<Guid> SeedCatOfAsync(Guid ownerId, CatSex sex, bool neutered = false, string name = "Tilly")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();

        var user = new UserContext(Guid.NewGuid(), Guid.NewGuid());
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, name, "Cat", sex, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2), DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby",
            DateOnly.FromDateTime(DateTime.UtcNow), user, Guid.NewGuid()));
        if (neutered)
            cat.RecordNeuterStatus(new CatNeuterStatusDto(NeuterStatus.Neutered, DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-3), DateOnly.FromDateTime(DateTime.UtcNow), user, Guid.NewGuid()));

        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    /// <summary>Publishes the consent form of a kind when missing (a shared database keeps it from run to run) and returns the current version number.</summary>
    protected static async Task<int> EnsureConsentFormPublishedAsync(HttpClient admin, string kind)
    {
        var key = ConsentForms.KeyFor(Enumeration.FromDisplayName<ConsentKind>(kind));
        if ((await admin.GetAsync($"/api/staff/consent-forms/{kind}")).StatusCode != HttpStatusCode.OK)
        {
            var create = await admin.PostAsJsonAsync("/api/admin/policies", new { key, title = $"Consent - {kind}", requiresAcceptance = false }, Json);
            Assert.True(create.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, await create.Content.ReadAsStringAsync());
            var publish = await admin.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = $"I consent to the {kind.ToLowerInvariant()} described.", changeSummary = "Initial wording" }, Json);
            Assert.True(publish.StatusCode == HttpStatusCode.Created, await publish.Content.ReadAsStringAsync());
        }

        var form = await admin.GetAsync($"/api/staff/consent-forms/{kind}");
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        return (await form.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("versionNumber").GetInt32();
    }

    /// <summary>Records one consent (through HO-EMR-001's real endpoint) for the surgery's appointment. Returns its id.</summary>
    protected static async Task<Guid> RecordConsentAsync(SurgeryWorld world, Guid appointmentId, string kind = "Surgery", Guid? catId = null)
    {
        var version = await EnsureConsentFormPublishedAsync(world.Admin, kind);
        decimal[][][] strokes = [[[0.10m, 0.20m], [0.20m, 0.30m], [0.30m, 0.25m], [0.40m, 0.40m], [0.50m, 0.35m], [0.60m, 0.50m]], [[0.15m, 0.60m], [0.35m, 0.62m], [0.55m, 0.58m], [0.75m, 0.65m], [0.85m, 0.60m]]];
        var response = await world.Reception.PostAsJsonAsync("/api/staff/consents", new
        {
            catId = catId ?? world.CatId, appointmentId, kind, procedureDescription = "Spay under general anaesthesia", method = "OnScreenSignature", signature = strokes,
            signedByName = "Rahim Uddin", signerRelationship = "Owner", signedByCustomerId = world.CustomerId, policyVersionNumber = version
        }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("consentId").GetGuid();
    }

    /// <summary>Both consents a surgery requires (surgery and anaesthesia).</summary>
    protected static async Task<(Guid Surgery, Guid Anaesthesia)> RecordBothConsentsAsync(SurgeryWorld world, Guid appointmentId, Guid? catId = null) =>
        (await RecordConsentAsync(world, appointmentId, "Surgery", catId), await RecordConsentAsync(world, appointmentId, "Anaesthesia", catId));

    /// <summary>Everything the checklist needs short of consent: fasting (nurse), bloods waived (vet), weight (the cat endpoint, so the EVENT completes the item), ASA grade (vet).</summary>
    protected async Task CompleteChecklistExceptConsentAsync(SurgeryWorld world, Guid surgeryId, Guid? catId = null)
    {
        await Ok204Async(await world.Nurse.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/checklist/FastingInstructionsIssued/complete", new { note = "Told the owner by phone" }, Json));
        await Ok204Async(await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/checklist/PreAnaestheticBloodWork/waive", new { reason = "Young healthy cat" }, Json));
        await RecordWeightAsync(world.Nurse, catId ?? world.CatId, 3.4m, LocalToday(world));
        await Ok204Async(await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/asa-grade", new { grade = "I", isEmergency = false }, Json));
    }

    /// <summary>Admits the surgery (the world clock must be inside the admission window).</summary>
    protected static async Task AdmitAsync(SurgeryWorld world, Guid surgeryId) =>
        await Ok204Async(await world.Reception.PostAsync($"/api/staff/surgeries/{surgeryId}/admit", null));

    /// <summary>Book (10:00 today), complete the checklist, record both consents, admit -- a surgery ready for a vet to start.</summary>
    protected async Task<(Guid SurgeryId, Guid AppointmentId)> ReadyToStartAsync(SurgeryWorld world, Guid? serviceId = null, Guid? catId = null, bool consent = true, int hour = 10)
    {
        var surgeryId = await BookAsync(world, LocalStart(world, 0, hour), serviceId, catId);
        var appointmentId = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("appointmentId").GetGuid();
        await CompleteChecklistExceptConsentAsync(world, surgeryId, catId);
        if (consent) await RecordBothConsentsAsync(world, appointmentId, catId);
        await AdmitAsync(world, surgeryId);
        return (surgeryId, appointmentId);
    }

    /// <summary>Start (consent on record) and return the surgery id: InTheatre.</summary>
    protected async Task<(Guid SurgeryId, Guid AppointmentId)> InTheatreAsync(SurgeryWorld world, Guid? serviceId = null, Guid? catId = null, int hour = 10)
    {
        var ready = await ReadyToStartAsync(world, serviceId, catId, hour: hour);
        await Ok204Async(await world.Nurse.PostAsync($"/api/staff/surgeries/{ready.SurgeryId}/start", null));
        return ready;
    }

    // ── SQL helpers (read-only, plus the rolled-back-transaction trigger probes) ───────────────────────────────────────────────

    protected static async Task<int> CountAsync(string sql, params (string Name, object Value)[] parameters) =>
        Convert.ToInt32(await ScalarAsync<long>(sql, parameters));
}
