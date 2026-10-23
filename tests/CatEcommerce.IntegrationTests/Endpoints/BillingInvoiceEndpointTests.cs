using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 §16 group C (I-C1…I-C10) and I-A3 -- itemised, tax-correct, gaplessly numbered, immutable invoices
/// (US-BIL-01, 02, 05, 07).</summary>
public sealed class BillingInvoiceEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private string FinancialYearCompact => DocumentNumberFormat.FinancialYearCompact(FinancialYearRules.For(Today, 7));

    private static NpgsqlConnection OpenRaw()
    {
        var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        connection.Open();
        return connection;
    }

    private static async Task<PostgresException> ExecuteExpectingTriggerAsync(string sql)
    {
        await using var connection = OpenRaw();
        await using var command = new NpgsqlCommand(sql, connection);
        return await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }

    // ── I-C1 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-C1 -- a consultation, a shop product and three boarding nights for one customer on ONE invoice, each line
    /// attributed to its business line, with tax per line (US-BIL-02, AC-16, US-ORD-17 server half).</summary>
    [Fact]
    public async Task I_C1_OneInvoice_CombinesClinicShopAndBoarding_WithTaxPerLine()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var stay = Guid.NewGuid();

        var consult = await RecordChargeAsync(context, customerId, 800m, 1m, itemKind: ItemKind.Service, sourceKind: ChargeSourceKind.Appointment, description: "Consultation");
        var product = await RecordChargeAsync(context, customerId, 450m, 2m, itemKind: ItemKind.Product, sourceKind: ChargeSourceKind.Order, description: "Cat food 2kg");
        var nights = new List<Guid>();
        for (var night = 1; night <= 3; night++)
            nights.Add(await RecordChargeAsync(context, customerId, 1000m, 1m, itemKind: ItemKind.BoardingNight, sourceKind: ChargeSourceKind.BoardingStay, sourceId: stay,
                sourceLineRef: $"night-{night}", taxClassId: context.ZeroTaxClassId, groupKind: BillingGroupKind.BoardingStay, groupId: stay, description: $"Boarding night {night}"));

        var chargeIds = new[] { consult, product }.Concat(nights).ToArray();

        var preview = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync("/api/staff/billing/invoices/preview", new { customerId, chargeIds }, Json)));
        Assert.True(preview.GetProperty("isEstimate").GetBoolean());
        Assert.Equal(4955m, AmountOf(preview.GetProperty("totals").GetProperty("total")));

        var invoiceId = await CreateDraftAsync(context.Admin, customerId, chargeIds);
        var draft = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.True(draft.GetProperty("isEstimate").GetBoolean());
        Assert.Equal(5, draft.GetProperty("lines").GetArrayLength());
        Assert.Equal(4955m, AmountOf(draft.GetProperty("totals").GetProperty("total")));

        var issue = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/issue", new { expectedVersion = draft.GetProperty("version").GetInt32() }, Json)));
        var number = issue.GetProperty("number").GetString()!;
        Assert.StartsWith($"INV-{context.BranchCode}-{FinancialYearCompact}-", number);

        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("Issued", invoice.GetProperty("status").GetString());
        Assert.False(invoice.GetProperty("isEstimate").GetBoolean());
        Assert.Equal(number, invoice.GetProperty("number").GetString());

        var totals = invoice.GetProperty("totals");
        Assert.Equal(4700m, AmountOf(totals.GetProperty("net")));
        Assert.Equal(255m, AmountOf(totals.GetProperty("tax")));
        Assert.Equal(4955m, AmountOf(totals.GetProperty("total")));
        Assert.Equal(4955m, AmountOf(invoice.GetProperty("outstanding")));

        var lines = invoice.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(["Clinic", "Shop", "Boarding", "Boarding", "Boarding"], lines.OrderBy(l => l.GetProperty("sortOrder").GetInt32()).Select(l => l.GetProperty("businessLine").GetString()!).ToArray());
        var consultLine = lines.Single(l => l.GetProperty("description").GetString() == "Consultation");
        Assert.Equal("Service", consultLine.GetProperty("itemKind").GetString());
        Assert.Equal("15.00", consultLine.GetProperty("taxRatePercent").GetString());
        Assert.Equal(800m, AmountOf(consultLine.GetProperty("netAmount")));
        Assert.Equal(120m, AmountOf(consultLine.GetProperty("taxAmount")));
        Assert.Equal(920m, AmountOf(consultLine.GetProperty("grossAmount")));
        var productLine = lines.Single(l => l.GetProperty("description").GetString() == "Cat food 2kg");
        Assert.Equal("2.000", productLine.GetProperty("quantity").GetString());
        Assert.Equal(135m, AmountOf(productLine.GetProperty("taxAmount")));
        var nightLine = lines.First(l => l.GetProperty("itemKind").GetString() == "BoardingNight");
        Assert.Equal("0.00", nightLine.GetProperty("taxRatePercent").GetString());
        Assert.False(string.IsNullOrWhiteSpace(nightLine.GetProperty("taxClassCode").GetString()));

        Assert.Equal("BIN-" + context.BranchCode, invoice.GetProperty("seller").GetProperty("taxRegistrationNumber").GetString());
        Assert.StartsWith("Billing Customer", invoice.GetProperty("billTo").GetProperty("fullName").GetString());

        // The charges are billed now; the unbilled list no longer shows them.
        var unbilled = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/charges?customerId={customerId}&status=unbilled"));
        Assert.Empty(unbilled.GetProperty("items").EnumerateArray());
        var billed = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/charges?customerId={customerId}&status=billed"));
        Assert.All(billed.GetProperty("items").EnumerateArray(), c => Assert.Equal(number, c.GetProperty("billedOnInvoice").GetProperty("number").GetString()));
    }

    // ── I-C2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C2_FiveInvoicesIssuedInSequence_HaveContiguousNumbers()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);

        var numbers = new List<string>();
        for (var i = 0; i < 5; i++) numbers.Add((await IssueSimpleInvoiceAsync(context, customerId)).Number);

        var sequences = numbers.Select(n => ParseNumber(n, context.BranchCode)).ToList();
        Assert.All(sequences, s => Assert.Equal(FinancialYearCompact, s.FinancialYearCompact));
        Assert.Equal(Enumerable.Range(1, 5).Select(i => (long)i), sequences.Select(s => s.Sequence));
        Assert.Equal(numbers, numbers.Distinct());
    }

    // ── I-C3 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-C3 -- twenty invoices issued at once in one branch: twenty distinct, contiguous numbers, no gap, no
    /// duplicate (US-BIL-01 AC2).</summary>
    [Fact]
    public async Task I_C3_TwentyInvoicesIssuedConcurrently_GetDistinctContiguousNumbers()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var chargeIds = new List<Guid>();
        for (var i = 0; i < 20; i++) chargeIds.Add(await RecordChargeAsync(context, customerId, 100m + i, taxClassId: context.ZeroTaxClassId));

        var results = await Task.WhenAll(chargeIds.Select(id => Task.Run(() => IssueFromChargesRawAsync(context.Admin, customerId, new { chargeIds = new[] { id } }))));

        foreach (var response in results) await ExpectAsync(HttpStatusCode.Created, response);
        var numbers = new List<string>();
        foreach (var response in results) numbers.Add((await ReadAsync(response)).GetProperty("number").GetString()!);

        Assert.Equal(20, numbers.Distinct().Count());
        var sequences = numbers.Select(n => ParseNumber(n, context.BranchCode).Sequence).OrderBy(s => s).ToList();
        Assert.Equal(Enumerable.Range(1, 20).Select(i => (long)i), sequences);

        // The database agrees: the series row advanced exactly to 21 and no invoice sequence repeats.
        var series = await WithDbAsync(db => db.DocumentNumberSeries.AsNoTracking().SingleAsync(s => s.BranchId == context.BranchId && s.DocumentKind == BillingDocumentKind.Invoice));
        Assert.Equal(21, series.NextSequence);
    }

    /// <summary>I-C3, the collision path itself (F35): two scopes read the same series row; the loser's
    /// <c>UPDATE … WHERE xmin</c> affects no row, it reloads ONLY the series row, takes the next number and saves again
    /// -- no gap, no reuse. The in-process gate never hides this: the two saves are not concurrent, the reads were.</summary>
    [Fact]
    public async Task I_C3_ACollisionOnTheSeriesRow_IsRetriedWithTheNextNumber()
    {
        var context = await CreateBillingContextAsync();
        await using var first = Factory.Services.CreateAsyncScope();
        await using var second = Factory.Services.CreateAsyncScope();
        var numberingA = first.ServiceProvider.GetRequiredService<IDocumentNumbering>();
        var numberingB = second.ServiceProvider.GetRequiredService<IDocumentNumbering>();

        var seriesA = await numberingA.EnsureSeriesAsync(context.BranchId, "2026-27", BillingDocumentKind.Invoice);
        var seriesB = await numberingB.EnsureSeriesAsync(context.BranchId, "2026-27", BillingDocumentKind.Invoice);
        Assert.Equal(seriesA.Id, seriesB.Id);

        var takenByA = seriesA.Take();
        await numberingA.SaveWithRetryAsync(seriesA, () => first.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync(), _ => { });

        var takenByB = seriesB.Take();
        Assert.Equal(takenByA, takenByB); // both read the same row: the same number, one of them must lose
        var renumbered = new List<long>();
        await numberingB.SaveWithRetryAsync(seriesB, () => second.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync(), renumbered.Add);

        Assert.Equal([takenByA + 1], renumbered);
        var row = await WithDbAsync(db => db.DocumentNumberSeries.AsNoTracking().SingleAsync(s => s.Id == seriesA.Id));
        Assert.Equal(takenByA + 2, row.NextSequence);
    }

