namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-BIL-001 §12.3 -- Billing's own rows in the solution-wide action-to-role table (ADR-036/ADR-341).
/// Appendix D "Invoices &amp; payments": Reception W, Shop Mgr W, Accountant F, Admin F;
/// "Refunds &amp; credit notes": Shop Mgr W, Accountant F, Admin F. Customer R (own) is a
/// customer-session read, not a staff action, so it carries no row here. Approval is Accountant and
/// Administrator only (OQ-5); closing the cash-up likewise (OQ-12).
/// </summary>
public static partial class ActionRoles
{
    private static readonly (string Action, string[] Roles)[] BillingRows =
    [
        (Actions.Billing.View, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Billing.ManageCharges, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Billing.DraftInvoices, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Billing.IssueInvoices, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Payments.Record, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Payments.Allocate, [StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Payments.Approve, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Payments.Reverse, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.CreditNotes.Issue, [StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Refunds.Issue, [StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.CashUp.Count, [StaffRoles.Reception, StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.CashUp.Close, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.Reconciliation.Manage, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.AccountingExport.Run, [StaffRoles.Accountant, StaffRoles.Administrator]),
        (Actions.BillingProfile.Manage, [StaffRoles.Administrator]),
    ];
}
