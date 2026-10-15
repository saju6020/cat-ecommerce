using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>
/// HO-ORD-001 (HO-POR-001 §8.3): the caller's most recent orders. <c>GetCustomerOrdersQuery { CustomerId }</c> is newest first and paged.
/// Since the owner decision of 2026-10-10 Orders' list item carries <c>trackingNumber</c> and <c>canReorder</c> (computed by Orders' own
/// rule, one source of truth), and both are passed straight through -- the portal keeps no status list of its own (ADR-268 amended).
/// The grand total is shown as Orders computed it; nothing is summed here. The portal has no re-order of its own (ADR-263 retired):
/// the frontend calls Orders' <c>POST /api/customers/me/orders/{id}/reorder</c>.
/// </summary>
public sealed class OrdersSectionReader(
    IQueryHandlerAsync<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>> ordersHandler)
    : IPortalSectionReader<PortalOrdersViewModel>
{
    public async Task<PortalOrdersViewModel> ReadAsync(PortalCaller caller)
    {
        var page = (await ordersHandler.HandleAsync(new GetCustomerOrdersQuery
        {
            CustomerId = caller.CustomerId,
            PageNumber = 1,
            PageSize = PortalRules.DashboardItemLimit,
            UserContext = caller.UserContext,
            CorrelationId = caller.CorrelationId,
        })).Result;

        var items = page.Items
            .OrderByDescending(o => o.PlacedOn)
            .Take(PortalRules.DashboardItemLimit)
            .Select(o => new PortalOrderItemViewModel(
                o.Id, o.OrderNumber, o.PlacedOn, o.Status, new PortalMoneyViewModel(o.GrandTotal.Amount, o.GrandTotal.CurrencyCode),
                o.ItemCount, o.CanReorder, o.TrackingNumber))
            .ToList();

        return new PortalOrdersViewModel(items);
    }
}