#pragma warning disable EF1002 // the table name is a literal chosen by the test, never input
    private async Task BumpRowAsync(string table, Guid id) =>
        await WithDbAsync(db => db.Database.ExecuteSqlRawAsync($"UPDATE billing.{table} SET \"Version\" = \"Version\" + 1 WHERE \"Id\" = {{0}}", id));
#pragma warning restore EF1002

    /// <summary>The retry is bounded (ADR-179): every attempt loses its xmin check -- a rival touches the series row between
    /// the read and the write, every time -- and after <c>MaxNumberingAttempts</c> the failure surfaces (a <c>409</c> at the edge).</summary>
    [Fact]
    public async Task I_C3_TheRetryIsBounded_AfterFiveAttemptsTheConcurrencyFailureSurfaces()
    {
        var context = await CreateBillingContextAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var numbering = scope.ServiceProvider.GetRequiredService<IDocumentNumbering>();
        var series = await numbering.EnsureSeriesAsync(context.BranchId, "2026-27", BillingDocumentKind.Invoice);
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        series.Take();

        var attempts = 0;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => numbering.SaveWithRetryAsync(
            series,
            async () =>
            {
                attempts++;
                // Since ORD 41 the unscoped issuer holds the series row lock for the whole save, so a rival can no longer bump the row from another
                // connection (it would wait). The stale read is simulated instead: the tracked row claims an xmin the database no longer has.
                var token = db.Entry(series).Properties.First(p => p.Metadata.IsConcurrencyToken);
                token.OriginalValue = Convert.ChangeType(1, token.Metadata.ClrType);
                await db.SaveChangesAsync();
            },
            _ => { }));

        Assert.Equal(BillingRules.MaxNumberingAttempts, attempts);
    }

    /// <summary>The retry RECOVERS (ADR-179, backlog ORD 45a): the first attempt loses its xmin check (simulated stale read, as in the bounded test), the second
    /// succeeds. The document is renumbered once with the series' next number and the series row advances by exactly one -- no gap, no reuse.</summary>
    [Fact]
    public async Task I_C3_ARetryThatRecovers_FailsOnceOnAStaleXmin_ThenSucceedsOnAttemptTwoWithNoGap()
    {
        var context = await CreateBillingContextAsync();
        await using var scope = Factory.Services.CreateAsyncScope();
        var numbering = scope.ServiceProvider.GetRequiredService<IDocumentNumbering>();
        var series = await numbering.EnsureSeriesAsync(context.BranchId, "2026-27", BillingDocumentKind.Invoice);
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var firstNumber = series.Take();

        var attempts = 0;
        var renumbered = new List<long>();
        await numbering.SaveWithRetryAsync(
            series,
            async () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    var token = db.Entry(series).Properties.First(p => p.Metadata.IsConcurrencyToken);
                    token.OriginalValue = Convert.ChangeType(1, token.Metadata.ClrType);
                }

                await db.SaveChangesAsync();
            },
            renumbered.Add);

        Assert.Equal(2, attempts);
        Assert.Equal([firstNumber], renumbered); // nothing else moved the row, so the reload hands out the same number: not skipped, not doubled
        var row = await WithDbAsync(d => d.DocumentNumberSeries.AsNoTracking().SingleAsync(s => s.Id == series.Id));
        Assert.Equal(firstNumber + 1, row.NextSequence);

        // The next issuer gets the very next number: the failed attempt consumed nothing.
        await using var next = Factory.Services.CreateAsyncScope();
        var nextSeries = await next.ServiceProvider.GetRequiredService<IDocumentNumbering>().EnsureSeriesAsync(context.BranchId, "2026-27", BillingDocumentKind.Invoice);
        Assert.Equal(firstNumber + 1, nextSeries.Take());
    }

    [Fact]
    public async Task AConcurrencyFailureOfSomethingOtherThanTheSeries_IsNotRetried()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var chargeId = await RecordChargeAsync(context, customerId);
        await using var scope = Factory.Services.CreateAsyncScope();
        var numbering = scope.ServiceProvider.GetRequiredService<IDocumentNumbering>();
        var series = await numbering.EnsureSeriesAsync(context.BranchId, "2026-27", BillingDocumentKind.Invoice);
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var charge = await db.Charges.FirstAsync(c => c.Id == chargeId);
        charge.Void("Another aggregate collided", Now, new CatEcommerce.Domain.Models.BillingActor(new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));

        var attempts = 0;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => numbering.SaveWithRetryAsync(
            series,
            async () =>
            {
                attempts++;
                await BumpRowAsync("charge", chargeId);
                await db.SaveChangesAsync();
            },
            _ => { }));

        Assert.Equal(1, attempts);
    }

    // ── I-C4 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-C4 -- an issued invoice keeps the rate it was issued at; a later rate change reaches only later
    /// invoices (US-BIL-07 AC2).</summary>
    [Fact]
    public async Task I_C4_ARateChange_NeverTouchesAnIssuedInvoice_ButAppliesToTheNext()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);

        var first = await IssueSimpleInvoiceAsync(context, customerId, 1000m, taxClassId: context.TaxClassId);
        var before = await GetInvoiceAsync(context.Admin, first.InvoiceId);
        Assert.Equal(150m, AmountOf(before.GetProperty("totals").GetProperty("tax")));

        await SeedTaxRateAsync(context.TaxClassId, 20m, Now.AddDays(1));
        Factory.Clock.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
        await ReSignInAsync(context);

        var second = await IssueSimpleInvoiceAsync(context, customerId, 1000m, taxClassId: context.TaxClassId);

        var after = await GetInvoiceAsync(context.Admin, first.InvoiceId);
        Assert.Equal("15.00", after.GetProperty("lines")[0].GetProperty("taxRatePercent").GetString());
        Assert.Equal(AmountOf(before.GetProperty("totals").GetProperty("total")), AmountOf(after.GetProperty("totals").GetProperty("total")));

        var next = await GetInvoiceAsync(context.Admin, second.InvoiceId);
        Assert.Equal("20.00", next.GetProperty("lines")[0].GetProperty("taxRatePercent").GetString());
        Assert.Equal(200m, AmountOf(next.GetProperty("totals").GetProperty("tax")));
        Assert.Equal(1200m, AmountOf(next.GetProperty("totals").GetProperty("total")));
    }

    // ── I-C5, I-C6 ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C5_TwoDraftsClaimingTheSameCharge_OneWins_TheOtherIs409()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var chargeId = await RecordChargeAsync(context, customerId);

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
            context.Admin.PostAsJsonAsync("/api/staff/billing/invoices", new { customerId, chargeIds = new[] { chargeId } }, Json))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Contains(BusinessViolationCodes.ChargeAlreadyInvoiced, await BodyAsync(loser));
        Assert.Equal(1, await WithDbAsync(db => db.Set<InvoiceAggregate>().AsNoTracking().CountAsync(i => i.CustomerId == customerId)));
    }

    [Fact]
    public async Task I_C6_ADiscardedDraft_ReturnsItsChargesToUnbilled_AndANewDraftCanTakeThem()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var chargeId = await RecordChargeAsync(context, customerId);

        var firstDraft = await CreateDraftAsync(context.Admin, customerId, [chargeId]);
        Assert.Equal(HttpStatusCode.Conflict, (await context.Admin.PostAsJsonAsync("/api/staff/billing/invoices", new { customerId, chargeIds = new[] { chargeId } }, Json)).StatusCode);

        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsync($"/api/staff/billing/invoices/{firstDraft}/discard", null));
        Assert.Equal("Discarded", (await GetInvoiceAsync(context.Admin, firstDraft)).GetProperty("status").GetString());

        var unbilled = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/charges?customerId={customerId}&status=unbilled"));
        Assert.Single(unbilled.GetProperty("items").EnumerateArray());

        var secondDraft = await CreateDraftAsync(context.Admin, customerId, [chargeId]);
        Assert.NotEqual(firstDraft, secondDraft);
    }

    [Fact]
    public async Task ADraft_CanHaveItsChargesReplaced_ButOnlyWithTheCurrentVersion()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var a = await RecordChargeAsync(context, customerId, 100m);
        var b = await RecordChargeAsync(context, customerId, 200m);
        var draftId = await CreateDraftAsync(context.Admin, customerId, [a]);
        var version = (await GetInvoiceAsync(context.Admin, draftId)).GetProperty("version").GetInt32();

        var stale = await context.Admin.PutAsJsonAsync($"/api/staff/billing/invoices/{draftId}/charges", new { expectedVersion = version + 7, chargeIds = new[] { a, b } }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var updated = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.PutAsJsonAsync($"/api/staff/billing/invoices/{draftId}/charges", new { expectedVersion = version, chargeIds = new[] { b } }, Json)));
        Assert.Equal(b, Assert.Single(updated.GetProperty("lines").EnumerateArray()).GetProperty("chargeId").GetGuid());

        // The dropped charge is unbilled again; the kept one is billed on the draft.
        var unbilled = (await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/charges?customerId={customerId}&status=unbilled"))).GetProperty("items");
        Assert.Equal(a, Assert.Single(unbilled.EnumerateArray()).GetProperty("chargeId").GetGuid());
    }

    // ── I-C7 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-C7 -- <c>issue-from-charges</c> with a <c>Source</c> selector takes only that source's charges; with a
    /// <c>BillingGroup</c> selector only that group's charges up to <c>serviceDateTo</c> (BRD-20 support).</summary>
    [Fact]
    public async Task I_C7_TheSelectorForms_TakeOnlyTheirOwnCharges()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);

        var orderId = Guid.NewGuid();
        await RecordChargeAsync(context, customerId, 100m, sourceKind: ChargeSourceKind.Order, sourceId: orderId, sourceLineRef: "l1");
        await RecordChargeAsync(context, customerId, 200m, sourceKind: ChargeSourceKind.Order, sourceId: orderId, sourceLineRef: "l2");
        var otherOrderCharge = await RecordChargeAsync(context, customerId, 999m, sourceKind: ChargeSourceKind.Order, sourceId: Guid.NewGuid());

        var bySource = await IssueFromChargesRawAsync(context.Admin, customerId, new { source = new { kind = "Order", id = orderId } });
        var sourceInvoice = await GetInvoiceAsync(context.Admin, (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, bySource))).GetProperty("invoiceId").GetGuid());
        Assert.Equal(2, sourceInvoice.GetProperty("lines").GetArrayLength());
        Assert.Equal(300m, AmountOf(sourceInvoice.GetProperty("totals").GetProperty("net")));

        var stay = Guid.NewGuid();
        foreach (var night in Enumerable.Range(0, 5))
            await RecordChargeAsync(context, customerId, 1000m, itemKind: ItemKind.BoardingNight, sourceKind: ChargeSourceKind.BoardingStay, sourceId: stay, sourceLineRef: $"night-{night}",
                groupKind: BillingGroupKind.BoardingStay, groupId: stay, serviceDate: Today.AddDays(night - 4));
        var vetDuringStay = await RecordChargeAsync(context, customerId, 500m, sourceKind: ChargeSourceKind.Visit, groupKind: BillingGroupKind.BoardingStay, groupId: stay, serviceDate: Today.AddDays(-3));

        var cutoff = Today.AddDays(-2);
        var byGroup = await IssueFromChargesRawAsync(context.Admin, customerId, new { billingGroup = new { kind = "BoardingStay", id = stay, serviceDateTo = cutoff.ToString("yyyy-MM-dd") } });
        var cycle = await GetInvoiceAsync(context.Admin, (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, byGroup))).GetProperty("invoiceId").GetGuid());
        // nights dated Today-4 .. Today-2 plus the vet visit of the stay (Today-3): four lines; the two later nights wait for the next cycle
        Assert.Equal(4, cycle.GetProperty("lines").GetArrayLength());
        Assert.Contains(cycle.GetProperty("lines").EnumerateArray(), l => l.GetProperty("chargeId").GetGuid() == vetDuringStay);
        Assert.Equal("Clinic", cycle.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("chargeId").GetGuid() == vetDuringStay).GetProperty("businessLine").GetString());

        var unbilled = (await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/charges?customerId={customerId}&status=unbilled"))).GetProperty("items");
        Assert.Equal(3, unbilled.GetArrayLength()); // the other order's charge and the two later nights
        Assert.Contains(unbilled.EnumerateArray(), c => c.GetProperty("chargeId").GetGuid() == otherOrderCharge);

        // Nothing matches -> the charge is not found; two forms at once -> refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await IssueFromChargesRawAsync(context.Admin, customerId, new { source = new { kind = "Order", id = Guid.NewGuid() } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await IssueFromChargesRawAsync(context.Admin, customerId, new { chargeIds = new[] { otherOrderCharge }, source = new { kind = "Order", id = orderId } })).StatusCode);
    }

    // ── I-C8 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C8_TheSameIdempotencyKey_IssuesOneInvoice_AndTheSecondCallReturnsTheSameNumber()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var chargeId = await RecordChargeAsync(context, customerId);
        var key = "key-" + Guid.NewGuid().ToString("N");

        var first = await ExpectAsync(HttpStatusCode.Created, await IssueFromChargesRawAsync(context.Admin, customerId, new { chargeIds = new[] { chargeId } }, key));
        var second = await ExpectAsync(HttpStatusCode.OK, await IssueFromChargesRawAsync(context.Admin, customerId, new { chargeIds = new[] { chargeId } }, key));

        var a = await ReadAsync(first);
        var b = await ReadAsync(second);
        Assert.Equal(a.GetProperty("number").GetString(), b.GetProperty("number").GetString());
        Assert.Equal(a.GetProperty("invoiceId").GetGuid(), b.GetProperty("invoiceId").GetGuid());
        Assert.Equal(1, await WithDbAsync(db => db.Set<InvoiceAggregate>().AsNoTracking().CountAsync(i => i.CustomerId == customerId)));

        // The same key for a different customer is a conflict, not a replay.
        var otherCustomer = await SeedCustomerAsync(context);
        var otherCharge = await RecordChargeAsync(context, otherCustomer);
        var clash = await IssueFromChargesRawAsync(context.Admin, otherCustomer, new { chargeIds = new[] { otherCharge } }, key);
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Contains(BusinessViolationCodes.IdempotencyKeyConflict, await BodyAsync(clash));
    }

    [Fact]
    public async Task TheIdempotencyKey_OnIssuingADraft_ReplaysForThatDraft_AndConflictsForAnother()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var draftA = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId)]);
        var draftB = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId)]);
        var key = "issue-" + Guid.NewGuid().ToString("N");
        var versionA = (await GetInvoiceAsync(context.Admin, draftA)).GetProperty("version").GetInt32();

        var issued = await ExpectAsync(HttpStatusCode.OK, await context.Admin.SendAsync(WithKey(HttpMethod.Post, $"/api/staff/billing/invoices/{draftA}/issue", new { expectedVersion = versionA }, key)));
        var replay = await ExpectAsync(HttpStatusCode.OK, await context.Admin.SendAsync(WithKey(HttpMethod.Post, $"/api/staff/billing/invoices/{draftA}/issue", new { expectedVersion = versionA }, key)));
        Assert.Equal((await ReadAsync(issued)).GetProperty("number").GetString(), (await ReadAsync(replay)).GetProperty("number").GetString());

        var versionB = (await GetInvoiceAsync(context.Admin, draftB)).GetProperty("version").GetInt32();
        var clash = await context.Admin.SendAsync(WithKey(HttpMethod.Post, $"/api/staff/billing/invoices/{draftB}/issue", new { expectedVersion = versionB }, key));
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal("Draft", (await GetInvoiceAsync(context.Admin, draftB)).GetProperty("status").GetString());
    }

    // ── Issue refusals ──────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task IssuingWithoutABillingProfile_IsRefused_AndLeavesTheDraftAlone()
    {
        var context = await CreateBillingContextAsync(withProfile: false);
        var customerId = await SeedCustomerAsync(context);
        var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId)]);
        var version = (await GetInvoiceAsync(context.Admin, draftId)).GetProperty("version").GetInt32();

        var response = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/issue", new { expectedVersion = version }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.BillingProfileMissing, await BodyAsync(response));
        Assert.Equal("Draft", (await GetInvoiceAsync(context.Admin, draftId)).GetProperty("status").GetString());
        Assert.Equal(0, await CountAsync<DocumentNumberSeriesAggregate>(s => s.BranchId == context.BranchId && s.NextSequence > 1)); // no number was consumed
    }

    [Fact]
    public async Task ALineWhoseTaxClassHasNoRate_RefusesToIssue_NamingTheClass_AndConsumesNoNumber()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var unrated = await context.Admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = "NORATE" + NewCode(), name = "No rate yet" }, Json);
        var unratedId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, unrated))).GetProperty("taxClassId").GetGuid();
        var chargeId = await RecordChargeAsync(context, customerId, taxClassId: unratedId);

        var response = await IssueFromChargesRawAsync(context.Admin, customerId, new { chargeIds = new[] { chargeId } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Contains(BusinessViolationCodes.TaxRateNotConfigured, body);
        Assert.Contains("NORATE", body);
        Assert.Equal(0, await CountAsync<InvoiceAggregate>(i => i.CustomerId == customerId)); // issue-from-charges left no draft behind
        Assert.Equal(0, await CountAsync<DocumentNumberSeriesAggregate>(s => s.BranchId == context.BranchId && s.NextSequence > 1));
    }

    [Fact]
    public async Task IssuingAnInvoiceAlreadyIssued_Or_WithAStaleVersion_IsRefused()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId)]);
        var version = (await GetInvoiceAsync(context.Admin, draftId)).GetProperty("version").GetInt32();

        Assert.Equal(HttpStatusCode.Conflict, (await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/issue", new { expectedVersion = version + 3 }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/issue", new { expectedVersion = version }, Json));

        var again = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/issue", new { expectedVersion = version + 1 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvoiceNotDraft, await BodyAsync(again));
    }

    [Fact]
    public async Task ABilledCharge_CannotBeVoided()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (_, _, chargeId) = await IssueSimpleInvoiceAsync(context, customerId);

        var response = await context.Admin.PostAsJsonAsync($"/api/staff/billing/charges/{chargeId}/void", new { reason = "oops" }, Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.ChargeAlreadyInvoiced, await BodyAsync(response));
    }

    // ── I-C9 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C9_TheInvoicePdf_IsAPdf_WithTheNumber_TheSellersTaxNumber_AndEachLine()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context, "Rahim Uddin");
        var consult = await RecordChargeAsync(context, customerId, 800m, itemKind: ItemKind.Service, sourceKind: ChargeSourceKind.Appointment, description: "Annual wellness consultation", taxClassId: context.TaxClassId);
        var product = await RecordChargeAsync(context, customerId, 450m, 2m, description: "Premium kitten food", taxClassId: context.TaxClassId);
        var (invoiceId, number) = await IssueFromChargesAsync(context.Admin, customerId, [consult, product]);

        var response = await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}.pdf"));

        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var pdf = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        var text = PdfTextExtractor.Extract(pdf);
        Assert.Contains(number, text);
        Assert.Contains("BIN-" + context.BranchCode, text);
        Assert.Contains("Annual wellness consultation", text);
        Assert.Contains("Premium kitten food", text);
        Assert.Contains("Rahim Uddin", text);
        Assert.Contains("15%", text);

        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.GetAsync($"/api/staff/billing/invoices/{Guid.NewGuid()}.pdf")).StatusCode);
    }

    [Fact]
    public async Task ADraftHasNoPdf()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId)]);

        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.GetAsync($"/api/staff/billing/invoices/{draftId}.pdf")).StatusCode);
    }

    // ── I-C10 ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C10_IssuingAnInvoice_PutsTheMessageInTheCustomersInbox_AfterTheDeliveryPass()
    {
        var context = await CreateBillingContextAsync();
        var (customerClient, customerId) = await RegisterAndSignInCustomerAsync();
        var (_, number, _) = await IssueSimpleInvoiceAsync(context, customerId, 1500m);

        await RunDeliveryPassesUntilAsync(async () => (await InboxBodiesAsync(customerClient)).Any(b => b.Contains(number)));

        var inbox = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await customerClient.GetAsync("/api/customers/me/inbox")));
        var item = inbox.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("body").GetString()!.Contains(number));
        Assert.Contains("BDT 1,500.00", item.GetProperty("body").GetString());
    }

    // ── I-A3 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-A3 -- through Npgsql, not EF: an issued invoice's content, its lines, and the row itself are protected by
    /// the database (US-BIL-05 AC1, D-5). The triggers are real; a test never disables them.</summary>
    [Fact]
    public async Task I_A3_TheDatabaseRefusesToChangeOrDeleteAnIssuedInvoice_OrItsLines()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m, taxClassId: context.TaxClassId);

        foreach (var sql in new[]
        {
            $"UPDATE billing.invoice SET total = total + 1 WHERE \"Id\" = '{invoiceId}'",
            $"UPDATE billing.invoice SET number = 'INV-HACKED' WHERE \"Id\" = '{invoiceId}'",
            $"UPDATE billing.invoice SET seller_tax_registration_number = 'X' WHERE \"Id\" = '{invoiceId}'",
            $"UPDATE billing.invoice SET customer_id = gen_random_uuid() WHERE \"Id\" = '{invoiceId}'",
            $"UPDATE billing.invoice SET status = {InvoiceStatus.Draft.Value} WHERE \"Id\" = '{invoiceId}'",
            $"DELETE FROM billing.invoice WHERE \"Id\" = '{invoiceId}'",
            $"UPDATE billing.invoice_line SET tax_rate_percent = 0 WHERE invoice_id = '{invoiceId}'",
            $"UPDATE billing.invoice_line SET description = 'edited' WHERE invoice_id = '{invoiceId}'",
            $"DELETE FROM billing.invoice_line WHERE invoice_id = '{invoiceId}'",
            $"INSERT INTO billing.invoice_line (id, invoice_id, business_line, item_kind, source_kind, source_id, description, quantity, unit_price, line_discount, invoice_discount_share, tax_class_id, tax_rate_percent, net_amount, tax_amount, gross_amount, sort_order, invoice_discarded, is_frozen) VALUES (gen_random_uuid(), '{invoiceId}', 1, 1, 1, gen_random_uuid(), 'sneaked in', 1, 1, 0, 0, gen_random_uuid(), 0, 1, 0, 1, 99, false, true)",
        })
        {
            var error = await ExecuteExpectingTriggerAsync(sql);
            Assert.Equal("P0001", error.SqlState);
        }

        // Nothing changed.
        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("Issued", invoice.GetProperty("status").GetString());
        Assert.Equal(1150m, AmountOf(invoice.GetProperty("totals").GetProperty("total")));
        Assert.Equal(1, invoice.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task I_A3_ADraftInvoicesLines_AreRemovable_ButNeverTheInvoiceRow()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId)]);

        var error = await ExecuteExpectingTriggerAsync($"DELETE FROM billing.invoice WHERE \"Id\" = '{draftId}'");
        Assert.Equal("P0001", error.SqlState);

        await using var connection = OpenRaw();
        await using var delete = new NpgsqlCommand($"DELETE FROM billing.invoice_line WHERE invoice_id = '{draftId}'", connection);
        Assert.Equal(1, await delete.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task TheStatusAndTheCachedTotals_MayStillMoveOnAnIssuedInvoice_ButNothingElse()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);

        await using var connection = OpenRaw();
        await using var move = new NpgsqlCommand(
            $"UPDATE billing.invoice SET paid_total = 400, status = {InvoiceStatus.PartPaid.Value} WHERE \"Id\" = '{invoiceId}'", connection);
        Assert.Equal(1, await move.ExecuteNonQueryAsync());
    }

    // ── Listing, export, access ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheInvoiceList_FiltersByStatusNumberAndBusinessLine_AndTheCsvIsInjectionGuarded()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context, "=cmd|' /C calc'!A0");
        var (invoiceId, number, _) = await IssueSimpleInvoiceAsync(context, customerId, 500m);
        var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId, sourceKind: ChargeSourceKind.BoardingStay, itemKind: ItemKind.BoardingNight)]);

        var issued = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/invoices?customerId={customerId}&status=Issued"));
        Assert.Equal(invoiceId, Assert.Single(issued.GetProperty("items").EnumerateArray()).GetProperty("invoiceId").GetGuid());

        var drafts = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/invoices?customerId={customerId}&status=Draft"));
        Assert.Equal(draftId, Assert.Single(drafts.GetProperty("items").EnumerateArray()).GetProperty("invoiceId").GetGuid());

        var byNumber = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/invoices?number={Uri.EscapeDataString(number[4..].ToLowerInvariant())}"));
        Assert.Contains(byNumber.GetProperty("items").EnumerateArray(), i => i.GetProperty("invoiceId").GetGuid() == invoiceId);

        var boarding = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/invoices?customerId={customerId}&businessLine=Boarding"));
        Assert.Equal(draftId, Assert.Single(boarding.GetProperty("items").EnumerateArray()).GetProperty("invoiceId").GetGuid());

        var csv = await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/invoices/export.csv?customerId={customerId}"));
        Assert.StartsWith("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("\"'=cmd|", text); // the formula is neutralised
        Assert.DoesNotContain(",\"=cmd", text);
        Assert.Contains(number, text);
        Assert.DoesNotContain("\r", text);
    }

    [Fact]
    public async Task AnotherBranchsInvoice_Is404_AndInvoiceEndpointsAreRestrictedToBillingStaff()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(mine);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(mine, customerId);

        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}.pdf")).StatusCode);

        var (vet, _) = await StaffClientAsync(mine, StaffRoles.Vet);
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync($"/api/staff/billing/invoices/{invoiceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync($"/api/staff/billing/invoices/{invoiceId}")).StatusCode);
    }

    [Fact]
    public async Task CustomersSeeOnlyTheirOwnIssuedInvoices_NeverADraft_NeverSomeoneElses()
    {
        var context = await CreateBillingContextAsync();
        var (clientA, customerA) = await RegisterAndSignInCustomerAsync();
        var (clientB, _) = await RegisterAndSignInCustomerAsync();
        var (invoiceId, number, _) = await IssueSimpleInvoiceAsync(context, customerA, 700m);
        var draftId = await CreateDraftAsync(context.Admin, customerA, [await RecordChargeAsync(context, customerA)]);

        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await clientA.GetAsync("/api/customers/me/invoices")));
        Assert.Equal(invoiceId, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("invoiceId").GetGuid());

        var detail = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await clientA.GetAsync($"/api/customers/me/invoices/{invoiceId}")));
        Assert.Equal(number, detail.GetProperty("invoice").GetProperty("number").GetString());
        Assert.Contains(number, detail.GetProperty("howToPay").GetProperty("referenceInstruction").GetString());
        Assert.Single(detail.GetProperty("howToPay").GetProperty("bankAccounts").EnumerateArray());

        var pdf = await ExpectAsync(HttpStatusCode.OK, await clientA.GetAsync($"/api/customers/me/invoices/{invoiceId}.pdf"));
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/customers/me/invoices/{draftId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/customers/me/invoices/{invoiceId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/customers/me/invoices/{invoiceId}.pdf")).StatusCode);
        Assert.Empty((await ReadAsync(await clientB.GetAsync("/api/customers/me/invoices"))).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync("/api/customers/me/invoices")).StatusCode);
    }
}
