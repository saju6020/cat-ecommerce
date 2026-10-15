using System.Globalization;
using CatEcommerce.Infrastructure.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatEcommerce.Infrastructure.Billing.Documents;

/// <summary>
/// HO-BIL-001 §12.2 -- the invoice PDF. The Community licence is confirmed by the owner (set once at host start-up, as the
/// cat medical-history renderer relies on). Every customer-supplied string goes through <c>Text(...)</c> -- never anything
/// that interprets markup. The Bengali fallback font is registered once per process by the cat renderer's constructor,
/// which this one calls so a Bengali customer name or item description never crashes the document.
/// </summary>
public sealed class QuestPdfInvoiceRenderer : IInvoicePdfRenderer
{
    public QuestPdfInvoiceRenderer() => BillingPdf.EnsureFonts();

    public byte[] Render(InvoiceDocument d)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(d.BranchTimeZoneId);
        var issuedLocal = TimeZoneInfo.ConvertTime(d.IssuedOn, zone);

        return Document.Create(container => container.Page(page =>
        {
            BillingPdf.Configure(page);

            page.Header().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(seller =>
                    {
                        seller.Item().Text(d.SellerLegalName).Bold().FontSize(15);
                        seller.Item().Text(d.SellerAddress);
                        seller.Item().Text($"Tax registration no.: {d.SellerTaxRegistrationNumber}");
                        if (!string.IsNullOrWhiteSpace(d.SellerPhone)) seller.Item().Text($"Phone: {d.SellerPhone}");
                        if (!string.IsNullOrWhiteSpace(d.SellerEmail)) seller.Item().Text($"Email: {d.SellerEmail}");
                    });

                    row.ConstantItem(190).Column(meta =>
                    {
                        meta.Item().AlignRight().Text("TAX INVOICE").Bold().FontSize(15);
                        meta.Item().AlignRight().Text($"No. {d.Number}").Bold();
                        meta.Item().AlignRight().Text($"Issued: {issuedLocal:yyyy-MM-dd HH:mm}");
                        if (d.DueOn is { } due) meta.Item().AlignRight().Text($"Due: {due:yyyy-MM-dd}");
                        if (d.CorrectsInvoiceNumber is { } corrects) meta.Item().AlignRight().Text($"Corrects invoice {corrects}");
                    });
                });
            });

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(8);

                column.Item().Column(billTo =>
                {
                    billTo.Item().Text("Bill to").Bold();
                    billTo.Item().Text(d.BillToName);
                    if (!string.IsNullOrWhiteSpace(d.BillToAddress)) billTo.Item().Text(d.BillToAddress);
                    if (!string.IsNullOrWhiteSpace(d.BillToMobile)) billTo.Item().Text(d.BillToMobile);
                    if (!string.IsNullOrWhiteSpace(d.BillToEmail)) billTo.Item().Text(d.BillToEmail);
                });

                column.Item().Element(c => BillingPdf.LinesTable(c, d.Lines, d.CurrencyCode));

                column.Item().AlignRight().Width(240).Column(totals =>
                {
                    BillingPdf.TotalRow(totals, "Net", d.NetTotal, d.CurrencyCode);
                    if (d.LoyaltyPoints is { } points && d.LoyaltyValue is { } value)
                        BillingPdf.TotalRow(totals, $"Loyalty redemption — {points} points", value, d.CurrencyCode, negative: true);
                    BillingPdf.TotalRow(totals, "Tax", d.TaxTotal, d.CurrencyCode);
                    BillingPdf.TotalRow(totals, "Total", d.Total, d.CurrencyCode, bold: true);
                    if (d.CreditedTotal > 0m) BillingPdf.TotalRow(totals, "Credited", d.CreditedTotal, d.CurrencyCode, negative: true);
                    if (d.PaidTotal > 0m) BillingPdf.TotalRow(totals, "Paid", d.PaidTotal, d.CurrencyCode, negative: true);
                    BillingPdf.TotalRow(totals, "Outstanding", d.Total - d.CreditedTotal - d.PaidTotal, d.CurrencyCode, bold: true);
                });

                if (d.Payments.Count > 0)
                {
                    column.Item().Column(payments =>
                    {
                        payments.Item().Text("Payments applied").Bold();
                        foreach (var payment in d.Payments)
                            payments.Item().Text($"{payment.ReceiptNumber}  {payment.Method}  {payment.OnDate:yyyy-MM-dd}  {BillingPdf.Money(payment.Amount, d.CurrencyCode)}");
                    });
                }

                if (d.BankAccounts.Count > 0)
                {
                    column.Item().Column(bank =>
                    {
                        bank.Item().Text("How to pay by bank transfer").Bold();
                        bank.Item().Text($"Please use the invoice number {d.Number} as your payment reference.");
                        foreach (var account in d.BankAccounts)
                        {
                            bank.Item().Text($"{account.BankName} — {account.AccountName} — {account.AccountNumber}" +
                                $"{(string.IsNullOrWhiteSpace(account.BranchName) ? "" : " — " + account.BranchName)}" +
                                $"{(string.IsNullOrWhiteSpace(account.RoutingNumber) ? "" : " — routing " + account.RoutingNumber)}");
                        }
                    });
                }

                if (!string.IsNullOrWhiteSpace(d.SellerFooter)) column.Item().PaddingTop(6).Text(d.SellerFooter).Italic();
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"{d.Number} — page ").FontSize(8);
                text.CurrentPageNumber().FontSize(8);
                text.Span(" of ").FontSize(8);
                text.TotalPages().FontSize(8);
            });
        })).GeneratePdf();
    }
}

