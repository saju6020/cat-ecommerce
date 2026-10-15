using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>Who is acting and the correlation id of the request -- carried by every Billing mutation so
/// the domain event it raises names its actor (the audit interceptor reads it, HO-ACC-001 §11.9).</summary>
public sealed record BillingActor(UserContext UserContext, Guid CorrelationId)
{
    public Guid UserId => UserContext.UserId;
}

// ── Billing profile (C1) ────────────────────────────────────────────────────────────────────────────
public sealed record BankAccountInput(string BankName, string AccountName, string AccountNumber, string? BranchName, string? RoutingNumber);

public sealed record BillingProfileSetDto(
    string LegalName, string TaxRegistrationNumber, IReadOnlyList<string> AddressLines, string? Phone, string? Email,
    string? InvoiceFooter, IReadOnlyList<BankAccountInput> BankAccounts, BillingActor Actor);

// ── Charges (§8.3) ──────────────────────────────────────────────────────────────────────────────────
/// <summary>The charge fields beyond <see cref="ChargeRequestDto"/> the aggregate needs to record it.</summary>
public sealed record ChargeRecordContext(string CurrencyCode, DateTimeOffset Now, BillingActor Actor);

// ── Invoices (§8.6) ─────────────────────────────────────────────────────────────────────────────────
public sealed record DraftInvoiceLineInput(
    Guid? ChargeId, Guid? CorrectsInvoiceLineId, BusinessLine BusinessLine, ItemKind ItemKind, ChargeSourceKind SourceKind,
    Guid SourceId, BillingGroupKind? BillingGroupKind, Guid? BillingGroupId, Guid? CatId, string Description,
    decimal Quantity, decimal UnitPrice, decimal LineDiscount, Guid TaxClassId);

public sealed record DraftInvoiceDto(
    Guid BranchId, Guid CustomerId, string CurrencyCode, IReadOnlyList<DraftInvoiceLineInput> Lines, string? Notes,
    Guid? CorrectsInvoiceId, Guid? CorrectsCreditNoteId, DateTimeOffset Now, BillingActor Actor);

/// <summary>What the aggregate needs about one tax class at the moment of issue: its code and the
/// rate effective at that instant (ADR-181: resolved at issue, frozen on the line).</summary>
public sealed record TaxRateInfo(Guid TaxClassId, string Code, decimal? RatePercent);

public sealed record InvoiceIssueDto(
    string Number, string FinancialYear, long SequenceNumber, IReadOnlyDictionary<Guid, TaxRateInfo> TaxRates,
    SellerSnapshot Seller, BillToSnapshot BillTo, DateTimeOffset Now, string TimeZoneId, int PaymentTermsDays,
    DateOnly IssueDate, string? IdempotencyKey, BillingActor Actor);

// ── Payments (§8.8) ─────────────────────────────────────────────────────────────────────────────────
public sealed record AllocationRequest(Guid InvoiceId, decimal Amount);

public sealed record PaymentRecordDto(
    Guid BranchId, Guid CustomerId, PaymentMethod Method, decimal Amount, string CurrencyCode, DateOnly ReceivedOn,
    string? ExternalReference, PaymentCollector? Collector, PaymentEarmark? Earmark, string? Notes,
    decimal ApprovalThreshold, IReadOnlyList<AllocationRequest> Allocations, DateOnly TodayLocal, DateTimeOffset Now,
    string? IdempotencyKey, BillingActor Actor, long? ReceiptNumber = null, string? ApprovalWaiverReason = null);

public sealed record PaymentAllocateDto(Guid InvoiceId, decimal Amount, bool InvoiceMatchesEarmark, DateTimeOffset Now, BillingActor Actor);

public sealed record PaymentReleaseDto(Guid InvoiceId, decimal Amount, AllocationReleaseReason Reason, DateTimeOffset Now, BillingActor Actor);

// ── Credit notes and refunds (§8.9) ─────────────────────────────────────────────────────────────────
public sealed record CreditNoteLineInput(
    Guid InvoiceLineId, string Description, decimal? Quantity, decimal? RequestedAmount, decimal NetAmount,
    decimal TaxRatePercent, decimal TaxAmount, decimal GrossAmount);

public sealed record CreditNoteIssueDto(
    Guid InvoiceId, Guid BranchId, Guid CustomerId, string CurrencyCode, string Number, string FinancialYear, long SequenceNumber,
    CreditNoteKind Kind, string Reason, IReadOnlyList<CreditNoteLineInput> Lines, decimal ReleasedToCustomerCredit,
    DateTimeOffset Now, DateOnly IssueDate, string? IdempotencyKey, BillingActor Actor);

public sealed record RefundIssueDto(
    Guid PaymentId, Guid? CreditNoteId, Guid BranchId, Guid CustomerId, string CurrencyCode, RefundMethod Method,
    decimal Amount, string? Reference, string Reason, DateTimeOffset Now, DateOnly BusinessDate, string? IdempotencyKey, BillingActor Actor);

// ── Cash-up, COD, bank (§8.10, §8.1) ────────────────────────────────────────────────────────────────
public sealed record CodRemittanceLineInput(Guid PaymentId, decimal AmountRemitted);

public sealed record CodRemittanceDto(
    string CourierCode, DateOnly ReceivedOn, decimal Amount, string CurrencyCode, RemittanceMethod Method, string? Reference,
    IReadOnlyList<CodRemittanceLineInput> Lines, DateTimeOffset Now, BillingActor Actor);

public sealed record BankStatementLineInput(
    int LineNumber, DateOnly TransactionDate, decimal Amount, BankEntryDirection Direction, string? Reference,
    string? Description, string? BankTransactionId);

public sealed record BankStatementImportDto(
    string BankAccountLabel, DateOnly PeriodFrom, DateOnly PeriodTo, string SourceFileName, string SourceSha256,
    IReadOnlyList<BankStatementLineInput> Lines, DateTimeOffset Now, BillingActor Actor);

public sealed record BankMatchDto(
    Guid StatementId, Guid StatementLineId, Guid PaymentId, BankMatchMethod Method, DateTimeOffset Now, BillingActor Actor);

public sealed record AccountingExportRunDto(
    DateOnly From, DateOnly To, Guid? BranchId, int InvoiceCount, int InvoiceLineCount, int PaymentCount, int AllocationCount,
    int CreditNoteCount, int RefundCount, string Sha256, DateTimeOffset Now, BillingActor Actor);

// ── Loyalty (§8.11) ─────────────────────────────────────────────────────────────────────────────────
public sealed record LoyaltyRedemptionDto(int Points, decimal Value, Guid LoyaltyEntryId, decimal QualifyingNet);

public sealed record LoyaltyEntryDto(
    Guid CustomerId, LoyaltyEntryKind Kind, int Points, Guid? InvoiceId, Guid? CreditNoteId, Guid? SourceCustomerId, int Shortfall,
    DateTimeOffset Now, BillingActor Actor);
