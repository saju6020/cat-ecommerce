namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §12.3 action names. Each family is its own nested class so a controller reads
/// <c>Actions.Payments.Record</c>, <c>Actions.Billing.View</c>, ... exactly as the handover's table
/// spells them.</summary>
public static partial class Actions
{
    public static class Billing
    {
        public const string View = "Billing.View";
        public const string ManageCharges = "Billing.ManageCharges";
        public const string DraftInvoices = "Billing.DraftInvoices";
        public const string IssueInvoices = "Billing.IssueInvoices";
    }

    public static class Payments
    {
        public const string Record = "Payments.Record";
        public const string Allocate = "Payments.Allocate";
        public const string Approve = "Payments.Approve";
        public const string Reverse = "Payments.Reverse";
    }

    public static class CreditNotes
    {
        public const string Issue = "CreditNotes.Issue";
    }

    public static class Refunds
    {
        public const string Issue = "Refunds.Issue";
    }

    public static class CashUp
    {
        public const string Count = "CashUp.Count";
        public const string Close = "CashUp.Close";
    }

    public static class Reconciliation
    {
        public const string Manage = "Reconciliation.Manage";
    }

    public static class AccountingExport
    {
        public const string Run = "AccountingExport.Run";
    }

    public static class BillingProfile
    {
        public const string Manage = "BillingProfile.Manage";
    }
}
