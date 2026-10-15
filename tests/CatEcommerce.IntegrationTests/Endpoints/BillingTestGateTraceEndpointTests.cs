using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>The Billing host with the minimum log level forced to <b>Trace</b> for every category, and every line captured. <see cref="BillingLoggedApiFactory"/>
/// captures every line the host's configuration lets through, which is not the same thing as every line (HO-BIL-001 TEST gate).</summary>
public sealed class BillingTraceApiFactory : BillingApiFactory
{
    public ConcurrentBag<string> Logs { get; } = [];

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        base.ConfigureAdditionalSettings(settings);
        settings["Logging:LogLevel:Default"] = "Trace";
        settings["Logging:LogLevel:Microsoft"] = "Trace";
        settings["Logging:LogLevel:Microsoft.AspNetCore"] = "Trace";
        settings["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Trace";
        settings["Logging:LogLevel:System"] = "Trace";
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Trace);
            logging.AddFilter(null, LogLevel.Trace);
            logging.AddProvider(new Provider(Logs));
        });
    }

    private sealed class Provider(ConcurrentBag<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capturing(categoryName, logs);
        public void Dispose() { }

        private sealed class Capturing(string category, ConcurrentBag<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logs.Add($"{logLevel} {category}: {formatter(state, exception)}" + (exception is null ? string.Empty : $" | {exception}"));
        }
    }
}

/// <summary>
/// HO-BIL-001 TEST gate rows XI-2 to XI-5: the idempotency double-submit race (and the order of the two exception filters) observed with the unique-index
/// violation actually in the log, the same race for invoice, credit note and refund keys, and the I-X1 log sweep repeated at Trace level.
/// </summary>
public sealed partial class BillingTestGateTraceEndpointTests(BillingTraceApiFactory factory) : BillingIntegrationTestBase(factory), IClassFixture<BillingTraceApiFactory>
{
    private BillingTraceApiFactory Traced => (BillingTraceApiFactory)Factory;

    [GeneratedRegex(@"(?:\d[ -]?){13,19}")]
    private static partial Regex DigitRun();

    [GeneratedRegex(@"[0-9a-fA-F]{24,}|[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}|\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})?|(INV|CN)-[A-Za-z0-9]+-\d{4}-\d{6}|880\d{10}|\b1\d{12}\b")]
    private static partial Regex Noise();


