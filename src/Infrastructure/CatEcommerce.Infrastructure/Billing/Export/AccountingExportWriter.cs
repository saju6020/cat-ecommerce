using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CatEcommerce.Infrastructure.Billing.Export;

/// <summary>
/// HO-BIL-001 section 12.2 (ADR-190, F36) -- builds the accounting export: a ZIP of CSV files whose bytes depend <b>only</b> on the facts given. Entries are
/// written in a fixed order with a fixed timestamp (<see cref="ZipArchiveEntry.LastWriteTime"/> is settable), rows are sorted here by their own
/// stable keys (so the order the query returned them in never matters), culture is invariant, text is UTF-8 without a BOM and lines
/// end in <c>\n</c>. Every row starts with <c>recordType</c> and <c>documentId</c>; no row carries a current status or a running total. Text
/// cells pass the CSV-injection guard (HO-ADM-001 ADR-051).
/// </summary>
public static class AccountingExportWriter
{
    public const string FormatVersion = "1";

    /// <summary>The earliest instant a ZIP entry can hold (the DOS epoch) -- fixed, so a re-run is byte-identical.</summary>
    public static readonly DateTimeOffset FixedTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly string[] EntryOrder =
        ["manifest.csv", "invoices.csv", "invoice-lines.csv", "payments.csv", "allocations.csv", "credit-notes.csv", "credit-note-lines.csv", "refunds.csv"];

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static AccountingExportFile Write(AccountingExportContent content)
    {
        var files = new (string Name, string Text)[]
        {
            (EntryOrder[0], Manifest(content)),
            (EntryOrder[1], Invoices(content.Invoices)),
            (EntryOrder[2], InvoiceLines(content.InvoiceLines)),
            (EntryOrder[3], Payments(content.Payments)),
            (EntryOrder[4], Allocations(content.Allocations)),
            (EntryOrder[5], CreditNotes(content.CreditNotes)),
            (EntryOrder[6], CreditNoteLines(content.CreditNoteLines)),
            (EntryOrder[7], Refunds(content.Refunds)),
        };

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, text) in files)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTimestamp;
                using var entryStream = entry.Open();
                var bytes = Utf8NoBom.GetBytes(text);
                entryStream.Write(bytes, 0, bytes.Length);
            }
        }

        var zip = stream.ToArray();
        return new AccountingExportFile(
            zip, Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant(), content.Invoices.Count, content.InvoiceLines.Count, content.Payments.Count,
            content.Allocations.Count, content.CreditNotes.Count, content.Refunds.Count);
    }

    private static string Manifest(AccountingExportContent c)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "key", "value");
        void Entry(string key, string value) => BillingCsv.Row(sb, BillingCsv.Text("Manifest"), BillingCsv.Text("manifest"), BillingCsv.Text(key), BillingCsv.Text(value));

        Entry("formatVersion", FormatVersion);
        Entry("from", BillingCsv.Date(c.From));
        Entry("to", BillingCsv.Date(c.To));
        Entry("branchId", c.BranchId?.ToString() ?? "all");
        Entry("currency", c.CurrencyCode);
        Entry("invoices", BillingCsv.Number(c.Invoices.Count));
        Entry("invoiceLines", BillingCsv.Number(c.InvoiceLines.Count));
        Entry("payments", BillingCsv.Number(c.Payments.Count));
        Entry("allocations", BillingCsv.Number(c.Allocations.Count));
        Entry("creditNotes", BillingCsv.Number(c.CreditNotes.Count));
        Entry("creditNoteLines", BillingCsv.Number(c.CreditNoteLines.Count));
        Entry("refunds", BillingCsv.Number(c.Refunds.Count));
        return sb.ToString();
    }

    private static string Invoices(IEnumerable<ExportInvoice> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "number", "financialYear", "branchId", "customerId", "customerName", "issueDate", "dueOn", "currency",
            "netTotal", "discountTotal", "taxTotal", "total", "correctsInvoiceId");

        foreach (var r in rows.OrderBy(r => r.IssueDate).ThenBy(r => r.Number, StringComparer.Ordinal).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text("Invoice"), BillingCsv.Text(r.Id.ToString()), BillingCsv.Text(r.Number), BillingCsv.Text(r.FinancialYear),
                BillingCsv.Text(r.BranchId.ToString()), BillingCsv.Text(r.CustomerId.ToString()), BillingCsv.Text(r.CustomerName), BillingCsv.Date(r.IssueDate), BillingCsv.Date(r.DueOn),
                BillingCsv.Text(r.CurrencyCode), BillingCsv.Amount(r.NetTotal), BillingCsv.Amount(r.DiscountTotal), BillingCsv.Amount(r.TaxTotal), BillingCsv.Amount(r.Total),
                BillingCsv.Text(r.CorrectsInvoiceId?.ToString()));

        return sb.ToString();
    }

    private static string InvoiceLines(IEnumerable<ExportInvoiceLine> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "invoiceId", "invoiceNumber", "lineNumber", "businessLine", "itemKind", "description", "quantity", "unitPrice",
            "lineDiscount", "invoiceDiscountShare", "taxClassCode", "taxRatePercent", "netAmount", "taxAmount", "grossAmount");

        foreach (var r in rows.OrderBy(r => r.InvoiceNumber, StringComparer.Ordinal).ThenBy(r => r.SortOrder).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text("InvoiceLine"), BillingCsv.Text(r.Id.ToString()), BillingCsv.Text(r.InvoiceId.ToString()), BillingCsv.Text(r.InvoiceNumber),
                BillingCsv.Number(r.SortOrder + 1), BillingCsv.Text(r.BusinessLine), BillingCsv.Text(r.ItemKind), BillingCsv.Text(r.Description), BillingCsv.Quantity(r.Quantity),
                BillingCsv.Amount(r.UnitPrice), BillingCsv.Amount(r.LineDiscount), BillingCsv.Amount(r.InvoiceDiscountShare), BillingCsv.Text(r.TaxClassCode),
                BillingCsv.Amount(r.TaxRatePercent), BillingCsv.Amount(r.NetAmount), BillingCsv.Amount(r.TaxAmount), BillingCsv.Amount(r.GrossAmount));

        return sb.ToString();
    }

    private static string Payments(IEnumerable<ExportPayment> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "receiptNumber", "branchId", "customerId", "method", "amount", "currency", "date", "receivedOn", "reference", "collectorKind");

        foreach (var r in rows.OrderBy(r => r.Date).ThenBy(r => r.RecordType, StringComparer.Ordinal).ThenBy(r => r.ReceiptNumber).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text(r.RecordType), BillingCsv.Text(r.Id.ToString()), r.ReceiptNumber is { } n ? BillingCsv.Number(n) : string.Empty,
                BillingCsv.Text(r.BranchId.ToString()), BillingCsv.Text(r.CustomerId.ToString()), BillingCsv.Text(r.Method), BillingCsv.Amount(r.Amount), BillingCsv.Text(r.CurrencyCode),
                BillingCsv.Date(r.Date), BillingCsv.Date(r.ReceivedOn), BillingCsv.Text(r.Reference), BillingCsv.Text(r.CollectorKind));

        return sb.ToString();
    }

    private static string Allocations(IEnumerable<ExportAllocation> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "paymentId", "invoiceId", "kind", "releaseReason", "amount", "date", "occurredOn");

        foreach (var r in rows.OrderBy(r => r.OccurredOn).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text("AllocationEntry"), BillingCsv.Text(r.Id.ToString()), BillingCsv.Text(r.PaymentId.ToString()), BillingCsv.Text(r.InvoiceId.ToString()),
                BillingCsv.Text(r.Kind), BillingCsv.Text(r.ReleaseReason), BillingCsv.Amount(r.Amount), BillingCsv.Date(r.Date), BillingCsv.Instant(r.OccurredOn));

        return sb.ToString();
    }

    private static string CreditNotes(IEnumerable<ExportCreditNote> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "number", "invoiceId", "invoiceNumber", "branchId", "customerId", "kind", "currency", "issueDate",
            "netTotal", "taxTotal", "total", "releasedToCustomerCredit");

        foreach (var r in rows.OrderBy(r => r.IssueDate).ThenBy(r => r.Number, StringComparer.Ordinal).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text("CreditNote"), BillingCsv.Text(r.Id.ToString()), BillingCsv.Text(r.Number), BillingCsv.Text(r.InvoiceId.ToString()),
                BillingCsv.Text(r.InvoiceNumber), BillingCsv.Text(r.BranchId.ToString()), BillingCsv.Text(r.CustomerId.ToString()), BillingCsv.Text(r.Kind), BillingCsv.Text(r.CurrencyCode),
                BillingCsv.Date(r.IssueDate), BillingCsv.Amount(r.NetTotal), BillingCsv.Amount(r.TaxTotal), BillingCsv.Amount(r.Total), BillingCsv.Amount(r.ReleasedToCustomerCredit));

        return sb.ToString();
    }

    private static string CreditNoteLines(IEnumerable<ExportCreditNoteLine> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "creditNoteId", "creditNoteNumber", "lineNumber", "invoiceLineId", "description", "taxRatePercent",
            "netAmount", "taxAmount", "grossAmount");

        foreach (var r in rows.OrderBy(r => r.CreditNoteNumber, StringComparer.Ordinal).ThenBy(r => r.SortOrder).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text("CreditNoteLine"), BillingCsv.Text(r.Id.ToString()), BillingCsv.Text(r.CreditNoteId.ToString()), BillingCsv.Text(r.CreditNoteNumber),
                BillingCsv.Number(r.SortOrder + 1), BillingCsv.Text(r.InvoiceLineId.ToString()), BillingCsv.Text(r.Description), BillingCsv.Amount(r.TaxRatePercent),
                BillingCsv.Amount(r.NetAmount), BillingCsv.Amount(r.TaxAmount), BillingCsv.Amount(r.GrossAmount));

        return sb.ToString();
    }

    private static string Refunds(IEnumerable<ExportRefund> rows)
    {
        var sb = new StringBuilder();
        BillingCsv.Row(sb, "recordType", "documentId", "paymentId", "creditNoteId", "branchId", "customerId", "method", "amount", "currency", "businessDate", "reference");

        foreach (var r in rows.OrderBy(r => r.BusinessDate).ThenBy(r => r.Id))
            BillingCsv.Row(sb, BillingCsv.Text("Refund"), BillingCsv.Text(r.Id.ToString()), BillingCsv.Text(r.PaymentId.ToString()), BillingCsv.Text(r.CreditNoteId?.ToString()),
                BillingCsv.Text(r.BranchId.ToString()), BillingCsv.Text(r.CustomerId.ToString()), BillingCsv.Text(r.Method), BillingCsv.Amount(r.Amount), BillingCsv.Text(r.CurrencyCode),
                BillingCsv.Date(r.BusinessDate), BillingCsv.Text(r.Reference));

        return sb.ToString();
    }
}