/// <summary>HO-BIL-001 ADR-186 -- the credit-note PDF: its own number, the invoice it credits, the credited lines at the
/// invoice line's frozen rate, and what was released to the customer's credit.</summary>
public sealed class QuestPdfCreditNoteRenderer : ICreditNotePdfRenderer
{
    public QuestPdfCreditNoteRenderer() => BillingPdf.EnsureFonts();

    public byte[] Render(CreditNoteDocument d)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(d.BranchTimeZoneId);
        var issuedLocal = TimeZoneInfo.ConvertTime(d.IssuedOn, zone);

        return Document.Create(container => container.Page(page =>
        {
            BillingPdf.Configure(page);

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(seller =>
                {
                    seller.Item().Text(d.SellerLegalName).Bold().FontSize(15);
                    seller.Item().Text(d.SellerAddress);
                    seller.Item().Text($"Tax registration no.: {d.SellerTaxRegistrationNumber}");
                    if (!string.IsNullOrWhiteSpace(d.SellerPhone)) seller.Item().Text($"Phone: {d.SellerPhone}");
                    if (!string.IsNullOrWhiteSpace(d.SellerEmail)) seller.Item().Text($"Email: {d.SellerEmail}");
                });

                row.ConstantItem(200).Column(meta =>
                {
                    meta.Item().AlignRight().Text("CREDIT NOTE").Bold().FontSize(15);
                    meta.Item().AlignRight().Text($"No. {d.Number}").Bold();
                    meta.Item().AlignRight().Text($"Issued: {issuedLocal:yyyy-MM-dd HH:mm}");
                    meta.Item().AlignRight().Text($"Against invoice {d.InvoiceNumber}");
                    meta.Item().AlignRight().Text($"Type: {d.Kind}");
                });
            });

            page.Content().PaddingVertical(10).Column(column =>
            {
                column.Spacing(8);

                column.Item().Column(billTo =>
                {
                    billTo.Item().Text("Credited to").Bold();
                    billTo.Item().Text(d.BillToName);
                    if (!string.IsNullOrWhiteSpace(d.BillToAddress)) billTo.Item().Text(d.BillToAddress);
                    if (!string.IsNullOrWhiteSpace(d.BillToMobile)) billTo.Item().Text(d.BillToMobile);
                });

                column.Item().Text($"Reason: {d.Reason}");
                column.Item().Element(c => BillingPdf.LinesTable(c, d.Lines, d.CurrencyCode));

                column.Item().AlignRight().Width(240).Column(totals =>
                {
                    BillingPdf.TotalRow(totals, "Net credited", d.NetTotal, d.CurrencyCode);
                    BillingPdf.TotalRow(totals, "Tax credited", d.TaxTotal, d.CurrencyCode);
                    BillingPdf.TotalRow(totals, "Total credited", d.Total, d.CurrencyCode, bold: true);
                    if (d.ReleasedToCustomerCredit > 0m)
                        BillingPdf.TotalRow(totals, "Held as your credit", d.ReleasedToCustomerCredit, d.CurrencyCode);
                });

                if (!string.IsNullOrWhiteSpace(d.SellerFooter)) column.Item().PaddingTop(6).Text(d.SellerFooter).Italic();
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span($"{d.Number} — page ").FontSize(8);
                text.CurrentPageNumber().FontSize(8);
            });
        })).GeneratePdf();
    }
}