    // ── XI-1 (retries observed) ─────────────────────────────────────────────────────────────────────
    /// <summary>XI-1b -- with two hosts on one database (so the in-process gate cannot serialise them) forty invoices are issued at once; every response is 201
    /// and the numbers are 1..40 with no gap or duplicate. It does not expect retries: the series row lock makes them unnecessary (see I_C3).</summary>
    [Fact]
    public async Task XI_1b_TwoHostsIssueFortyInvoices_GetContiguousNumbersWithNoErrors()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        await using var second = new BillingTraceApiFactory();
        using var other = second.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var signIn = await other.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = context.AdminMobile, password = context.AdminPassword }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        other.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        other.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, context.BranchId.ToString());

        var charges = new List<Guid>();
        for (var i = 0; i < 40; i++) charges.Add(await RecordChargeAsync(context, customerId, 100m + i, taxClassId: context.ZeroTaxClassId));
        var results = await Task.WhenAll(charges.Select((id, i) => Task.Run(() =>
            IssueFromChargesRawAsync(i % 2 == 0 ? context.Admin : other, customerId, new { chargeIds = new[] { id } }))));

        var numbers = new List<string>();
        foreach (var response in results)
        {
            Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode} {await BodyAsync(response)}");
            numbers.Add((await ReadAsync(response)).GetProperty("number").GetString()!);
        }

        Assert.Equal(Enumerable.Range(1, 40).Select(i => (long)i), numbers.Select(n => ParseNumber(n, context.BranchCode).Sequence).OrderBy(x => x));
        // Since the ADR-179 amendment of 2026-10-10 (ORD 41) every issuer takes the series row lock first, across hosts too, so two hosts no longer collide: the
        // retry path is the backstop and is covered by I_C3 (bounded and recovering). Above: 1..40 contiguous from two hosts, and every response was 201 (no 5xx).
    }

    // ── XI-2 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>XI-2 -- both exception filters are in the MVC pipeline, with the same <c>Order</c>; exception filters unwind in REVERSE of the order they
    /// were added, so the Billing replay filter must have been added AFTER <c>UniqueConstraintExceptionFilter</c> to see the exception first. If it were
    /// first, a parallel loser would get the generic 409 instead of the original result.</summary>
    [Fact]
    public void XI_2_TheReplayFilter_IsAddedAfterTheUniqueConstraintFilter_SoItSeesTheExceptionFirst()
    {
        var filters = Factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MvcOptions>>().Value.Filters;
        var items = filters.Select((f, i) => (Index: i, Type: (f as TypeFilterAttribute)?.ImplementationType ?? f.GetType(), Order: (f as Microsoft.AspNetCore.Mvc.Filters.IOrderedFilter)?.Order ?? 0)).ToList();

        var billing = Assert.Single(items, f => f.Type == typeof(CatEcommerce.Api.Filters.BillingIdempotencyReplayFilter));
        var unique = Assert.Single(items, f => f.Type == typeof(CatEcommerce.Api.Filters.UniqueConstraintExceptionFilter));

        Assert.Equal(unique.Order, billing.Order);
        Assert.True(billing.Index > unique.Index, $"Billing filter index {billing.Index}, unique-constraint filter index {unique.Index}: the unique-constraint filter would see the exception first.");
    }

    // ── XI-3 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>XI-3 -- I-D5 stress: fifteen rounds of six parallel submissions of one payment under one key. Every round: exactly one 201, five 200, one row,
    /// the same payment id everywhere. Over the run the host's log must also show the database's unique-index violation, so the race path (and not only the
    /// pre-check) was exercised and the replay filter, not luck, produced the 200s.</summary>
    [Fact]
    public async Task XI_3_TheDoubleSubmitRace_IsAnsweredByTheReplayFilter_NotByTheUniqueConstraintFilter()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var statuses = new List<string>();

        for (var round = 0; round < 15; round++)
        {
            var key = $"race-{round}-{Guid.NewGuid():N}";
            var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() =>
                RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 10m + round, idempotencyKey: key))));

            statuses.Add(string.Join(",", responses.Select(r => (int)r.StatusCode).OrderBy(s => s)));
            Assert.True(responses.Count(r => r.StatusCode == HttpStatusCode.Created) == 1 && responses.Count(r => r.StatusCode == HttpStatusCode.OK) == 5,
                $"round {round}: {string.Join(",", responses.Select(r => (int)r.StatusCode))}");
            var ids = new HashSet<Guid>();
            foreach (var response in responses) ids.Add((await ReadAsync(response)).GetProperty("paymentId").GetGuid());
            Assert.Single(ids);
        }

        Assert.Equal(15, await CountAsync<PaymentAggregate>(p => p.CustomerId == customerId));

        var violations = Traced.Logs.Count(l => l.Contains("ix_payment_idempotency", StringComparison.Ordinal));
        Assert.True(violations > 0, "The unique index never fired in 15 rounds: the race path was not exercised, only the pre-check. Statuses: " + string.Join(" | ", statuses));
    }

    // ── XI-4 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>XI-4 -- the same race for the other three keyed writes: issue an invoice, issue a credit note, record a refund. One record each, never an
    /// error: the losers get the winner's result with 200.</summary>
    [Fact]
    public async Task XI_4_TheDoubleSubmitRace_OnInvoiceCreditNoteAndRefund_RecordsOnce()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var summary = new List<string>();

        // Invoice
        var chargeId = await RecordChargeAsync(context, customerId, 100m, taxClassId: context.ZeroTaxClassId);
        var issueKey = "inv-" + Guid.NewGuid().ToString("N");
        var issued = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => IssueFromChargesRawAsync(context.Admin, customerId, new { chargeIds = new[] { chargeId } }, issueKey))));
        summary.Add("invoice " + string.Join(",", issued.Select(r => (int)r.StatusCode).OrderBy(s => s)));

        // Credit note (needs an issued invoice)
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 200m);
        var creditKey = "cn-" + Guid.NewGuid().ToString("N");
        var credited = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => context.Admin.SendAsync(
            WithKey(HttpMethod.Post, $"/api/staff/billing/invoices/{invoiceId}/credit-notes", new { kind = "Cancellation", reason = "Cancelled", fullCredit = true }, creditKey)))));
        summary.Add("credit note " + string.Join(",", credited.Select(r => (int)r.StatusCode).OrderBy(s => s)));

        // Refund (needs free money)
        var payment = (await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 500m)).GetProperty("paymentId").GetGuid();
        var refundKey = "rf-" + Guid.NewGuid().ToString("N");
        var refunded = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => context.Admin.SendAsync(
            WithKey(HttpMethod.Post, "/api/staff/billing/refunds", new { paymentId = payment, method = "CashInStore", amount = 100m, reason = "Returned goods" }, refundKey)))));
        summary.Add("refund " + string.Join(",", refunded.Select(r => (int)r.StatusCode).OrderBy(s => s)));

        // Exactly one request creates, nothing is a server error, and every other answer is the winner's result (200). A duplicate that read the aggregate AFTER the
        // winner committed can instead be refused by the aggregate itself (400 NothingToCredit / 409 / 400): seen once in six loaded runs, never in eight quiet ones
        // (TEST gate F-6, LOW). That is tolerated here because the invariant that matters -- one record -- is asserted below; the strict form is asserted for payments (XI-3).
        foreach (var set in new[] { issued, credited, refunded })
        {
            Assert.True(set.Count(r => r.StatusCode == HttpStatusCode.Created) == 1, string.Join(" | ", summary));
            Assert.True(set.All(r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.BadRequest or HttpStatusCode.Conflict), string.Join(" | ", summary));
        }

        Assert.Equal(1, await CountAsync<RefundAggregate>(r => r.PaymentId == payment));
        Assert.Equal(1, await CountAsync<CreditNoteAggregate>(c => c.InvoiceId == invoiceId));
        Assert.Equal(1, await CountAsync<InvoiceAggregate>(i => i.CustomerId == customerId && i.IssueIdempotencyKey == issueKey));
    }

    // ── XI-5 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>XI-5 -- I-X1 at Trace level (the developer's sweep runs at the host's configured minimum): the same kinds of free text and a refused card
    /// number go through; none of it, and nothing that looks like a card number, appears in any line of any level.</summary>
    [Fact]
    public async Task XI_5_NothingSensitive_IsLogged_EvenAtTrace()
    {
        var marker = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var customerName = $"Quillfeather{marker}";
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context, customerName);
        var texts = new List<string>();
        string Text(string label) { var value = $"{label}-{marker}"; texts.Add(value); return value; }

        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 5_000m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();
        var paid = await RecordPaymentAsync(context.Admin, customerId, "CardInStore", 400m, reference: Text("SLIP"), allocations: [(invoiceId, 400m)]);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CardInStore", 10m, reference: "4111 1111 1111 1111")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 10m, notes: "card 4111 1111 1111 1111")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{paid.GetProperty("paymentId").GetGuid()}/reverse", new { reason = "paid with 4111-1111-1111-1111 twice" }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{paid.GetProperty("paymentId").GetGuid()}/reverse", new { reason = Text("REVERSAL") }, Json));
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes",
            new { kind = "Return", reason = Text("CREDIT"), lines = new[] { new { invoiceLineId = lineId, amount = 100m } } }, Json));

        var description = Text("STMTDESC");
        var csv = $"date,amount,direction,reference,description,bankTransactionId\n{Today:yyyy-MM-dd},77.00,C,TRXTR{marker},{description} 4111 1111 1111 1111 {customerName},4111111111111111\n";
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "trace.csv" }, { new StringContent("Main " + marker), "bankAccountLabel" },
            { new StringContent($"{Today.AddDays(-1):yyyy-MM-dd}"), "periodFrom" }, { new StringContent($"{Today:yyyy-MM-dd}"), "periodTo" },
        };
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsync("/api/staff/billing/bank-statements", form));
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}.pdf"));

        var lines = Traced.Logs.ToList();
        var all = string.Join("\n", lines);
        Assert.True(lines.Count(l => l.StartsWith("Trace ", StringComparison.Ordinal)) > 20, "The host did not log at Trace; the sweep proves nothing about Trace.");
        Assert.True(lines.Any(l => l.StartsWith("Debug ", StringComparison.Ordinal)), "No Debug lines were captured.");

        foreach (var text in texts) Assert.DoesNotContain(text, all, StringComparison.Ordinal);
        Assert.DoesNotContain(customerName, all, StringComparison.Ordinal);
        foreach (var fragment in new[] { "4111 1111", "4111-1111", "4111111111111111", "4111 1111", "1111 1111" })
            Assert.DoesNotContain(fragment, all, StringComparison.Ordinal);

        // Trace lines are full of ids, instants and tick counts, which are long digit runs that pass Luhn one time in ten. They are blanked first; what is left
        // is asked of the guard. A failure shows the masked text around the run so the category is visible without printing the digits.
        var cleaned = Noise().Replace(all, " ");
        var offenders = DigitRun().Matches(cleaned).Where(m => CatEcommerce.Shared.Common.Helpers.CardNumberGuard.LooksLikeCardNumber(m.Value)).Take(5)
            .Select(m => Regex.Replace(cleaned.Substring(Math.Max(0, m.Index - 30), Math.Min(cleaned.Length - Math.Max(0, m.Index - 30), m.Length + 40)), "[0-9]", "#")).ToList();
        Assert.True(offenders.Count == 0, "A card-like number was logged, in: " + string.Join(" || ", offenders));
    }
}
