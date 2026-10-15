using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Queries;
using Xunit;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-POR-001 §16 groups A and B for the sections built so far: visits (Phase B, EMR), balances (Phase C, BIL) alongside the Phase A
/// sections. Upstream data is arranged through the owning modules' own endpoints and ports (§16.1 rule 2); every test mints its own customers.
/// The prescriptions, orders, stays and subscriptions sections are not built (HO-PHM-001, HO-ORD-001, HO-BRD-001, HO-SUB-001 are not merged).</summary>
public sealed class PortalDashboardSectionsEndpointTests(PortalApiFactory factory, ITestOutputHelper output) : PortalIntegrationTestBase(factory)
{
    private const decimal InvoiceAmount = 1500m;
    private const string SecretDischargeText = "Keep the ear dry and give the drops twice a day.";

    private sealed record Scenario(
        PortalCustomer Customer, Guid Mitu, Guid Rex, Guid Gone, Guid FinalisedVisit, Guid DraftVisit, Guid GoneVisit, Guid AppointmentId, Guid InvoiceId);

    private static readonly SemaphoreSlim ScenarioLock = new(1, 1);
    private static Scenario? _shared;

    /// <summary>The I-B2 customer: two alive cats (one with a finalised visit, one with only a draft), a deceased cat that had a finalised
    /// visit, a booked appointment, and an issued invoice. Built once for the class (the customer is read-only afterwards).</summary>
    private async Task<Scenario> SharedScenarioAsync()
    {
        await ScenarioLock.WaitAsync();
        try { return _shared ??= await BuildScenarioAsync(Factory); }
        finally { ScenarioLock.Release(); }
    }

    private async Task<Scenario> BuildScenarioAsync(PortalApiFactory host)
    {
        _ = host;
        var clinic = await SeedBookableBranchAsync();
        var taxClassId = await PrepareBillingAsync(clinic);
        var customer = await RegisterCustomerAsync("Nusrat Jahan");

        var mitu = await RegisterCatAsync(customer, "Mitu");
        var rex = await RegisterCatAsync(customer, "Rex");
        var gone = await RegisterCatAsync(customer, "Departed");

        var finalised = await FinaliseVisitAsync(clinic, customer.CustomerId, mitu, SecretDischargeText);
        var (draft, _) = await OpenDraftVisitAsync(clinic, customer.CustomerId, rex);
        var goneVisit = await FinaliseVisitAsync(clinic, customer.CustomerId, gone);
        await MarkCatDeceasedAsync(gone);

        var appointmentId = await BookConfirmedAppointmentAsync(customer.Client, clinic, mitu, BookableSlot());
        var invoiceId = await IssueInvoiceAsync(clinic, taxClassId, customer.CustomerId, InvoiceAmount);

        return new Scenario(customer, mitu, rex, gone, finalised, draft, goneVisit, appointmentId, invoiceId);
    }

