using System.Text;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Unit;

/// <summary>HO-RPT-001 section 15 A -- U-A9 (the CSV writer) and U-A10 (the PDF renderer). Plain unit tests: no host, no database.</summary>
public sealed class ReportingExportTests
{
    internal static ReportResult Result(IReadOnlyList<ReportColumn> columns, IReadOnlyList<ReportRow> rows, ReportRow? totals = null, IReadOnlyList<ReportNote>? notes = null, ReportCoverage? coverage = null) =>
        new("unit-report", "Unit test report", DateTimeOffset.Parse("2026-10-13T04:30:00+06:00"), "Test Admin",
            new ReportFilters(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 13), Guid.NewGuid(), "Gulshan Clinic", "Asia/Dhaka", null, new Dictionary<string, object> { ["mode"] = "Open" }),
            columns, rows, totals, notes ?? [], coverage, 1, rows.Count, rows.Count);

    private static ReportRow Row(params (string Key, object? Value)[] cells)
    {
        var row = new ReportRow();
        foreach (var (key, value) in cells) row[key] = value;
        return row;
    }

    // ── U-A9 ───────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_A9_AFormulaCell_ATabLeadingCell_AreGuarded_BengaliSurvives_NullIsEmpty_MoneyIsTwoDecimals()
    {
        var columns = new[] { new ReportColumn("name", "Name", ReportColumnType.Text), new ReportColumn("amount", "Amount", ReportColumnType.Money) };
        var rows = new[]
        {
            Row(("name", "=HYPERLINK(\"http://x\")"), ("amount", new ReportMoney(1500.5m, "BDT"))),
            Row(("name", "+1"), ("amount", new ReportMoney(0m, "BDT"))),
            Row(("name", "-1"), ("amount", new ReportMoney(-2.5m, "BDT"))),
            Row(("name", "@x"), ("amount", null)),
            Row(("name", "\tleading tab"), ("amount", new ReportMoney(1m, "BDT"))),
            Row(("name", "বিড়াল"), ("amount", new ReportMoney(3m, "BDT"))),
            Row(("name", null), ("amount", new ReportMoney(4m, "BDT"))),
        };

        var bytes = ReportCsvWriter.Build(Result(columns, rows));

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        Assert.Contains("\r\n", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));

        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("\"Name\",\"Amount\",\"Currency\"", lines[0]);
        Assert.Equal("\"'=HYPERLINK(\"\"http://x\"\")\",1500.50,\"BDT\"", lines[1]);
        Assert.StartsWith("\"'+1\"", lines[2]);
        Assert.StartsWith("\"'-1\",-2.50", lines[3]); // the money cell is a number, not guarded
        Assert.Equal("\"'@x\",,\"\"", lines[4]);
        Assert.StartsWith("\"'\tleading tab\"", lines[5]);
        Assert.Contains("বিড়াল", lines[6]);
        Assert.StartsWith(",4.00", lines[7]); // a null text cell is empty
    }

    [Fact]
    public void U_A9_Instants_AreInTheReportZone_Dates_AreIso_Percent_HasOneDecimal_Booleans_AreYesNo()
    {
        var columns = new[]
        {
            new ReportColumn("at", "At", ReportColumnType.Instant), new ReportColumn("on", "On", ReportColumnType.Date), new ReportColumn("pct", "Pct", ReportColumnType.Percent),
            new ReportColumn("flag", "Flag", ReportColumnType.Boolean), new ReportColumn("qty", "Qty", ReportColumnType.Decimal), new ReportColumn("n", "N", ReportColumnType.Integer),
        };
        var row = Row(("at", DateTimeOffset.Parse("2026-10-13T00:30:00Z")), ("on", new DateOnly(2026, 10, 13)), ("pct", 12.5m), ("flag", true), ("qty", 3.250m), ("n", 7L));

        var bytes = ReportCsvWriter.Build(Result(columns, [row]));
        var text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n")[1];

        Assert.StartsWith("2026-10-13 06:30,2026-10-13,12.5,Yes,3.250,7", text); // 00:30Z is 06:30 in Dhaka
    }

    [Fact]
    public void U_A9_ACurrencyColumn_IsOnlyWrittenWhenThereIsAMoneyColumn()
    {
        var plain = ReportCsvWriter.Build(Result([new ReportColumn("name", "Name", ReportColumnType.Text)], [Row(("name", "a"))]));

        Assert.DoesNotContain("Currency", Encoding.UTF8.GetString(plain));
    }

    // ── U-A10 ──────────────────────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(4, false)]
    [InlineData(6, false)]
    [InlineData(9, true)]
    public void U_A10_ThePdf_IsAPdf_WithTitlePeriodZoneBranchAndGenerator_PortraitUpToSixColumns(int columnCount, bool landscape)
    {
        var columns = Enumerable.Range(1, columnCount).Select(i => new ReportColumn($"c{i}", $"Column {i}", ReportColumnType.Text)).ToList();
        var rows = Enumerable.Range(1, 3).Select(r => Row(columns.Select(c => (c.Key, (object?)$"v{r}{c.Key}")).ToArray())).ToList();

        var bytes = new QuestPdfReportRenderer().Render(Result(columns, rows), new ReportRenderContext("PurrCare"));

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        var text = PdfTextExtractor.Extract(bytes);
        Assert.Contains("Unit test report", text);
        Assert.Contains("Gulshan Clinic", text);
        Assert.Contains("2026-10-01", text);
        Assert.Contains("2026-10-13", text);
        Assert.Contains("Asia/Dhaka", text);
        Assert.Contains("Test Admin", text);
        Assert.Contains("Column 1", text);
        Assert.Contains("v1c1", text);

        // The page box: A4 is 595 x 842 pt portrait, 842 x 595 landscape.
        var raw = Encoding.Latin1.GetString(bytes);
        Assert.Matches(landscape ? @"/MediaBox\s*\[\s*0\s+0\s+8[0-9\.]+\s+5[0-9\.]+" : @"/MediaBox\s*\[\s*0\s+0\s+5[0-9\.]+\s+8[0-9\.]+", raw);
    }

    [Fact]
    public void U_A10_ThePdf_PrintsTotals_Notes_Coverage_AndADashForANull_AndKeepsBengali()
    {
        var columns = new[] { new ReportColumn("name", "Name", ReportColumnType.Text), new ReportColumn("amount", "Amount", ReportColumnType.Money), new ReportColumn("margin", "Margin", ReportColumnType.Money) };
        var rows = new[] { Row(("name", "বিড়াল"), ("amount", new ReportMoney(100m, "BDT")), ("margin", null)) };
        var totals = Row(("amount", new ReportMoney(100m, "BDT")));
        var result = Result(columns, rows, totals, [new ReportNote("MarginCoverage", "Margin covers 83.0 % of shop revenue.")],
            new ReportCoverage(new ReportMoney(83m, "BDT"), new ReportMoney(100m, "BDT"), 83.0m));

        var pdf = new QuestPdfReportRenderer().Render(result, new ReportRenderContext("PurrCare"));
        var text = PdfTextExtractor.Extract(pdf);

        Assert.Contains("Total", text);
        Assert.Contains("Margin covers 83.0 % of shop revenue.", text);
        Assert.Contains("—", text);

        // Complex-script shaping re-orders glyph clusters, so the extractor cannot be expected to reproduce the text; the embedded fallback font is the evidence (OQ-11).
        Assert.Contains("NotoSansBengali", Encoding.Latin1.GetString(pdf));
    }

    [Fact]
    public void U_A10_ThePdf_Display_NeverPrintsZeroForAnUnrecordedValue()
    {
        var column = new ReportColumn("x", "X", ReportColumnType.Money);

        Assert.Equal("—", QuestPdfReportRenderer.Display(column, null, TimeZoneInfo.Utc));
        Assert.Equal("0.00 BDT", QuestPdfReportRenderer.Display(column, new ReportMoney(0m, "BDT"), TimeZoneInfo.Utc));
    }
}
