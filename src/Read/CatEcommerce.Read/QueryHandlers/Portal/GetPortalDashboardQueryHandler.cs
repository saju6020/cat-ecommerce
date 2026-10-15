using CatEcommerce.Read.Queries.Portal;
using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal;

/// <summary>
/// HO-POR-001 Q1 -- the dashboard (§8.2). A pure composer: no DbContext, no dispatcher, no repository (ADR-258) -- every fact comes
/// from a section reader that calls the owning module's published query handler. Sections run one after another, in the fixed order
/// below (ADR-267): the shared DbContext is not thread-safe (F36), and the order makes tests deterministic -- it is not a priority.
/// A section appears in the response only once the area that owns its data has been merged and its reader built (§17).
/// </summary>
public sealed class GetPortalDashboardQueryHandler(
    PortalSectionComposer composer,
    IPortalSectionReader<PortalCatsViewModel> cats,
    IPortalSectionReader<PortalAppointmentsViewModel> appointments,
    IPortalSectionReader<PortalOrdersViewModel> orders,
    IPortalSectionReader<PortalBalancesViewModel> balances,
    IPortalSectionReader<PortalVisitsViewModel> visits,
    IPortalSectionReader<PortalPrescriptionsViewModel> prescriptions,
    IPortalSectionReader<PortalCustomerViewModel> profile,
    TimeProvider timeProvider)
    : IQueryHandlerAsync<GetPortalDashboardQuery, PortalDashboardViewModel>
{
    public async Task<QueryResponse<PortalDashboardViewModel>> HandleAsync(GetPortalDashboardQuery query)
    {
        var caller = new PortalCaller(query.UserContext.UserId, query.UserContext, query.CorrelationId);

        // Order is the section diagram's of §8.2; sections whose upstream is not merged yet are simply not here.
        var catsSection = await composer.RunAsync("cats", caller.CorrelationId, () => cats.ReadAsync(caller));
        var appointmentsSection = await composer.RunAsync("appointments", caller.CorrelationId, () => appointments.ReadAsync(caller));
        var ordersSection = await composer.RunAsync("orders", caller.CorrelationId, () => orders.ReadAsync(caller));
        var balancesSection = await composer.RunAsync("balances", caller.CorrelationId, () => balances.ReadAsync(caller));
        var visitsSection = await composer.RunAsync("visits", caller.CorrelationId, () => visits.ReadAsync(caller));
        var prescriptionsSection = await composer.RunAsync("prescriptions", caller.CorrelationId, () => prescriptions.ReadAsync(caller));
        var customerSection = await composer.RunAsync("customer", caller.CorrelationId, () => profile.ReadAsync(caller));

        return new QueryResponse<PortalDashboardViewModel>
        {
            Result = new PortalDashboardViewModel(timeProvider.GetUtcNow(), customerSection, catsSection, appointmentsSection, balancesSection, visitsSection, prescriptionsSection, ordersSection)
        };
    }
}