    // I-B2 (sections built so far) -- US-POR-01 AC1
    [Fact]
    public async Task I_B2_EveryBuiltSectionIsReadyAndPopulated_DeceasedCatAndDraftVisitAreAbsent()
    {
        var s = await SharedScenarioAsync();

        var board = await GetDashboardAsync(s.Customer.Client);

        foreach (var section in new[] { "customer", "cats", "appointments", "balances", "visits" })
            Assert.Equal("Ready", board.GetProperty(section).GetProperty("state").GetString());

        var cats = board.GetProperty("cats").GetProperty("data").GetProperty("items").EnumerateArray().Select(c => c.GetProperty("catId").GetGuid()).Order().ToList();
        Assert.Equal(new[] { s.Mitu, s.Rex }.Order().ToList(), cats);

        // Appointments Phase A reads Appointments' own "upcoming" scope, which also lists the walk-ins the visit arrangement checked in.
        var booked = board.GetProperty("appointments").GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(a => a.GetProperty("appointmentId").GetGuid() == s.AppointmentId);
        Assert.Equal("Booked", booked.GetProperty("status").GetString());
        Assert.True(board.GetProperty("appointments").GetProperty("data").GetProperty("items").GetArrayLength() <= PortalRules.DashboardItemLimit);

        // Visits: only the finalised visit of an alive cat. The draft visit (Rex) and the deceased cat's finalised visit never appear.
        var visit = Assert.Single(board.GetProperty("visits").GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(s.FinalisedVisit, visit.GetProperty("visitId").GetGuid());
        Assert.Equal(s.Mitu, visit.GetProperty("catId").GetGuid());
        Assert.Equal("Mitu", visit.GetProperty("catName").GetString());
        Assert.True(visit.GetProperty("hasDischargeSummary").GetBoolean());
        Assert.NotEqual(s.DraftVisit, visit.GetProperty("visitId").GetGuid());
        Assert.NotEqual(s.GoneVisit, visit.GetProperty("visitId").GetGuid());

        var balances = board.GetProperty("balances").GetProperty("data");
        Assert.True(balances.GetProperty("hasOutstanding").GetBoolean());
        var outstanding = Assert.Single(balances.GetProperty("outstanding").EnumerateArray());
        Assert.Equal(InvoiceAmount, decimal.Parse(outstanding.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal("BDT", outstanding.GetProperty("currencyCode").GetString());
        Assert.Equal(1, balances.GetProperty("openInvoiceCount").GetInt32());
        Assert.Equal(s.InvoiceId, Assert.Single(balances.GetProperty("invoices").EnumerateArray()).GetProperty("invoiceId").GetGuid());
        Assert.Equal(PortalMessages.HowToPayOffline, balances.GetProperty("howToPay").GetString());
    }

    // I-B7 -- ADR-262
    [Fact]
    public async Task I_B7_TheRawDashboardJson_HasNoClinicalKeyAnywhere_AndNoDischargeText()
    {
        var s = await SharedScenarioAsync();
        var response = await s.Customer.Client.GetAsync("/api/portal/dashboard");
        var raw = await response.Content.ReadAsStringAsync();
        var board = JsonDocument.Parse(raw).RootElement;

        string[] forbidden = ["diagnosis", "soap", "dose", "frequency", "strength", "instructions", "allergies", "handlingnote", "notes", "problems", "treatments"];
        var keys = new List<string>();
        CollectKeys(board, keys);

        Assert.NotEmpty(keys);
        Assert.DoesNotContain(keys, k => forbidden.Contains(k.ToLowerInvariant()));
        Assert.DoesNotContain(SecretDischargeText, raw);
        Assert.DoesNotContain("Otitis", raw);
    }

    private static void CollectKeys(JsonElement element, List<string> keys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) { keys.Add(property.Name); CollectKeys(property.Value, keys); }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) CollectKeys(item, keys);
                break;
        }
    }

    // I-B3 -- ADR-261 (IDOR): each customer sees only their own records, and nothing the client sends changes whose dashboard it is
    [Fact]
    public async Task I_B3_TwoCustomers_EachDashboardHasOnlyItsOwnRecords_AndAForgedCustomerIdIsIgnored()
    {
        var clinic = await SeedBookableBranchAsync();
        var taxClassId = await PrepareBillingAsync(clinic);
        var first = await RegisterCustomerAsync("Owner One");
        var second = await RegisterCustomerAsync("Owner Two");

        var firstCat = await RegisterCatAsync(first, "OneCat");
        var secondCat = await RegisterCatAsync(second, "TwoCat");
        var firstVisit = await FinaliseVisitAsync(clinic, first.CustomerId, firstCat);
        var secondVisit = await FinaliseVisitAsync(clinic, second.CustomerId, secondCat);
        var firstInvoice = await IssueInvoiceAsync(clinic, taxClassId, first.CustomerId, 300m);
        var secondInvoice = await IssueInvoiceAsync(clinic, taxClassId, second.CustomerId, 700m);

        var firstBoard = await GetDashboardAsync(first.Client);
        var secondBoard = await GetDashboardAsync(second.Client);

        Assert.Equal("Owner One", firstBoard.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());
        Assert.Equal("Owner Two", secondBoard.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());
        Assert.Equal([firstCat], Ids(firstBoard, "cats", "catId"));
        Assert.Equal([secondCat], Ids(secondBoard, "cats", "catId"));
        Assert.Equal([firstVisit], Ids(firstBoard, "visits", "visitId"));
        Assert.Equal([secondVisit], Ids(secondBoard, "visits", "visitId"));
        Assert.Equal([firstInvoice], Ids(firstBoard, "balances", "invoiceId", "invoices"));
        Assert.Equal([secondInvoice], Ids(secondBoard, "balances", "invoiceId", "invoices"));
        Assert.Equal("300.00", firstBoard.GetProperty("balances").GetProperty("data").GetProperty("outstanding")[0].GetProperty("amount").GetString());

        // A client cannot choose whose dashboard it gets: the query has no customer-id property, and a forged one is simply never bound.
        var forged = await first.Client.GetAsync($"/api/portal/dashboard?customerId={second.CustomerId}&ownerId={second.CustomerId}");
        await ExpectAsync(HttpStatusCode.OK, forged);
        var forgedBoard = await ReadAsync(forged);
        Assert.Equal([firstCat], Ids(forgedBoard, "cats", "catId"));
        Assert.Equal("Owner One", forgedBoard.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());
    }

