using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>HO-SHP-001 section 8.5, 8.3.3 -- food-only fields. Country of origin and the
/// prescription flag are separate concerns (section 8.3, ADR-081): country of origin is set via
/// <c>UpdateDetails</c>; this file is the owned <c>FoodDetails</c> value object and the
/// prescription flag only.</summary>
public sealed partial class ProductAggregate
{
    public void SetFoodDetails(FoodDetails details, UserContext userContext, Guid correlationId)
    {
        if (!IsFood)
            throw new DomainRuleViolationException(BusinessViolationCodes.FoodDetailsOnlyForFood, "Food details can only be set on a CatFood product.", nameof(Category));

        FoodDetails = details;
        RefreshSearchText();

        AddDomainEvent(new ProductFoodDetailsUpdatedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    /// <summary>
    /// Turning the flag ON is refused while a SKU of this product is a component of a non-archived
    /// bundle (OQ-14 / ADR-085 / BR-2: a kit cannot carry an authorisation per cat, so a bundle may
    /// never contain a prescription diet). The handler supplies the names of those bundles; turning
    /// the flag off is always allowed.
    /// </summary>
    public void SetPrescriptionRequired(bool prescriptionRequired, UserContext userContext, Guid correlationId, IReadOnlyList<string>? containingBundleNames = null)
    {
        if (!IsFood)
            throw new DomainRuleViolationException(BusinessViolationCodes.PrescriptionOnlyForFood, "Only food products can require a prescription.", nameof(PrescriptionRequired));

        if (prescriptionRequired && containingBundleNames is { Count: > 0 })
            throw new DomainRuleViolationException(BusinessViolationCodes.PrescriptionProductInBundle,
                "A prescription product cannot be part of a bundle. Remove it from: " + string.Join(", ", containingBundleNames) + ".", nameof(PrescriptionRequired));

        PrescriptionRequired = prescriptionRequired;

        AddDomainEvent(new ProductPrescriptionRequirementChangedEvent
        {
            ProductId = Id, PrescriptionRequired = prescriptionRequired, CorrelationId = correlationId, UserContext = userContext
        });
        UpdateVersion();
    }
}
