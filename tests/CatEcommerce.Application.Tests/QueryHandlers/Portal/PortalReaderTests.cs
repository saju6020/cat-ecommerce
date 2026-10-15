using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.Queries.Cat;
using CatEcommerce.Read.Queries.Clinic;
using CatEcommerce.Read.Queries.Account;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.Queries.Prescription;
using CatEcommerce.Read.Queries.Visit;
using CatEcommerce.Read.QueryHandlers.Order;
using CatEcommerce.Read.QueryHandlers.Portal;
using CatEcommerce.Read.QueryHandlers.Portal.Sections;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;

namespace CatEcommerce.Application.Tests.QueryHandlers.Portal;

/// <summary>HO-POR-001 §15 group B -- one section reader at a time against hand-written upstream fakes (ADR-259). The readers for the
/// Stays (HO-BRD-001) and Subscriptions (HO-SUB-001) sections are not built (§17, §20.1).</summary>
public sealed class PortalReaderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly PortalCaller Caller = new(CustomerId, PortalTestCallers.For(CustomerId), Guid.NewGuid());

    private static CatListItemViewModel Cat(string name, bool highRisk = false) =>
        new(Guid.NewGuid(), CustomerId, name, "Cat", null, "Female", highRisk, "do not touch", "Alive");

    private static CatVaccinationStatusViewModel Status(Guid catId, string overall = "DueSoon", bool eligible = true) =>
        new(catId, overall, new DateOnly(2026, 10, 30), eligible, []);

    private static FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>> AliveCats(params Guid[] catIds) =>
        new(_ => Page(catIds.Select(id => new CatListItemViewModel(id, CustomerId, "Cat", "Cat", null, "Female", false, "n", "Alive")).ToList()));

    private static PagedResult<CatListItemViewModel> Page(IReadOnlyList<CatListItemViewModel> items) => new(items, 1, items.Count, items.Count);

    // ── Cats ────────────────────────────────────────────────────────────────────────────────────────
    private static (CatsSectionReader Reader, FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>> Cats,
        FakeQueryHandler<GetCatVaccinationStatusQuery, CatVaccinationStatusViewModel?> Vaccination)
        CatsReader(IReadOnlyList<CatListItemViewModel> cats, Func<GetCatVaccinationStatusQuery, CatVaccinationStatusViewModel?>? status = null)
    {
        var catsHandler = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(_ => Page(cats));
        var vaccination = new FakeQueryHandler<GetCatVaccinationStatusQuery, CatVaccinationStatusViewModel?>(status ?? (q => Status(q.CatId)));
        return (new CatsSectionReader(catsHandler, vaccination, NullLogger<CatsSectionReader>.Instance), catsHandler, vaccination);
    }

    [Fact]
    public async Task U_B3_ThirteenCatsForLimitTwelve_TwelveShown_HasMore_AndTheQueryWasOwnerFilteredAndAliveOnlyWithPageSizeThirteen()
    {
        var (reader, cats, _) = CatsReader(Enumerable.Range(0, 13).Select(i => Cat($"Cat{i:00}")).ToList());

        var section = await reader.ReadAsync(Caller);

        Assert.Equal(12, section.Items.Count);
        Assert.True(section.HasMore);
        var query = Assert.Single(cats.Received);
        Assert.Equal(CustomerId, query.OwnerId);
        Assert.False(query.IncludeDeceased);
        Assert.Equal(13, query.PageSize);
        Assert.Equal(1, query.PageNumber);
    }

    [Fact]
    public async Task U_B4_ZeroCats_HasCatsFalse_NoVaccinationCalls()
    {
        var (reader, _, vaccination) = CatsReader([]);

        var section = await reader.ReadAsync(Caller);

        Assert.False(section.HasCats);
        Assert.False(section.HasMore);
        Assert.Empty(section.Items);
        Assert.Empty(vaccination.Received);
    }

    [Fact]
    public async Task U_B6_OneVaccinationReadThrows_ThatCatIsUnavailable_TheOthersMapped()
    {
        var a = Cat("A");
        var b = Cat("B");
        var c = Cat("C");
        var (reader, _, _) = CatsReader([a, b, c], q => q.CatId == b.Id ? throw new InvalidOperationException("boom") : Status(q.CatId, "Current"));

        var section = await reader.ReadAsync(Caller);

        Assert.Equal(3, section.Items.Count);
        Assert.Equal("Ready", section.Items[0].VaccinationState);
        Assert.Equal("Current", section.Items[0].Vaccination!.OverallStatus);
        Assert.Equal("Unavailable", section.Items[1].VaccinationState);
        Assert.Null(section.Items[1].Vaccination);
        Assert.Equal("Ready", section.Items[2].VaccinationState);
    }

    [Fact]
    public async Task U_B6_AVaccinationReadThatCancels_IsNotSwallowed()
    {
        var (reader, _, _) = CatsReader([Cat("A")], _ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => reader.ReadAsync(Caller));
    }

    [Fact]
    public async Task U_B7_VaccinationIsRequestedOnlyForIdsTheOwnerFilteredListReturned_InOrder_AndNeverMoreThanTwelve()
    {
        var returned = Enumerable.Range(0, 13).Select(i => Cat($"Cat{i:00}")).ToList();
        var (reader, _, vaccination) = CatsReader(returned);

        await reader.ReadAsync(Caller);

        Assert.Equal(returned.Take(12).Select(c => c.Id), vaccination.Received.Select(q => q.CatId));
    }

    [Fact]
    public async Task CatItems_CarryNameAndFlagsOnly_NoHandlingNote()
    {
        var (reader, _, _) = CatsReader([Cat("Mitu", highRisk: true)]);

        var item = Assert.Single((await reader.ReadAsync(Caller)).Items);

        Assert.True(item.IsHighRisk);
        Assert.DoesNotContain(typeof(PortalCatItemViewModel).GetProperties(), p => p.Name.Contains("Handling", StringComparison.OrdinalIgnoreCase));
    }

    // ── Appointments ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task U_B8_Appointments_UpcomingScope_PageSizeFive_FieldsMapped_BranchZoneCarried()
    {
        var startsOn = new DateTimeOffset(2026, 10, 20, 4, 0, 0, TimeSpan.Zero);
        var appointment = new AppointmentSummaryViewModel(
            Guid.NewGuid(), Guid.NewGuid(), "Mitu", new ServiceSnapshotViewModel("Vaccination", "Consultation", 30, 500m, "BDT"),
            startsOn, startsOn.AddMinutes(30), "Dr Rahman", new BranchRefViewModel(Guid.NewGuid(), "Main", "0123"), "Asia/Dhaka", "Booked", true, null);
        var handler = new FakeQueryHandler<GetCustomerAppointmentsQuery, PagedResult<AppointmentSummaryViewModel>>(_ => new([appointment], 1, 5, 1));

        var section = await new AppointmentsSectionReader(handler).ReadAsync(Caller);

        var query = Assert.Single(handler.Received);
        Assert.Equal("upcoming", query.Scope);
        Assert.Equal(5, query.PageSize);
        Assert.Equal(1, query.PageNumber);
        Assert.Equal(CustomerId, query.CustomerId);

        var item = Assert.Single(section.Items);
        Assert.Equal(appointment.Id, item.AppointmentId);
        Assert.Equal(appointment.CatId, item.CatId);
        Assert.Equal("Mitu", item.CatName);
        Assert.Equal("Vaccination", item.ServiceName);
        Assert.Equal(startsOn, item.StartsOn);
        Assert.Equal(startsOn.AddMinutes(30), item.EndsOn);
        Assert.Equal("Dr Rahman", item.PerformerName);
        Assert.Equal("Main", item.BranchName);
        Assert.Equal("Asia/Dhaka", item.BranchTimeZoneId);
        Assert.Equal("Booked", item.Status);
    }

    // ── Profile ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Profile_MapsFullNameOnly_AndAMissingProfileIsAnUpstreamFault()
    {
        var found = new FakeQueryHandler<GetCustomerProfileQuery, CustomerProfileViewModel?>(_ =>
            new CustomerProfileViewModel("Nusrat Jahan", "01711111111", "n@example.test", true, true, true, [], DateTimeOffset.UtcNow));
        Assert.Equal("Nusrat Jahan", (await new ProfileReader(found).ReadAsync(Caller)).FullName);
        Assert.Equal(typeof(string), Assert.Single(typeof(PortalCustomerViewModel).GetProperties()).PropertyType);

        var missing = new FakeQueryHandler<GetCustomerProfileQuery, CustomerProfileViewModel?>(_ => null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProfileReader(missing).ReadAsync(Caller));
    }

    // ── Visits ──────────────────────────────────────────────────────────────────────────────────────
    private static CustomerVisitListItemViewModel Visit(DateOnly date, string service = "Consultation", DateOnly? followUpOn = null) =>
        new(Guid.NewGuid(), date, service, "Dr Rahman", ["Gingivitis"], true, followUpOn);

    [Fact]
    public async Task U_B9_Visits_OnlyTheLatestVisitPerCat_AndNoClinicalContentOrderedLatestFirst()
    {
        var mitu = Cat("Mitu");
        var tom = Cat("Tom");
        var older = Visit(new DateOnly(2026, 8, 1));
        var latest = Visit(new DateOnly(2026, 9, 30), "Dental");
        var tomVisit = Visit(new DateOnly(2026, 9, 1));
        var cats = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(_ => Page([mitu, tom]));
        var visits = new FakeQueryHandler<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?>(q =>
            q.CatId == mitu.Id ? [older, latest] : [tomVisit]);

        var section = await new VisitsSectionReader(cats, visits).ReadAsync(Caller);

        Assert.Equal([latest.Id, tomVisit.Id], section.Items.Select(i => i.VisitId));
        Assert.Equal(["Mitu", "Tom"], section.Items.Select(i => i.CatName));
        Assert.Equal(new DateOnly(2026, 9, 30), section.Items[0].VisitedOn);
        Assert.Equal("Dental", section.Items[0].ServiceName);
        Assert.Equal("Dr Rahman", section.Items[0].VetName);
        Assert.True(section.Items[0].HasDischargeSummary);
    }

    [Fact]
    public async Task Visits_AskForOwnerFilteredAliveCats_ThenPerCatWithTheCallerAsCustomer_AndSkipACatUpstreamDeniesWithNull()
    {
        var mine = Cat("Mine");
        var denied = Cat("Denied");
        var cats = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(_ => Page([mine, denied]));
        var visits = new FakeQueryHandler<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?>(q =>
            q.CatId == mine.Id ? [Visit(new DateOnly(2026, 9, 1))] : null);

        var section = await new VisitsSectionReader(cats, visits).ReadAsync(Caller);

        var catsQuery = Assert.Single(cats.Received);
        Assert.Equal(CustomerId, catsQuery.OwnerId);
        Assert.False(catsQuery.IncludeDeceased);
        Assert.Equal([mine.Id, denied.Id], visits.Received.Select(q => q.CatId));
        Assert.All(visits.Received, q => Assert.Equal(CustomerId, q.CustomerId));
        Assert.Equal("Mine", Assert.Single(section.Items).CatName);
    }

    [Fact]
    public void U_A5_TheVisitsSectionTypesHaveOnlyTheAllowListedProperties()
    {
        Assert.Equal(
            ["CatId", "CatName", "FollowUpOn", "HasDischargeSummary", "ServiceName", "VetName", "VisitId", "VisitedOn"],
            typeof(PortalVisitItemViewModel).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Items"], typeof(PortalVisitsViewModel).GetProperties().Select(p => p.Name));
    }

    // ── Balances ────────────────────────────────────────────────────────────────────────────────────
    private static MoneyViewModel Money(string amount, string currency = "BDT") => new(amount, currency);

    private static CustomerBillingSummaryViewModel Summary(string outstanding, string overdue = "0.00", DateOnly? oldest = null, string currency = "BDT") =>
        new(Money(outstanding, currency), Money(overdue, currency), Money("0.00", currency), [], Money("0.00", currency), null, false, oldest);

    private static InvoiceSummaryViewModel Invoice(string status, string balance, DateOnly? dueOn, string number = "INV-1", string currency = "BDT") =>
        new(Guid.NewGuid(), number, status, CustomerId, "", Guid.NewGuid(), new DateOnly(2026, 9, 1), dueOn,
            Money("1500.00", currency), Money("0.00", currency), Money("0.00", currency), Money(balance, currency), 1);

    private static BalancesSectionReader BalancesReader(
        CustomerBillingSummaryViewModel summary, IReadOnlyList<InvoiceSummaryViewModel> invoices,
        out FakeQueryHandler<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel> summaryHandler,
        out FakeQueryHandler<GetCustomerInvoicesQuery, PagedResult<InvoiceSummaryViewModel>> invoiceHandler)
    {
        summaryHandler = new(_ => summary);
        invoiceHandler = new(q => new PagedResult<InvoiceSummaryViewModel>(invoices, q.PageNumber, q.PageSize, invoices.Count));
        return new BalancesSectionReader(summaryHandler, invoiceHandler);
    }

    [Fact]
    public async Task U_B11_Balances_OutstandingIsNeverSummedAcrossCurrencies_AndZeroMeansNoOutstandingAndAnEmptyList()
    {
        // Billing returns one Money in the commerce currency (real contract); a USD summary is never added to a BDT one.
        var bdt = await BalancesReader(Summary("1500.00", "500.00", new DateOnly(2026, 9, 1)), [], out _, out _).ReadAsync(Caller);
        var usd = await BalancesReader(Summary("20.00", currency: "USD"), [], out _, out _).ReadAsync(Caller);

        Assert.True(bdt.HasOutstanding);
        Assert.Equal([new PortalMoneyViewModel("1500.00", "BDT")], bdt.Outstanding);
        Assert.Equal([new PortalMoneyViewModel("500.00", "BDT")], bdt.Overdue);
        Assert.Equal(new DateOnly(2026, 9, 1), bdt.OldestDueOn);
        Assert.Equal([new PortalMoneyViewModel("20.00", "USD")], usd.Outstanding);

        var none = await BalancesReader(Summary("0.00"), [], out _, out _).ReadAsync(Caller);
        Assert.False(none.HasOutstanding);
        Assert.Empty(none.Outstanding);
        Assert.Empty(none.Overdue);
    }

    [Fact]
    public async Task Balances_OpenInvoicesOnly_OldestDueFirst_LimitFive_CountIsAllOpen_HowToPayIsGuidanceOnly()
    {
        var invoices = new List<InvoiceSummaryViewModel>
        {
            Invoice("Paid", "0.00", new DateOnly(2026, 8, 1), "PAID"),
            Invoice("Credited", "0.00", new DateOnly(2026, 8, 2), "CREDITED"),
            Invoice("Issued", "100.00", new DateOnly(2026, 11, 1), "D"),
            Invoice("PartPaid", "50.00", new DateOnly(2026, 9, 1), "B"),
            Invoice("Issued", "10.00", null, "NODUE"),
            Invoice("Issued", "70.00", new DateOnly(2026, 10, 1), "C"),
            Invoice("Issued", "20.00", new DateOnly(2026, 8, 15), "A"),
            Invoice("Issued", "30.00", new DateOnly(2026, 12, 1), "E"),
        };

        var section = await BalancesReader(Summary("280.00"), invoices, out _, out _).ReadAsync(Caller);

        Assert.Equal(6, section.OpenInvoiceCount);
        Assert.Equal(["A", "B", "C", "D", "E"], section.Invoices.Select(i => i.InvoiceNumber));
        Assert.Equal(PortalMessages.HowToPayOffline, section.HowToPay);
        Assert.Equal("20.00", section.Invoices[0].BalanceDue.Amount);
        Assert.DoesNotContain(typeof(PortalBalancesViewModel).GetProperties().Concat(typeof(PortalInvoiceItemViewModel).GetProperties()),
            p => new[] { "Url", "Link", "Gateway", "Checkout" }.Any(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task Balances_WalksInvoicePages_UntilTheLastOne_AndStopsAtTheCap()
    {
        var summaryHandler = new FakeQueryHandler<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel>(_ => Summary("1.00"));
        var invoiceHandler = new FakeQueryHandler<GetCustomerInvoicesQuery, PagedResult<InvoiceSummaryViewModel>>(q =>
            new PagedResult<InvoiceSummaryViewModel>(
                Enumerable.Range(0, PortalRules.BalanceInvoicePageSize).Select(_ => Invoice("Paid", "0.00", null)).ToList(),
                q.PageNumber, q.PageSize, 10_000));

        await new BalancesSectionReader(summaryHandler, invoiceHandler).ReadAsync(Caller);

        Assert.Equal(PortalRules.BalanceInvoiceMaxPages, invoiceHandler.Received.Count);
        Assert.Equal(Enumerable.Range(1, PortalRules.BalanceInvoiceMaxPages), invoiceHandler.Received.Select(q => q.PageNumber));
        Assert.All(invoiceHandler.Received, q => Assert.Equal(PortalRules.BalanceInvoicePageSize, q.PageSize));
    }

    // ── Prescriptions ───────────────────────────────────────────────────────────────────────────────
    private static CustomerPrescriptionViewModel Rx(Guid catId, string catName, DateOnly endsOn, bool active = true) =>
        new(Guid.NewGuid(), catId, catName, new PrescriptionMedicineViewModel("Amoxicillin", "50 mg", "Tablet", false),
            new QuantityViewModel("1.00", "tablet"), "Oral", "Twice daily", 5, endsOn, "Dr Rahman", DateTimeOffset.UtcNow, "Active", active,
            new CustomerRepeatViewModel(false, 0, endsOn, false, null, null, null));

    [Fact]
    public async Task U_B10_Prescriptions_ThreeActiveAcrossTwoCats_CountsAndEarliestEnd_NoMedicineOrDose()
    {
        var mitu = Guid.NewGuid();
        var luna = Guid.NewGuid();
        IReadOnlyList<CustomerPrescriptionViewModel> rows =
        [
            Rx(mitu, "Mitu", new DateOnly(2026, 10, 25)), Rx(luna, "Luna", new DateOnly(2026, 10, 18)), Rx(mitu, "Mitu", new DateOnly(2026, 10, 20)),
            Rx(luna, "Luna", new DateOnly(2026, 9, 1), active: false),
        ];
        var handler = new FakeQueryHandler<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>(_ => rows);

        var section = await new PrescriptionsSectionReader(AliveCats(mitu, luna), handler).ReadAsync(Caller);

        Assert.Equal(2, section.ByCat.Count);
        Assert.Equal(new PortalPrescriptionCatViewModel(luna, "Luna", 1, new DateOnly(2026, 10, 18)), section.ByCat[0]);
        Assert.Equal(new PortalPrescriptionCatViewModel(mitu, "Mitu", 2, new DateOnly(2026, 10, 20)), section.ByCat[1]);
        var query = Assert.Single(handler.Received);
        Assert.Equal("active", query.Scope);
        Assert.Equal(CustomerId, query.CustomerId);
        Assert.Null(query.CatId);
    }

    [Fact]
    public async Task Prescriptions_ADeceasedCatsActivePrescription_IsNotShown()
    {
        var alive = Guid.NewGuid();
        var deceased = Guid.NewGuid();
        IReadOnlyList<CustomerPrescriptionViewModel> rows = [Rx(alive, "Mitu", new DateOnly(2026, 10, 25)), Rx(deceased, "Gone", new DateOnly(2026, 10, 20))];
        var handler = new FakeQueryHandler<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>(_ => rows);

        var section = await new PrescriptionsSectionReader(AliveCats(alive), handler).ReadAsync(Caller);

        var only = Assert.Single(section.ByCat);
        Assert.Equal(alive, only.CatId);
    }

    // POR backlog 17: the alive-cat filter reads every page of the caller's alive cats, not the dashboard's 12-cat page.
    [Fact]
    public async Task Prescriptions_ACustomerWithThirteenCats_StillSeesThePrescriptionOfTheThirteenth()
    {
        var cats = Enumerable.Range(1, 13).Select(i => new CatListItemViewModel(Guid.NewGuid(), CustomerId, $"Cat{i:00}", "Cat", null, "Female", false, "n", "Alive")).ToList();
        var catsHandler = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(q =>
            new(cats.Skip(q.Skip).Take(q.Take).ToList(), q.PageNumber, q.PageSize, cats.Count));
        var thirteenth = cats[12];
        IReadOnlyList<CustomerPrescriptionViewModel> rows = [Rx(thirteenth.Id, thirteenth.Name, new DateOnly(2026, 10, 25))];
        var handler = new FakeQueryHandler<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>(_ => rows);

        var section = await new PrescriptionsSectionReader(catsHandler, handler).ReadAsync(Caller);

        Assert.Equal(thirteenth.Id, Assert.Single(section.ByCat).CatId);
        Assert.All(catsHandler.Received, q => Assert.True(q.PageSize > 12));
    }

    [Fact]
    public async Task Prescriptions_MoreAliveCatsThanOnePage_ReadsEveryPage()
    {
        var cats = Enumerable.Range(1, 130).Select(i => new CatListItemViewModel(Guid.NewGuid(), CustomerId, $"Cat{i:000}", "Cat", null, "Female", false, "n", "Alive")).ToList();
        var catsHandler = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(q =>
            new(cats.Skip(q.Skip).Take(q.Take).ToList(), q.PageNumber, q.PageSize, cats.Count));
        var last = cats[129];
        IReadOnlyList<CustomerPrescriptionViewModel> rows = [Rx(last.Id, last.Name, new DateOnly(2026, 10, 25))];
        var handler = new FakeQueryHandler<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>(_ => rows);

        var section = await new PrescriptionsSectionReader(catsHandler, handler).ReadAsync(Caller);

        Assert.Equal(last.Id, Assert.Single(section.ByCat).CatId);
        Assert.Equal(2, catsHandler.Received.Count);
    }

    [Fact]
    public async Task Prescriptions_NoneActive_IsAnEmptyList()
    {
        var handler = new FakeQueryHandler<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>(_ => []);
        Assert.Empty((await new PrescriptionsSectionReader(AliveCats(), handler).ReadAsync(Caller)).ByCat);
    }

    [Fact]
    public void U_A5_ThePrescriptionsSectionTypesHaveOnlyTheAllowListedProperties()
    {
        Assert.Equal(["ActiveCount", "CatId", "CatName", "EarliestEndsOn"],
            typeof(PortalPrescriptionCatViewModel).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["ByCat"], typeof(PortalPrescriptionsViewModel).GetProperties().Select(p => p.Name));
    }

    private static bool CustomerOrderRulesFor(string status) => status is "Cancelled" or "Delivered" or "Returned" or "Refunded";   // fixture only

    // ── Orders ──────────────────────────────────────────────────────────────────────────────────────
    private static CustomerOrderSummaryViewModel OrderRow(string status, DateTimeOffset placedOn, string number = "SO-1", string? tracking = null) =>
        new(Guid.NewGuid(), number, placedOn, status, "Delivery", "CashOnDelivery", Money("3450.00"), 3, "Asia/Dhaka", tracking, CustomerOrderRulesFor(status));

    [Fact]
    public async Task U_B13_Orders_LimitFive_TotalPassedThroughUnsummed_QueryCarriesTheCaller()
    {
        var at = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        IReadOnlyList<CustomerOrderSummaryViewModel> rows = Enumerable.Range(0, 6).Select(i => OrderRow("Delivered", at.AddDays(-i), "SO-" + i)).ToList();
        var handler = new FakeQueryHandler<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>>(q => new(rows, q.PageNumber, q.PageSize, rows.Count));

        var section = await new OrdersSectionReader(handler).ReadAsync(Caller);

        Assert.Equal(PortalRules.DashboardItemLimit, section.Items.Count);
        Assert.Equal(new PortalMoneyViewModel("3450.00", "BDT"), section.Items[0].Total);
        var query = Assert.Single(handler.Received);
        Assert.Equal(CustomerId, query.CustomerId);
        Assert.Equal(1, query.PageNumber);
        Assert.Equal(PortalRules.DashboardItemLimit, query.PageSize);
    }

    [Theory]
    [InlineData("Cancelled", true)]
    [InlineData("Delivered", true)]
    [InlineData("Returned", true)]
    [InlineData("Refunded", true)]
    [InlineData("PendingPayment", false)]
    [InlineData("Confirmed", false)]
    [InlineData("Packing", false)]
    [InlineData("Dispatched", false)]
    public void U_B13_OrdersCanReorderRule_PerStatus_IsTheSingleSourceOrdersListAndDetailShare(string status, bool expected)
    {
        var parsed = new[] { OrderStatus.PendingPayment, OrderStatus.Confirmed, OrderStatus.Packing, OrderStatus.Dispatched, OrderStatus.Delivered,
            OrderStatus.Cancelled, OrderStatus.Returned, OrderStatus.Refunded }.Single(s => s.DisplayName == status);
        Assert.Equal(expected, CustomerOrderRules.CanReorder(parsed));
    }

    [Fact]
    public async Task Orders_CanReorderAndTrackingNumber_ArePassedThroughFromOrdersListItem()
    {
        var withTracking = OrderRow("Dispatched", DateTimeOffset.UtcNow, tracking: "TRK-9");
        var handler = new FakeQueryHandler<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>>(
            _ => new([withTracking with { CanReorder = true }], 1, 5, 1));   // the flag is Orders' to decide: the portal does not recompute it

        var item = Assert.Single((await new OrdersSectionReader(handler).ReadAsync(Caller)).Items);

        Assert.True(item.CanReorder);
        Assert.Equal("TRK-9", item.TrackingNumber);
    }

    [Fact]
    public async Task Visits_FollowUpOn_IsPassedThroughFromTheEmrListItem()
    {
        var cat = Cat("Mitu");
        var due = new DateOnly(2026, 11, 1);
        var cats = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(_ => Page([cat]));
        var visits = new FakeQueryHandler<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?>(_ => [Visit(new DateOnly(2026, 10, 1), followUpOn: due)]);

        Assert.Equal(due, Assert.Single((await new VisitsSectionReader(cats, visits).ReadAsync(Caller)).Items).FollowUpOn);
    }

    [Fact]
    public async Task Orders_NewestFirst_AndTheItemHasNoPaymentOrBankField()
    {
        var at = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var handler = new FakeQueryHandler<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>>(
            _ => new([OrderRow("Delivered", at, "OLD"), OrderRow("Delivered", at.AddDays(3), "NEW")], 1, 5, 2));

        var section = await new OrdersSectionReader(handler).ReadAsync(Caller);

        Assert.Equal(["NEW", "OLD"], section.Items.Select(i => i.OrderNumber));
        Assert.Equal(["CanReorder", "ItemCount", "OrderId", "OrderNumber", "PlacedOn", "Status", "Total", "TrackingNumber"],
            typeof(PortalOrderItemViewModel).GetProperties().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    // ── U-B12: every reader hands its upstream queries the caller id, UserContext and CorrelationId ──
    [Fact]
    public async Task U_B12_EveryReader_PassesTheCallerIdUserContextAndCorrelationIdToItsUpstreamQueries()
    {
        var cat = Cat("Mitu");
        var (catsReader, catsHandler, vaccination) = CatsReader([cat]);
        await catsReader.ReadAsync(Caller);
        Assert.All(catsHandler.Received, q => { Assert.Equal(CustomerId, q.OwnerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });
        Assert.All(vaccination.Received, q => { Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });

        var appointments = new FakeQueryHandler<GetCustomerAppointmentsQuery, PagedResult<AppointmentSummaryViewModel>>(_ => new([], 1, 5, 0));
        await new AppointmentsSectionReader(appointments).ReadAsync(Caller);
        Assert.All(appointments.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });

        var profile = new FakeQueryHandler<GetCustomerProfileQuery, CustomerProfileViewModel?>(_ =>
            new CustomerProfileViewModel("N", null, null, false, false, false, [], DateTimeOffset.UtcNow));
        await new ProfileReader(profile).ReadAsync(Caller);
        Assert.All(profile.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });

        var visitCats = new FakeQueryHandler<GetCatsQuery, PagedResult<CatListItemViewModel>>(_ => Page([cat]));
        var visits = new FakeQueryHandler<GetCustomerCatVisitsQuery, IReadOnlyList<CustomerVisitListItemViewModel>?>(_ => []);
        await new VisitsSectionReader(visitCats, visits).ReadAsync(Caller);
        Assert.All(visitCats.Received, q => { Assert.Equal(CustomerId, q.OwnerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });
        Assert.All(visits.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });

        var prescriptionCats = AliveCats();
        var prescriptions = new FakeQueryHandler<GetCustomerPrescriptionsQuery, IReadOnlyList<CustomerPrescriptionViewModel>>(_ => []);
        await new PrescriptionsSectionReader(prescriptionCats, prescriptions).ReadAsync(Caller);
        Assert.All(prescriptionCats.Received, q => { Assert.Equal(CustomerId, q.OwnerId); Assert.False(q.IncludeDeceased); });
        Assert.All(prescriptions.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });

        var orders = new FakeQueryHandler<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>>(_ => new([], 1, 5, 0));
        await new OrdersSectionReader(orders).ReadAsync(Caller);
        Assert.All(orders.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });

        var balances = BalancesReader(Summary("0.00"), [], out var summaryHandler, out var invoiceHandler);
        await balances.ReadAsync(Caller);
        Assert.All(summaryHandler.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });
        Assert.All(invoiceHandler.Received, q => { Assert.Equal(CustomerId, q.CustomerId); Assert.Same(Caller.UserContext, q.UserContext); Assert.Equal(Caller.CorrelationId, q.CorrelationId); });
    }
}