    private static List<Guid> Ids(JsonElement board, string section, string idName, string list = "items") =>
        board.GetProperty(section).GetProperty("data").GetProperty(list).EnumerateArray().Select(i => i.GetProperty(idName).GetGuid()).ToList();

    // I-B1 (the sections built so far) -- a customer with nothing recorded gets Ready/empty everywhere
    [Fact]
    public async Task I_B1_NewCustomer_VisitsAndBalancesAreReadyAndEmpty_NotUnavailable()
    {
        var customer = await RegisterCustomerAsync();

        var board = await GetDashboardAsync(customer.Client);

        Assert.Equal("Ready", board.GetProperty("visits").GetProperty("state").GetString());
        Assert.Empty(board.GetProperty("visits").GetProperty("data").GetProperty("items").EnumerateArray());

        Assert.Equal("Ready", board.GetProperty("balances").GetProperty("state").GetString());
        var balances = board.GetProperty("balances").GetProperty("data");
        Assert.False(balances.GetProperty("hasOutstanding").GetBoolean());
        Assert.Empty(balances.GetProperty("outstanding").EnumerateArray());
        Assert.Empty(balances.GetProperty("overdue").EnumerateArray());
        Assert.Equal(0, balances.GetProperty("openInvoiceCount").GetInt32());
        Assert.Empty(balances.GetProperty("invoices").EnumerateArray());
    }

