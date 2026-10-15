using CatEcommerce.Domain.Events.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Inventory;

/// <summary>
/// HO-IMP-001 §8.4 -- Inventory's own record of how a product must be kept. <c>Id</c> IS the <c>ProductId</c> (1:1,
/// created on first write). Separate from HO-SHP-001's customer-facing storage-instructions text.
/// </summary>
public sealed class ProductStorageProfileAggregate : AggregateRoot
{
    public StorageCondition Condition { get; private set; } = StorageCondition.Ambient;
    public decimal? MinTemperatureC { get; private set; }
    public decimal? MaxTemperatureC { get; private set; }
    public string? HandlingNotes { get; private set; }

    private ProductStorageProfileAggregate() { }

    public ProductStorageProfileAggregate(Guid productId) : base(productId) { }

    public void Set(StorageProfileDto dto)
    {
        foreach (var temperature in new[] { dto.MinTemperatureC, dto.MaxTemperatureC })
        {
            if (temperature is < InventoryRules.MinTemperatureC or > InventoryRules.MaxTemperatureC)
                throw InventoryViolation.Of(BusinessViolationCodes.InvalidTemperatureRange, "A storage temperature must be between -40 and 60 degrees Celsius.", nameof(MinTemperatureC));
        }

        if (dto.MinTemperatureC is { } min && dto.MaxTemperatureC is { } max && min > max)
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidTemperatureRange, "The minimum temperature cannot be above the maximum.", nameof(MinTemperatureC));

        if (dto.HandlingNotes is { Length: > 500 })
            throw InventoryViolation.Of(BusinessViolationCodes.InvalidTemperatureRange, "Handling notes cannot exceed 500 characters.", nameof(HandlingNotes));

        Condition = dto.Condition;
        MinTemperatureC = dto.MinTemperatureC is null ? null : decimal.Round(dto.MinTemperatureC.Value, 1);
        MaxTemperatureC = dto.MaxTemperatureC is null ? null : decimal.Round(dto.MaxTemperatureC.Value, 1);
        HandlingNotes = string.IsNullOrWhiteSpace(dto.HandlingNotes) ? null : dto.HandlingNotes.Trim();

        SetDefaultValue(dto.Actor.UserContext);
        AddDomainEvent(new ProductStorageProfileSetEvent { ProductId = Id, Condition = Condition, CorrelationId = dto.Actor.CorrelationId, UserContext = dto.Actor.UserContext });
        UpdateVersion();
    }
}
