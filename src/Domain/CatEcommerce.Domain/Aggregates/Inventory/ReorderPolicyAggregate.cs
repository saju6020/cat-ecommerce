using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.13 (US-IMP-12). The reorder point of a (SKU, location) pair. The EFFECTIVE point of a pair is its policy's, else
/// <c>inventory.defaultReorderPoint</c> (HO-ADM-001's key -- this is its consumer). <c>ReorderPoint = 0</c> silences a pair.
/// </summary>
public sealed class ReorderPolicyAggregate : AggregateRoot
{
    public Guid SkuId { get; private set; }
    public Guid LocationId { get; private set; }
    public decimal ReorderPoint { get; private set; }
    public decimal? ReorderQuantity { get; private set; }
    public Guid? PreferredSupplierId { get; private set; }

    private ReorderPolicyAggregate() { }

    public ReorderPolicyAggregate(Guid id) : base(id) { }

    public void Set(ReorderPolicyDto dto)
    {
        if (dto.ReorderPoint < 0m || decimal.Round(dto.ReorderPoint, 3) != dto.ReorderPoint)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidReorderPolicy, "The reorder point cannot be negative, with at most three decimal places.", nameof(ReorderPoint));
        if (dto.ReorderQuantity is { } quantity && (quantity <= 0m || decimal.Round(quantity, 3) != quantity))
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidReorderPolicy, "The reorder quantity must be greater than zero, with at most three decimal places.", nameof(ReorderQuantity));

        SkuId = dto.SkuId;
        LocationId = dto.LocationId;
        ReorderPoint = dto.ReorderPoint;
        ReorderQuantity = dto.ReorderQuantity;
        PreferredSupplierId = dto.PreferredSupplierId;

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new ReorderPolicySetEvent { SkuId = SkuId, LocationId = LocationId, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }
}
