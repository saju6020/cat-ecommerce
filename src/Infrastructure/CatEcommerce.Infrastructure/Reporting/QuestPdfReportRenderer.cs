using System.Globalization;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Documents;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CatEcommerce.Infrastructure.Reporting;

/// <summary>
/// HO-RPT-001 section 8.12 (ADR-285) -- A4, portrait up to six columns and landscape above. Header with the clinic, branch, report title, period with its zone, filters, generated on and generated
/// by; a repeated table header; a totals row; the notes (coverage included); page n of m. The QuestPDF Community licence is owner-confirmed (HO-CAT-001 section 1 Amended, 2026-09-26) and is set
/// once by <c>CatProfilesModule</c>. Fonts are HO-CAT-001's: Lato with Noto Sans Bengali as the fallback, so a Bengali customer name renders instead of crashing the document (OQ-11).
/// </summary>
public sealed class QuestPdfReportRenderer : IReportExportRenderer
{
    private const string BengaliFallback = "Noto Sans Bengali";
    private const int PortraitMaxColumns = 6;

    public ReportFormat Format => ReportFormat.Pdf;
    public string ContentType => "application/pdf";
    public string Extension => "pdf";

    public QuestPdfReportRenderer()
    {
        // Registers the embedded Bengali fallback font once per process (the shared, locked registration of HO-CAT-001's renderer).
        _ = new QuestPdfCatMedicalHistoryRenderer();
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(ReportResult result, ReportRenderContext context)
    {
        var zone = ResolveZone(result.Filters.TimeZoneId);
        var landscape = result.Columns.Count > PortraitMaxColumns;

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Lato", BengaliFallback));

                page.Header().Column(column =>
                {
                    column.Item().Text(context.ClinicName).FontSize(9).FontColor(Colors.Grey.Darken2);
                    column.Item().Text(result.Title).FontSize(15).Bold();
                    column.Item().Text($"Branch: {result.Filters.BranchName}");
                    column.Item().Text($"Period: {result.Filters.From:yyyy-MM-dd} to {result.Filters.To:yyyy-MM-dd} ({result.Filters.TimeZoneId})");

                    var filterText = FilterText(result);
                    if (filterText.Length > 0) column.Item().Text($"Filters: {filterText}");

                    column.Item().Text(
                        $"Generated on {TimeZoneInfo.ConvertTime(result.GeneratedOn, zone):yyyy-MM-dd HH:mm} by {result.GeneratedBy}").FontColor(Colors.Grey.Darken1);
                    column.Item().PaddingBottom(6);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    column.Item().Element(c => Table(c, result, zone));

                    foreach (var note in result.Notes)
                        column.Item().Text(note.Text).Italic().FontSize(7.5f);

                    if (result.Coverage is { } coverage)
                    {
                        column.Item().Text(
                            $"Margin covers {coverage.Percent.ToString("0.0", CultureInfo.InvariantCulture)} % of revenue ({coverage.CostedRevenue.Amount.ToString("N2", CultureInfo.InvariantCulture)} of " +
                            $"{coverage.TotalRevenue.Amount.ToString("N2", CultureInfo.InvariantCulture)} {coverage.TotalRevenue.CurrencyCode}).").Italic().FontSize(7.5f);
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Table(IContainer container, ReportResult result, TimeZoneInfo zone)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in result.Columns)
                    columns.RelativeColumn(column.Type == ReportColumnType.Text ? 3f : 2f);
            });

            static IContainer HeaderCell(IContainer c) => c.BorderBottom(1).PaddingVertical(3).DefaultTextStyle(x => x.Bold());
            static IContainer BodyCell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
            static IContainer TotalCell(IContainer c) => c.BorderTop(1).PaddingVertical(3).DefaultTextStyle(x => x.Bold());

            table.Header(header =>
            {
                foreach (var column in result.Columns)
                    Aligned(header.Cell().Element(HeaderCell), column).Text(column.Label);
            });

            foreach (var row in result.Rows)
            {
                foreach (var column in result.Columns)
                    Aligned(table.Cell().Element(BodyCell), column).Text(Display(column, row.GetValueOrDefault(column.Key), zone));
            }

            if (result.Totals is { } totals)
            {
                for (var i = 0; i < result.Columns.Count; i++)
                {
                    var column = result.Columns[i];
                    var text = totals.TryGetValue(column.Key, out var value) && value is not null ? Display(column, value, zone) : i == 0 ? "Total" : "";
                    Aligned(table.Cell().Element(TotalCell), column).Text(text);
                }
            }
        });
    }

    private static IContainer Aligned(IContainer container, ReportColumn column) =>
        column.Type == ReportColumnType.Money || column.Type == ReportColumnType.Decimal || column.Type == ReportColumnType.Integer || column.Type == ReportColumnType.Percent
            ? container.AlignRight()
            : container;

    /// <summary>The printed form of a cell. A value the system does not record prints as a dash, never as 0.</summary>
    public static string Display(ReportColumn column, object? value, TimeZoneInfo zone)
    {
        if (value is null) return "—";

        return value switch
        {
            ReportMoney money => money.Amount.ToString("N2", CultureInfo.InvariantCulture) + " " + money.CurrencyCode,
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateTimeOffset instant => TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            bool flag => flag ? "Yes" : "No",
            decimal number when column.Type == ReportColumnType.Percent => number.ToString("0.0", CultureInfo.InvariantCulture) + " %",
            decimal number => number.ToString("N2", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "",
        };
    }

    private static string FilterText(ReportResult result)
    {
        var parts = new List<string>();
        if (result.Filters.GroupBy is not null) parts.Add($"grouped by {result.Filters.GroupBy.ToLowerInvariant()}");
        parts.AddRange(result.Filters.Parameters.Select(p => $"{p.Key} = {p.Value}"));
        return string.Join(", ", parts);
    }

    private static TimeZoneInfo ResolveZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