    // I-B5 -- ADR-260 with a test-host override (a throwing fake), not a production switch
    [Fact]
    public async Task I_B5_ABalancesHandlerThatThrows_Leaves200_WithBalancesUnavailableAndNullData_AndOneErrorLogWithTheCorrelationId()
    {
        var (logged, logs) = Factory.WithCapturedLogs();
        using var host = logged.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var existing = services.Single(d => d.ServiceType == typeof(IQueryHandlerAsync<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel>));
            services.Remove(existing);
            services.AddScoped<IQueryHandlerAsync<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel>, ThrowingBillingSummaryHandler>();
        }));

        var customer = await RegisterCustomerAsync("Nusrat Jahan", host);
        await RegisterCatAsync(customer, "Mitu");

        var response = await customer.Client.GetAsync("/api/portal/dashboard");
        await ExpectAsync(HttpStatusCode.OK, response);
        var board = await ReadAsync(response);

        Assert.Equal("Unavailable", board.GetProperty("balances").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, board.GetProperty("balances").GetProperty("data").ValueKind);
        foreach (var section in new[] { "customer", "cats", "appointments", "visits" })
            Assert.Equal("Ready", board.GetProperty(section).GetProperty("state").GetString());
        Assert.Equal("Mitu", board.GetProperty("cats").GetProperty("data").GetProperty("items")[0].GetProperty("name").GetString());

        // The captured line is "{category}: {message}[ | {exception}]" (the level is asserted by unit test U-B1).
        var errorLines = logs.Where(l => l.Contains("Portal section balances unavailable", StringComparison.Ordinal)).ToList();
        var line = Assert.Single(errorLines);
        Assert.StartsWith("CatEcommerce.Read.QueryHandlers.Portal.PortalSectionComposer: ", line, StringComparison.Ordinal);
        Assert.Matches(@"CorrelationId=[0-9a-fA-F-]{36}", line);
        Assert.Contains("billing is down", line, StringComparison.Ordinal); // the exception is attached to the entry
        Assert.DoesNotContain("Nusrat", line); // I-X1: no customer data on the line
    }

    private sealed class ThrowingBillingSummaryHandler : IQueryHandlerAsync<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel>
    {
        public Task<QueryResponse<CustomerBillingSummaryViewModel>> HandleAsync(GetCustomerBillingSummaryQuery query) =>
            throw new InvalidOperationException("billing is down");
    }

    // I-A2 -- no persistence (ADR-257): the model has no Portal entity or table, and no migration mentions the portal.
    [Fact]
    public async Task I_A2_ThePortalOwnsNoEntityTableOrMigration()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var entityNames = db.Model.GetEntityTypes().Select(e => e.Name + "|" + e.GetTableName() + "|" + e.GetSchema()).ToList();
        Assert.DoesNotContain(entityNames, n => n.Contains("portal", StringComparison.OrdinalIgnoreCase));

        var migrations = typeof(CatEcommerceDbContext).Assembly.GetTypes().Where(t => t.Namespace?.EndsWith("Migrations", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(migrations, t => t.Name.Contains("Portal", StringComparison.Ordinal));
    }

    // I-B8 -- NFR-PRF-3 (acceptance A17): 20 sequential dashboard calls for a customer with records in EVERY built section; p95 <= 1500 ms.
    // The customer has a profile, two alive cats (one with a vaccination record), a booked appointment, a finalised visit, an ACTIVE prescription
    // issued through Pharmacy's vet endpoint, an issued (outstanding) invoice and a PLACED order made through Ordering's customer endpoints.
    // Not measured here: the 12-cat worst case (POR 1) and the stays / subscriptions sections (deferred by the owner, not built).
    [Fact]
    public async Task I_B8_TwentySequentialDashboardCalls_P95IsWithinOneAndAHalfSeconds()
    {
        var clinic = await SeedBookableBranchAsync();
        var taxClassId = await PrepareBillingAsync(clinic);
        var customer = await RegisterCustomerAsync("Latency Customer");
        var mitu = await RegisterCatAsync(customer, "Mitu");
        var rex = await RegisterCatAsync(customer, "Rex");

        await RecordVaccinationAsync(clinic, mitu);
        await FinaliseVisitAsync(clinic, customer.CustomerId, mitu);
        var (draftVisit, vet) = await OpenDraftVisitAsync(clinic, customer.CustomerId, rex);
        await PharmacyPrescribingHelper.IssueActivePrescriptionAsync(clinic.Admin, vet, rex, draftVisit);
        await BookConfirmedAppointmentAsync(customer.Client, clinic, mitu, BookableSlot());
        await IssueInvoiceAsync(clinic, taxClassId, customer.CustomerId, InvoiceAmount);
        using (var ordering = new OrderingShopHelper(new OrderingApiFactory()))
            await ordering.PlaceCashOnDeliveryOrderAsync(customer.Mobile, CustomerPassword, customer.CustomerId);

        // The measured board really is populated in every built section (otherwise the timing proves nothing).
        var board = await GetDashboardAsync(customer.Client);
        foreach (var section in new[] { "customer", "cats", "appointments", "balances", "visits", "prescriptions", "orders" })
            Assert.Equal("Ready", board.GetProperty(section).GetProperty("state").GetString());
        Assert.Equal("Latency Customer", board.GetProperty("customer").GetProperty("data").GetProperty("fullName").GetString());
        Assert.Equal(2, board.GetProperty("cats").GetProperty("data").GetProperty("items").GetArrayLength());
        Assert.NotEmpty(board.GetProperty("appointments").GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Single(board.GetProperty("visits").GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(rex, Assert.Single(board.GetProperty("prescriptions").GetProperty("data").GetProperty("byCat").EnumerateArray()).GetProperty("catId").GetGuid());
        Assert.NotEmpty(board.GetProperty("balances").GetProperty("data").GetProperty("invoices").EnumerateArray()); // the clinic invoice and the order's own
        Assert.Single(board.GetProperty("orders").GetProperty("data").GetProperty("items").EnumerateArray());

        var timings = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var watch = Stopwatch.StartNew();
            await ExpectAsync(HttpStatusCode.OK, await customer.Client.GetAsync("/api/portal/dashboard"));
            timings.Add(watch.Elapsed.TotalMilliseconds);
        }

        timings.Sort();
        var p95 = timings[(int)Math.Ceiling(0.95 * timings.Count) - 1];
        output.WriteLine($"I-B8 dashboard p95 = {p95:F0} ms (min {timings[0]:F0}, median {timings[timings.Count / 2]:F0}, max {timings[^1]:F0}) over {timings.Count} calls, 2 cats, all 7 built sections populated: customer, cats, appointments, balances, visits, prescriptions, orders");
        Assert.True(p95 <= 1500, $"p95 {p95:F0} ms exceeds the 1500 ms budget");
    }

    private async Task RecordVaccinationAsync(ClinicContext clinic, Guid catId)
    {
        var vaccine = await clinic.Admin.PostAsJsonAsync("/api/vaccines", new
        {
            code = $"POR-{Guid.NewGuid():N}"[..20], name = "Portal latency vaccine", isCore = false, validityMonths = 12, description = (string?)null,
        }, Json);
        await ExpectAsync(HttpStatusCode.Created, vaccine);
        var vaccineId = (await ReadAsync(vaccine)).GetProperty("vaccineId").GetGuid();

        var vet = await SignInStaffOnBranchAsync(clinic.VetMobile, clinic.VetPassword, clinic.BranchId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await ExpectAsync(HttpStatusCode.Created, await vet.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId, batchReference = $"B-{Guid.NewGuid():N}"[..20], batchExpiresOn = today.AddMonths(13), administeredOn = today,
            administeredByVetId = clinic.VetStaffAccountId, notes = (string?)null,
        }, Json));
    }

    // I-X1 (sections built so far) -- nothing sensitive reaches the log: no customer name, mobile, discharge text or money amount
    [Fact]
    public async Task I_X1_TheLogsOfADashboardRead_CarryNoCustomerNameMobileMoneyOrDischargeText()
    {
        const decimal amount = 7351.42m;
        var clinic = await SeedBookableBranchAsync();
        var taxClassId = await PrepareBillingAsync(clinic);
        var (logged, logs) = Factory.WithCapturedLogs();
        using var host = logged.WithWebHostBuilder(_ => { });

        var customer = await RegisterCustomerAsync("Zainab Quddus", host);
        var cat = await RegisterCatAsync(customer, "Logged");
        await FinaliseVisitAsync(clinic, customer.CustomerId, cat, SecretDischargeText);
        await IssueInvoiceAsync(clinic, taxClassId, customer.CustomerId, amount);
        var before = logs.Count;

        var board = await GetDashboardAsync(customer.Client);
        await GetDashboardAsync(customer.Client);
        await GetDashboardAsync(customer.Client);

        // The amount really is on the board (so its absence from the log is meaningful) ...
        Assert.Equal("7351.42", board.GetProperty("balances").GetProperty("data").GetProperty("outstanding")[0].GetProperty("amount").GetString());
        Assert.True(logs.Count > before, "the dashboard reads produced no log lines at all");

        // ... and appears in no captured line (registration, arrangement and the three reads), in any common rendering.
        var lines = logs.ToList();
        Assert.All(lines, l => Assert.DoesNotContain("Zainab", l));
        Assert.All(lines, l => Assert.DoesNotContain(customer.Mobile, l));
        Assert.DoesNotContain(lines, l => l.Contains(SecretDischargeText, StringComparison.Ordinal));
        Assert.All(lines, l => Assert.DoesNotContain("7351", l));
        Assert.All(lines, l => Assert.DoesNotContain("7,351", l));
    }
}

