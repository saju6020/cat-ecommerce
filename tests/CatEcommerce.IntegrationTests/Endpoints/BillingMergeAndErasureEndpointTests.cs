using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 ADR-192 (I-J4 part) and HO-XCT-001 C-ALL-2/3 -- a merge moves what is still mutable and leaves what is
/// immutable; the bill-to snapshot of an issued invoice is retained (D-9) and the eraser says so.</summary>
public sealed class BillingMergeAndErasureEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    [Fact]
    public async Task I_J4_AMerge_MovesUnbilledChargesAndDrafts_ButNeverAnIssuedInvoice()
    {
        var context = await CreateBillingContextAsync();
        var (survivorClient, survivorId) = await RegisterAndSignInCustomerAsync();
        var (duplicateClient, duplicateId) = await RegisterAndSignInCustomerAsync();
        var shared = NewMobile();
        await ShareContactNumberAsync(survivorClient, shared);
        await ShareContactNumberAsync(duplicateClient, shared);

        var unbilled = await RecordChargeAsync(context, duplicateId, 100m);
        var draftCharge = await RecordChargeAsync(context, duplicateId, 200m);
        var draftId = await CreateDraftAsync(context.Admin, duplicateId, [draftCharge]);
        var (issuedId, issuedNumber, _) = await IssueSimpleInvoiceAsync(context, duplicateId, 300m);
        var payment = await RecordPaymentAsync(context.Admin, duplicateId, "CashInStore", 120m, allocations: [(issuedId, 100m)]);
        var paymentId = payment.GetProperty("paymentId").GetGuid();

        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge",
            new { survivingCustomerId = survivorId, reason = "Same person registered twice" }, Json));

        // The mutable records moved to the survivor ...
        Assert.Equal(survivorId, (await WithDbAsync(db => db.Charges.FindAsync(unbilled).AsTask()))!.CustomerId);
        Assert.Equal(survivorId, (await WithDbAsync(db => db.Set<InvoiceAggregate>().FindAsync(draftId).AsTask()))!.CustomerId);

        // ... the issued invoice keeps the original customer id (ADR-180: an issued document is never rewritten) ...
        Assert.Equal(duplicateId, (await WithDbAsync(db => db.Set<InvoiceAggregate>().FindAsync(issuedId).AsTask()))!.CustomerId);
        Assert.Equal(duplicateId, (await WithDbAsync(db => db.Set<PaymentAggregate>().FindAsync(paymentId).AsTask()))!.CustomerId); // a payment is immutable too

        // ... and the survivor's own reads fold the merged customer in: they see the duplicate's issued invoice.
        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await survivorClient.GetAsync("/api/customers/me/invoices")));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), i => i.GetProperty("invoiceId").GetGuid() == issuedId);
        var detail = await survivorClient.GetAsync($"/api/customers/me/invoices/{issuedId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(issuedNumber, (await ReadAsync(detail)).GetProperty("invoice").GetProperty("number").GetString());

        var payments = await ReadAsync(await survivorClient.GetAsync("/api/customers/me/payments"));
        Assert.Contains(payments.GetProperty("items").EnumerateArray(), p => p.GetProperty("paymentId").GetGuid() == paymentId);

        // The staff summary of the survivor folds the duplicate in: 200 outstanding, 20 of free credit.
        var summary = await GetSummaryAsync(context.Admin, survivorId);
        Assert.Equal(200m, AmountOf(summary.GetProperty("outstanding")));
        Assert.Equal(20m, AmountOf(summary.GetProperty("freeCredit")));
    }

    [Fact]
    public async Task TheEraser_ReportsHeld_WhileAnIssuedInvoiceHoldsTheBillToSnapshot_AndCompletedOtherwise()
    {
        var context = await CreateBillingContextAsync();
        var withInvoice = await SeedCustomerAsync(context);
        var onlyACharge = await SeedCustomerAsync(context);
        var (_, _, _) = await IssueSimpleInvoiceAsync(context, withInvoice);
        await RecordChargeAsync(context, onlyACharge);

        await using var scope = Factory.Services.CreateAsyncScope();
        var erasers = scope.ServiceProvider.GetServices<IPersonalDataEraser>().ToList();
        var eraser = Assert.Single(erasers, e => e.ContextKey == "billing");
        var actor = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        var held = Assert.IsType<ErasureOutcome.Held>(await eraser.EraseAsync(withInvoice, Now, actor, CancellationToken.None));
        Assert.Equal(Today.AddYears(6), held.Until);
        Assert.Contains("D-9", held.Basis);

        Assert.IsType<ErasureOutcome.Completed>(await eraser.EraseAsync(onlyACharge, Now, actor, CancellationToken.None));
        Assert.IsType<ErasureOutcome.Completed>(await eraser.EraseAsync(Guid.NewGuid(), Now, actor, CancellationToken.None));

        // Past its period the held record is a review flag only -- the eraser reports Completed and nothing is deleted.
        Assert.IsType<ErasureOutcome.Completed>(await eraser.EraseAsync(withInvoice, Now.AddYears(7), actor, CancellationToken.None));
    }

    [Fact]
    public async Task TheRetentionReviewSource_CountsInvoicesPastTheirPeriod_AndDeletesNothing()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId);

        await using var scope = Factory.Services.CreateAsyncScope();
        var source = Assert.Single(scope.ServiceProvider.GetServices<IRetentionReviewSource>(), s => s.SourceKey == "billing-financial");

        var now = await source.CountPastRetentionAsync(Today, CancellationToken.None);
        Assert.DoesNotContain(now, c => c.RecordKind == "invoice" && c.OldestAnchor == Today);

        var later = await source.CountPastRetentionAsync(Today.AddYears(7), CancellationToken.None);
        var invoices = Assert.Single(later, c => c.RecordKind == "invoice");
        Assert.True(invoices.Count >= 1);
        Assert.Equal(RecordClass.Financial, invoices.RecordClass);

        Assert.Equal(1, await CountAsync<InvoiceAggregate>(i => i.Id == invoiceId)); // a review flag, never a delete (ADR-301)
    }
}
