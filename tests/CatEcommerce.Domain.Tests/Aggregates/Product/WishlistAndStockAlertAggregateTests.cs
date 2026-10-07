using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 Phase G -- U-G1 (wishlist), U-G2 (stock alert status table).</summary>
public sealed class WishlistAndStockAlertAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);

    // ── U-G1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G1_AddTheSameSkuTwice_OneItem()
    {
        var wishlist = new WishlistAggregate(Guid.NewGuid());
        var sku = Guid.NewGuid();
        var product = Guid.NewGuid();

        Assert.True(wishlist.AddItem(sku, product, Now, TestUserContext.Create(), Guid.NewGuid()));
        Assert.False(wishlist.AddItem(sku, product, Now.AddMinutes(1), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Single(wishlist.Items);
    }

    [Fact]
    public void U_G1_TheTwoHundredAndFirstItem_IsRefused()
    {
        var wishlist = new WishlistAggregate(Guid.NewGuid());
        for (var i = 0; i < WishlistRules.MaxItems; i++)
            wishlist.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            wishlist.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.WishlistLimitReached, ex.ErrorCode);
        Assert.Equal(WishlistRules.MaxItems, wishlist.Items.Count);
    }

    [Fact]
    public void U_G1_RemovingAnAbsentSku_IsANoOp()
    {
        var wishlist = new WishlistAggregate(Guid.NewGuid());
        wishlist.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());
        var version = wishlist.Version;

        Assert.False(wishlist.RemoveItem(Guid.NewGuid(), TestUserContext.Create(), Guid.NewGuid()));

        Assert.Single(wishlist.Items);
        Assert.Equal(version, wishlist.Version);
    }

    [Fact]
    public void U_G1_Clear_EmptiesTheWishlist()
    {
        var wishlist = new WishlistAggregate(Guid.NewGuid());
        wishlist.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());
        wishlist.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(2, wishlist.Clear(TestUserContext.Create(), Guid.NewGuid()));
        Assert.Empty(wishlist.Items);
        Assert.Equal(0, wishlist.Clear(TestUserContext.Create(), Guid.NewGuid()));
    }

    [Fact]
    public void U_G1_Merge_UnionsItems_AndDropsTheOldestBeyondTheCap()
    {
        var survivor = new WishlistAggregate(Guid.NewGuid());
        var shared = Guid.NewGuid();
        survivor.AddItem(shared, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());
        for (var i = 0; i < WishlistRules.MaxItems - 4; i++)
            survivor.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

        // 197 in the survivor (room for 3); the duplicate has the shared SKU plus 5 distinct ones, oldest first.
        var duplicateItems = new List<WishlistItem> { new(shared, Guid.NewGuid(), Now.AddDays(-9)) };
        var distinct = Enumerable.Range(0, 5).Select(i => new WishlistItem(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-i))).ToList();
        duplicateItems.AddRange(distinct);

        var (moved, dropped) = survivor.MergeItemsFrom(duplicateItems, TestUserContext.Create());

        Assert.Equal(3, moved);
        Assert.Equal(2, dropped);
        Assert.Equal(WishlistRules.MaxItems, survivor.Items.Count);

        // The newest duplicate items were kept; the two oldest were dropped.
        Assert.Contains(survivor.Items, i => i.SkuId == distinct[0].SkuId);
        Assert.DoesNotContain(survivor.Items, i => i.SkuId == distinct[4].SkuId);
        Assert.DoesNotContain(survivor.Items, i => i.SkuId == distinct[3].SkuId);
    }

    // ── U-G2 ────────────────────────────────────────────────────────────────────────────────────

    private static StockAlertAggregate NewAlert()
    {
        var alert = new StockAlertAggregate(Guid.NewGuid());
        alert.Request(Guid.NewGuid(), Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());
        return alert;
    }

    [Fact]
    public void U_G2_Request_StartsPending()
    {
        var alert = NewAlert();

        Assert.Equal(StockAlertStatus.Pending, alert.Status);
        Assert.Null(alert.NotifiedOn);
    }

    [Fact]
    public void U_G2_MarkNotified_ThenAgain_TheSecondIsRefused()
    {
        var alert = NewAlert();

        alert.MarkNotified(Now.AddHours(1), TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(StockAlertStatus.Notified, alert.Status);
        Assert.Equal(Now.AddHours(1), alert.NotifiedOn);

        var ex = Assert.Throws<DomainRuleViolationException>(() => alert.MarkNotified(Now.AddHours(2), TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.InvalidStockAlertTransition, ex.ErrorCode);
    }

    [Theory]
    [InlineData(nameof(StockAlertClosureReason.CancelledByCustomer))]
    [InlineData(nameof(StockAlertClosureReason.SkuNoLongerSold))]
    [InlineData(nameof(StockAlertClosureReason.CustomerAnonymised))]
    [InlineData(nameof(StockAlertClosureReason.MergedDuplicate))]
    public void U_G2_Close_FromPending_RecordsTheReason_AndIsTerminal(string reasonName)
    {
        var reason = Enumeration.FromDisplayName<StockAlertClosureReason>(reasonName);
        var alert = NewAlert();

        alert.Close(reason, TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(StockAlertStatus.Closed, alert.Status);
        Assert.Equal(reason, alert.ClosureReason);
        Assert.Throws<DomainRuleViolationException>(() => alert.Close(reason, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Throws<DomainRuleViolationException>(() => alert.MarkNotified(Now, TestUserContext.Create(), Guid.NewGuid()));
    }

    [Fact]
    public void U_G2_NotifiedIsTerminal_CannotBeClosed()
    {
        var alert = NewAlert();
        alert.MarkNotified(Now, TestUserContext.Create(), Guid.NewGuid());

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            alert.Close(StockAlertClosureReason.CancelledByCustomer, TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.InvalidStockAlertTransition, ex.ErrorCode);
    }

    [Fact]
    public void U_G2_ReassignCustomer_IsIdempotent()
    {
        var alert = NewAlert();
        var survivor = Guid.NewGuid();

        Assert.True(alert.ReassignCustomer(survivor));
        Assert.False(alert.ReassignCustomer(survivor));
        Assert.Equal(survivor, alert.CustomerId);
    }
}
