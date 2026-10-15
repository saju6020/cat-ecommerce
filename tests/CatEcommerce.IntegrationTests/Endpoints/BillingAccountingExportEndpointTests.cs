using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 section 16 group H (I-H1..I-H3) -- the accounting export: invoices, payments, credit notes and refunds of a date range as a ZIP of
/// CSVs, identical on every re-run, every download recorded (US-BIL-09; ADR-190). This class moves the (fake) clock, so its factory is its own.</summary>
public sealed class BillingAccountingExportEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static string Url(DateOnly from, DateOnly to, Guid? branchId = null) =>
        $"/api/staff/billing/accounting-exports/download?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}" + (branchId is { } b ? $"&branchId={b}" : string.Empty);

    private static Dictionary<string, string> Unzip(byte[] zip)
    {
        using var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(e => e.FullName, e =>
        {
            using var reader = new StreamReader(e.Open(), new UTF8Encoding(false));
            return reader.ReadToEnd();
        });
    }

    private static async Task<byte[]> DownloadAsync(HttpClient client, DateOnly from, DateOnly to, Guid? branchId)
    {
        var response = await ExpectAsync(HttpStatusCode.OK, await client.GetAsync(Url(from, to, branchId)));
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal($"purrcare-accounts-{from:yyyy-MM-dd}-{to:yyyy-MM-dd}.zip", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        return await response.Content.ReadAsByteArrayAsync();
    }

    /// <summary>I-H1 + I-H2 -- the export of a day holds that day's invoices, lines, payments, allocations, credit notes and refunds, each row with its
    /// recordType and documentId; downloading it again after other days have changed (a payment and a credit note on the next day, an
    /// invoice settled) gives the same bytes, and each download is a run record with the file's SHA-256 (US-BIL-09 AC1, AC2).</summary>
    [Fact]
    public async Task I_H1_H2_TheExport_HoldsTheDaysFacts_AndIsIdenticalOnReRun()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var day = Today;

        var (invoiceId, invoiceNumber, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 600m, allocations: [(invoiceId, 600m)]);
        var paymentId = payment.GetProperty("paymentId").GetGuid();
        var note = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsyncCompat(
            $"/api/staff/billing/invoices/{invoiceId}/credit-notes", new { kind = "Return", reason = "Returned", lines = new[] { new { invoiceLineId = lineId, amount = 500m } } })));
        var refund = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsyncCompat(
            "/api/staff/billing/refunds", new { paymentId, method = "CashInStore", amount = 50m, reason = "Part refund" })));

        var first = await DownloadAsync(context.Admin, day, day, context.BranchId);
        var files = Unzip(first);

        Assert.Equal(AccountingExportFileNames, files.Keys.ToArray());
        Assert.Contains(invoiceNumber, files["invoices.csv"]);
        Assert.Contains(invoiceId.ToString(), files["invoices.csv"]);
        Assert.Contains(lineId.ToString(), files["invoice-lines.csv"]);
        Assert.Contains(paymentId.ToString(), files["payments.csv"]);
        Assert.Contains("\"Allocate\"", files["allocations.csv"]);
        Assert.Contains(note.GetProperty("creditNoteId").GetGuid().ToString(), files["credit-notes.csv"]);
        Assert.Contains(note.GetProperty("creditNoteId").GetGuid().ToString(), files["credit-note-lines.csv"]);
        Assert.Contains(refund.GetProperty("refundId").GetGuid().ToString(), files["refunds.csv"]);
        Assert.Contains("\"invoices\",\"1\"", files["manifest.csv"]);

        // Every data row names its recordType and documentId, and carries no running status.
        foreach (var (name, text) in files.Where(f => f.Key != "manifest.csv"))
        {
            var header = text.Split('\n')[0];
            Assert.StartsWith("recordType,documentId", header);
            Assert.DoesNotContain("status", header, StringComparison.OrdinalIgnoreCase);
            Assert.All(text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1), row => Assert.Matches("^\"[A-Za-z]+\",\"[0-9a-f-]{36}\"", row));
        }

        // The next day: another payment, another credit note, and the first invoice's status moves -- none of it is in the first day's range.
        Factory.Clock.Advance(TimeSpan.FromDays(1));
        await ReSignInAsync(context);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m);
        var other = await IssueSimpleInvoiceAsync(context, customerId, 500m);
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsyncCompat(
            $"/api/staff/billing/invoices/{other.InvoiceId}/credit-notes", new { kind = "Goodwill", reason = "Sorry", fullCredit = true }));

        var second = await DownloadAsync(context.Admin, day, day, context.BranchId);
        Assert.Equal(first, second);

        // ... but the next day's own export has them.
        var nextDay = Unzip(await DownloadAsync(context.Admin, day.AddDays(1), day.AddDays(1), context.BranchId));
        Assert.Contains(other.Number, nextDay["invoices.csv"]);
        Assert.DoesNotContain(invoiceNumber, nextDay["invoices.csv"]);

        // Two downloads of the first day are two run records of the same fingerprint, and the response header names it.
        var runs = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/billing/accounting-exports")));
        var mine = runs.GetProperty("items").EnumerateArray().Where(r => r.GetProperty("branchId").ValueKind == System.Text.Json.JsonValueKind.String
            && r.GetProperty("branchId").GetGuid() == context.BranchId && r.GetProperty("from").GetString() == day.ToString("yyyy-MM-dd")).ToList();
        Assert.Equal(2, mine.Count);
        var expectedSha = Convert.ToHexString(SHA256.HashData(first)).ToLowerInvariant();
        Assert.All(mine, r => Assert.Equal(expectedSha, r.GetProperty("sha256").GetString()));
        Assert.All(mine, r => Assert.Equal(1, r.GetProperty("invoiceCount").GetInt32()));
        Assert.All(mine, r => Assert.Equal(context.AdminStaffId, r.GetProperty("runBy").GetGuid()));

        var audit = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.Category == AuditCategory.BusinessEvent && a.Action == "AccountingExportRun" && a.AfterValue != null && a.AfterValue.Contains(expectedSha)).ToListAsync());
        Assert.Equal(2, audit.Count);
    }

    /// <summary>I-H3 -- a range over 366 days is refused before anything is built or recorded.</summary>
    [Fact]
    public async Task I_H3_ARangeOfFourHundredDays_IsRefused_AndRecordsNothing()
    {
        var context = await CreateBillingContextAsync();
        var before = await CountAsync<AccountingExportRunAggregate>(r => r.BranchId == context.BranchId);

        var response = await context.Admin.GetAsync(Url(Today.AddDays(-399), Today, context.BranchId));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.ExportRangeTooLarge, await ErrorCodesAsync(response));
        Assert.Equal(before, await CountAsync<AccountingExportRunAggregate>(r => r.BranchId == context.BranchId));

        var backwards = await context.Admin.GetAsync(Url(Today, Today.AddDays(-1), context.BranchId));
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);

        // 366 days exactly is allowed.
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync(Url(Today.AddDays(-365), Today, context.BranchId)));
    }

    [Fact]
    public async Task TheExport_IsForAccountantsAndAdministrators_AndTheRunsAreAppendOnly()
    {
        var context = await CreateBillingContextAsync();
        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);
        var (manager, _) = await StaffClientAsync(context, StaffRoles.ShopManager);
        var (accountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync(Url(Today, Today, context.BranchId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync(Url(Today, Today, context.BranchId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/staff/billing/accounting-exports")).StatusCode);
        await DownloadAsync(accountant, Today, Today, context.BranchId);

        var runId = (await WithDbAsync(db => db.Set<AccountingExportRunAggregate>().AsNoTracking().Where(r => r.BranchId == context.BranchId).Select(r => r.Id).ToListAsync())).Single();
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB")
            ?? "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres");
        await connection.OpenAsync();
        foreach (var sql in new[] { $"UPDATE billing.accounting_export_run SET sha256 = '{new string('0', 64)}' WHERE \"Id\" = '{runId}'", $"DELETE FROM billing.accounting_export_run WHERE \"Id\" = '{runId}'" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("P0001", error.SqlState);
        }
    }

    private static readonly string[] AccountingExportFileNames =
        ["manifest.csv", "invoices.csv", "invoice-lines.csv", "payments.csv", "allocations.csv", "credit-notes.csv", "credit-note-lines.csv", "refunds.csv"];
}

internal static class HttpClientJsonExtensions
{
    /// <summary>The camel-case JSON post every Billing integration test uses, as one call.</summary>
    public static Task<HttpResponseMessage> PostAsJsonAsyncCompat(this HttpClient client, string url, object body) =>
        System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(client, url, body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
}
