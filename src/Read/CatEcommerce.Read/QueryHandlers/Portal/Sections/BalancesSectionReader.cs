using System.Globalization;
using CatEcommerce.Read.Queries.Billing;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Portal.Sections;

/// <summary>
/// HO-BIL-001 (HO-POR-001 §8.3, ADR-264, ADR-266): what is owed, and how to pay it offline. There is no payment action, link or field.
///
/// Reconciled with the real contract (HO-POR-001 §1 Amended 2026-10-09). Billing publishes <c>GetCustomerBillingSummaryQuery</c> (its own
/// comment names HO-POR-001 as the consumer) with ONE <c>outstanding</c> and ONE <c>overdue</c> Money in the commerce currency, no invoice
/// count; and <c>GetCustomerInvoicesQuery</c>, which lists the customer's issued invoices newest first and cannot filter by status. The
/// reader therefore (1) never sums: each Money becomes at most one list entry, so a second currency would be a second entry, never a
/// total across currencies; (2) walks at most <c>BalanceInvoiceMaxPages</c> pages of the invoice list and keeps the open ones
/// (<c>Issued</c> or <c>PartPaid</c> with a balance), ordered oldest due first. Billing's customer set (the caller plus any customer merged
/// into them, ADR-192) is applied by Billing; this reader only ever passes the caller's id.
/// </summary>
public sealed class BalancesSectionReader(
    IQueryHandlerAsync<GetCustomerBillingSummaryQuery, CustomerBillingSummaryViewModel> summaryHandler,
    IQueryHandlerAsync<GetCustomerInvoicesQuery, PagedResult<InvoiceSummaryViewModel>> invoicesHandler)
    : IPortalSectionReader<PortalBalancesViewModel>
{
    public async Task<PortalBalancesViewModel> ReadAsync(PortalCaller caller)
    {
        var summary = (await summaryHandler.HandleAsync(new GetCustomerBillingSummaryQuery
        {
            CustomerId = caller.CustomerId, UserContext = caller.UserContext, CorrelationId = caller.CorrelationId,
        })).Result;

        var open = await ReadOpenInvoicesAsync(caller);

        var outstanding = NonZero(summary.Outstanding);
        var overdue = NonZero(summary.Overdue);

        var invoices = open
            .OrderBy(i => i.DueOn ?? DateOnly.MaxValue)
            .ThenBy(i => i.IssueDate ?? DateOnly.MaxValue)
            .Take(PortalRules.DashboardItemLimit)
            .Select(i => new PortalInvoiceItemViewModel(
                i.InvoiceId, i.Number, i.IssueDate, i.DueOn, Map(i.Total), Map(i.Outstanding), i.Status))
            .ToList();

        return new PortalBalancesViewModel(
            outstanding.Count > 0, outstanding, overdue, open.Count, summary.OldestDueOn, invoices, PortalMessages.HowToPayOffline);
    }

    private async Task<List<InvoiceSummaryViewModel>> ReadOpenInvoicesAsync(PortalCaller caller)
    {
        var open = new List<InvoiceSummaryViewModel>();
        string[] openStatuses = [InvoiceStatus.Issued.DisplayName, InvoiceStatus.PartPaid.DisplayName];

        for (var page = 1; page <= PortalRules.BalanceInvoiceMaxPages; page++)
        {
            var result = (await invoicesHandler.HandleAsync(new GetCustomerInvoicesQuery
            {
                CustomerId = caller.CustomerId,
                PageNumber = page,
                PageSize = PortalRules.BalanceInvoicePageSize,
                UserContext = caller.UserContext,
                CorrelationId = caller.CorrelationId,
            })).Result;

            open.AddRange(result.Items.Where(i => openStatuses.Contains(i.Status) && IsPositive(i.Outstanding)));

            if (result.Items.Count < PortalRules.BalanceInvoicePageSize || page * PortalRules.BalanceInvoicePageSize >= result.TotalCount) break;
        }

        return open;
    }

    private static List<PortalMoneyViewModel> NonZero(MoneyViewModel money) => IsPositive(money) ? [Map(money)] : [];

    private static bool IsPositive(MoneyViewModel money) =>
        decimal.Parse(money.Amount, NumberStyles.Number, CultureInfo.InvariantCulture) > 0m;

    private static PortalMoneyViewModel Map(MoneyViewModel money) => new(money.Amount, money.CurrencyCode);
}
