using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-PHM-001 §8.7 -- Inventory's published <see cref="DispensaryBatch"/> facts become the planner's offers: unlinked SKUs are dropped, free quantity is clamped
/// at zero and a batch that is not <c>Available</c> is offered but not dispensable, with a reason.</summary>
public sealed class BatchOfferFactoryTests
{
    private static DispensaryBatch Batch(Guid sku, BatchStatus status, decimal onHand = 10m, decimal reserved = 0m) =>
        new(sku, Guid.NewGuid(), "LOT-" + Guid.NewGuid().ToString("N")[..4], new DateOnly(2027, 1, 1), status, Guid.NewGuid(), onHand, reserved);

    [Fact]
    public void AnAvailableBatchOfALinkedSku_IsOfferedAsDispensable_WithItsFreeQuantity()
    {
        var sku = Guid.NewGuid();
        var batch = Batch(sku, BatchStatus.Available, onHand: 10m, reserved: 3m);

        var offer = Assert.Single(BatchOfferFactory.FromCandidates([sku], [batch]));

        Assert.Equal((batch.BatchId, batch.LocationId, 7m, true, null), (offer.BatchId, offer.LocationId, offer.Available, offer.IsDispensable, offer.NotDispensableReason));
    }

    [Fact]
    public void ABatchThatIsNotAvailable_IsOfferedButNotDispensable_WithAReason()
    {
        var sku = Guid.NewGuid();

        foreach (var status in new[] { BatchStatus.Quarantined, BatchStatus.Recalled, BatchStatus.WrittenOff })
        {
            var offer = Assert.Single(BatchOfferFactory.FromCandidates([sku], [Batch(sku, status)]));

            Assert.False(offer.IsDispensable);
            Assert.False(string.IsNullOrWhiteSpace(offer.NotDispensableReason));
        }
    }

    [Fact]
    public void AnUnlinkedSku_IsDropped_AndOverReservedStockClampsToZero()
    {
        var linked = Guid.NewGuid();
        var overReserved = Batch(linked, BatchStatus.Available, onHand: 2m, reserved: 5m);

        var offers = BatchOfferFactory.FromCandidates([linked], [Batch(Guid.NewGuid(), BatchStatus.Available), overReserved]);

        Assert.Equal(0m, Assert.Single(offers).Available);
    }
}
