namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.9/10.1 -- computed only.</summary>
public sealed class PurchaseBlockReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PurchaseBlockReason OutOfStock = new(1, nameof(OutOfStock));
    public static readonly PurchaseBlockReason PrescriptionSignInRequired = new(2, nameof(PrescriptionSignInRequired));
    public static readonly PurchaseBlockReason PrescriptionAuthorisationRequired = new(3, nameof(PrescriptionAuthorisationRequired));
    public static readonly PurchaseBlockReason NotOnSale = new(4, nameof(NotOnSale));
}
