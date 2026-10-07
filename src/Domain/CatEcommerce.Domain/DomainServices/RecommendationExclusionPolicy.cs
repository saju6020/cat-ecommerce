namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.10 -- pure. Applied to every recommendation block: a candidate is
/// excluded when it is not on the storefront, out of stock, the product itself, or a prescription
/// product the caller cannot buy (always for a guest; for a customer unless the gate found an
/// eligible cat).
/// </summary>
public static class RecommendationExclusionPolicy
{
    public static bool IsIncluded(
        bool isActiveAndListed, bool inStock, bool isSameProduct,
        bool prescriptionRequired, bool isGuest, bool callerHasEligibleCat)
    {
        if (!isActiveAndListed || !inStock || isSameProduct) return false;
        if (!prescriptionRequired) return true;
        return !isGuest && callerHasEligibleCat;
    }
}
