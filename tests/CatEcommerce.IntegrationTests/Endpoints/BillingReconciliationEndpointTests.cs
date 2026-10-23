using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 section 16 group G (I-G1..I-G5) -- reconciling courier cash-on-delivery against what was remitted, and bank transfers against
/// the bank statement, with unmatched entries flagged both ways (US-BIL-06 AC1, AC2; ADR-189; AC-8 reconciliation half).</summary>
public sealed class BillingReconciliationEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private const string Header = "date,amount,direction,reference,description,bankTransactionId";

    private static object Courier(string code = "PATHAO") => new { kind = "Courier", courierCode = code, courierName = code + " Courier" };

    private static string Row(DateOnly date, decimal amount, string direction, string reference, string description = "Transfer in") =>
        $"{date:yyyy-MM-dd},{amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)},{direction},{reference},{description},";

    private static MultipartFormDataContent Upload(string csv, string label, DateOnly from, DateOnly to, string fileName = "statement.csv")
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", fileName);
        content.Add(new StringContent(label), "bankAccountLabel");
        content.Add(new StringContent(from.ToString("yyyy-MM-dd")), "periodFrom");
        content.Add(new StringContent(to.ToString("yyyy-MM-dd")), "periodTo");
        return content;
    }

    private static string Csv(params string[] rows) => Header + "\n" + string.Join("\n", rows) + "\n";

    private async Task<Guid> ImportAsync(HttpClient client, string csv, string label, DateOnly from, DateOnly to)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await client.PostAsync("/api/staff/billing/bank-statements", Upload(csv, label, from, to)));
        return (await ReadAsync(response)).GetProperty("statementId").GetGuid();
    }

    private async Task<JsonElement> StatementViewAsync(HttpClient client, Guid statementId) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync($"/api/staff/billing/bank-statements/{statementId}/reconciliation")));

    // ── COD ─────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-G1 -- three courier collections of 500, 700 and 300; a remittance of 1 400 covers the first two with 500 and 600: a
    /// shortfall of 100 on the second, the third listed unremitted, 300 of the remittance not applied (US-BIL-06 AC1).</summary>
    [Fact]
    public async Task I_G1_ARemittance_ReconcilesTheCollections_AndListsTheShortfall()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var first = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 500m, collector: Courier())).GetProperty("paymentId").GetGuid();
        var second = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 700m, collector: Courier())).GetProperty("paymentId").GetGuid();
        var third = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 300m, collector: Courier())).GetProperty("paymentId").GetGuid();

        var created = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/cod/remittances", new
        {
            courierCode = "pathao", receivedOn = Today.ToString("yyyy-MM-dd"), amount = 1400m, method = "BankTransfer", reference = "REM-1",
            lines = new[] { new { paymentId = first, amountRemitted = 500m }, new { paymentId = second, amountRemitted = 600m } },
        }, Json)));
        Assert.Equal(300m, created.GetProperty("unapplied").GetDecimal());

        var collections = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/billing/cod/collections?courierCode=PATHAO")));
        var items = collections.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("paymentId").GetGuid());
        Assert.Equal("Remitted", items[first].GetProperty("status").GetString());
        Assert.Equal("Shortfall", items[second].GetProperty("status").GetString());
        Assert.Equal(100m, AmountOf(items[second].GetProperty("shortfall")));
        Assert.Equal("Unremitted", items[third].GetProperty("status").GetString());
        Assert.Equal(300m, AmountOf(items[third].GetProperty("outstanding")));

        var onlyShort = await ReadAsync(await context.Admin.GetAsync("/api/staff/billing/cod/collections?status=shortfall"));
        Assert.Equal(second, Assert.Single(onlyShort.GetProperty("items").EnumerateArray()).GetProperty("paymentId").GetGuid());
        var onlyUnremitted = await ReadAsync(await context.Admin.GetAsync("/api/staff/billing/cod/collections?status=unremitted"));
        Assert.Equal(third, Assert.Single(onlyUnremitted.GetProperty("items").EnumerateArray()).GetProperty("paymentId").GetGuid());

        var reconciliation = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/billing/cod/reconciliation?courierCode=PATHAO")));
        var courier = Assert.Single(reconciliation.GetProperty("couriers").EnumerateArray());
        Assert.Equal(1500m, AmountOf(courier.GetProperty("collected")));
        Assert.Equal(1100m, AmountOf(courier.GetProperty("remitted")));
        Assert.Equal(100m, AmountOf(courier.GetProperty("shortfall")));
        Assert.Equal(300m, AmountOf(courier.GetProperty("unremitted")));
        Assert.Equal(300m, AmountOf(courier.GetProperty("unappliedRemittances")));
        Assert.Equal(second, Assert.Single(courier.GetProperty("shortfallLines").EnumerateArray()).GetProperty("paymentId").GetGuid());

        var remittances = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/billing/cod/remittances")));
        Assert.Equal(300m, AmountOf(Assert.Single(remittances.GetProperty("items").EnumerateArray()).GetProperty("unapplied")));

        // U-G6 at the endpoint: the courier then remits the missing 100 -- the shortfall closes.
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/cod/remittances", new
        {
            courierCode = "PATHAO", receivedOn = Today.ToString("yyyy-MM-dd"), amount = 100m, method = "Cash",
            lines = new[] { new { paymentId = second, amountRemitted = 100m } },
        }, Json));
        var after = await ReadAsync(await context.Admin.GetAsync("/api/staff/billing/cod/collections?status=remitted"));
        Assert.Equal(2, after.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task ARemittance_OfTheWrongCourier_ANonCodPayment_OrMoreThanOutstanding_IsRefused()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var cod = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 500m, collector: Courier())).GetProperty("paymentId").GetGuid();
        var staffCod = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 100m, collector: new { kind = "Staff", staffAccountId = context.AdminStaffId })).GetProperty("paymentId").GetGuid();
        var cash = (await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 100m)).GetProperty("paymentId").GetGuid();

        Task<HttpResponseMessage> Remit(string courier, Guid paymentId, decimal amount, decimal received) =>
            context.Admin.PostAsJsonAsync("/api/staff/billing/cod/remittances", new
            {
                courierCode = courier, receivedOn = Today.ToString("yyyy-MM-dd"), amount = received, method = "Cash", lines = new[] { new { paymentId, amountRemitted = amount } },
            }, Json);

        var wrongCourier = await Remit("STEADFAST", cod, 100m, 100m);
        Assert.Equal(HttpStatusCode.BadRequest, wrongCourier.StatusCode);
        Assert.Contains(BusinessViolationCodes.CollectionCourierMismatch, await ErrorCodesAsync(wrongCourier));

        foreach (var notCourierCollected in new[] { staffCod, cash })
        {
            var refused = await Remit("PATHAO", notCourierCollected, 50m, 50m);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Contains(BusinessViolationCodes.CollectionNotCod, await ErrorCodesAsync(refused));
        }

        await ExpectAsync(HttpStatusCode.Created, await Remit("PATHAO", cod, 400m, 400m));
        var over = await Remit("PATHAO", cod, 101m, 101m);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains(BusinessViolationCodes.RemittanceExceedsCollection, await ErrorCodesAsync(over));
        Assert.Contains("BDT 100.00", await BodyAsync(over));

        var coversMoreThanReceived = await Remit("PATHAO", cod, 100m, 90m);
        Assert.Equal(HttpStatusCode.BadRequest, coversMoreThanReceived.StatusCode);

        var unknown = await Remit("PATHAO", Guid.NewGuid(), 10m, 10m);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    // ── Bank statements ─────────────────────────────────────────────────────────────────────────────
    /// <summary>I-G2 -- a statement of five credits against four recorded transfers: three match on their own; the pair that fights over the
    /// same payment is left for a person, flagged on both sides; a manual match then settles it (US-BIL-06 AC2).</summary>
    [Fact]
    public async Task I_G2_AutoMatch_MatchesTheUnambiguous_AndFlagsTheRestBothWays()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var p1 = (await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 100m, reference: $"TRX{tag}A1")).GetProperty("paymentId").GetGuid();
        var p2 = (await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 200m, reference: $"TRX{tag}A2")).GetProperty("paymentId").GetGuid();
        var p3 = (await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 300m, reference: $"TRX{tag}A3")).GetProperty("paymentId").GetGuid();
        var p4 = (await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 400m, reference: $"TRX{tag}A4")).GetProperty("paymentId").GetGuid();

        var statementId = await ImportAsync(context.Admin, Csv(
            Row(Today, 100m, "C", $"NEFT TRX-{tag}-A1"), Row(Today, 200m, "C", "", $"credit TRX{tag}A2 dhaka"), Row(Today, 300m, "C", $"trx {tag} a3"),
            Row(Today, 400m, "C", $"TRX{tag}A4"), Row(Today, 400m, "C", $"TRX{tag}A4 duplicate"), Row(Today, 50m, "D", "BANKFEE", "Bank charge")),
            "Main " + tag, Today.AddDays(-5), Today);

        var result = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null)));
        Assert.Equal(3, result.GetProperty("matched").GetInt32());
        Assert.Equal(2, result.GetProperty("ambiguous").GetInt32());
        Assert.Equal(0, result.GetProperty("none").GetInt32());

        var view = await StatementViewAsync(context.Admin, statementId);
        Assert.Equal(2, view.GetProperty("unmatchedStatementLines").GetArrayLength());
        Assert.Equal(p4, Assert.Single(view.GetProperty("unmatchedPayments").EnumerateArray()).GetProperty("paymentId").GetGuid());
        Assert.Equal(3, view.GetProperty("matches").GetArrayLength());
        Assert.Equal(1, view.GetProperty("debitLineCount").GetInt32());
        Assert.All(view.GetProperty("matches").EnumerateArray(), m => Assert.Equal("Auto", m.GetProperty("method").GetString()));
        Assert.Equal(new[] { p1, p2, p3 }.Order(), view.GetProperty("matches").EnumerateArray().Select(m => m.GetProperty("paymentId").GetGuid()).Order());

        // A second run adds nothing: what is matched stays matched, the ambiguous pair stays ambiguous.
        var again = await ReadAsync(await context.Admin.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null));
        Assert.Equal(0, again.GetProperty("matched").GetInt32());
        Assert.Equal(2, again.GetProperty("ambiguous").GetInt32());

        // A person settles one of the pair; the other remains flagged, and no payment is left unexplained.
        var lineId = view.GetProperty("unmatchedStatementLines")[0].GetProperty("lineId").GetGuid();
        var matched = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/bank-matches",
            new { statementId, statementLineId = lineId, paymentId = p4 }, Json)));
        var final = await StatementViewAsync(context.Admin, statementId);
        Assert.Single(final.GetProperty("unmatchedStatementLines").EnumerateArray());
        Assert.Equal(0, final.GetProperty("unmatchedPayments").GetArrayLength());
        Assert.Equal(4, final.GetProperty("statement").GetProperty("matchedCount").GetInt32());

        // Undoing a match keeps the row, frees both sides, and the reason is audited.
        var matchId = matched.GetProperty("matchId").GetGuid();
        var noReason = await context.Admin.PostAsJsonAsync($"/api/staff/billing/bank-matches/{matchId}/unmatch", new { reason = "" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/billing/bank-matches/{matchId}/unmatch", new { reason = "Matched the wrong credit" }, Json));
        Assert.Equal(2, (await StatementViewAsync(context.Admin, statementId)).GetProperty("unmatchedStatementLines").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.PostAsJsonAsync($"/api/staff/billing/bank-matches/{Guid.NewGuid()}/unmatch", new { reason = "x" }, Json)).StatusCode);

        var audit = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityId == matchId && a.Category == AuditCategory.BusinessEvent && a.Action == "BankLineUnmatched").ToListAsync());
        Assert.Equal("Matched the wrong credit", Assert.Single(audit).AfterValue);
        Assert.Equal(1, await CountAsync<BankMatchAggregate>(m => m.Id == matchId && m.UnmatchedOn != null));
    }

    /// <summary>I-G3 -- the same file again is refused (by its SHA-256); a different file for the account is fine.</summary>
    [Fact]
    public async Task I_G3_ImportingTheSameFileTwice_IsRefused()
    {
        var context = await CreateBillingContextAsync();
        var label = "Main " + NewCode();
        var csv = Csv(Row(Today, 100m, "C", "REF-1"));

        await ImportAsync(context.Admin, csv, label, Today.AddDays(-1), Today);
        var again = await context.Admin.PostAsync("/api/staff/billing/bank-statements", Upload(csv, label, Today.AddDays(-1), Today));

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.StatementDuplicate, await ErrorCodesAsync(again));

        await ImportAsync(context.Admin, csv, "Another " + NewCode(), Today.AddDays(-1), Today);
        await ImportAsync(context.Admin, Csv(Row(Today, 101m, "C", "REF-2")), label, Today.AddDays(-1), Today);
    }

    /// <summary>I-G4 -- a manual match needs equal amounts and an accepted bank transfer; a debit line cannot be matched.</summary>
    [Fact]
    public async Task I_G4_AManualMatch_NeedsEqualAmounts_AndABankTransfer()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var transfer = (await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 250m, reference: "TRX-MANUAL-1")).GetProperty("paymentId").GetGuid();
        var cash = (await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 250m)).GetProperty("paymentId").GetGuid();
        var statementId = await ImportAsync(context.Admin, Csv(Row(Today, 260m, "C", "x"), Row(Today, 250m, "C", "y"), Row(Today, 250m, "D", "z")),
            "Main " + NewCode(), Today.AddDays(-2), Today);
        var view = await StatementViewAsync(context.Admin, statementId);
        var lines = view.GetProperty("unmatchedStatementLines").EnumerateArray().ToList();
        var line260 = lines.Single(l => AmountOf(l.GetProperty("amount")) == 260m).GetProperty("lineId").GetGuid();
        var line250 = lines.Single(l => AmountOf(l.GetProperty("amount")) == 250m).GetProperty("lineId").GetGuid();

        Task<HttpResponseMessage> Match(Guid lineId, Guid paymentId) =>
            context.Admin.PostAsJsonAsync("/api/staff/billing/bank-matches", new { statementId, statementLineId = lineId, paymentId }, Json);

        var mismatch = await Match(line260, transfer);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Contains(BusinessViolationCodes.MatchAmountMismatch, await ErrorCodesAsync(mismatch));

        var notTransfer = await Match(line250, cash);
        Assert.Equal(HttpStatusCode.BadRequest, notTransfer.StatusCode);
        Assert.Contains(BusinessViolationCodes.MatchMethodNotTransfer, await ErrorCodesAsync(notTransfer));

        var unknownLine = await Match(Guid.NewGuid(), transfer);
        Assert.Equal(HttpStatusCode.BadRequest, unknownLine.StatusCode);
        Assert.Contains(BusinessViolationCodes.StatementLineNotFound, await ErrorCodesAsync(unknownLine));

        await ExpectAsync(HttpStatusCode.Created, await Match(line250, transfer));
        var again = await Match(line250, transfer);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.BankLineAlreadyMatched, await ErrorCodesAsync(again));
    }

    /// <summary>I-G5 -- two auto-matches at once leave exactly one match per line and per payment, and neither request fails.</summary>
    [Fact]
    public async Task I_G5_ConcurrentAutoMatches_CreateNoDuplicates()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var rows = new List<string>();
        for (var i = 1; i <= 6; i++)
        {
            await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 100m * i, reference: $"CON{tag}{i}");
            rows.Add(Row(Today, 100m * i, "C", $"CON{tag}{i}"));
        }

        var statementId = await ImportAsync(context.Admin, Csv([.. rows]), "Main " + tag, Today.AddDays(-1), Today);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => context.Admin.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var results = await Task.WhenAll(responses.Select(ReadAsync));
        Assert.Equal(6, results.Sum(r => r.GetProperty("matched").GetInt32()));
        Assert.Equal(6, await CountAsync<BankMatchAggregate>(m => m.StatementId == statementId && m.UnmatchedOn == null));
        Assert.Equal(6, (await StatementViewAsync(context.Admin, statementId)).GetProperty("matches").GetArrayLength());
    }

    /// <summary>C-5 / U-G5 at the endpoint -- a card number in a statement description is stored masked; a malformed row is named; a file over
    /// 1 MiB is refused with 413; nothing is kept of the file.</summary>
    [Fact]
    public async Task AStatement_IsMaskedOnImport_ARowErrorIsNamed_AndAnOversizedFileIsRefused()
    {
        var context = await CreateBillingContextAsync();
        var label = "Main " + NewCode();
        var statementId = await ImportAsync(context.Admin, Csv(Row(Today, 90m, "C", "R1", "paid by 4111 1111 1111 1111 thanks")), label, Today.AddDays(-1), Today);

        var line = await WithDbAsync(db => db.Set<BankStatementAggregate>().AsNoTracking().Where(s => s.Id == statementId).SelectMany(s => s.Lines).ToListAsync());
        Assert.Contains("••••1111", Assert.Single(line).Description);
        Assert.DoesNotContain("4111", line[0].Description);
        Assert.Equal(0, await CountAsync<AuditEntryAggregate>(a => a.AfterValue != null && a.AfterValue.Contains("4111 1111")));

        var bad = await context.Admin.PostAsync("/api/staff/billing/bank-statements",
            Upload(Csv(Row(Today, 1m, "C", "A"), "2026-13-45,1.00,C,SECRET,x,"), "Main " + NewCode(), Today.AddDays(-1), Today));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains(BusinessViolationCodes.StatementMalformed, await ErrorCodesAsync(bad));
        var body = await BodyAsync(bad);
        Assert.Contains("Row 3", body);
        Assert.DoesNotContain("SECRET", body);

        var huge = new string('x', BillingRules.MaxStatementBytes + 10);
        var tooLarge = await context.Admin.PostAsync("/api/staff/billing/bank-statements", Upload(Header + "\n" + huge, "Main " + NewCode(), Today.AddDays(-1), Today));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, tooLarge.StatusCode);
        Assert.Contains(BusinessViolationCodes.StatementTooLarge, await ErrorCodesAsync(tooLarge));

        var noFile = await context.Admin.PostAsync("/api/staff/billing/bank-statements", new MultipartFormDataContent { { new StringContent("Main"), "bankAccountLabel" } });
        Assert.Equal(HttpStatusCode.BadRequest, noFile.StatusCode);
    }

    [Fact]
    public async Task Reconciliation_IsForAccountantsAndAdministrators_AndScopedToTheBranch()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var (reception, _) = await StaffClientAsync(mine, StaffRoles.Reception);
        var (accountant, _) = await StaffClientAsync(mine, StaffRoles.Accountant);
        var statementId = await ImportAsync(accountant, Csv(Row(Today, 10m, "C", "R-1")), "Main " + NewCode(), Today.AddDays(-1), Today);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/staff/billing/cod/collections")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/staff/billing/bank-statements")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/billing/bank-statements/{statementId}/reconciliation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null)).StatusCode);

        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await accountant.GetAsync("/api/staff/billing/bank-statements")));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), s => s.GetProperty("statementId").GetGuid() == statementId);
        Assert.Equal(0, (await ReadAsync(await other.Admin.GetAsync("/api/staff/billing/bank-statements"))).GetProperty("items").GetArrayLength());
    }

    /// <summary>ADR-180 -- statements, matches, remittances are append-only in the database: a changed fact or a delete is refused.</summary>
    [Fact]
    public async Task ReconciliationRecords_CannotBeRewrittenOrDeleted()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var payment = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 100m, collector: Courier())).GetProperty("paymentId").GetGuid();
        var remittance = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/cod/remittances", new
        {
            courierCode = "PATHAO", receivedOn = Today.ToString("yyyy-MM-dd"), amount = 100m, method = "Cash", lines = new[] { new { paymentId = payment, amountRemitted = 100m } },
        }, Json)))).GetProperty("remittanceId").GetGuid();
        var statementId = await ImportAsync(context.Admin, Csv(Row(Today, 1m, "C", "R-9")), "Main " + NewCode(), Today.AddDays(-1), Today);

        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();
        foreach (var sql in new[]
        {
            $"UPDATE billing.cod_remittance SET amount = 1 WHERE \"Id\" = '{remittance}'",
            $"DELETE FROM billing.cod_remittance WHERE \"Id\" = '{remittance}'",
            $"UPDATE billing.cod_remittance_line SET amount_remitted = 1 WHERE remittance_id = '{remittance}'",
            $"UPDATE billing.bank_statement SET source_sha256 = '{new string('b', 64)}' WHERE \"Id\" = '{statementId}'",
            $"UPDATE billing.bank_statement_line SET amount = 2 WHERE statement_id = '{statementId}'",
            $"DELETE FROM billing.bank_statement_line WHERE statement_id = '{statementId}'",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("P0001", error.SqlState);
        }
    }
}
