using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001-FU1 (owner decisions 2026-10-10), through real HTTP against the per-branch database:
/// D1 a counter sale to a mobile held by a Merged account sells to the surviving account (chains followed; a survivor that is not Active, or a loop, keeps the cashier-facing
/// <c>AccountNotActive</c>); D2 a walk-in buyer is stored even when the sale is refused, and a later online registration with the same mobile is offered the reset that completes
/// the same account (no duplicate); D4 cash on delivery and counter tenders above Billing's approval threshold are recorded as received at once.
/// (D3, the delivery tax, is in <see cref="OrderingFollowUp1DeliveryTaxEndpointTests"/>: it changes a global rate and gets a class of its own.)
/// </summary>
public sealed class OrderingFollowUp1EndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private sealed record Till(Shop Shop, HttpClient Cashier, OrderingSku Item);

    private async Task<Till> TillAsync(decimal stock = 20m)
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var item = await CreateSkuAsync(shop.Context, price: 100m, stock: stock, taxClassId: shop.Context.ZeroTaxClassId, name: "FU1 item " + Guid.NewGuid().ToString("N")[..8]);
        var (cashier, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        return new Till(shop, cashier, item);
    }

    private static Task<HttpResponseMessage> SellAsync(Till till, string mobile, string name = "Rina Akter", int quantity = 1, decimal cash = 100m) => StaffPostAsync(till.Cashier, "/api/staff/pos/sales", new
    {
        customerName = name, customerMobile = mobile,
        lines = new[] { new { skuId = till.Item.SkuId, quantity } },
        tenders = new object[] { new { method = "Cash", amount = cash } },
    });

    private static string Stored(string mobile) => MobileNumber.Create(mobile).Value;

    private Task<Guid> AccountIdAsync(string mobile) =>
        WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.MobileNumber!.Value == Stored(mobile)).Select(c => c.Id).SingleAsync());

    private Task<int> AccountCountAsync(string mobile) =>
        WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == Stored(mobile)));

    /// <summary>Marks <paramref name="duplicate"/> as merged into <paramref name="survivor"/> (the state HO-ACC-001's merge leaves), or sets a bare status.</summary>
    private Task SetMergedAsync(Guid duplicate, Guid? survivor, CustomerAccountStatus status) => WithDbAsync(async db =>
    {
        var account = await db.CustomerAccounts.SingleAsync(c => c.Id == duplicate);
        db.Entry(account).Property(c => c.Status).CurrentValue = status;
        db.Entry(account).Property(c => c.MergedIntoCustomerId).CurrentValue = survivor;
        return await db.SaveChangesAsync();
    });

    // ── D1: follow a merge to the surviving account ─────────────────────────────────────────────────
    [Fact]
    public async Task D1_ACounterSale_ToAMobileHeldByAMergedAccount_SellsToTheSurvivingAccount()
    {
        var till = await TillAsync();
        var oldMobile = NewMobile();
        var survivorMobile = NewMobile();
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, oldMobile, "Old Record"));
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, survivorMobile, "Current Record"));
        var oldId = await AccountIdAsync(oldMobile);
        var survivorId = await AccountIdAsync(survivorMobile);
        await SetMergedAsync(oldId, survivorId, CustomerAccountStatus.Merged);

        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, oldMobile, "Typed Name Is Ignored")));

        var order = await LoadOrderAsync(sale.GetProperty("orderId").GetGuid());
        Assert.Equal(survivorId, order.CustomerId);                                                                  // the sale belongs to the surviving account
        Assert.Equal(survivorId, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == order.InvoiceId).Select(i => i.CustomerId).SingleAsync()));
        Assert.Equal(1, await AccountCountAsync(oldMobile));                                                         // no new account was made for the merged mobile
        Assert.Empty(await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().Where(i => i.BranchId == till.Shop.Context.BranchId).ToListAsync()));
    }

    [Fact]
    public async Task D1_AChainOfMerges_IsFollowedToTheLastAccount()
    {
        var till = await TillAsync();
        var (a, b, c) = (NewMobile(), NewMobile(), NewMobile());
        foreach (var mobile in new[] { a, b, c }) await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, mobile));
        var (idA, idB, idC) = (await AccountIdAsync(a), await AccountIdAsync(b), await AccountIdAsync(c));
        await SetMergedAsync(idA, idB, CustomerAccountStatus.Merged);
        await SetMergedAsync(idB, idC, CustomerAccountStatus.Merged);

        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, a)));

        Assert.Equal(idC, (await LoadOrderAsync(sale.GetProperty("orderId").GetGuid())).CustomerId);
    }

    [Fact]
    public async Task D1_AMergedAccount_WhoseSurvivorIsNotActive_KeepsTheCashierFacingRefusal_AndNothingMoves()
    {
        var till = await TillAsync();
        var (oldMobile, survivorMobile) = (NewMobile(), NewMobile());
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, oldMobile));
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, survivorMobile));
        var (oldId, survivorId) = (await AccountIdAsync(oldMobile), await AccountIdAsync(survivorMobile));
        await SetMergedAsync(oldId, survivorId, CustomerAccountStatus.Merged);
        await SetMergedAsync(survivorId, null, CustomerAccountStatus.Anonymised);
        var stockBefore = await SellableAsync(till.Shop.Context.BranchId, till.Item.SkuId);

        var response = await SellAsync(till, oldMobile);
        var body = await BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("AccountNotActive", body);
        Assert.DoesNotContain("MobileNumberAlreadyRegistered", body);
        Assert.Equal(stockBefore, await SellableAsync(till.Shop.Context.BranchId, till.Item.SkuId));
        Assert.Empty(await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().Where(i => i.BranchId == till.Shop.Context.BranchId).ToListAsync()));
    }

    [Fact]
    public async Task D1_ALoopOfMerges_IsARefusal_NotAnEndlessWalk()
    {
        var till = await TillAsync();
        var (a, b) = (NewMobile(), NewMobile());
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, a));
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, b));
        var (idA, idB) = (await AccountIdAsync(a), await AccountIdAsync(b));
        await SetMergedAsync(idA, idB, CustomerAccountStatus.Merged);
        await SetMergedAsync(idB, idA, CustomerAccountStatus.Merged);

        await AssertErrorAsync(await SellAsync(till, a), "AccountNotActive");
    }

    // ── D2: the walk-in is stored even when the sale is refused ─────────────────────────────────────
    [Fact]
    public async Task D2_ASaleRefusedForStock_StillStoresTheWalkIn_AndTheRetryReusesIt()
    {
        var till = await TillAsync(stock: 5m);
        var mobile = NewMobile();

        await AssertErrorAsync(await SellAsync(till, mobile, "Rina Akter", quantity: 6, cash: 9999m), "StockNoLongerAvailable", HttpStatusCode.Conflict);

        Assert.Equal(1, await AccountCountAsync(mobile));                                                            // the buyer is stored
        var account = await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.MobileNumber!.Value == Stored(mobile))
            .Select(c => new { c.Id, c.FullName, HasPassword = c.PasswordCredential != null }).SingleAsync());
        Assert.Equal("Rina Akter", account.FullName);
        Assert.False(account.HasPassword);                                                                           // passwordless: the person has not claimed it
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == account.Id)));  // and nothing was sold
        Assert.Equal(5m, await SellableAsync(till.Shop.Context.BranchId, till.Item.SkuId));

        var retry = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, mobile, "Rina Akter")));
        Assert.Equal(account.Id, (await LoadOrderAsync(retry.GetProperty("orderId").GetGuid())).CustomerId);        // the retry sells to the same account
        Assert.Equal(1, await AccountCountAsync(mobile));
    }

    [Fact]
    public async Task D2_ASaleRefusedForItsTenders_StillStoresTheWalkIn()
    {
        var till = await TillAsync();
        var mobile = NewMobile();

        await AssertErrorAsync(await SellAsync(till, mobile, cash: 10m), "TendersDoNotCoverTotal");

        Assert.Equal(1, await AccountCountAsync(mobile));
    }

    [Fact]
    public async Task D2_ARequestThatIsNotShapeValid_StoresNobody()
    {
        var till = await TillAsync();
        var mobile = NewMobile();

        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(till, mobile, name: " ")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SellAsync(till, "12345")).StatusCode);

        Assert.Equal(0, await AccountCountAsync(mobile));
    }

    [Fact]
    public async Task D2_TheStoredWalkIn_WhoLaterRegistersOnline_WithTheSameMobile_IsLinkedToTheSameAccount_NotDuplicated()
    {
        var till = await TillAsync(stock: 5m);
        var mobile = NewMobile();
        await AssertErrorAsync(await SellAsync(till, mobile, "Rina Akter", quantity: 6, cash: 9999m), "StockNoLongerAvailable", HttpStatusCode.Conflict);
        var walkInId = await AccountIdAsync(mobile);

        // 1. the online registration cannot create a second account: it is answered with the reset offer (ADR-038, ADR-216)
        using var client = CreateClientOn(Factory);
        var captcha = await client.PostAsync("/api/captcha", null);
        var captchaId = (await ReadAsync(captcha)).GetProperty("captchaId").GetString()!;
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName = "Rina Akter", mobileNumber = mobile, password = CustomerPassword, captchaId, captchaAnswer = "TEST1" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var error = (await ReadAsync(register)).GetProperty("errors").EnumerateArray().Single();
        Assert.Equal("MobileNumberAlreadyRegistered", error.GetProperty("errorCode").GetString());
        Assert.Contains("reset your password", error.GetProperty("errorMessage").GetString());
        Assert.Equal(1, await AccountCountAsync(mobile));

        // 2. following the offer completes the walk-in's own account: reset the password, then sign in
        var withSpy = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<IPasswordResetTokenDispatcher, SpyPasswordResetTokenDispatcher>()));
        using var resetClient = CreateClientOn(withSpy);
        Assert.Equal(HttpStatusCode.OK, (await resetClient.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json)).StatusCode);
        var dispatched = SpyPasswordResetTokenDispatcher.LastFor(walkInId);
        Assert.NotNull(dispatched);                                                                                 // the token went to the walk-in account itself
        const string chosen = "ClaimedPassword-2026!";
        Assert.Equal(HttpStatusCode.NoContent, (await resetClient.PostAsJsonAsync("/api/auth/password-reset/confirm", new { token = dispatched!.PlainToken, newPassword = chosen }, Json)).StatusCode);

        var signIn = await resetClient.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = chosen }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        Assert.Equal(1, await AccountCountAsync(mobile));
        Assert.Equal(walkInId, await AccountIdAsync(mobile));                                                        // still the very same account
        Assert.True(await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.Id == walkInId).Select(c => c.PasswordCredential != null).SingleAsync()));
    }

    // ── D4: COD and counter payments above the approval threshold are received at once ─────────────
    private async Task<Guid> CreateCourierAsync(OrderingContext context)
    {
        var response = await context.Admin.PostAsJsonAsync("/api/admin/couriers",
            new { name = "FU1 Courier " + Guid.NewGuid().ToString("N")[..6], trackingUrlTemplate = "https://track.example.test/t/{tracking}", supportsCod = true, isActive = true }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("courierId").GetGuid();
    }

    [Fact]
    public async Task D4_ACodCollection_AboveTheApprovalThreshold_IsRecordedAsReceived_WithNoPendingApproval()
    {
        var shop = await SetUpShopAsync();
        await SetApprovalThresholdAsync(shop.Context, "1000.00");
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var courierId = await CreateCourierAsync(shop.Context);
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", new { courierId, trackingNumber = "TRK-FU1" }, Json));
        var order = await LoadOrderAsync(orderId);
        Assert.True(order.Totals.GrandTotal > 1000m);                                                               // above the threshold

        await ExpectAsync(HttpStatusCode.NoContent, await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery", new
        {
            expectedVersion = order.Version, deliveredOn = Now.AddMinutes(-1),
            codCollection = new { amount = order.Totals.GrandTotal, collectorKind = "Courier", collectorName = "Rider Rahim", collectedOn = Now.AddMinutes(-2) },
        }));

        var done = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, done.Status);
        Assert.Equal(order.Totals.GrandTotal, done.AmountPaid);
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == done.Payments.Single().PaymentId).Select(p => p.Status).SingleAsync());
        Assert.Equal(PaymentStatus.Accepted, payment);                                                              // not PendingApproval
        await AssertWaiverRecordedAsync(done.Payments.Single().PaymentId, "CodCollection");
        Assert.Equal(order.Totals.GrandTotal, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == done.InvoiceId).Select(i => i.PaidTotal).SingleAsync()));
    }

    [Fact]
    public async Task D4_ACounterTender_AboveTheApprovalThreshold_IsRecordedAsReceived_WithNoPendingApproval()
    {
        var till = await TillAsync();
        await SetApprovalThresholdAsync(till.Shop.Context, "50.00");

        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, NewMobile(), quantity: 3, cash: 300m)));   // 300.00 > 50.00

        var order = await LoadOrderAsync(sale.GetProperty("orderId").GetGuid());
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(300m, order.AmountPaid);
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == order.Payments.Single().PaymentId).Select(p => p.Status).SingleAsync());
        Assert.Equal(PaymentStatus.Accepted, payment);
        await AssertWaiverRecordedAsync(order.Payments.Single().PaymentId, "CounterTender");
        Assert.Equal(300m, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == order.InvoiceId).Select(i => i.PaidTotal).SingleAsync()));
    }

    // ORD-52: the waiver and its reason are on the payment and in the audit trail.
    private async Task AssertWaiverRecordedAsync(Guid paymentId, string reason)
    {
        Assert.Equal(reason, await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == paymentId).Select(p => p.ApprovalWaiverReason).SingleAsync()));
        var audit = await WithDbAsync(db => db.Set<CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityType == "PaymentAggregate" && a.EntityId == paymentId && a.Action == "PaymentApprovalWaived").Select(a => a.AfterValue).ToListAsync());
        Assert.Contains($"Reason={reason}", Assert.Single(audit));
    }

    [Fact]
    public async Task D4_ABankTransfer_AboveTheThreshold_IsStillHeldForASecondPerson()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        await SetApprovalThresholdAsync(shop.Context, "1000.00");
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (recorder, _) = await StaffClientAsync(shop.Context, StaffRoles.Administrator);
        var order = await LoadOrderAsync(orderId);

        var confirmed = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(recorder, orderId, order.Version, order.Totals.GrandTotal)));

        Assert.True(confirmed.GetProperty("awaitingApproval").GetBoolean());
        Assert.Equal(PaymentStatus.PendingApproval, await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.CustomerId == shop.CustomerId).Select(p => p.Status).SingleAsync()));
        Assert.Null(await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.CustomerId == shop.CustomerId).Select(p => p.ApprovalWaiverReason).SingleAsync()));   // never waived
    }
}