/// <summary>Issues an active prescription through Pharmacy's own admin (formulary) and vet (weight, prescribe) endpoints, on the portal host, for a cat
/// that has an open visit -- the same arrangement <c>PortalPrescriptionsEndpointTests</c> uses. Static members only; the type exists to reach
/// the Pharmacy base's protected helpers.</summary>
internal sealed class PharmacyPrescribingHelper(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    public static async Task<Guid> IssueActivePrescriptionAsync(HttpClient admin, HttpClient vet, Guid catId, Guid visitId)
    {
        await RecordWeightAsync(vet, catId, 4.0m);
        var medicineId = await CreateMedicineAsync(admin, MedicineBody(name: "Portalmeloxicam" + Guid.NewGuid().ToString("N")[..6]));
        var response = await vet.PostAsJsonAsync($"/api/staff/cats/{catId}/prescriptions", PharmacyPrescribingEndpointTests.IssueBody(visitId, medicineId), Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("prescriptionId").GetGuid();
    }
}

/// <summary>Arranges Ordering's shop (branch, billing profile, SKUs, delivery zone) through its own endpoints and places a cash-on-delivery order for a customer
/// registered on another host over the same database. The customer signs in again on the Ordering host: its clock follows the wall clock, so the shop's
/// prices are in force there (the portal host's frozen clock was created earlier and does not see them yet). Owns the Ordering factory it was given.</summary>
internal sealed class OrderingShopHelper(OrderingApiFactory factory) : OrderingShopTestBase(factory), IDisposable
{
    public void Dispose() => factory.Dispose();

    public async Task PlaceCashOnDeliveryOrderAsync(string mobile, string password, Guid customerId)
    {
        var customerClient = CreateClientOn(factory);
        var signIn = await customerClient.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        customerClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var shop = await SetUpShopAsync(fillCart: false);
        var addressId = await AddAddressAsync(customerClient, shop.District);
        await ExpectAsync(HttpStatusCode.Created, await customerClient.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 2 }, Json));
        await ExpectAsync(HttpStatusCode.Created, await customerClient.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Toy.SkuId, quantity = 1 }, Json));
        await PlaceCodOrderAsync(shop with { Customer = customerClient, CustomerId = customerId, AddressId = addressId });
    }
}
