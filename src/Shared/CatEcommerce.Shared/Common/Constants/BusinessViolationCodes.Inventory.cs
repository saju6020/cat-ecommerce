namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-IMP-001 §10.4 -- Inventory's own violation codes, appended per ADR-341. Three codes in §10.4's list
/// already exist in <c>BusinessViolationCodes.Shop.cs</c> with the same name and meaning
/// (<c>SkuNotFound</c>, <c>InvalidCountryCode</c>, <c>StockAlertNotFound</c>) and are reused, not redeclared.
/// The additions after the list are marked "beyond §10.4".
/// </summary>
public static partial class BusinessViolationCodes
{
    // Locations, SKUs, quantities
    public const string StockLocationCodeAlreadyExists = nameof(StockLocationCodeAlreadyExists);
    public const string StockLocationNotFound = nameof(StockLocationNotFound);
    public const string StockLocationHasStock = nameof(StockLocationHasStock);
    public const string QuarantineLocationAlreadyExists = nameof(QuarantineLocationAlreadyExists);
    public const string MovementNotPermittedAtLocation = nameof(MovementNotPermittedAtLocation);
    public const string SkuInactive = nameof(SkuInactive);
    public const string InvalidQuantity = nameof(InvalidQuantity);
    public const string FractionalQuantityNotAllowed = nameof(FractionalQuantityNotAllowed);
    public const string InvalidTemperatureRange = nameof(InvalidTemperatureRange);

    // Suppliers, POs
    public const string SupplierCodeAlreadyExists = nameof(SupplierCodeAlreadyExists);
    public const string SupplierNotFound = nameof(SupplierNotFound);
    public const string SupplierInactive = nameof(SupplierInactive);
    public const string InvalidCurrencyCode = nameof(InvalidCurrencyCode);
    public const string PurchaseOrderNotFound = nameof(PurchaseOrderNotFound);
    public const string InvalidPurchaseOrderTransition = nameof(InvalidPurchaseOrderTransition);
    public const string PurchaseOrderLinesRequired = nameof(PurchaseOrderLinesRequired);
    public const string DuplicatePurchaseOrderSku = nameof(DuplicatePurchaseOrderSku);
    public const string PurchaseOrderHasReceipts = nameof(PurchaseOrderHasReceipts);
    public const string ExpectedDeliveryInPast = nameof(ExpectedDeliveryInPast);
    public const string CloseReasonRequired = nameof(CloseReasonRequired);

    // Consignments
    public const string ConsignmentNotFound = nameof(ConsignmentNotFound);
    public const string InvalidConsignmentTransition = nameof(InvalidConsignmentTransition);
    public const string ConsignmentPurchaseOrderMismatch = nameof(ConsignmentPurchaseOrderMismatch);
    public const string ClearanceDocumentsMissing = nameof(ClearanceDocumentsMissing);
    public const string InvalidHsCode = nameof(InvalidHsCode);
    public const string DocumentLimitExceeded = nameof(DocumentLimitExceeded);
    public const string InvalidDocumentContentType = nameof(InvalidDocumentContentType);
    public const string DocumentTooLarge = nameof(DocumentTooLarge);
    public const string ExchangeRateMissing = nameof(ExchangeRateMissing);
    public const string InvalidExchangeRate = nameof(InvalidExchangeRate);
    public const string NoReceivedQuantityToAllocate = nameof(NoReceivedQuantityToAllocate);

