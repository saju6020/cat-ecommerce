namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-POR-001 §10.3, OQ-8 -- fixed constants, not administrator settings.</summary>
public static class PortalRules
{
    /// <summary>Appointments, stays, orders, visits and open invoices shown per section.</summary>
    public const int DashboardItemLimit = 5;

    /// <summary>Cats shown on the dashboard. One more is requested so the reader can say "there are more".</summary>
    public const int DashboardCatLimit = 12;

    /// <summary>HO-POR-001 §20.2 adaptation (recorded in §1 Amended): Billing's customer invoice list is not filterable by status
    /// and is newest first, so the balances reader walks at most this many pages of <see cref="BalanceInvoicePageSize"/> looking for
    /// open invoices. A customer with more issued invoices than that sees the open ones among the newest only.</summary>
    public const int BalanceInvoiceMaxPages = 5;

    public const int BalanceInvoicePageSize = 100;
}
