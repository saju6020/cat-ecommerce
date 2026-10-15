using System.IO.Compression;
using System.Text;
using CatEcommerce.Infrastructure.Billing.Export;

namespace CatEcommerce.Infrastructure.Tests.Billing;

/// <summary>HO-BIL-001 section 15 group H (U-H1..U-H4; ADR-190, F36) -- the accounting export is a deterministic ZIP of CSVs of immutable facts.</summary>
public sealed class AccountingExportWriterTests
{
    private static readonly DateOnly Day = new(2026, 10, 7);
    private static readonly Guid Branch = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Customer = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static AccountingExportContent Content(string customerName = "Jane Doe", string description = "Consultation", bool reverse = false)
    {
        var invoiceA = new ExportInvoice(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "INV-MAIN-2627-000001", "2026-27", Branch, Customer, customerName, Day, Day, "BDT", 100m, 0m, 15m, 115m, null);
        var invoiceB = new ExportInvoice(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002"), "INV-MAIN-2627-000002", "2026-27", Branch, Customer, customerName, Day.AddDays(1), null, "BDT", 200m, 0m, 0m, 200m, invoiceA.Id);
        var lineA = new ExportInvoiceLine(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"), invoiceA.Id, invoiceA.Number, 0, "Clinic", "Service", description, 1m, 100m, 0m, 0m, "STD", 15m, 100m, 15m, 115m);
        var payment = new ExportPayment("Payment", Guid.Parse("cccccccc-0000-0000-0000-000000000001"), 1, Branch, Customer, "CashInStore", 115m, "BDT", Day, Day, null, null);
        var reversal = payment with { RecordType = "PaymentReversal", Date = Day.AddDays(2) };
        var entry = new ExportAllocation(Guid.Parse("dddddddd-0000-0000-0000-000000000001"), payment.Id, invoiceA.Id, "Allocate", null, 115m, Day, new DateTimeOffset(2026, 10, 7, 4, 0, 0, TimeSpan.Zero));
        var note = new ExportCreditNote(Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"), "CN-MAIN-2627-000001", invoiceA.Id, invoiceA.Number, Branch, Customer, "Return", "BDT", Day, 10m, 1.5m, 11.5m, 0m);
        var noteLine = new ExportCreditNoteLine(Guid.Parse("ffffffff-0000-0000-0000-000000000001"), note.Id, note.Number, 0, lineA.Id, description, 15m, 10m, 1.5m, 11.5m);
        var refund = new ExportRefund(Guid.Parse("99999999-0000-0000-0000-000000000001"), payment.Id, note.Id, Branch, Customer, "CashInStore", 11.5m, "BDT", Day, null);

        ExportInvoice[] invoices = [invoiceA, invoiceB];
        ExportPayment[] payments = [payment, reversal];
        return new AccountingExportContent(
            Day, Day.AddDays(3), null, "BDT", reverse ? invoices.Reverse().ToArray() : invoices, [lineA], reverse ? payments.Reverse().ToArray() : payments, [entry], [note], [noteLine], [refund]);
    }

    private static Dictionary<string, string> Unzip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var reader = new StreamReader(e.Open(), new UTF8Encoding(false));
            return reader.ReadToEnd();
        });
    }

    [Fact]
    public void U_H1_TheSameFacts_ProduceByteIdenticalOutput_AndTheSameFingerprint()
    {
        var first = AccountingExportWriter.Write(Content());
        var second = AccountingExportWriter.Write(Content());

        Assert.Equal(first.Bytes, second.Bytes);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(64, first.Sha256.Length);

        // The order the facts arrive in never changes the output.
        Assert.Equal(first.Bytes, AccountingExportWriter.Write(Content(reverse: true)).Bytes);
        // A different fact does.
        Assert.NotEqual(first.Sha256, AccountingExportWriter.Write(Content(description: "Vaccination")).Sha256);
    }

    [Fact]
    public void U_H2_TheEntries_AreInAFixedOrder_WithAFixedTimestamp()
    {
        var file = AccountingExportWriter.Write(Content());

        using var archive = new ZipArchive(new MemoryStream(file.Bytes), ZipArchiveMode.Read);
        Assert.Equal(AccountingExportWriter.EntryOrder, archive.Entries.Select(e => e.FullName).ToArray());
        Assert.All(archive.Entries, e => Assert.Equal(AccountingExportWriter.FixedTimestamp.Year, e.LastWriteTime.Year));
        Assert.All(archive.Entries, e => Assert.Equal(1980, e.LastWriteTime.Year));
    }

    [Theory]
    [InlineData("=SUM(A1)")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@cmd")]
    [InlineData("  =HYPERLINK(\"x\")")]
    [InlineData("\t=1+1")]
    public void U_H3_ACellStartingWithAFormulaCharacter_IsPrefixedWithAQuote(string text)
    {
        var files = Unzip(AccountingExportWriter.Write(Content(customerName: text, description: text)).Bytes);

        Assert.Contains("\"'" + text.Replace("\"", "\"\"") + "\"", files["invoices.csv"]);
        Assert.Contains("\"'" + text.Replace("\"", "\"\"") + "\"", files["invoice-lines.csv"]);
    }

    [Fact]
    public void U_H4_EveryRow_HasARecordTypeAndADocumentId_AndNoCurrentStatusColumn()
    {
        var files = Unzip(AccountingExportWriter.Write(Content()).Bytes);

        foreach (var (name, text) in files)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var header = lines[0].Split(',');
            Assert.Equal("recordType", header[0]);
            Assert.Equal("documentId", header[1]);
            Assert.DoesNotContain(header, h => h is "status" or "paidTotal" or "creditedTotal" or "outstanding" or "paid" or "credited");

            foreach (var row in lines.Skip(1))
            {
                var cells = row.Split(',');
                Assert.False(string.IsNullOrWhiteSpace(cells[0].Trim('"')), $"{name}: a row has no recordType");
                Assert.False(string.IsNullOrWhiteSpace(cells[1].Trim('"')), $"{name}: a row has no documentId");
            }
        }

        Assert.Contains("PaymentReversal", files["payments.csv"]);
        Assert.Contains("\"Refund\"", files["refunds.csv"]);
    }

    [Fact]
    public void TheCsv_IsUtf8WithoutABom_InvariantCulture_AndEndsLinesWithLineFeed()
    {
        var zip = AccountingExportWriter.Write(Content()).Bytes;
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        using var raw = new MemoryStream();
        using (var stream = archive.GetEntry("invoices.csv")!.Open()) stream.CopyTo(raw);
        var bytes = raw.ToArray();

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("\n", text);
        Assert.Contains("115.00", text);
        Assert.DoesNotContain("115,00", text);
    }

    [Fact]
    public void TheManifest_ListsTheRangeAndEveryCount_AndAnEmptyExportIsStillValid()
    {
        var files = Unzip(AccountingExportWriter.Write(Content()).Bytes);
        Assert.Contains("\"invoices\",\"2\"", files["manifest.csv"]);
        Assert.Contains("\"from\",\"2026-10-07\"", files["manifest.csv"]);
        Assert.Contains("\"branchId\",\"all\"", files["manifest.csv"]);

        var empty = AccountingExportWriter.Write(new AccountingExportContent(Day, Day, Branch, "BDT", [], [], [], [], [], [], []));
        Assert.Equal(0, empty.InvoiceCount);
        Assert.Equal(AccountingExportWriter.EntryOrder, Unzip(empty.Bytes).Keys.ToArray());
    }
}
