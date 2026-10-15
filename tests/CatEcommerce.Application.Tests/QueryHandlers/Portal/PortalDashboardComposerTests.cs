using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Read.Queries.Portal;
using CatEcommerce.Read.QueryHandlers.Portal;
using CatEcommerce.Read.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.QueryHandlers.Portal;

/// <summary>HO-POR-001 §15 B -- the composer (ADR-260), the dashboard handler's fault isolation, and sequential composition (ADR-267).</summary>
public sealed class PortalDashboardComposerTests
{
    private sealed class DelegateReader<T>(Func<PortalCaller, Task<T>> read) : IPortalSectionReader<T> where T : class
    {
        public Task<T> ReadAsync(PortalCaller caller) => read(caller);
    }

    private static readonly PortalCatsViewModel CatsData = new(true, false, []);
    private static readonly PortalAppointmentsViewModel AppointmentsData = new([]);
    private static readonly PortalCustomerViewModel CustomerData = new("Nusrat Jahan");
    private static readonly PortalVisitsViewModel VisitsData = new([]);
    private static readonly PortalPrescriptionsViewModel PrescriptionsData = new([]);
    private static readonly PortalOrdersViewModel OrdersData = new([]);
    private static readonly PortalBalancesViewModel BalancesData = new(false, [], [], 0, null, [], "how");

