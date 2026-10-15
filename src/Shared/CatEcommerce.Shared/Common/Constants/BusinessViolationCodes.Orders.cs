namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §10.4 -- Ordering's own violation codes (ADR-341 per-area partial). Already declared elsewhere and reused, not
/// redeclared: <c>CatNotOwnedByCaller</c> (Administration), <c>DeliveryAddressNotFound</c> (Accounts), <c>CancellationReasonRequired</c>
/// (Appointments), <c>BatchExpired</c> (Inventory) and <c>RefundMethodNotAllowed</c> (Billing -- same value; the order refund path reuses it).</summary>
public static partial class BusinessViolationCodes
{
    // Cart
    public const string CartNotFound = nameof(CartNotFound);
    public const string CartFull = nameof(CartFull);
    public const string CartLineNotFound = nameof(CartLineNotFound);
    public const string DuplicateCartLine = nameof(DuplicateCartLine);
    public const string SkuNotPurchasable = nameof(SkuNotPurchasable);
    public const string SkuUnavailable = nameof(SkuUnavailable);
    public const string QuantityOutOfRange = nameof(QuantityOutOfRange);
    public const string PrescriptionCatRequired = nameof(PrescriptionCatRequired);
    public const string PrescriptionNotAuthorised = nameof(PrescriptionNotAuthorised);

    // Checkout
    public const string CheckoutQuoteChanged = nameof(CheckoutQuoteChanged);
    public const string CartEmpty = nameof(CartEmpty);
    public const string NothingToPlace = nameof(NothingToPlace);
    public const string PolicyAcceptanceRequired = nameof(PolicyAcceptanceRequired);
    public const string AddressOutsideServedZones = nameof(AddressOutsideServedZones);
    public const string DeliveryTaxNotConfigured = nameof(DeliveryTaxNotConfigured); // FU1 ORD-54: the DELIVERY tax class is missing, inactive or has no rate
    public const string ServiceLevelUnavailable = nameof(ServiceLevelUnavailable);
    public const string DeliverySlotUnavailable = nameof(DeliverySlotUnavailable);
    public const string PickupBranchUnavailable = nameof(PickupBranchUnavailable);
    public const string PaymentMethodUnavailable = nameof(PaymentMethodUnavailable);
    public const string RecipientPhoneRequired = nameof(RecipientPhoneRequired);
    public const string StockNoLongerAvailable = nameof(StockNoLongerAvailable);
    public const string DuplicateSubmission = nameof(DuplicateSubmission);
    public const string IdempotencyKeyRequired = nameof(IdempotencyKeyRequired);
    public const string IdempotencyKeyReused = nameof(IdempotencyKeyReused);
    public const string RequestInProgress = nameof(RequestInProgress);

    // Discounts
    public const string DiscountCodeNotFound = nameof(DiscountCodeNotFound);
    public const string DiscountCodeInactive = nameof(DiscountCodeInactive);
    public const string DiscountCodeNotYetValid = nameof(DiscountCodeNotYetValid);
    public const string DiscountCodeExpired = nameof(DiscountCodeExpired);
    public const string DiscountUsageLimitReached = nameof(DiscountUsageLimitReached);
    public const string DiscountCustomerLimitReached = nameof(DiscountCustomerLimitReached);
    public const string DiscountFirstOrderOnly = nameof(DiscountFirstOrderOnly);
    public const string DiscountNotApplicableToItems = nameof(DiscountNotApplicableToItems);
    public const string DiscountMinimumSpendNotMet = nameof(DiscountMinimumSpendNotMet);
    public const string DiscountCodeAlreadyExists = nameof(DiscountCodeAlreadyExists);
    public const string InvalidDiscountDefinition = nameof(InvalidDiscountDefinition);

    // Order lifecycle
    public const string OrderNotFound = nameof(OrderNotFound);
    public const string InvalidOrderTransition = nameof(InvalidOrderTransition);
    public const string CancellationNotAvailable = nameof(CancellationNotAvailable);
    public const string OrderNotAwaitingPayment = nameof(OrderNotAwaitingPayment);
    public const string OrderNotExpirable = nameof(OrderNotExpirable);
    public const string TransferAmountMismatch = nameof(TransferAmountMismatch);
    public const string PaymentAlreadyAwaitingApproval = nameof(PaymentAlreadyAwaitingApproval);
    public const string NoPaymentAwaitingApproval = nameof(NoPaymentAwaitingApproval);
    public const string SecondApproverMustDiffer = nameof(SecondApproverMustDiffer);
    public const string OrderNotFullyAllocated = nameof(OrderNotFullyAllocated);
    public const string BatchRecalled = nameof(BatchRecalled);
    public const string CourierNotFound = nameof(CourierNotFound);
    public const string CodCollectionRequired = nameof(CodCollectionRequired);
    public const string CollectionPaymentMismatch = nameof(CollectionPaymentMismatch);
    public const string TerminalReferenceRequired = nameof(TerminalReferenceRequired);
    public const string DeliveryFailureReasonInvalid = nameof(DeliveryFailureReasonInvalid);
    public const string TendersDoNotCoverTotal = nameof(TendersDoNotCoverTotal);
    public const string ReallocationReasonRequired = nameof(ReallocationReasonRequired);

    // Returns and refunds
    public const string ReturnNotFound = nameof(ReturnNotFound);
    public const string ReturnWindowClosed = nameof(ReturnWindowClosed);
    public const string ReturnQuantityExceeded = nameof(ReturnQuantityExceeded);
    public const string ReturnNotEligibleOpenedFood = nameof(ReturnNotEligibleOpenedFood);
    public const string ReturnNotEligibleUsedLitter = nameof(ReturnNotEligibleUsedLitter);
    public const string DefectConfirmationRequired = nameof(DefectConfirmationRequired);
    public const string InvalidReturnTransition = nameof(InvalidReturnTransition);
    public const string ReturnReceiptExceedsApproved = nameof(ReturnReceiptExceedsApproved);
    public const string RefundExceedsAmountPaid = nameof(RefundExceedsAmountPaid);
    public const string RefundMethodUnavailable = nameof(RefundMethodUnavailable);
    public const string RefundReferenceRequired = nameof(RefundReferenceRequired);

    // Configuration
    public const string DeliveryZoneNotFound = nameof(DeliveryZoneNotFound);
    public const string DeliveryZoneNameAlreadyExists = nameof(DeliveryZoneNameAlreadyExists);
    public const string InvalidDeliveryZone = nameof(InvalidDeliveryZone);
    public const string CourierNameAlreadyExists = nameof(CourierNameAlreadyExists);
    public const string InvalidBankTransferAccount = nameof(InvalidBankTransferAccount);
    public const string InvalidCourier = nameof(InvalidCourier); // not in §10.4: a malformed tracking-link template needs a code of its own
    public const string ReconciliationIssueNotFound = nameof(ReconciliationIssueNotFound);
    public const string ReconciliationIssueAlreadyResolved = nameof(ReconciliationIssueAlreadyResolved); // not in §10.4: resolving twice is not "not found"
    public const string ReconciliationNoteRequired = nameof(ReconciliationNoteRequired); // not in §10.4
}
