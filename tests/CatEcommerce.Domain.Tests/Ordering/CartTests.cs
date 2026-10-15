using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Tests.Ordering;

/// <summary>HO-ORD-001 §15 group C -- the cart (US-ORD-01, ADR-199).</summary>
public sealed class CartTests
{
    private static readonly UserContext User = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);
    private const int MaxQty = 20;
    private const int MaxLines = 50;

    private static CartAggregate CustomerCart()
    {
        var cart = new CartAggregate(Guid.NewGuid());
        cart.OpenForCustomer(Guid.NewGuid(), T0, User);
        return cart;
    }

    private static CartAggregate GuestCart()
    {
        var cart = new CartAggregate(Guid.NewGuid());
        cart.OpenForGuest(new string('a', 64), T0, User);
        return cart;
    }

    private static Guid Add(CartAggregate cart, Guid sku, int quantity, Guid? cat = null, decimal price = 100m, DateTimeOffset? at = null, int maxLines = MaxLines) =>
        cart.AddLine(sku, quantity, cat, price, "BDT", at ?? T0, MaxQty, maxLines);

    // ── U-C1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_C1_AddingOneSkuTwice_IsOneLineWithTheQuantitiesSummed_CappedAtTheMaximum()
    {
        var cart = CustomerCart();
        var sku = Guid.NewGuid();

        var first = Add(cart, sku, 2);
        var second = Add(cart, sku, 3);

        Assert.Equal(first, second);
        Assert.Equal(5, Assert.Single(cart.Lines).Quantity);

        Add(cart, sku, 19);
        Assert.Equal(MaxQty, Assert.Single(cart.Lines).Quantity);
    }

    [Fact]
    public void U_C1_APrescriptionSku_AddedOncePerCat_IsTwoLines()
    {
        var cart = CustomerCart();
        var sku = Guid.NewGuid();
        var mitu = Guid.NewGuid();
        var luna = Guid.NewGuid();

        Add(cart, sku, 1, mitu);
        Add(cart, sku, 1, luna);
        Add(cart, sku, 2, mitu);

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(3, cart.Lines.Single(l => l.CatId == mitu).Quantity);
        Assert.Equal(1, cart.Lines.Single(l => l.CatId == luna).Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(MaxQty + 1)]
    public void AQuantityOutsideOneToMax_IsRefused(int quantity)
    {
        var cart = CustomerCart();
        var ex = Assert.Throws<DomainRuleViolationException>(() => Add(cart, Guid.NewGuid(), quantity));
        Assert.Equal(BusinessViolationCodes.QuantityOutOfRange, ex.ErrorCode);
    }

    [Fact]
    public void ACartHoldsAtMostTheConfiguredLines_AndRefusesTheNext()
    {
        var cart = CustomerCart();
        for (var i = 0; i < 3; i++) Add(cart, Guid.NewGuid(), 1, maxLines: 3);

        var ex = Assert.Throws<DomainRuleViolationException>(() => Add(cart, Guid.NewGuid(), 1, maxLines: 3));
        Assert.Equal(BusinessViolationCodes.CartFull, ex.ErrorCode);

        // adding to an existing line is still fine at the cap
        Add(cart, cart.Lines[0].SkuId, 1, maxLines: 3);
        Assert.Equal(2, cart.Lines[0].Quantity);
    }

    [Fact]
    public void TheHardCapOf50_NoSettingCanRaise()
    {
        var cart = CustomerCart();
        for (var i = 0; i < 50; i++) Add(cart, Guid.NewGuid(), 1, maxLines: 500);

        Assert.Equal(BusinessViolationCodes.CartFull, Assert.Throws<DomainRuleViolationException>(() => Add(cart, Guid.NewGuid(), 1, maxLines: 500)).ErrorCode);
    }

    // ── U-C4 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_C4_APriceChangeAfterTheLineWasAdded_NeverMovesPriceWhenAdded()
    {
        var cart = CustomerCart();
        var sku = Guid.NewGuid();

        Add(cart, sku, 1, price: 100m);
        Add(cart, sku, 1, price: 150m, at: T0.AddHours(1)); // the SKU now costs more; the line keeps what the customer first saw

        var line = Assert.Single(cart.Lines);
        Assert.Equal(100m, line.PriceWhenAdded);
        Assert.Equal(2, line.Quantity);
    }

    // ── U-C2 / U-C3 ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_C2_Merge_GuestA2B1_IntoAccountA3C1_GivesA3B1C1_WithNoDuplicateLine()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var guest = GuestCart();
        Add(guest, a, 2);
        Add(guest, b, 1);
        var account = CustomerCart();
        Add(account, a, 3);
        Add(account, c, 1);

        var result = account.MergeFrom(guest, T0.AddMinutes(5), MaxQty, MaxLines);

        Assert.Equal(3, account.Lines.Count);
        Assert.Equal(3, account.Lines.Single(l => l.SkuId == a).Quantity); // the larger, not 2 + 3
        Assert.Equal(1, account.Lines.Single(l => l.SkuId == b).Quantity);
        Assert.Equal(1, account.Lines.Single(l => l.SkuId == c).Quantity);
        Assert.Equal(account.Lines.Count, account.Lines.Select(l => (l.SkuId, l.CatId)).Distinct().Count());
        Assert.Equal(2, result.MergedLineCount);
        Assert.Empty(result.DroppedSkuIds);
        Assert.Equal(CartStatus.Merged, guest.Status);
        Assert.Equal(account.Id, guest.MergedIntoCartId);
    }

    [Fact]
    public void U_C2_Merge_TakesTheLargerQuantity_WhicheverCartHasIt_AndKeepsTheOlderPrice()
    {
        var sku = Guid.NewGuid();
        var guest = GuestCart();
        Add(guest, sku, 7, price: 90m, at: T0);                 // older line, older price
        var account = CustomerCart();
        Add(account, sku, 3, price: 120m, at: T0.AddDays(1));

        account.MergeFrom(guest, T0.AddDays(2), MaxQty, MaxLines);

        var line = Assert.Single(account.Lines);
        Assert.Equal(7, line.Quantity);
        Assert.Equal(90m, line.PriceWhenAdded);
    }

    [Fact]
    public void U_C2_Merge_KeepsEachCatKeySeparate()
    {
        var sku = Guid.NewGuid();
        var mitu = Guid.NewGuid();
        var luna = Guid.NewGuid();
        var guest = GuestCart();
        Add(guest, sku, 1, mitu);
        var account = CustomerCart();
        Add(account, sku, 2, luna);

        account.MergeFrom(guest, T0, MaxQty, MaxLines);

        Assert.Equal(2, account.Lines.Count);
    }

    [Fact]
    public void U_C2_Merging_IsIdempotent_AMergedGuestCartChangesNothingTheSecondTime()
    {
        var guest = GuestCart();
        Add(guest, Guid.NewGuid(), 2);
        var account = CustomerCart();

        account.MergeFrom(guest, T0, MaxQty, MaxLines);
        var again = account.MergeFrom(guest, T0, MaxQty, MaxLines);

        Assert.Equal(0, again.MergedLineCount);
        Assert.Single(account.Lines);
        Assert.Equal(0, account.MergeFrom(account, T0, MaxQty, MaxLines).MergedLineCount);
    }

    [Fact]
    public void U_C3_Merge_BeyondTheLineCap_DropsTheExtraLines_AndReportsThem()
    {
        var guest = GuestCart();
        var guestSkus = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
        foreach (var sku in guestSkus) Add(guest, sku, 1);

        var account = CustomerCart();
        Add(account, Guid.NewGuid(), 1, maxLines: 3);
        Add(account, Guid.NewGuid(), 1, maxLines: 3);

        var result = account.MergeFrom(guest, T0, MaxQty, maxLines: 3);

        Assert.Equal(3, account.Lines.Count);
        Assert.Equal(1, result.MergedLineCount);
        Assert.Equal(guestSkus.Skip(1), result.DroppedSkuIds);
        Assert.Equal(CartStatus.Merged, guest.Status);
    }

    // ── editing ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void SetQuantity_ChangesOneLine_AndRefusesZeroAndTooMany()
    {
        var cart = CustomerCart();
        var line = Add(cart, Guid.NewGuid(), 1);

        cart.SetQuantity(line, 9, T0.AddMinutes(1), MaxQty);
        Assert.Equal(9, cart.Lines[0].Quantity);

        Assert.Equal(BusinessViolationCodes.QuantityOutOfRange, Assert.Throws<DomainRuleViolationException>(() => cart.SetQuantity(line, 0, T0, MaxQty)).ErrorCode);
        Assert.Equal(BusinessViolationCodes.QuantityOutOfRange, Assert.Throws<DomainRuleViolationException>(() => cart.SetQuantity(line, MaxQty + 1, T0, MaxQty)).ErrorCode);
        Assert.Equal(BusinessViolationCodes.CartLineNotFound, Assert.Throws<DomainRuleViolationException>(() => cart.SetQuantity(Guid.NewGuid(), 2, T0, MaxQty)).ErrorCode);
    }

    [Fact]
    public void SetLineCat_RefusesACollidingKey_AndAllowsAFreeOne()
    {
        var cart = CustomerCart();
        var sku = Guid.NewGuid();
        var mitu = Guid.NewGuid();
        var luna = Guid.NewGuid();
        var first = Add(cart, sku, 1, mitu);
        var second = Add(cart, sku, 1, luna);

        Assert.Equal(BusinessViolationCodes.DuplicateCartLine, Assert.Throws<DomainRuleViolationException>(() => cart.SetLineCat(second, mitu, T0)).ErrorCode);

        var kiki = Guid.NewGuid();
        cart.SetLineCat(first, kiki, T0);
        Assert.Contains(cart.Lines, l => l.Id == first && l.CatId == kiki);
    }

    [Fact]
    public void RemoveLine_AndRemovePlacedLines_AreIdempotent()
    {
        var cart = CustomerCart();
        var one = Add(cart, Guid.NewGuid(), 1);
        var two = Add(cart, Guid.NewGuid(), 1);
        var three = Add(cart, Guid.NewGuid(), 1);

        Assert.True(cart.RemoveLine(one, T0));
        Assert.False(cart.RemoveLine(one, T0));

        cart.RemovePlacedLines([two, Guid.NewGuid()], T0);
        cart.RemovePlacedLines([two], T0);

        Assert.Equal(three, Assert.Single(cart.Lines).Id);
    }

    [Fact]
    public void EveryMutation_MovesLastActivityOn_AndTheVersion()
    {
        var cart = CustomerCart();
        var version = cart.Version;

        Add(cart, Guid.NewGuid(), 1, at: T0.AddMinutes(10));

        Assert.Equal(T0.AddMinutes(10), cart.LastActivityOn);
        Assert.True(cart.Version > version);
        Assert.Empty(cart.Events); // carts raise no domain events
    }

    [Fact]
    public void AGuestCart_CanBeAbandoned_ACustomerCartCannot_AndAMergedCartAcceptsNothing()
    {
        var guest = GuestCart();
        guest.Abandon(T0);
        Assert.Equal(CartStatus.Abandoned, guest.Status);
        Assert.Throws<DomainRuleViolationException>(() => Add(guest, Guid.NewGuid(), 1));

        Assert.Throws<InvalidOperationException>(() => CustomerCart().Abandon(T0));

        var merged = GuestCart();
        CustomerCart().MergeFrom(merged, T0, MaxQty, MaxLines);
        Assert.Equal(BusinessViolationCodes.CartNotFound, Assert.Throws<DomainRuleViolationException>(() => Add(merged, Guid.NewGuid(), 1)).ErrorCode);
    }

    [Fact]
    public void AGuestCart_IsKeyedByA64CharacterHash_OnlyAndStoresNoCustomer()
    {
        Assert.Throws<ArgumentException>(() => new CartAggregate(Guid.NewGuid()).OpenForGuest("short", T0, User));

        var guest = GuestCart();
        Assert.Equal(CartOwnerKind.Guest, guest.OwnerKind);
        Assert.Null(guest.CustomerId);
        Assert.Equal(64, guest.GuestTokenHash!.Length);
    }

    [Fact]
    public void ReassignCustomer_AndAnonymisation_AreIdempotent()
    {
        var cart = CustomerCart();
        Add(cart, Guid.NewGuid(), 1);
        var survivor = Guid.NewGuid();

        Assert.True(cart.ReassignCustomer(survivor));
        Assert.False(cart.ReassignCustomer(survivor));

        Assert.True(cart.AbandonForAnonymisation(T0));
        Assert.False(cart.AbandonForAnonymisation(T0));
        Assert.Empty(cart.Lines);
        Assert.Equal(CartStatus.Abandoned, cart.Status);
    }
}
