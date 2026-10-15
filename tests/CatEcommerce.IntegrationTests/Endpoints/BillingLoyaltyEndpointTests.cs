using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 section 16 group I (I-I1..I-I3) -- loyalty points earned once on a paid invoice, redeemed as a discount shown on the invoice and
/// its PDF, taken back by a credit note; and customer credit applied to an invoice, partially then fully (US-BIL-08, US-BIL-10; ADR-191).
/// Loyalty is a global setting, so each test switches it on for its own duration and back off afterwards.</summary>
public sealed class BillingLoyaltyEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private async Task SetLoyaltyAsync(HttpClient admin, bool enabled) =>
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync("/api/admin/settings", new
        {
            changes = new[]
            {
                new { key = SettingKeys.BillingLoyaltyEnabled, value = enabled ? "true" : "false" },
                new { key = SettingKeys.BillingLoyaltySpendPerPoint, value = "100" },
                new { key = SettingKeys.BillingLoyaltyPointValue, value = "1" },
                new { key = SettingKeys.BillingLoyaltyMinimumRedemptionPoints, value = "100" },
            },
        }, Json));

    private async Task<JsonElement> LoyaltyAsync(HttpClient client, Guid customerId) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync($"/api/staff/billing/customers/{customerId}/loyalty")));

    /// <summary>I-I1 -- loyalty on: an invoice paid in full earns floor(net / 100) points, once; a credit note takes back what its net earned.</summary>
    [Fact]
    public async Task I_I1_APaidInvoice_AccruesPointsOnce_AndACreditNoteTakesThemBack()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, true);
        try
        {
            var customerId = await SeedCustomerAsync(context);
            var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1050m);
            var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();

            await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 500m, allocations: [(invoiceId, 500m)]);
            Assert.Equal(0, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32()); // part-paid earns nothing yet

            await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 550m, allocations: [(invoiceId, 550m)]);
            var ledger = await LoyaltyAsync(context.Admin, customerId);
            Assert.True(ledger.GetProperty("enabled").GetBoolean());
            Assert.Equal(10, ledger.GetProperty("balance").GetInt32());
            var accrual = Assert.Single(ledger.GetProperty("entries").GetProperty("items").EnumerateArray());
            Assert.Equal("Accrual", accrual.GetProperty("kind").GetString());
            Assert.Equal(10, accrual.GetProperty("points").GetInt32());
            Assert.Equal(invoiceId, accrual.GetProperty("invoiceId").GetGuid());

            Assert.Equal(1, await CountAsync<LoyaltyEntryAggregate>(e => e.CustomerId == customerId && e.Kind == LoyaltyEntryKind.Accrual));

            // The customer sees it too, and the summary carries the balance (HO-POR-001's input).
            var summary = await GetSummaryAsync(context.Admin, customerId);
            Assert.True(summary.GetProperty("loyaltyEnabled").GetBoolean());
            Assert.Equal(10, summary.GetProperty("loyaltyPoints").GetInt32());

            // A credit note for 350 of the net takes back floor(350 / 100) = 3 points.
            await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes", new
            { kind = "Return", reason = "Returned", lines = new[] { new { invoiceLineId = lineId, amount = 350m } } }, Json));
            var after = await LoyaltyAsync(context.Admin, customerId);
            Assert.Equal(7, after.GetProperty("balance").GetInt32());
            Assert.Contains(after.GetProperty("entries").GetProperty("items").EnumerateArray(), e => e.GetProperty("kind").GetString() == "AccrualReversal" && e.GetProperty("points").GetInt32() == 3);
        }
        finally
        {
            await SetLoyaltyAsync(context.Admin, false);
        }
    }

    [Fact]
    public async Task WhenLoyaltyIsOff_NothingAccrues_AndARedemptionIsRefused()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, false);
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 5000m);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 5000m, allocations: [(invoiceId, 5000m)]);

        Assert.Equal(0, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("entries").GetProperty("items").GetArrayLength());

        var draft = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId, 1000m, 1m, taxClassId: context.ZeroTaxClassId)]);
        var refused = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draft}/loyalty-redemption", new { points = 100 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.LoyaltyNotEnabled, await ErrorCodesAsync(refused));
    }

    /// <summary>I-I2 -- redeem 200 points on a draft and issue it: the discount shows on the invoice and its PDF, tax is charged on the reduced
    /// nets, and the balance is spent. Removing the redemption (or discarding the draft) gives the points back (US-BIL-10 AC2).</summary>
    [Fact]
    public async Task I_I2_ARedemption_IsADiscountOnTheInvoice_TaxedOnTheReducedNet()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, true);
        try
        {
            var customerId = await SeedCustomerAsync(context);
            var (earned, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 30_000m);
            await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 30_000m, allocations: [(earned, 30_000m)]);
            Assert.Equal(300, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32());

            var chargeId = await RecordChargeAsync(context, customerId, 1000m, 1m, taxClassId: context.TaxClassId);
            var draftId = await CreateDraftAsync(context.Admin, customerId, [chargeId]);

            var belowMinimum = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/loyalty-redemption", new { points = 50 }, Json);
            Assert.Contains(BusinessViolationCodes.LoyaltyBelowMinimum, await ErrorCodesAsync(belowMinimum));
            var tooMany = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/loyalty-redemption", new { points = 301 }, Json);
            Assert.Contains(BusinessViolationCodes.LoyaltyInsufficientPoints, await ErrorCodesAsync(tooMany));
            var aboveNet = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/loyalty-redemption", new { points = 300 }, Json);
            Assert.Equal(HttpStatusCode.OK, aboveNet.StatusCode); // 300 is worth 300, within the 1 000 net

            // Remove it (points return), then redeem 200.
            var removed = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.DeleteAsync($"/api/staff/billing/invoices/{draftId}/loyalty-redemption")));
            Assert.Equal(JsonValueKind.Null, removed.GetProperty("invoice").GetProperty("loyaltyRedemption").ValueKind);
            Assert.Equal(300, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32());

            var applied = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync(
                $"/api/staff/billing/invoices/{draftId}/loyalty-redemption", new { points = 200 }, Json)));
            Assert.Equal(200, applied.GetProperty("invoice").GetProperty("loyaltyRedemption").GetProperty("points").GetInt32());
            Assert.Equal(100, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32());

            var again = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/loyalty-redemption", new { points = 100 }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

            var draft = await GetInvoiceAsync(context.Admin, draftId);
            await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync(
                $"/api/staff/billing/invoices/{draftId}/issue", new { expectedVersion = draft.GetProperty("version").GetInt32() }, Json));

            var invoice = await GetInvoiceAsync(context.Admin, draftId);
            var totals = invoice.GetProperty("totals");
            Assert.Equal(800m, AmountOf(totals.GetProperty("net")));  // 1 000 less the 200 redemption, before tax
            Assert.Equal(120m, AmountOf(totals.GetProperty("tax")));   // 15 percent of the reduced net of 800
            Assert.Equal(920m, AmountOf(totals.GetProperty("total")));
            var line = Assert.Single(invoice.GetProperty("lines").EnumerateArray());
            Assert.Equal(200m, AmountOf(line.GetProperty("invoiceDiscountShare")));

            var pdf = await (await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/invoices/{draftId}.pdf"))).Content.ReadAsByteArrayAsync();
            Assert.Contains("Loyalty redemption", PdfTextExtractor.Extract(pdf));
            Assert.Contains("200 points", PdfTextExtractor.Extract(pdf));

            // A redemption entry stays; spending is on the ledger.
            var ledger = await LoyaltyAsync(context.Admin, customerId);
            Assert.Contains(ledger.GetProperty("entries").GetProperty("items").EnumerateArray(), e => e.GetProperty("kind").GetString() == "Redemption" && e.GetProperty("points").GetInt32() == 200);
        }
        finally
        {
            await SetLoyaltyAsync(context.Admin, false);
        }
    }

    [Fact]
    public async Task ADiscardedDraft_GivesItsRedeemedPointsBack()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, true);
        try
        {
            var customerId = await SeedCustomerAsync(context);
            var (earned, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 20_000m);
            await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 20_000m, allocations: [(earned, 20_000m)]);

            var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId, 1000m, 1m, taxClassId: context.ZeroTaxClassId)]);
            await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draftId}/loyalty-redemption", new { points = 150 }, Json));
            Assert.Equal(50, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32());

            await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsync($"/api/staff/billing/invoices/{draftId}/discard", null));

            Assert.Equal(200, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32());
        }
        finally
        {
            await SetLoyaltyAsync(context.Admin, false);
        }
    }

    /// <summary>I-I3 -- customer credit applied to an invoice partially, then fully: the outstanding and the credit left are shown after each
    /// (US-BIL-08).</summary>
    [Fact]
    public async Task I_I3_CustomerCredit_AppliesPartiallyThenFully()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var credit = (await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m)).GetProperty("paymentId").GetGuid();
        var deposit = (await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 5000m, earmark: new { kind = "BoardingStay", sourceId = Guid.NewGuid(), label = "Stay deposit" })).GetProperty("paymentId").GetGuid();
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 400m);

        Assert.Equal(1000m, AmountOf((await GetSummaryAsync(context.Admin, customerId)).GetProperty("freeCredit")));

        var part = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync(
            $"/api/staff/billing/customers/{customerId}/apply-credit", new { invoiceId, amount = 150m }, Json)));
        Assert.Equal(250m, part.GetProperty("invoiceOutstanding").GetDecimal());
        Assert.Equal(850m, part.GetProperty("freeCreditRemaining").GetDecimal());
        Assert.Equal(150m, Assert.Single(part.GetProperty("applied").EnumerateArray()).GetProperty("amount").GetDecimal());

        var full = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync(
            $"/api/staff/billing/customers/{customerId}/apply-credit", new { invoiceId }, Json)));
        Assert.Equal(0m, full.GetProperty("invoiceOutstanding").GetDecimal());
        Assert.Equal(600m, full.GetProperty("freeCreditRemaining").GetDecimal());

        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("Paid", invoice.GetProperty("status").GetString());
        Assert.Equal(400m, invoice.GetProperty("payments").EnumerateArray().Sum(p => AmountOf(p.GetProperty("amount"))));

        var summary = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(600m, AmountOf(summary.GetProperty("freeCredit")));
        Assert.Equal(5000m, AmountOf(Assert.Single(summary.GetProperty("earmarkedCredit").EnumerateArray()).GetProperty("amount"))); // the deposit was never touched
        Assert.NotEqual(Guid.Empty, credit);
        Assert.NotEqual(Guid.Empty, deposit);

        var settled = await context.Admin.PostAsJsonAsync($"/api/staff/billing/customers/{customerId}/apply-credit", new { invoiceId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, settled.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvoiceNotIssued, await ErrorCodesAsync(settled));
    }

    [Fact]
    public async Task CustomerCredit_IsForTheRightRole_TheCustomersOwnInvoice_AndBranch()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(mine);
        var stranger = await SeedCustomerAsync(mine);
        await RecordPaymentAsync(mine.Admin, customerId, "CashInStore", 500m);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(mine, stranger, 100m);
        var (vet, _) = await StaffClientAsync(mine, StaffRoles.Vet);

        Assert.Equal(HttpStatusCode.Forbidden, (await vet.PostAsJsonAsync($"/api/staff/billing/customers/{customerId}/apply-credit", new { invoiceId }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.PostAsJsonAsync($"/api/staff/billing/customers/{customerId}/apply-credit", new { invoiceId }, Json)).StatusCode);

        var wrongOwner = await mine.Admin.PostAsJsonAsync($"/api/staff/billing/customers/{customerId}/apply-credit", new { invoiceId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, wrongOwner.StatusCode);
        Assert.Contains(BusinessViolationCodes.AllocationInvoiceCustomerMismatch, await ErrorCodesAsync(wrongOwner));
    }

    /// <summary>Two partial credit notes on one paid invoice each take their points back (an earlier version collided on the per-invoice key and the
    /// second credit note answered 500 after it had committed, leaving the customer the points).</summary>
    [Fact]
    public async Task TwoPartialCreditNotes_OnOnePaidInvoice_EachTakePointsBack()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, true);
        try
        {
            var customerId = await SeedCustomerAsync(context);
            var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 2000m);
            var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();
            await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 2000m, allocations: [(invoiceId, 2000m)]);
            Assert.Equal(20, (await LoyaltyAsync(context.Admin, customerId)).GetProperty("balance").GetInt32());

            await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes",
                new { kind = "Return", reason = "First return", lines = new[] { new { invoiceLineId = lineId, amount = 500m } } }, Json));
            await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes",
                new { kind = "Return", reason = "Second return", lines = new[] { new { invoiceLineId = lineId, amount = 700m } } }, Json));

            var ledger = await LoyaltyAsync(context.Admin, customerId);
            var items = ledger.GetProperty("entries").GetProperty("items").EnumerateArray().ToList();
            var reversals = items.Where(e => e.GetProperty("kind").GetString() == "AccrualReversal").ToList();

            Assert.Equal(20 - 5 - 7, ledger.GetProperty("balance").GetInt32());
            Assert.Equal(2, reversals.Count);
            Assert.All(reversals, r => Assert.Equal(invoiceId, r.GetProperty("invoiceId").GetGuid())); // still shown against its invoice
            var fromLedger = items.Sum(e => e.GetProperty("kind").GetString() == "Accrual" ? e.GetProperty("points").GetInt32()
                : e.GetProperty("kind").GetString() == "AccrualReversal" ? -e.GetProperty("points").GetInt32() : 0);
            Assert.Equal(ledger.GetProperty("balance").GetInt32(), fromLedger); // the balance always equals its ledger
        }
        finally
        {
            await SetLoyaltyAsync(context.Admin, false);
        }
    }

    /// <summary>ADR-192 -- a merge moves the duplicate whole points balance to the survivor, once (two ledger entries, keyed by the duplicate).</summary>
    [Fact]
    public async Task AMerge_MovesTheLoyaltyBalanceToTheSurvivor_Once()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, true);
        try
        {
            var (survivorClient, survivorId) = await RegisterAndSignInCustomerAsync();
            var (duplicateClient, duplicateId) = await RegisterAndSignInCustomerAsync();
            var shared = NewMobile();
            await ShareContactNumberAsync(survivorClient, shared);
            await ShareContactNumberAsync(duplicateClient, shared);

            var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, duplicateId, 800m);
            await RecordPaymentAsync(context.Admin, duplicateId, "CashInStore", 800m, allocations: [(invoiceId, 800m)]);
            Assert.Equal(8, (await LoyaltyAsync(context.Admin, duplicateId)).GetProperty("balance").GetInt32());

            await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge",
                new { survivingCustomerId = survivorId, reason = "Same person registered twice" }, Json));

            Assert.Equal(0, (await LoyaltyAsync(context.Admin, duplicateId)).GetProperty("balance").GetInt32());
            var survivor = await LoyaltyAsync(context.Admin, survivorId);
            Assert.Equal(8, survivor.GetProperty("balance").GetInt32());
            Assert.Contains(survivor.GetProperty("entries").GetProperty("items").EnumerateArray(), e => e.GetProperty("kind").GetString() == "MergeTransferIn" && e.GetProperty("points").GetInt32() == 8);
            Assert.Equal(1, await CountAsync<LoyaltyEntryAggregate>(e => e.CustomerId == duplicateId && e.Kind == LoyaltyEntryKind.MergeTransferOut));

            var own = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await survivorClient.GetAsync("/api/customers/me/loyalty")));
            Assert.Equal(8, own.GetProperty("balance").GetInt32());
        }
        finally
        {
            await SetLoyaltyAsync(context.Admin, false);
        }
    }

    [Fact]
    public async Task ACustomer_ReadsTheirOwnLedger()
    {
        var (client, customerId) = await RegisterAndSignInCustomerAsync();
        var response = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync("/api/customers/me/loyalty")));

        Assert.Equal(0, response.GetProperty("balance").GetInt32());
        Assert.Equal(0, response.GetProperty("entries").GetProperty("items").GetArrayLength());
        Assert.NotEqual(Guid.Empty, customerId);
    }

    // TEST gate XI-10 lives in this class because loyalty is a global setting: every test that switches it on and off must run in one class (sequentially).
    private static int Signed(LoyaltyEntryAggregate e) =>
        e.Kind == LoyaltyEntryKind.Accrual || e.Kind == LoyaltyEntryKind.RedemptionReversal || e.Kind == LoyaltyEntryKind.MergeTransferIn ? e.Points : -e.Points;

    private async Task<(int Balance, int FromLedger)> BalanceAndLedgerAsync(Guid customerId) => await WithDbAsync(async db =>
    {
        var balance = await db.LoyaltyAccounts.AsNoTracking().Where(a => a.Id == customerId).Select(a => a.Balance).SingleOrDefaultAsync();
        var entries = await db.LoyaltyEntries.AsNoTracking().Where(e => e.CustomerId == customerId).ToListAsync();
        return (balance, entries.Sum(Signed));
    });

    /// <summary>XI-10 -- I-J4 loyalty half: the duplicate holds accruals and a redemption, the survivor an accrual of its own; after the merge BOTH
    /// accounts' balances equal the signed sum of their ledger entries, and so does every other account in the database.</summary>
    [Fact]
    public async Task XI_10_AfterAMerge_EveryAccountsBalance_EqualsTheSumOfItsLedger()
    {
        var context = await CreateBillingContextAsync();
        await SetLoyaltyAsync(context.Admin, true);
        try
        {
            var (survivorClient, survivorId) = await RegisterAndSignInCustomerAsync();
            var (duplicateClient, duplicateId) = await RegisterAndSignInCustomerAsync();
            var shared = NewMobile();
            await ShareContactNumberAsync(survivorClient, shared);
            await ShareContactNumberAsync(duplicateClient, shared);

            var (own, _, _) = await IssueSimpleInvoiceAsync(context, survivorId, 350m);
            await RecordPaymentAsync(context.Admin, survivorId, "CashInStore", 350m, allocations: [(own, 350m)]);

            var (first, _, _) = await IssueSimpleInvoiceAsync(context, duplicateId, 20_000m);
            await RecordPaymentAsync(context.Admin, duplicateId, "CashInStore", 20_000m, allocations: [(first, 20_000m)]);
            var (second, _, _) = await IssueSimpleInvoiceAsync(context, duplicateId, 777m);
            await RecordPaymentAsync(context.Admin, duplicateId, "CashInStore", 777m, allocations: [(second, 777m)]);
            var draft = await CreateDraftAsync(context.Admin, duplicateId, [await RecordChargeAsync(context, duplicateId, 5_000m, 1m, taxClassId: context.ZeroTaxClassId)]);
            await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{draft}/loyalty-redemption", new { points = 150 }, Json));

            var beforeDuplicate = await BalanceAndLedgerAsync(duplicateId);
            var beforeSurvivor = await BalanceAndLedgerAsync(survivorId);
            Assert.Equal(200 + 7 - 150, beforeDuplicate.Balance);
            Assert.Equal(beforeDuplicate.Balance, beforeDuplicate.FromLedger);
            Assert.Equal(3, beforeSurvivor.Balance);
            Assert.Equal(beforeSurvivor.Balance, beforeSurvivor.FromLedger);

            await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge",
                new { survivingCustomerId = survivorId, reason = "Same person registered twice" }, Json));

            var duplicate = await BalanceAndLedgerAsync(duplicateId);
            var survivor = await BalanceAndLedgerAsync(survivorId);
            Assert.Equal(0, duplicate.Balance);
            Assert.Equal(duplicate.Balance, duplicate.FromLedger);
            Assert.Equal(3 + 57, survivor.Balance);
            Assert.Equal(survivor.Balance, survivor.FromLedger);

            // The invariant everywhere in the shared database (other tests' accounts too).
            var offenders = await WithDbAsync(async db =>
            {
                var balances = await db.LoyaltyAccounts.AsNoTracking().ToDictionaryAsync(a => a.Id, a => a.Balance);
                var entries = (await db.LoyaltyEntries.AsNoTracking().ToListAsync()).GroupBy(e => e.CustomerId).ToDictionary(g => g.Key, g => g.Sum(Signed));
                return balances.Where(b => b.Value != entries.GetValueOrDefault(b.Key)).Select(b => $"{b.Key}: balance {b.Value}, ledger {entries.GetValueOrDefault(b.Key)}")
                    .Concat(entries.Where(e => !balances.ContainsKey(e.Key) && e.Value != 0).Select(e => $"{e.Key}: no account, ledger {e.Value}")).ToList();
            });
            Assert.True(offenders.Count == 0, "Accounts whose balance differs from their ledger: " + string.Join(" | ", offenders.Take(10)));
        }
        finally
        {
            await SetLoyaltyAsync(context.Admin, false);
        }
    }

}