    private static GetPortalDashboardQueryHandler Handler(
        CapturingLogger<PortalSectionComposer> logger,
        Func<PortalCaller, Task<PortalBalancesViewModel>>? balances = null,
        Func<PortalCaller, Task<PortalCatsViewModel>>? cats = null,
        Func<PortalCaller, Task<PortalAppointmentsViewModel>>? appointments = null,
        Func<PortalCaller, Task<PortalVisitsViewModel>>? visits = null,
        Func<PortalCaller, Task<PortalCustomerViewModel>>? profile = null,
        Func<PortalCaller, Task<PortalPrescriptionsViewModel>>? prescriptions = null,
        Func<PortalCaller, Task<PortalOrdersViewModel>>? orders = null) =>
        new(new PortalSectionComposer(logger),
            new DelegateReader<PortalCatsViewModel>(cats ?? (_ => Task.FromResult(CatsData))),
            new DelegateReader<PortalAppointmentsViewModel>(appointments ?? (_ => Task.FromResult(AppointmentsData))),
            new DelegateReader<PortalOrdersViewModel>(orders ?? (_ => Task.FromResult(OrdersData))),
            new DelegateReader<PortalBalancesViewModel>(balances ?? (_ => Task.FromResult(BalancesData))),
            new DelegateReader<PortalVisitsViewModel>(visits ?? (_ => Task.FromResult(VisitsData))),
            new DelegateReader<PortalPrescriptionsViewModel>(prescriptions ?? (_ => Task.FromResult(PrescriptionsData))),
            new DelegateReader<PortalCustomerViewModel>(profile ?? (_ => Task.FromResult(CustomerData))),
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 12, 4, 30, 0, TimeSpan.Zero)));

    private static GetPortalDashboardQuery QueryFor(Guid customerId, Guid correlationId) =>
        new() { UserContext = PortalTestCallers.For(customerId), CorrelationId = correlationId };

    [Fact]
    public async Task U_B1_ABalancesReaderThatThrows_MarksOnlyBalancesUnavailable_WithNullData_AndLogsOneErrorWithSectionAndCorrelationId()
    {
        var logger = new CapturingLogger<PortalSectionComposer>();
        var correlationId = Guid.NewGuid();
        var handler = Handler(logger, balances: _ => throw new InvalidOperationException("billing is down"));

        var dashboard = (await handler.HandleAsync(QueryFor(Guid.NewGuid(), correlationId))).Result;

        Assert.Equal("Unavailable", dashboard.Balances.State);
        Assert.Null(dashboard.Balances.Data);
        Assert.Equal("Ready", dashboard.Cats.State);
        Assert.Equal("Ready", dashboard.Appointments.State);
        Assert.Equal("Ready", dashboard.Visits.State);
        Assert.Equal("Ready", dashboard.Customer.State);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("balances", entry.Message);
        Assert.Contains(correlationId.ToString(), entry.Message);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    }

    [Fact]
    public async Task U_B1_TheLogLineCarriesNoCustomerDataOrUpstreamPayload()
    {
        var logger = new CapturingLogger<PortalSectionComposer>();
        var handler = Handler(logger, cats: _ => throw new InvalidOperationException("upstream said"));

        await handler.HandleAsync(QueryFor(Guid.NewGuid(), Guid.NewGuid()));

        var entry = Assert.Single(logger.Entries);
        Assert.DoesNotContain("Nusrat", entry.Message);
        Assert.DoesNotContain("upstream said", entry.Message); // the exception object is attached, its text is not templated into the message
    }

    [Fact]
    public async Task U_B2_ACancelledRead_Propagates_NoSectionResult()
    {
        var logger = new CapturingLogger<PortalSectionComposer>();
        var handler = Handler(logger, visits: _ => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(QueryFor(Guid.NewGuid(), Guid.NewGuid())));
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ThePrescriptionsAndOrdersReaders_FailOnTheirOwn_WithoutBlankingTheRest()
    {
        var logger = new CapturingLogger<PortalSectionComposer>();
        var handler = Handler(logger, prescriptions: _ => throw new InvalidOperationException(), orders: _ => throw new InvalidOperationException());

        var dashboard = (await handler.HandleAsync(QueryFor(Guid.NewGuid(), Guid.NewGuid()))).Result;

        Assert.Equal("Unavailable", dashboard.Prescriptions.State);
        Assert.Null(dashboard.Prescriptions.Data);
        Assert.Equal("Unavailable", dashboard.Orders.State);
        Assert.Null(dashboard.Orders.Data);
        Assert.Equal("Ready", dashboard.Cats.State);
        Assert.Equal("Ready", dashboard.Balances.State);
        Assert.Equal(2, logger.Entries.Count);
    }

    [Fact]
    public async Task U_B5_ACatsSectionThatIsUnavailable_HasNullData_SoHasCatsIsAbsent_NeverFalse()
    {
        var handler = Handler(new CapturingLogger<PortalSectionComposer>(), cats: _ => throw new InvalidOperationException());

        var dashboard = (await handler.HandleAsync(QueryFor(Guid.NewGuid(), Guid.NewGuid()))).Result;

        Assert.Equal("Unavailable", dashboard.Cats.State);
        Assert.Null(dashboard.Cats.Data);
        var json = System.Text.Json.JsonSerializer.Serialize(dashboard, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.DoesNotContain("hasCats", json);
    }

    [Fact]
    public async Task U_B12_TheCallerIsTheSessionUserAndTheQueryCorrelationId_ForEverySection()
    {
        var customerId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var seen = new List<PortalCaller>();
        Task<T> Spy<T>(T value, PortalCaller caller) { seen.Add(caller); return Task.FromResult(value); }
        var handler = Handler(new CapturingLogger<PortalSectionComposer>(),
            cats: c => Spy(CatsData, c), appointments: c => Spy(AppointmentsData, c), balances: c => Spy(BalancesData, c),
            visits: c => Spy(VisitsData, c), profile: c => Spy(CustomerData, c),
            prescriptions: c => Spy(PrescriptionsData, c), orders: c => Spy(OrdersData, c));

        var query = QueryFor(customerId, correlationId);
        await handler.HandleAsync(query);

        Assert.Equal(7, seen.Count);
        Assert.All(seen, c =>
        {
            Assert.Equal(customerId, c.CustomerId);
            Assert.Same(query.UserContext, c.UserContext);
            Assert.Equal(correlationId, c.CorrelationId);
        });
    }

    [Fact]
    public async Task U_B16_TheComposerInvokesReadersOneAfterAnother_NeverOverlapping_InTheFixedOrder()
    {
        var overlap = new OverlapDetector();
        var order = new List<string>();
        Task<T> Run<T>(string name, T value) { order.Add(name); return overlap.RunAsync(value); }
        var handler = Handler(new CapturingLogger<PortalSectionComposer>(),
            cats: _ => Run("cats", CatsData), appointments: _ => Run("appointments", AppointmentsData), balances: _ => Run("balances", BalancesData),
            visits: _ => Run("visits", VisitsData), profile: _ => Run("customer", CustomerData),
            prescriptions: _ => Run("prescriptions", PrescriptionsData), orders: _ => Run("orders", OrdersData));

        await handler.HandleAsync(QueryFor(Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(overlap.Overlapped);
        Assert.Equal(["cats", "appointments", "orders", "balances", "visits", "prescriptions", "customer"], order);
    }

    [Fact]
    public async Task GeneratedOn_IsTheClockInstant_AndTheDashboardQueryHasNoCustomerIdProperty()
    {
        var handler = Handler(new CapturingLogger<PortalSectionComposer>());

        var dashboard = (await handler.HandleAsync(QueryFor(Guid.NewGuid(), Guid.NewGuid()))).Result;

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 4, 30, 0, TimeSpan.Zero), dashboard.GeneratedOn);
        // ADR-261: the only properties are the platform base's; nothing a client could set to pick another customer.
        var own = typeof(GetPortalDashboardQuery).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        Assert.Empty(own);
    }
}
