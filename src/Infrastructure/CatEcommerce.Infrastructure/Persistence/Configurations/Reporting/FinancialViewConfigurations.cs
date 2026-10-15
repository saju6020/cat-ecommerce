using CatEcommerce.Infrastructure.Persistence.ReportingViews;

namespace CatEcommerce.Infrastructure.Persistence.Configurations.Reporting;

public sealed class RvInvoiceLineConfiguration() : ReportViewConfiguration<RvInvoiceLineRow>("rv_invoice_line");
public sealed class RvCreditNoteLineConfiguration() : ReportViewConfiguration<RvCreditNoteLineRow>("rv_credit_note_line");
public sealed class RvCreditNoteConfiguration() : ReportViewConfiguration<RvCreditNoteRow>("rv_credit_note");
public sealed class RvRefundConfiguration() : ReportViewConfiguration<RvRefundRow>("rv_refund");
public sealed class RvPaymentConfiguration() : ReportViewConfiguration<RvPaymentRow>("rv_payment");
public sealed class RvCashUpConfiguration() : ReportViewConfiguration<RvCashUpRow>("rv_cash_up");
public sealed class RvInvoiceBalanceConfiguration() : ReportViewConfiguration<RvInvoiceBalanceRow>("rv_invoice_balance");
