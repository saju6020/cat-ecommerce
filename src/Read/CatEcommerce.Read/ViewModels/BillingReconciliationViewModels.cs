namespace CatEcommerce.Read.ViewModels;

// Reconciliation (HO-BIL-001 section 12.4)
public sealed record CodCollectionViewModel(
    Guid PaymentId, string? ReceiptNumber, Guid CustomerId, string CustomerName, string? CourierCode, string? CourierName, DateOnly ReceivedOn,
    MoneyViewModel Collected, MoneyViewModel Remitted, MoneyViewModel Outstanding, MoneyViewModel Shortfall, string Status, bool Overdue);

public sealed record CodRemittanceViewModel(
    Guid RemittanceId, string CourierCode, DateOnly ReceivedOn, MoneyViewModel Amount, string Method, string? Reference, MoneyViewModel Applied,
    MoneyViewModel Unapplied, int LineCount, Guid RecordedBy, string RecordedByName, DateTimeOffset RecordedOn);

public sealed record CodCourierReconciliationViewModel(
    string CourierCode, MoneyViewModel Collected, MoneyViewModel Remitted, MoneyViewModel Shortfall, MoneyViewModel Unremitted,
    MoneyViewModel UnappliedRemittances, IReadOnlyList<CodCollectionViewModel> ShortfallLines);

public sealed record CodReconciliationViewModel(IReadOnlyList<CodCourierReconciliationViewModel> Couriers);

public sealed record BankStatementSummaryViewModel(
    Guid StatementId, string BankAccountLabel, DateOnly PeriodFrom, DateOnly PeriodTo, string SourceFileName, int LineCount, int MatchedCount,
    DateTimeOffset ImportedOn, Guid ImportedBy, string ImportedByName);

public sealed record BankLineViewModel(Guid LineId, int LineNumber, DateOnly TransactionDate, MoneyViewModel Amount, string Direction, string? Reference, string? Description);

public sealed record UnmatchedPaymentViewModel(
    Guid PaymentId, string? ReceiptNumber, Guid CustomerId, string CustomerName, MoneyViewModel Amount, DateOnly ReceivedOn, string? Reference);

public sealed record BankMatchViewModel(
    Guid MatchId, Guid StatementLineId, Guid PaymentId, string? ReceiptNumber, MoneyViewModel Amount, string Method, DateTimeOffset MatchedOn, Guid MatchedBy,
    DateTimeOffset? UnmatchedOn, string? UnmatchReason);

/// <summary>Flagged both ways (US-BIL-06 AC2): credits with no payment, payments with no credit, and what is matched.</summary>
public sealed record BankStatementReconciliationViewModel(
    BankStatementSummaryViewModel Statement, IReadOnlyList<BankLineViewModel> UnmatchedStatementLines, IReadOnlyList<UnmatchedPaymentViewModel> UnmatchedPayments,
    IReadOnlyList<BankMatchViewModel> Matches, int DebitLineCount);
