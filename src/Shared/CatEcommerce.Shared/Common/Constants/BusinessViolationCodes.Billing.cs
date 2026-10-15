namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §10.4 -- Billing's own violation codes (ADR-341 per-area partial).
/// <c>CurrencyMismatch</c> (Shop), <c>TaxClassNotFound</c> (Administration), <c>ReasonRequired</c>
/// (Cat), <c>CustomerNotFound</c>, <c>BranchNotFound</c> and <c>ServiceNotFound</c> already exist and
/// are reused, not redeclared.</summary>
public static partial class BusinessViolationCodes
{
    public const string ChargeNotFound = nameof(ChargeNotFound);
    public const string ChargeAlreadyRecorded = nameof(ChargeAlreadyRecorded);
    public const string ChargeAlreadyInvoiced = nameof(ChargeAlreadyInvoiced);
    public const string ChargeNotActive = nameof(ChargeNotActive);
    public const string ChargesMixedCustomerOrBranch = nameof(ChargesMixedCustomerOrBranch);
    public const string TaxRateNotConfigured = nameof(TaxRateNotConfigured);
    // InvalidQuantity: shared with HO-IMP-001, declared once in BusinessViolationCodes.Inventory.cs (same value; merged 2026-10-07).
    public const string DiscountExceedsAmount = nameof(DiscountExceedsAmount);
    public const string InvoiceNotFound = nameof(InvoiceNotFound);
    public const string InvoiceNotDraft = nameof(InvoiceNotDraft);
    public const string InvoiceTooManyLines = nameof(InvoiceTooManyLines);
    public const string BillingProfileMissing = nameof(BillingProfileMissing);
    public const string FinancialYearConfigurationChanged = nameof(FinancialYearConfigurationChanged);
    public const string AllocationExceedsOutstanding = nameof(AllocationExceedsOutstanding);
    public const string AllocationExceedsUnallocated = nameof(AllocationExceedsUnallocated);
    public const string EarmarkMismatch = nameof(EarmarkMismatch);
    public const string PaymentNotFound = nameof(PaymentNotFound);
    public const string PaymentNotAccepted = nameof(PaymentNotAccepted);
    public const string PaymentNotPending = nameof(PaymentNotPending);
    // ApproverMustDiffer: shared with HO-IMP-001, declared once in BusinessViolationCodes.Inventory.cs (same value; merged 2026-10-07).
    public const string ReferenceRequired = nameof(ReferenceRequired);
    public const string ApprovalWaiverNotAllowed = nameof(ApprovalWaiverNotAllowed); // FU1 ORD-53: only cash on delivery and counter cash/card tenders may skip the second approval
    public const string CollectorRequired = nameof(CollectorRequired);
    public const string ReferenceLooksLikeCardNumber = nameof(ReferenceLooksLikeCardNumber);
    public const string ReceivedDateInFuture = nameof(ReceivedDateInFuture);
    public const string ReceivedDateTooOld = nameof(ReceivedDateTooOld);
    public const string PaymentHasRefunds = nameof(PaymentHasRefunds);
    public const string CreditExceedsInvoice = nameof(CreditExceedsInvoice);
    public const string CreditExceedsLine = nameof(CreditExceedsLine);
    public const string CreditNoteNotFound = nameof(CreditNoteNotFound);
    public const string RefundMethodNotAllowed = nameof(RefundMethodNotAllowed);
    public const string RefundExceedsAvailable = nameof(RefundExceedsAvailable);
    public const string CashUpClosed = nameof(CashUpClosed);
    public const string CashUpDateInFuture = nameof(CashUpDateInFuture);
    public const string CountRequired = nameof(CountRequired);
    public const string VarianceExplanationRequired = nameof(VarianceExplanationRequired);
    public const string PendingApprovalsExist = nameof(PendingApprovalsExist);
    public const string CollectionNotCod = nameof(CollectionNotCod);
    public const string CollectionCourierMismatch = nameof(CollectionCourierMismatch);
    public const string RemittanceExceedsCollection = nameof(RemittanceExceedsCollection);
    public const string StatementDuplicate = nameof(StatementDuplicate);
    public const string StatementTooLarge = nameof(StatementTooLarge);
    public const string StatementMalformed = nameof(StatementMalformed);
    public const string StatementLineNotFound = nameof(StatementLineNotFound);
    public const string StatementNotFound = nameof(StatementNotFound);
    public const string BankLineAlreadyMatched = nameof(BankLineAlreadyMatched);
    public const string PaymentAlreadyMatched = nameof(PaymentAlreadyMatched);
    public const string MatchAmountMismatch = nameof(MatchAmountMismatch);
    public const string MatchMethodNotTransfer = nameof(MatchMethodNotTransfer);
    public const string ExportRangeTooLarge = nameof(ExportRangeTooLarge);
    public const string LoyaltyNotEnabled = nameof(LoyaltyNotEnabled);
    public const string LoyaltyInsufficientPoints = nameof(LoyaltyInsufficientPoints);
    public const string LoyaltyBelowMinimum = nameof(LoyaltyBelowMinimum);
    public const string LoyaltyRedemptionExceedsNet = nameof(LoyaltyRedemptionExceedsNet);
    public const string IdempotencyKeyConflict = nameof(IdempotencyKeyConflict);

    // Not named by §10.4 but required by the handlers the handover specifies (recorded in the
    // handover's Amended rows): shape/lookup refusals that need their own stable code.
    public const string InvoiceNotIssued = nameof(InvoiceNotIssued);
    public const string InvoiceLineNotFound = nameof(InvoiceLineNotFound);
    public const string InvalidAmount = nameof(InvalidAmount);
    public const string PaymentNotEarmarked = nameof(PaymentNotEarmarked);
    public const string BankMatchNotFound = nameof(BankMatchNotFound);
    public const string RemittanceNotFound = nameof(RemittanceNotFound);
    public const string CustomerCreditUnavailable = nameof(CustomerCreditUnavailable);
    public const string NothingToCredit = nameof(NothingToCredit);
    public const string CorrectedDraftInvalid = nameof(CorrectedDraftInvalid);
    public const string BranchRequired = nameof(BranchRequired);
    public const string AllocationInvoiceCustomerMismatch = nameof(AllocationInvoiceCustomerMismatch);
    public const string ChargeServiceDateAfterRange = nameof(ChargeServiceDateAfterRange);
    public const string DescriptionRequired = nameof(DescriptionRequired);
    public const string TooManyAllocations = nameof(TooManyAllocations);
    public const string InvoiceHasNoLines = nameof(InvoiceHasNoLines);
    public const string SourceNotFound = nameof(SourceNotFound);
    public const string SourceKindNotCapturable = nameof(SourceKindNotCapturable);
    public const string ServiceNotPriced = nameof(ServiceNotPriced);
    public const string StaleVersion = nameof(StaleVersion);
}
