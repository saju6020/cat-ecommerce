namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-IMP-001 §10.3 -- the text of every refusal that can be produced by TWO paths (the port or
/// handler's own check, and the constraint filter's race path), shared so the two cannot drift
/// (HO-ACC-001 §20.6).
/// </summary>
public static class InventoryMessages
{
    public const string InsufficientStock = "That quantity is no longer available — someone may have just taken it. Refresh and try again.";
    public const string BatchNotUsable = "That batch can no longer be used. Refresh and choose another.";
    public const string StockLocationCodeAlreadyExists = "A location with this code already exists at this branch.";
    public const string QuarantineLocationAlreadyExists = "This branch already has an active quarantine location.";
    public const string SupplierCodeAlreadyExists = "This supplier code is already in use.";
    public const string DuplicateReceiptLine = "The same SKU, lot and expiry cannot appear twice in one receipt.";
    public const string CountLocationBusy = "A stock count is already in progress at this location.";
    public const string ExpiryDateRequired = "Food and medicine must be received with an expiry date.";

    /// <summary>The generic (race-path) text: the trigger cannot name the lot.</summary>
    public const string BatchExpiredGeneric = "That batch has expired and cannot be sold, dispensed or fed.";

    public static string BatchExpired(string lotNumber, DateOnly expiresOn) =>
        $"Batch {lotNumber} expired on {expiresOn:yyyy-MM-dd} and cannot be sold, dispensed or fed.";

    public static string InsufficientStockAvailable(decimal available) =>
        $"Only {available:0.###} is available. " + InsufficientStock;
}
