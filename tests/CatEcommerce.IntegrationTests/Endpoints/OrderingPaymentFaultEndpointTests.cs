using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase E, the failure paths -- run on a host whose Billing payment handler and idempotency purge can be made to throw
/// (<see cref="OrderingFaults"/>, keyed so no other test is touched): I-E7 (US-ORD-08 AC2, ADR-205) and U-X1 (a throwing worker pass never stops the next).
/// </summary>
public sealed class OrderingPaymentFaultEndpointTests(OrderingFaultApiFactory factory) : OrderingShopTestBase(factory), IClassFixture<OrderingFaultApiFactory>
{
    [Fact]
    public async Task I_E7_WhenBillingsPaymentStageFails_TheStaffEntryBecomesAnOpenReconciliationIssue_AndTheOrderStillAwaitsPayment()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var (accountant, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var order = await LoadOrderAsync(orderId);

        OrderingFaults.FailPaymentsFor[shop.CustomerId] = 1;
        var response = await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m, "BANK-FAIL-77");
        OrderingFaults.FailPaymentsFor.TryRemove(shop.CustomerId, out _);

        Assert.True((int)response.StatusCode >= 500, $"Expected a 5xx but got {(int)response.StatusCode}");

        // everything that unit of work did is gone: the invoice, the charges, the payment, the confirmation
        var after = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.PendingPayment, after.Status);
        Assert.Null(after.InvoiceId);
        Assert.Equal(0m, after.AmountPaid);
        Assert.Equal(0, await WithDbAsync(db => db.Charges.AsNoTracking().CountAsync(c => c.CustomerId == shop.CustomerId)));
        Assert.Equal(0, await WithDbAsync(db => db.Invoices.AsNoTracking().CountAsync(i => i.CustomerId == shop.CustomerId)));
        Assert.Equal(0, await NotificationCountAsync(shop.CustomerId, "order.confirmed"));

        // but what the staff member entered is kept, for the accountants
        var issues = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await accountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Open&pageSize=100")));
        var issue = issues.GetProperty("items").EnumerateArray().Single(i => i.TryGetProperty("orderId", out var id) && id.ValueKind != System.Text.Json.JsonValueKind.Null && id.GetGuid() == orderId);
        Assert.Equal("TransferConfirmationFailed", issue.GetProperty("kind").GetString());
        Assert.Equal("BankTransfer", issue.GetProperty("paymentMethod").GetString());
        Assert.Equal("2135.00", issue.GetProperty("amount").GetProperty("amount").GetString());
        Assert.Equal("BANK-FAIL-77", issue.GetProperty("paymentReference").GetString());
        Assert.Equal("Open", issue.GetProperty("status").GetString());
        // the error summary names the exception TYPE and a fixed message -- never the exception's own message
        var summary = issue.GetProperty("errorSummary").GetString()!;
        Assert.True(summary.Contains("InvalidOperationException"), "summary was: " + summary);
        Assert.DoesNotContain("injected", summary);

        // an accountant resolves it with a note, once
        var issueId = issue.GetProperty("id").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await accountant.PostAsJsonAsync($"/api/staff/orders/reconciliation-issues/{issueId}/resolve", new { note = "Entered by hand in Billing" }, Json));
        await AssertErrorAsync(await accountant.PostAsJsonAsync($"/api/staff/orders/reconciliation-issues/{issueId}/resolve", new { note = "again" }, Json), "ReconciliationIssueAlreadyResolved");
        var resolved = await ReadAsync(await accountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Resolved&pageSize=100"));
        var done = resolved.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == issueId);
        Assert.Equal("Entered by hand in Billing", done.GetProperty("resolutionNote").GetString());

        // the staff member can simply try again once the fault is gone
        var retry = await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(reception, orderId, (await LoadOrderAsync(orderId)).Version, 2135.00m, "BANK-FAIL-77"));
        Assert.Equal("Confirmed", (await ReadAsync(retry)).GetProperty("status").GetString());
    }

    /// <summary>SECURITY MEDIUM 2 (2026-10-09): an issue belongs to the branch it was raised at. Another branch's accountant neither lists it nor resolves it (404, the same
    /// answer as an issue that does not exist) and it stays <c>Open</c> for the branch that owns it.</summary>
    [Fact]
    public async Task AReconciliationIssue_IsListedAndResolvedOnlyByItsOwnBranch()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var (accountant, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var (otherAccountant, _) = await StaffClientAsync(await CreateContextAsync(), StaffRoles.Accountant);
        var order = await LoadOrderAsync(orderId);

        OrderingFaults.FailPaymentsFor[shop.CustomerId] = 1;
        await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m, "BANK-BR-1");
        OrderingFaults.FailPaymentsFor.TryRemove(shop.CustomerId, out _);

        var own = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await accountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Open&pageSize=100")));
        var issue = own.GetProperty("items").EnumerateArray().Single(i => i.TryGetProperty("orderId", out var id) && id.ValueKind != System.Text.Json.JsonValueKind.Null && id.GetGuid() == orderId);
        var issueId = issue.GetProperty("id").GetGuid();
        Assert.Equal(shop.Context.BranchId, await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().Where(i => i.Id == issueId).Select(i => i.BranchId!.Value).SingleAsync()));

        var other = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await otherAccountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Open&pageSize=100")));
        Assert.DoesNotContain(other.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == issueId);
        Assert.Equal(HttpStatusCode.NotFound, (await otherAccountant.PostAsJsonAsync($"/api/staff/orders/reconciliation-issues/{issueId}/resolve", new { note = "not mine" }, Json)).StatusCode);
        Assert.Equal("Open", await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().Where(i => i.Id == issueId).Select(i => i.Status.DisplayName).SingleAsync()));

        await ExpectAsync(HttpStatusCode.NoContent, await accountant.PostAsJsonAsync($"/api/staff/orders/reconciliation-issues/{issueId}/resolve", new { note = "Entered by hand" }, Json));
    }

    /// <summary>CODE_REVIEW backlog 2 (2026-10-09): a benign concurrency conflict (two staff acting at once, the worker expiring the order) leaves NO reconciliation issue -- nothing was
    /// half-recorded, so there is nothing for an accountant to finish -- and answers a plain conflict the client retries.</summary>
    [Fact]
    public async Task ABenignConcurrencyConflict_OpensNoReconciliationIssue_AndAnswers409()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var order = await LoadOrderAsync(orderId);

        OrderingFaults.ConflictPaymentsFor[shop.CustomerId] = 1;
        var response = await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m, "BANK-CONFLICT-1");
        OrderingFaults.ConflictPaymentsFor.TryRemove(shop.CustomerId, out _);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().CountAsync(i => i.OrderId == orderId)));
        Assert.Equal(OrderStatus.PendingPayment, (await LoadOrderAsync(orderId)).Status);
    }

    [Fact]
    public async Task AReferenceThatIsACardNumber_IsMaskedInTheIssueAnyway()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var (accountant, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var order = await LoadOrderAsync(orderId);

        // the aggregate refuses it before anything is written (so no issue is raised for that); the recorder's own masking is the second line of defence
        await AssertErrorAsync(await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m, "4111 1111 1111 1111"), "ReferenceLooksLikeCardNumber");
        var issues = await ReadAsync(await accountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Open&pageSize=100"));
        Assert.DoesNotContain(issues.GetProperty("items").EnumerateArray(), i => (i.GetProperty("paymentReference").GetString() ?? "").Contains("4111 1111"));
    }

    // ── U-X1 (§15): a throwing pass is logged and the next pass still runs ──────────────────────────
    [Fact]
    public async Task U_X1_AThrowingPass_DoesNotStopTheNextPass()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();

        // make the order due without moving the shared clock (the cycle below is restricted to this branch; only the purge failure path is added, and it throws before deleting anything)
        await using (var connection = await OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ordering.sales_order SET payment_due_by = now() - interval '1 hour' WHERE \"Id\" = @id";
            command.Parameters.AddWithValue("id", orderId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        OrderingFaults.PurgeThrows = true;
        try
        {
            await RunOrderWorkCycleAsync(shop.Context.BranchId, withIdempotencyPurge: true);   // must not throw: the idempotency purge fails, the expiry pass before it already ran
        }
        finally
        {
            OrderingFaults.PurgeThrows = false;
        }

        Assert.Equal(OrderStatus.Cancelled, (await LoadOrderAsync(orderId)).Status);
        await RunOrderWorkCycleAsync(shop.Context.BranchId);       // and the next poll runs normally
    }
}
