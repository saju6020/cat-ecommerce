using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Boarding;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Vaccination;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 gate-fix round 2026-10-15 -- the SECURITY and CODE_REVIEW findings, each pinned (I-S1 .. I-S10).</summary>
[Collection("Boarding")]
public sealed class BoardingGateFixEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE
    ];

    private sealed record Reserved(BoardingBranch Branch, HttpClient Customer, Guid CustomerId, string Mobile, Guid CatId, Guid StayId, DateOnly Arrival);

    private async Task<Reserved> ReservedStayAsync(int daysAhead = 10, int nights = 4)
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard", nightly: "1000.00");
        var (customer, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(daysAhead);
        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(nights), "Standard", cat);
        return new Reserved(branch, customer, customerId, mobile, cat, stayId, arrival);
    }

    private async Task<JsonElement> StaffStayAsync(BoardingBranch branch, Guid stayId) => await ReadAsync(await branch.Admin.GetAsync($"/api/staff/boarding/stays/{stayId}"));

    private async Task<Guid> UploadMediaFileAsync(HttpClient staff)
    {
        using var form = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"));
        var content = new ByteArrayContent(PngBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "cat.png");
        var upload = await staff.PostAsync("/api/staff/files?purpose=BoardingMedia", form);
        Assert.True(upload.IsSuccessStatusCode, (int)upload.StatusCode + " " + await upload.Content.ReadAsStringAsync());
        return (await ReadAsync(upload)).GetProperty("fileId").GetGuid();
    }

    // ── SECURITY ─────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_S1_APaymentOfAnotherCustomer_EarmarkedToTheStay_DoesNotConfirmIt()
    {
        var b = await ReservedStayAsync();
        var (_, otherCustomerId, _) = await RegisterAndSignInCustomerAsync();
        await RecordDepositAsync(b.Branch, otherCustomerId, b.StayId, "1200.00"); // customer A's money, earmarked to customer B's stay

        var confirm = await b.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{b.StayId}/confirm", null);
        await RunBoardingWorkCycleAsync();

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        Assert.Contains(BusinessViolationCodes.DepositNotYetReceived, await confirm.Content.ReadAsStringAsync());
        var stay = await StaffStayAsync(b.Branch, b.StayId);
        Assert.Equal("Reserved", stay.GetProperty("status").GetString());
        Assert.Equal("0.00", stay.GetProperty("deposit").GetProperty("received").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_S2_StaffChangingAReservedStaysVetAuthority_NeedAReason_AndLeaveAnAuditRow_TheOwnerChangesTheirOwnFreely()
    {
        var b = await ReservedStayAsync();
        var url = $"/api/staff/boarding/stays/{b.StayId}/cats/{b.CatId}/care-profile";
        object Body(string limit, string? reason) => new
        {
            feedings = new[] { new { time = "08:00", portion = "1/3 cup", foodSource = "OwnFood", foodDescription = "Dry" } }, medications = Array.Empty<object>(), litterType = "Clumping",
            vetVisitAuthorised = true, vetSpendingLimit = limit, emergencyContact = new { name = "May", phone = "01711111111" }, vetAuthorityChangeReason = reason
        };

        var withoutReason = await b.Branch.Admin.PutAsJsonAsync(url, Body("9000.00", null), Json);
        Assert.Equal(HttpStatusCode.BadRequest, withoutReason.StatusCode);
        Assert.Contains(BusinessViolationCodes.VetAuthorityReasonRequired, await withoutReason.Content.ReadAsStringAsync());

        var withReason = await b.Branch.Admin.PutAsJsonAsync(url, Body("9000.00", "The owner phoned and raised it"), Json);
        Assert.Equal(HttpStatusCode.NoContent, withReason.StatusCode);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var audit = await db.Set<AuditEntryAggregate>().AsNoTracking().Where(a => a.EntityId == b.StayId && a.Action == "BoardingVetAuthorityChanged").ToListAsync();
            var row = Assert.Single(audit);
            Assert.Equal("The owner phoned and raised it", row.Reason);
            Assert.Contains("Limit=5000.00", row.BeforeValue);
            Assert.Contains("Limit=9000.00", row.AfterValue);
        }

        var own = await b.Customer.PutAsJsonAsync($"/api/customers/me/boarding/stays/{b.StayId}/cats/{b.CatId}/care-profile", ProfileBody(limit: "3000.00"), Json);
        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
    }

    [Fact]
    public async Task I_S3_ALowIncident_IsInternal_TheOwnerCannotAcknowledgeIt_AndItsPhotoNeverReachesTheDiary()
    {
        var stay = await CheckedInStayAsync();
        var incident = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/incidents", new
        { catId = stay.CatIds[0], kind = "Injury", severity = "Low", description = "Small scratch", immediateAction = "Cleaned", occurredOn = Now.AddMinutes(-10) }, Json);
        var incidentId = (await ReadAsync(incident)).GetProperty("incidentId").GetGuid();

        var plainFile = await UploadMediaFileAsync(stay.Branch.Admin);
        var lowFile = await UploadMediaFileAsync(stay.Branch.Admin);
        var url = $"/api/staff/boarding/stays/{stay.StayId}/media";
        Assert.Equal(HttpStatusCode.Created, (await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId = plainFile, caption = "Nap", capturedOnLocalDate = LocalToday }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId = lowFile, incidentId, caption = "The scratch", capturedOnLocalDate = LocalToday }, Json)).StatusCode);

        var ack = await stay.Customer.PostAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NotFound, ack.StatusCode);

        var diary = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/diary"));
        var captions = diary.GetProperty("days").EnumerateArray().SelectMany(d => d.GetProperty("media").EnumerateArray()).Select(m => m.GetProperty("caption").GetString()).ToList();
        Assert.Contains("Nap", captions);
        Assert.DoesNotContain("The scratch", captions);
    }

    [Fact]
    public async Task I_S4_DaycareCannotBeBookedBeyondTheBookingHorizon()
    {
        var branch = await SeedBranchAsync();
        var set = await branch.Admin.PutAsJsonAsync($"/api/admin/settings?branchId={branch.BranchId}", new { changes = new[] { new { key = "boarding.daycareDailyCapacity", value = "2" } } }, Json);
        Assert.True(set.IsSuccessStatusCode, await set.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, (await branch.Admin.PostAsJsonAsync("/api/staff/boarding/daycare-rate-cards",
            new { hourlyRate = "200.00", dayRate = "1000.00", taxClassId = branch.TaxClassId, effectiveFrom = Now }, Json)).StatusCode);
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);

        var far = await branch.Admin.PostAsJsonAsync("/api/staff/daycare", new { customerId, catId = cat, localDate = LocalToday.AddDays(2000), basis = "Day" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, far.StatusCode);
        Assert.Contains(BusinessViolationCodes.StayBeyondHorizon, await far.Content.ReadAsStringAsync());
    }

    // ── CODE_REVIEW ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_S5_APaidReservation_WhoseCatNoLongerPassesTheGate_IsFlaggedForStaff_AndLapsesOnceTheHoldHasPassed_AndTheDepositIsListedToGiveBack()
    {
        var b = await ReservedStayAsync();
        await RecordDepositAsync(b.Branch, b.CustomerId, b.StayId, "1200.00");

        var newVaccine = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            // A core vaccine the cat does not have: the gate now blocks it (the Boarding host looks at BRD vaccines only; this one is withdrawn again below).
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VaccineAggregate>>();
            var vaccine = new VaccineAggregate(newVaccine);
            vaccine.Register(new VaccineRegistrationDto(BoardingTestVaccineScope.CodePrefix + "X" + NewCode(), "Gate test vaccine", true, 12, null, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.SaveAsync(vaccine, EventPublishOption.InMemoryPublishOnly);
        }

        try
        {
            await RunBoardingWorkCycleAsync();
            var flagged = await StaffStayAsync(b.Branch, b.StayId);
            Assert.Equal("Reserved", flagged.GetProperty("status").GetString());
            Assert.True(flagged.GetProperty("needsAttention").GetBoolean());
            Assert.Equal("VaccinationGateBlocked", flagged.GetProperty("attentionReason").GetString());

            Factory.Clock.Advance(TimeSpan.FromHours(49));
            await ReauthenticateAsync(b.Branch.Admin, b.Branch.AdminLogin);
            await RunBoardingWorkCycleAsync();

            Assert.Equal("Cancelled", (await StaffStayAsync(b.Branch, b.StayId)).GetProperty("status").GetString());
            var dashboard = await ReadAsync(await b.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"));
            Assert.Contains(dashboard.GetProperty("refundDue").EnumerateArray(), r => r.GetProperty("stayId").GetGuid() == b.StayId && r.GetProperty("reason").GetString() == "Deposit to refund");
        }
        finally
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"update clinic.vaccine set \"IsActive\" = false where \"Id\" = {newVaccine}");
        }
    }

    [Fact]
    public async Task I_S6_TwoWorkersGeneratingTheSameDaysTasks_AreBothSuccesses_AndNoTaskIsDuplicated()
    {
        var stay = await CheckedInStayAsync(nights: 4);
        var today = LocalToday;

        async Task<bool> Generate()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            var response = await dispatcher.SendAsync(new GenerateCareTasksCommand
            { StayId = stay.StayId, LocalDate = today, UserContext = new UserContext(Guid.NewGuid(), Guid.Empty), CorrelationId = Guid.NewGuid(), ValidateCommand = true });
            return response.IsSuccess;
        }

        var results = await Task.WhenAll(Generate(), Generate(), Generate());

        Assert.All(results, Assert.True);
        await using var check = Factory.Services.CreateAsyncScope();
        var db = check.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var keys = await db.BoardingCareTasks.AsNoTracking().Where(t => t.StayId == stay.StayId && t.LocalDate == today).Select(t => new { t.CatId, t.SourceKey }).ToListAsync();
        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public async Task I_S7_ACheckOutAfterTheBookedDeparture_ChargesTheExtraNights_WithoutExtendingTheAllocation()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var late = stay.Arrival.AddDays(5);
        Factory.Clock.SetUtcNow(new DateTimeOffset(late.Year, late.Month, late.Day, 10, 0, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);

        var preview = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out-preview"));
        Assert.True(preview.GetProperty("isLate").GetBoolean());
        Assert.Equal(5, preview.GetProperty("nightsCharged").GetInt32());

        var body = new { cats = stay.CatIds.Select(id => new { catId = id, weightKg = 4.1m, belongings = Array.Empty<object>(), signature = TypedSignature() }).ToArray() };
        var response = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", body, Json);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = (await ReadAsync(response)).GetProperty("stay");
        Assert.Equal(5, view.GetProperty("nights").GetInt32());
        Assert.Equal("5000.00", view.GetProperty("quote").GetProperty("total").GetProperty("amount").GetString());
        var allocation = view.GetProperty("allocations").EnumerateArray().Last();
        Assert.True(allocation.GetProperty("occupiedTo").GetDateTimeOffset() < Now); // the allocation is NOT extended: the extra nights are charges only
    }

    [Fact]
    public async Task I_S8_AnEarlyCheckOut_AfterACycleInvoice_KeepsTheInvoicedMonth_AndThePreviewSaysSo()
    {
        var stay = await CheckedInStayAsync(nights: 45, deposit: "15000.00");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var rates = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Administration.TaxRateAggregate>>();
            var rate = new CatEcommerce.Domain.Aggregates.Administration.TaxRateAggregate(Guid.NewGuid());
            rate.Create(new TaxRateCreateDto(stay.Branch.TaxClassId, 0m, Now.AddYears(-1), Now.AddYears(-1).AddDays(-1), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await rates.SaveAsync(rate, EventPublishOption.InMemoryPublishOnly);
        }

        var profile = await stay.Branch.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{stay.Branch.BranchId}", new
        {
            legalName = "PurrCare Test Ltd", taxRegistrationNumber = "BIN-" + NewCode(), addressLines = new[] { "1 Test Road" }, phone = "01700000000", email = "billing@example.test",
            invoiceFooter = "Thank you", bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, profile.StatusCode);

        var day = stay.Arrival.AddDays(31);
        Factory.Clock.SetUtcNow(new DateTimeOffset(day.Year, day.Month, day.Day, 10, 0, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await RunBoardingWorkCycleAsync();
        var invoiced = (await StaffStayAsync(stay.Branch, stay.StayId)).GetProperty("charges").EnumerateArray().Count(c => c.GetProperty("billed").GetBoolean());
        Assert.Equal(1, invoiced);

        var preview = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out-preview"));

        Assert.True(preview.GetProperty("isEarly").GetBoolean());
        Assert.Equal(31, preview.GetProperty("nightsCharged").GetInt32());
        var lines = preview.GetProperty("lines").EnumerateArray().ToList();
        Assert.Contains(lines, l => l.GetProperty("action").GetString() == "Keep" && l.GetProperty("lineRef").GetString()!.Contains("Month")); // the invoiced month is never changed
        Assert.Contains(lines, l => l.GetProperty("action").GetString() == "Void");
        Assert.Equal("None", preview.GetProperty("creditOutcome").GetString());

        var body = new { cats = stay.CatIds.Select(id => new { catId = id, weightKg = 4.1m, belongings = Array.Empty<object>(), signature = TypedSignature() }).ToArray() };
        Assert.Equal(HttpStatusCode.OK, (await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", body, Json)).StatusCode);
        var after = await StaffStayAsync(stay.Branch, stay.StayId);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("creditDue").ValueKind); // preview credit == the stay's credit
        Assert.Equal(1, after.GetProperty("charges").EnumerateArray().Count(c => c.GetProperty("billed").GetBoolean()));
    }

    [Fact]
    public async Task I_S9_AnExtensionRefusedForTheUnit_NamesTheFreeUnit_InTheMessageAndInRelocationOptions()
    {
        var stay = await CheckedInStayAsync(nights: 4);
        var departure = stay.Arrival.AddDays(4);
        var spareCode = "SP" + NewCode();
        var spare = await CreateUnitAsync(stay.Branch, "Standard", 1, code: spareCode, block: "B");
        var (otherCustomer, otherId, _) = await RegisterAndSignInCustomerAsync();
        await ReserveOkAsync(otherCustomer, stay.Branch.BranchId, departure.AddDays(1), departure.AddDays(4), "Standard", await SeedVaccinatedCatAsync(otherId));
        var version = (await StaffStayAsync(stay.Branch, stay.StayId)).GetProperty("version").GetInt32();

        var response = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/extend", new { expectedVersion = version, newDepartureOn = departure.AddDays(3) }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Contains(BusinessViolationCodes.UnitNotFreeForExtension, body.ToString());
        Assert.Contains(spareCode, body.GetProperty("errors")[0].GetProperty("errorMessage").GetString());
        Assert.Contains(body.GetProperty("relocationOptions").EnumerateArray(), o => o.GetProperty("unitId").GetGuid() == spare);
    }

    private static object CheckOutBody(Guid[] catIds) =>
        new { cats = catIds.Select(id => new { catId = id, weightKg = 4.1m, belongings = Array.Empty<object>(), signature = TypedSignature() }).ToArray() };

    [Fact]
    public async Task I_S10_ALateCheckOut_WithABookingFollowingOnTheSameUnit_Succeeds_BillsTheExtraNights_AndWarnsStaff()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var nextDeparture = stay.Arrival.AddDays(3);
        var (next, nextId, _) = await RegisterAndSignInCustomerAsync();
        var nextStay = await ReserveOkAsync(next, stay.Branch.BranchId, nextDeparture, nextDeparture.AddDays(2), "Standard", await SeedVaccinatedCatAsync(nextId));

        var late = stay.Arrival.AddDays(5);
        Factory.Clock.SetUtcNow(new DateTimeOffset(late.Year, late.Month, late.Day, 10, 0, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);

        var response = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds), Json);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync()); // never blocked
        var view = (await ReadAsync(response)).GetProperty("stay");
        Assert.Equal("CheckedOut", view.GetProperty("status").GetString());
        Assert.Equal("5000.00", view.GetProperty("quote").GetProperty("total").GetProperty("amount").GetString()); // the extra nights are billed
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var charged = await db.Charges.AsNoTracking().Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stay.StayId && c.Status == ChargeStatus.Active)
                .Select(c => c.Quantity * c.UnitPriceAmount - c.LineDiscountAmount).ToListAsync();
            Assert.Equal(5000m, charged.Sum());
        }

        var warned = await StaffStayAsync(stay.Branch, nextStay);
        Assert.True(warned.GetProperty("needsAttention").GetBoolean());
        Assert.Equal("UnitOverstayed", warned.GetProperty("attentionReason").GetString());
        var dashboard = await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"));
        Assert.Contains(dashboard.GetProperty("attention").EnumerateArray(), a => a.GetProperty("stayId").GetGuid() == nextStay);
    }

    [Fact]
    public async Task I_S11_TheLedgerNetsCreditNoteLinesAgainstTheChargesTheyCredit()
    {
        var stay = await CheckedInStayAsync(nights: 45, deposit: "15000.00");
        await InvoiceFirstCycleAsync(stay);
        var summary = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}/summary"));
        var invoiceId = summary.GetProperty("billing").GetProperty("invoiceIds")[0].GetGuid();
        var invoice = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}"));
        var lineId = invoice.GetProperty("lines")[0].GetProperty("lineId").GetGuid();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var before = await scope.ServiceProvider.GetRequiredService<CatEcommerce.Domain.DomainServices.IBoardingChargeLedger>().GetRowsAsync(stay.StayId);
            Assert.All(before, r => Assert.Equal(0m, r.CreditedNet));
        }

        var note = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes",
            new { kind = "Return", reason = "Returned early", lines = new[] { new { invoiceLineId = lineId, amount = 500m } } }, Json);
        Assert.True(note.StatusCode == HttpStatusCode.Created, await note.Content.ReadAsStringAsync());

        await using var after = Factory.Services.CreateAsyncScope();
        var rows = await after.ServiceProvider.GetRequiredService<CatEcommerce.Domain.DomainServices.IBoardingChargeLedger>().GetRowsAsync(stay.StayId);
        var billed = Assert.Single(rows.Where(r => r.IsBilled));
        Assert.Equal(500m, billed.CreditedNet);
        Assert.All(rows.Where(r => !r.IsBilled), r => Assert.Equal(0m, r.CreditedNet));
        // The planner owes only what is not yet credited when the invoiced charge no longer applies.
        var plan = CatEcommerce.Domain.DomainServices.BoardingChargePlanner.Plan(rows, [], 1);
        Assert.Equal(billed.Net - 500m, plan.CreditDue);
    }

    private async Task InvoiceFirstCycleAsync(InHouse stay)
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var rates = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Administration.TaxRateAggregate>>();
            var rate = new CatEcommerce.Domain.Aggregates.Administration.TaxRateAggregate(Guid.NewGuid());
            rate.Create(new TaxRateCreateDto(stay.Branch.TaxClassId, 0m, Now.AddYears(-1), Now.AddYears(-1).AddDays(-1), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await rates.SaveAsync(rate, EventPublishOption.InMemoryPublishOnly);
        }

        var profile = await stay.Branch.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{stay.Branch.BranchId}", new
        {
            legalName = "PurrCare Test Ltd", taxRegistrationNumber = "BIN-" + NewCode(), addressLines = new[] { "1 Test Road" }, phone = "01700000000", email = "billing@example.test",
            invoiceFooter = "Thank you", bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, profile.StatusCode);

        var day = stay.Arrival.AddDays(31);
        Factory.Clock.SetUtcNow(new DateTimeOffset(day.Year, day.Month, day.Day, 10, 0, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await RunBoardingWorkCycleAsync();
        Assert.Equal(1, (await StaffStayAsync(stay.Branch, stay.StayId)).GetProperty("charges").EnumerateArray().Count(c => c.GetProperty("billed").GetBoolean()));
    }

    [Fact]
    public async Task I_S12_TheWaitlistAndDaycareCaps_RefuseTheNextOne_WithTheirOwnCodes()
    {
        // waitlist: ten live entries for the customer, then an eleventh
        var branch = await SeedBranchAsync();
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Boarding.BoardingWaitlistEntryAggregate>>();
            for (var i = 0; i < StayRules.MaxLiveWaitlistEntriesPerCustomer; i++)
            {
                var entry = new CatEcommerce.Domain.Aggregates.Boarding.BoardingWaitlistEntryAggregate(Guid.NewGuid());
                entry.Join(branch.BranchId, customerId, LocalToday.AddDays(20 + i * 5), LocalToday.AddDays(22 + i * 5), 1, null, LocalToday, Now, new UserContext(customerId, Guid.Empty));
                await repository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
            }
        }

        var waitlist = await customer.PostAsJsonAsync("/api/customers/me/boarding/waitlist",
            new { branchId = branch.BranchId, arrivalOn = LocalToday.AddDays(100), departureOn = LocalToday.AddDays(102), catCount = 1 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, waitlist.StatusCode);
        Assert.Contains(BusinessViolationCodes.WaitlistLimitReached, await waitlist.Content.ReadAsStringAsync());

        // daycare: thirty upcoming visits, then a thirty-first
        var set = await branch.Admin.PutAsJsonAsync($"/api/admin/settings?branchId={branch.BranchId}", new { changes = new[] { new { key = "boarding.daycareDailyCapacity", value = "2" } } }, Json);
        Assert.True(set.IsSuccessStatusCode, await set.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, (await branch.Admin.PostAsJsonAsync("/api/staff/boarding/daycare-rate-cards",
            new { hourlyRate = "200.00", dayRate = "1000.00", taxClassId = branch.TaxClassId, effectiveFrom = Now }, Json)).StatusCode);
        var cat = await SeedVaccinatedCatAsync(customerId);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Boarding.DaycareVisitAggregate>>();
            var card = new CatEcommerce.Domain.Aggregates.Boarding.DaycareRateCardAggregate(Guid.NewGuid());
            card.Create(branch.BranchId, 200m, 1000m, branch.TaxClassId, "BDT", Now, Now, new UserContext(customerId, Guid.Empty));
            for (var i = 0; i < StayRules.MaxFutureDaycareVisitsPerCustomer; i++)
            {
                var visit = new CatEcommerce.Domain.Aggregates.Boarding.DaycareVisitAggregate(Guid.NewGuid());
                visit.Book(branch.BranchId, customerId, cat, "Mitu", LocalToday.AddDays(1 + i), 1, DaycareBasis.Day, null, card, LocalToday, Now, new UserContext(customerId, Guid.Empty));
                await repository.SaveAsync(visit, EventPublishOption.InMemoryPublishOnly);
            }
        }

        var otherCat = await SeedVaccinatedCatAsync(customerId, "Second");
        var daycare = await customer.PostAsJsonAsync("/api/customers/me/daycare", new { branchId = branch.BranchId, catId = otherCat, localDate = LocalToday.AddDays(60), basis = "Day" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, daycare.StatusCode);
        Assert.Contains(BusinessViolationCodes.DaycareLimitReached, await daycare.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_S13_APaidGateBlockedStay_AlreadyFlaggedForAnotherReason_StillLapses_AndIsNotReSavedEveryPass()
    {
        var b = await ReservedStayAsync();
        await RecordDepositAsync(b.Branch, b.CustomerId, b.StayId, "1200.00");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Boarding.BoardingStayAggregate>>();
            var aggregate = (await repo.GetByIdAsync(b.StayId))!;
            aggregate.FlagAttention(StayAttentionReason.CatOwnerChanged);
            await repo.UpdateAsync(aggregate, EventPublishOption.InMemoryPublishOnly);
        }

        var newVaccine = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<VaccineAggregate>>();
            var vaccine = new VaccineAggregate(newVaccine);
            vaccine.Register(new VaccineRegistrationDto(BoardingTestVaccineScope.CodePrefix + "X" + NewCode(), "Gate test vaccine", true, 12, null, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.SaveAsync(vaccine, EventPublishOption.InMemoryPublishOnly);
        }

        try
        {
            await RunBoardingWorkCycleAsync();
            var first = await StaffStayAsync(b.Branch, b.StayId);
            await RunBoardingWorkCycleAsync();
            var second = await StaffStayAsync(b.Branch, b.StayId);
            Assert.Equal("CatOwnerChanged", first.GetProperty("attentionReason").GetString()); // the first flag is kept
            Assert.Equal(first.GetProperty("version").GetInt32(), second.GetProperty("version").GetInt32()); // no re-save while it waits

            Factory.Clock.Advance(TimeSpan.FromHours(49));
            await ReauthenticateAsync(b.Branch.Admin, b.Branch.AdminLogin);
            await RunBoardingWorkCycleAsync();

            Assert.Equal("Cancelled", (await StaffStayAsync(b.Branch, b.StayId)).GetProperty("status").GetString());
        }
        finally
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"update clinic.vaccine set \"IsActive\" = false where \"Id\" = {newVaccine}");
        }
    }
}