    // Receipts, batches
    public const string GoodsReceiptLinesRequired = nameof(GoodsReceiptLinesRequired);
    public const string ExpiryDateRequired = nameof(ExpiryDateRequired);
    public const string BatchAlreadyExpired = nameof(BatchAlreadyExpired);
    public const string InvalidBatchDates = nameof(InvalidBatchDates);
    public const string RejectionReasonRequired = nameof(RejectionReasonRequired);
    public const string LotNumberRequired = nameof(LotNumberRequired);
    public const string ReceiptExceedsOutstanding = nameof(ReceiptExceedsOutstanding);
    public const string PurchaseOrderLineMismatch = nameof(PurchaseOrderLineMismatch);
    public const string UnitCostRequired = nameof(UnitCostRequired);
    public const string DuplicateReceiptLine = nameof(DuplicateReceiptLine);
    public const string ConsignmentNotCleared = nameof(ConsignmentNotCleared);
    public const string BatchNotFound = nameof(BatchNotFound);
    public const string InvalidBatchTransition = nameof(InvalidBatchTransition);

    // Movements
    public const string InsufficientStock = nameof(InsufficientStock);
    public const string BatchExpired = nameof(BatchExpired);
    public const string BatchNotUsable = nameof(BatchNotUsable);
    public const string AllocationOverrideReasonRequired = nameof(AllocationOverrideReasonRequired);
    public const string StockPartyRequired = nameof(StockPartyRequired);
    public const string ReservationNotFound = nameof(ReservationNotFound);
    public const string ReturnNotTraceable = nameof(ReturnNotTraceable);

    // Stock control
    public const string TransferLocationsMustDiffer = nameof(TransferLocationsMustDiffer);
    public const string AdjustmentReasonRequired = nameof(AdjustmentReasonRequired);
    public const string AdjustmentNoteRequired = nameof(AdjustmentNoteRequired);
    public const string InvalidAdjustmentDirection = nameof(InvalidAdjustmentDirection);
    public const string AdjustmentReasonNotApplicable = nameof(AdjustmentReasonNotApplicable);
    public const string AdjustmentNotPending = nameof(AdjustmentNotPending);
    public const string AdjustmentWithdrawNotRequester = nameof(AdjustmentWithdrawNotRequester);
    public const string ApproverMustDiffer = nameof(ApproverMustDiffer);
    public const string CountLocationBusy = nameof(CountLocationBusy);
    public const string InvalidCountTransition = nameof(InvalidCountTransition);
    public const string CountLineOutsideScope = nameof(CountLineOutsideScope);

    // Alerts, recall, breach
    public const string StockAlertNotOpen = nameof(StockAlertNotOpen);
    public const string RecallConfirmationMismatch = nameof(RecallConfirmationMismatch);
    public const string RecallNotActive = nameof(RecallNotActive);
    public const string RecallStockStillHeld = nameof(RecallStockStillHeld);
    public const string StorageBreachNotFound = nameof(StorageBreachNotFound);
    public const string InvalidStorageBreachTransition = nameof(InvalidStorageBreachTransition);
    public const string NoStockAtLocation = nameof(NoStockAtLocation);

    // Beyond §10.4 -- lookups and the stale-version / validation cases the endpoints of §12.4 need.
    public const string GoodsReceiptNotFound = nameof(GoodsReceiptNotFound);
    public const string StockTransferNotFound = nameof(StockTransferNotFound);
    public const string StockAdjustmentNotFound = nameof(StockAdjustmentNotFound);
    public const string StockCountNotFound = nameof(StockCountNotFound);
    public const string ConsignmentDocumentNotFound = nameof(ConsignmentDocumentNotFound);
    public const string ConsignmentVersionConflict = nameof(ConsignmentVersionConflict);
    public const string InvalidStockLocationCode = nameof(InvalidStockLocationCode);
    public const string PurchaseOrderNotIssued = nameof(PurchaseOrderNotIssued);
    public const string ReceiptSupplierMismatch = nameof(ReceiptSupplierMismatch);
    public const string InvalidSupplier = nameof(InvalidSupplier);
    public const string InvalidConsignment = nameof(InvalidConsignment);
    public const string InvalidReorderPolicy = nameof(InvalidReorderPolicy);
    public const string InvalidDateRange = nameof(InvalidDateRange);
    public const string MovementKindNotAllowed = nameof(MovementKindNotAllowed);
}