internal static class BillingPdf
{
    private const string Fallback = "Noto Sans Bengali";

    public static void EnsureFonts() => _ = new QuestPdfCatMedicalHistoryRenderer();

    public static void Configure(PageDescriptor page)
    {
        page.Size(PageSizes.A4);
        page.Margin(32);
        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato", Fallback));
    }

    public static string Money(decimal amount, string currencyCode) =>
        $"{currencyCode} {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    public static void LinesTable(IContainer container, IReadOnlyList<DocumentLine> lines, string currencyCode)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(20);
                columns.RelativeColumn(4);
                columns.ConstantColumn(48);
                columns.ConstantColumn(54);
                columns.ConstantColumn(44);
                columns.ConstantColumn(36);
                columns.ConstantColumn(58);
                columns.ConstantColumn(50);
                columns.ConstantColumn(60);
            });

            static IContainer HeaderCell(IContainer c) => c.BorderBottom(1).PaddingVertical(3).DefaultTextStyle(x => x.Bold());

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Item");
                header.Cell().Element(HeaderCell).Text("Line");
                header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                header.Cell().Element(HeaderCell).AlignRight().Text("Unit");
                header.Cell().Element(HeaderCell).AlignRight().Text("Tax %");
                header.Cell().Element(HeaderCell).AlignRight().Text("Net");
                header.Cell().Element(HeaderCell).AlignRight().Text("Tax");
                header.Cell().Element(HeaderCell).AlignRight().Text("Gross");
            });

            foreach (var line in lines)
            {
                static IContainer Cell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);

                table.Cell().Element(Cell).Text(line.No.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).Column(item =>
                {
                    item.Item().Text(line.Description);
                    var detail = new List<string> { line.ItemKind };
                    if (line.LineDiscount > 0m) detail.Add("discount " + Money(line.LineDiscount, currencyCode));
                    if (line.InvoiceDiscountShare > 0m) detail.Add("loyalty " + Money(line.InvoiceDiscountShare, currencyCode));
                    item.Item().Text(string.Join(" · ", detail)).FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                table.Cell().Element(Cell).Text(line.BusinessLine);
                table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString("0.###", CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).AlignRight().Text(line.UnitPrice.ToString("N2", CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).AlignRight().Text($"{line.TaxRatePercent.ToString("0.##", CultureInfo.InvariantCulture)}%");
                table.Cell().Element(Cell).AlignRight().Text(line.NetAmount.ToString("N2", CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).AlignRight().Text(line.TaxAmount.ToString("N2", CultureInfo.InvariantCulture));
                table.Cell().Element(Cell).AlignRight().Text(line.GrossAmount.ToString("N2", CultureInfo.InvariantCulture));
            }
        });
    }

    public static void TotalRow(ColumnDescriptor column, string label, decimal amount, string currencyCode, bool bold = false, bool negative = false)
    {
        column.Item().Row(row =>
        {
            var left = row.RelativeItem().Text(label);
            var right = row.ConstantItem(90).AlignRight().Text((negative ? "- " : "") + Money(amount, currencyCode));
            if (bold)
            {
                left.Bold();
                right.Bold();
            }
        });
    }
}
