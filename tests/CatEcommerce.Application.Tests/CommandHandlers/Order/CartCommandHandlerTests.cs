using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Order;

/// <summary>HO-ORD-001 §11.4 C5-C8 -- the cart handlers over in-memory doubles (the HTTP behaviour is proved by the integration tests).</summary>
public sealed class CartCommandHandlerTests
{
    private static readonly string GuestHash = new('a', 64);
    private static readonly string OtherGuestHash = new('b', 64);

    private readonly FakeAggregateRootRepository<CartAggregate> _carts = new();
    private readonly FakeAggregateRootRepository<CatAggregate> _cats = new();
    private readonly FakeCatalogueLookup _catalogue = new();
    private readonly FakeSkuAvailabilityReader _availability = new();
    private readonly FakeDietAuthorisationGate _gate = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 9, 8, 0, 0, TimeSpan.Zero));
    private readonly Guid _customerId = Guid.NewGuid();

    private UserContext Customer() => new(_customerId, Guid.NewGuid()) { Roles = ["Customer"] };
    private static UserContext Guest() => new(Guid.Empty, Guid.NewGuid());

    private AddCartLineCommandHandler AddHandler(Dictionary<string, object>? settings = null) =>
        new(_carts, _cats, _catalogue, _availability, _gate, OrderingTestSettings.Defaults(settings), _time);

    private OrderCatalogueSku StockedSku(decimal price = 100m, bool prescription = false)
    {
        var sku = _catalogue.Add(price, prescription: prescription);
        _availability.InStock.Add(sku.SkuId);
        return sku;
    }

    private static AddCartLineCommand Add(UserContext user, Guid skuId, int quantity = 1, string? guestHash = null, string? newGuestHash = null, Guid? catId = null) => new()
    {
        NewCartId = Guid.NewGuid(), GuestTokenHash = guestHash, NewGuestTokenHash = newGuestHash, SkuId = skuId, Quantity = quantity, CatId = catId,
        UserContext = user, CorrelationId = Guid.NewGuid(),
    };

    private static string[] Codes(Platform.Infrastructure.Core.Commands.CommandResponse response) =>
        response.ValidationResult?.Errors.Select(e => e.ErrorCode).ToArray() ?? [];

    private CatAggregate SeedCat(Guid ownerId)
    {
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(
            ownerId, "Mitu", "Cat", CatSex.Female, DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-2), DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby",
            DateOnly.FromDateTime(DateTime.UtcNow), new UserContext(ownerId, Guid.NewGuid()), Guid.NewGuid()));
        _cats.Seed(cat);
        return cat;
    }

    [Fact]
    public async Task AGuestFirstAdd_CreatesTheCartFromTheMintedHash_AndReportsItWasCreated()
    {
        var sku = StockedSku();

        var response = await AddHandler().HandleAsync(Add(Guest(), sku.SkuId, 2, newGuestHash: GuestHash));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<AddCartLineResult>(response.Result);
        Assert.True(result.GuestCartCreated);
        var cart = Assert.Single(_carts.Saved);
        Assert.Equal(GuestHash, cart.GuestTokenHash);
        Assert.Equal(CartOwnerKind.Guest, cart.OwnerKind);
        Assert.Equal(2, Assert.Single(cart.Lines).Quantity);
        Assert.Equal(100m, cart.Lines[0].PriceWhenAdded);
    }

    [Fact]
    public async Task AGuestWithALiveCookie_AddsToTheirCart_AndTheMintedHashIsNotUsed()
    {
        var sku = StockedSku();
        await AddHandler().HandleAsync(Add(Guest(), sku.SkuId, newGuestHash: GuestHash));

        var response = await AddHandler().HandleAsync(Add(Guest(), sku.SkuId, 3, guestHash: GuestHash, newGuestHash: OtherGuestHash));

        var result = Assert.IsType<AddCartLineResult>(response.Result);
        Assert.False(result.GuestCartCreated);
        Assert.Single(_carts.Saved);
        Assert.Single(_carts.Updated);
        Assert.Equal(4, _carts.Saved[0].Lines[0].Quantity);
    }

    [Fact]
    public async Task ACookieForAMergedCart_StartsAFreshCartWithTheNewHash()
    {
        var sku = StockedSku();
        await AddHandler().HandleAsync(Add(Guest(), sku.SkuId, newGuestHash: GuestHash));
        _carts.Saved[0].Abandon(_time.GetUtcNow()); // the old cart is no longer active

        var response = await AddHandler().HandleAsync(Add(Guest(), sku.SkuId, guestHash: GuestHash, newGuestHash: OtherGuestHash));

        Assert.True(Assert.IsType<AddCartLineResult>(response.Result).GuestCartCreated);
        Assert.Equal(OtherGuestHash, _carts.Saved[1].GuestTokenHash);
    }

    [Fact]
    public async Task ACustomerFirstAdd_CreatesTheirCart_AndNoGuestHashIsNeeded()
    {
        var sku = StockedSku();

        var response = await AddHandler().HandleAsync(Add(Customer(), sku.SkuId));

        Assert.True(response.IsSuccess);
        Assert.False(Assert.IsType<AddCartLineResult>(response.Result).GuestCartCreated);
        Assert.Equal(_customerId, Assert.Single(_carts.Saved).CustomerId);
    }

    [Fact]
    public async Task ACallerWhoIsNeitherACustomerNorAGuestWithAHash_HasNoCart()
    {
        var sku = StockedSku();

        var response = await AddHandler().HandleAsync(Add(Guest(), sku.SkuId));

        Assert.Contains("CartNotFound", Codes(response));
        Assert.Empty(_carts.Saved);
    }

    [Fact]
    public async Task ASkuThatIsNotPurchasable_OrUnknown_IsRefused_BeforeAnyCartIsTouched()
    {
        var hidden = _catalogue.Add(purchasable: false);
        _availability.InStock.Add(hidden.SkuId);

        Assert.Contains("SkuNotPurchasable", Codes(await AddHandler().HandleAsync(Add(Customer(), hidden.SkuId))));
        Assert.Contains("SkuNotPurchasable", Codes(await AddHandler().HandleAsync(Add(Customer(), Guid.NewGuid()))));
        Assert.Empty(_carts.Saved);
    }

    [Fact]
    public async Task ASkuWithNoStockToSell_IsSkuUnavailable()
    {
        var sku = _catalogue.Add(); // purchasable, but the availability reader says OutOfStock

        Assert.Contains("SkuUnavailable", Codes(await AddHandler().HandleAsync(Add(Customer(), sku.SkuId))));
        Assert.Empty(_carts.Saved);
    }

    [Fact]
    public async Task APrescriptionSku_NeedsACustomer_ACat_OwnershipAndALiveAuthorisation()
    {
        var rx = StockedSku(prescription: true);
        var own = SeedCat(_customerId);
        var strangers = SeedCat(Guid.NewGuid());

        Assert.Contains("PrescriptionCatRequired", Codes(await AddHandler().HandleAsync(Add(Guest(), rx.SkuId, newGuestHash: GuestHash))));      // a guest
        Assert.Contains("PrescriptionCatRequired", Codes(await AddHandler().HandleAsync(Add(Customer(), rx.SkuId))));                              // no cat
        Assert.Contains("CatNotOwnedByCaller", Codes(await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: strangers.Id))));            // not theirs
        Assert.Contains("CatNotOwnedByCaller", Codes(await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: Guid.NewGuid()))));          // unknown
        Assert.Contains("PrescriptionNotAuthorised", Codes(await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: own.Id))));            // owned, no authorisation
        Assert.Empty(_carts.Saved);

        // the gate is asked only for a cat the caller owns, and with the customer, the cat and the SKU
        Assert.Equal([(_customerId, own.Id, rx.SkuId)], _gate.Checks);

        _gate.Authorised.Add((own.Id, rx.SkuId));
        var ok = await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: own.Id));
        Assert.True(ok.IsSuccess);
        Assert.Equal(own.Id, Assert.Single(_carts.Saved).Lines[0].CatId);
    }

    [Fact]
    public async Task APrescriptionSku_ForTwoCats_IsTwoLines()
    {
        var rx = StockedSku(prescription: true);
        var mitu = SeedCat(_customerId);
        var luna = SeedCat(_customerId);
        _gate.Authorised.Add((mitu.Id, rx.SkuId));
        _gate.Authorised.Add((luna.Id, rx.SkuId));

        await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: mitu.Id));
        await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: luna.Id));

        Assert.Equal(2, _carts.Saved[0].Lines.Count);
    }

    [Fact]
    public async Task AnOrdinarySku_NeverCarriesACat_EvenIfOneIsSent()
    {
        var sku = StockedSku();
        var cat = SeedCat(_customerId);

        await AddHandler().HandleAsync(Add(Customer(), sku.SkuId, catId: cat.Id));

        Assert.Null(_carts.Saved[0].Lines[0].CatId);
        Assert.Empty(_gate.Checks);
    }

    [Fact]
    public async Task TheLineCapAndTheQuantityMaximum_AreTheSettings_NotLiterals()
    {
        var one = StockedSku();
        var two = StockedSku();
        var settings = new Dictionary<string, object> { [SettingKeys.OrdersMaxCartLines] = 1, [SettingKeys.OrdersMaxQuantityPerLine] = 3 };

        Assert.True((await AddHandler(settings).HandleAsync(Add(Customer(), one.SkuId, 3))).IsSuccess);
        Assert.Contains("CartFull", Codes(await AddHandler(settings).HandleAsync(Add(Customer(), two.SkuId))));
        Assert.Contains("QuantityOutOfRange", Codes(await AddHandler(settings).HandleAsync(Add(Customer(), one.SkuId, 4))));
        Assert.Equal(3, _carts.Saved[0].Lines[0].Quantity); // adding to an existing line at the maximum stays at the maximum
    }

    [Fact]
    public async Task UpdatingALine_IsScopedToTheCallersOwnCart()
    {
        var sku = StockedSku();
        var add = await AddHandler().HandleAsync(Add(Customer(), sku.SkuId));
        var lineId = Assert.IsType<AddCartLineResult>(add.Result).LineId;
        var handler = new UpdateCartLineCommandHandler(_carts, _cats, _catalogue, _gate, OrderingTestSettings.Defaults(), _time);

        var stranger = await handler.HandleAsync(new UpdateCartLineCommand { LineId = lineId, Quantity = 9, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Customer"] }, CorrelationId = Guid.NewGuid() });
        Assert.Contains("CartLineNotFound", Codes(stranger));
        Assert.Equal(1, _carts.Saved[0].Lines[0].Quantity);

        var mine = await handler.HandleAsync(new UpdateCartLineCommand { LineId = lineId, Quantity = 9, UserContext = Customer(), CorrelationId = Guid.NewGuid() });
        Assert.True(mine.IsSuccess);
        Assert.Equal(9, _carts.Saved[0].Lines[0].Quantity);
    }

    [Fact]
    public async Task ChangingAPrescriptionLinesCat_ReChecksOwnershipAndAuthorisation()
    {
        var rx = StockedSku(prescription: true);
        var mitu = SeedCat(_customerId);
        var luna = SeedCat(_customerId);
        _gate.Authorised.Add((mitu.Id, rx.SkuId));
        var add = await AddHandler().HandleAsync(Add(Customer(), rx.SkuId, catId: mitu.Id));
        var lineId = Assert.IsType<AddCartLineResult>(add.Result).LineId;
        var handler = new UpdateCartLineCommandHandler(_carts, _cats, _catalogue, _gate, OrderingTestSettings.Defaults(), _time);

        var denied = await handler.HandleAsync(new UpdateCartLineCommand { LineId = lineId, CatId = luna.Id, UserContext = Customer(), CorrelationId = Guid.NewGuid() });
        Assert.Contains("PrescriptionNotAuthorised", Codes(denied));
        Assert.Equal(mitu.Id, _carts.Saved[0].Lines[0].CatId);

        _gate.Authorised.Add((luna.Id, rx.SkuId));
        Assert.True((await handler.HandleAsync(new UpdateCartLineCommand { LineId = lineId, CatId = luna.Id, UserContext = Customer(), CorrelationId = Guid.NewGuid() })).IsSuccess);
        Assert.Equal(luna.Id, _carts.Saved[0].Lines[0].CatId);
    }

    [Fact]
    public async Task RemovingALine_IsIdempotent_AndSavesOnlyWhenSomethingChanged()
    {
        var sku = StockedSku();
        var add = await AddHandler().HandleAsync(Add(Customer(), sku.SkuId));
        var lineId = Assert.IsType<AddCartLineResult>(add.Result).LineId;
        var handler = new RemoveCartLineCommandHandler(_carts, _time);

        Assert.True((await handler.HandleAsync(new RemoveCartLineCommand { LineId = lineId, UserContext = Customer(), CorrelationId = Guid.NewGuid() })).IsSuccess);
        Assert.True((await handler.HandleAsync(new RemoveCartLineCommand { LineId = lineId, UserContext = Customer(), CorrelationId = Guid.NewGuid() })).IsSuccess);
        Assert.True((await handler.HandleAsync(new RemoveCartLineCommand { LineId = Guid.NewGuid(), GuestTokenHash = GuestHash, UserContext = Guest(), CorrelationId = Guid.NewGuid() })).IsSuccess);

        Assert.Single(_carts.Updated);
        Assert.Empty(_carts.Saved[0].Lines);
    }

    [Fact]
    public async Task Merge_IntoAnExistingCustomerCart_TakesTheLargerQuantity_AndMarksTheGuestCartMerged()
    {
        var a = StockedSku();
        var b = StockedSku();
        await AddHandler().HandleAsync(Add(Customer(), a.SkuId, 3));
        await AddHandler().HandleAsync(Add(Guest(), a.SkuId, 2, newGuestHash: GuestHash));
        await AddHandler().HandleAsync(Add(Guest(), b.SkuId, 1, guestHash: GuestHash));
        var handler = new MergeGuestCartCommandHandler(_carts, _catalogue, OrderingTestSettings.Defaults(), _time);

        var response = await handler.HandleAsync(new MergeGuestCartCommand { NewCartId = Guid.NewGuid(), GuestTokenHash = GuestHash, UserContext = Customer(), CorrelationId = Guid.NewGuid() });

        var result = Assert.IsType<MergeGuestCartResult>(response.Result);
        Assert.Equal(2, result.MergedLineCount);
        Assert.Empty(result.DroppedLines);
        var customerCart = _carts.Saved.Single(c => c.CustomerId == _customerId);
        Assert.Equal(3, customerCart.Lines.Single(l => l.SkuId == a.SkuId).Quantity);
        Assert.Equal(CartStatus.Merged, _carts.Saved.Single(c => c.GuestTokenHash == GuestHash).Status);

        // run again: the guest cart is no longer active, so nothing happens
        var again = await handler.HandleAsync(new MergeGuestCartCommand { NewCartId = Guid.NewGuid(), GuestTokenHash = GuestHash, UserContext = Customer(), CorrelationId = Guid.NewGuid() });
        Assert.Equal(0, Assert.IsType<MergeGuestCartResult>(again.Result).MergedLineCount);
    }

    [Fact]
    public async Task Merge_ReportsTheGuestLinesThatHadNoRoom_ByProductName()
    {
        var existing = StockedSku();
        var fits = StockedSku();
        var noRoom = _catalogue.Add(name: "Royal Canin Kitten 2 kg");
        _availability.InStock.Add(noRoom.SkuId);
        var settings = new Dictionary<string, object> { [SettingKeys.OrdersMaxCartLines] = 2 };

        await AddHandler(settings).HandleAsync(Add(Customer(), existing.SkuId));
        await AddHandler(settings).HandleAsync(Add(Guest(), fits.SkuId, newGuestHash: GuestHash));
        await AddHandler(settings).HandleAsync(Add(Guest(), noRoom.SkuId, guestHash: GuestHash));
        var handler = new MergeGuestCartCommandHandler(_carts, _catalogue, OrderingTestSettings.Defaults(settings), _time);

        var response = await handler.HandleAsync(new MergeGuestCartCommand { NewCartId = Guid.NewGuid(), GuestTokenHash = GuestHash, UserContext = Customer(), CorrelationId = Guid.NewGuid() });

        var result = Assert.IsType<MergeGuestCartResult>(response.Result);
        var dropped = Assert.Single(result.DroppedLines);
        Assert.Equal("Royal Canin Kitten 2 kg", dropped.ProductName);
        Assert.Equal("CartFull", dropped.Reason);
    }

    [Fact]
    public async Task Merge_NeedsASignedInCustomer()
    {
        var handler = new MergeGuestCartCommandHandler(_carts, _catalogue, OrderingTestSettings.Defaults(), _time);

        var response = await handler.HandleAsync(new MergeGuestCartCommand { NewCartId = Guid.NewGuid(), GuestTokenHash = GuestHash, UserContext = Guest(), CorrelationId = Guid.NewGuid() });

        Assert.Contains("CartNotFound", Codes(response));
    }

    [Fact]
    public async Task Purge_AbandonsTheStaleBatch_InOneSave_AtTheConfiguredRetention()
    {
        var finder = new FakeCartFinder();
        for (var i = 0; i < 3; i++)
        {
            var cart = new CartAggregate(Guid.NewGuid());
            cart.OpenForGuest(new string((char)('a' + i), 64), _time.GetUtcNow().AddDays(-40), Guest());
            _carts.Seed(cart);
            finder.StaleGuestCartIds.Add(cart.Id);
        }

        var handler = new PurgeStaleGuestCartsCommandHandler(_carts, finder, OrderingTestSettings.Defaults(), _time);
        var response = await handler.HandleAsync(new PurgeStaleGuestCartsCommand { UserContext = Guest(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(3, Assert.IsType<PurgeStaleGuestCartsResult>(response.Result).Abandoned);
        Assert.Equal(_time.GetUtcNow().AddDays(-30), finder.LastCutoff);
        Assert.Equal(CartRules.PurgeBatchSize, finder.LastTake);
        Assert.Single(_carts.Updated); // one save commits the whole tracked batch
        Assert.All(finder.StaleGuestCartIds, id => Assert.Equal(CartStatus.Abandoned, _carts.GetByIdAsync(id).Result.Status));
    }

    [Fact]
    public async Task Purge_WithNothingStale_WritesNothing()
    {
        var handler = new PurgeStaleGuestCartsCommandHandler(_carts, new FakeCartFinder(), OrderingTestSettings.Defaults(), _time);

        var response = await handler.HandleAsync(new PurgeStaleGuestCartsCommand { UserContext = Guest(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(0, Assert.IsType<PurgeStaleGuestCartsResult>(response.Result).Abandoned);
        Assert.Empty(_carts.Updated);
    }
}
