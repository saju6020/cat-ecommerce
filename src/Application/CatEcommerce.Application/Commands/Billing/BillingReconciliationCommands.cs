using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Billing;

/// <summary>How much of one courier-collected COD payment a remittance covers.</summary>
public sealed record RemittanceLineInput(Guid PaymentId, decimal AmountRemitted);

/// <summary>HO-BIL-001 section 11.1 C22 (US-BIL-06 AC1) -- the money a courier handed over, and which collections it covers.</summary>
public sealed class RecordCodRemittanceCommand : Command
{
    public required Guid RemittanceId { get; init; }
    public required Guid BranchId { get; init; }
    public required string CourierCode { get; init; }
    public required DateOnly ReceivedOn { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>A <c>RemittanceMethod</c> name -- <c>Cash</c> or <c>BankTransfer</c>.</summary>
    public required string Method { get; init; }
    public string? Reference { get; init; }
    public required IReadOnlyList<RemittanceLineInput> Lines { get; init; }
}

/// <summary>One parsed statement row (the controller parses the file, the handler never sees it).</summary>
public sealed record StatementLineInput(
    int LineNumber, DateOnly TransactionDate, decimal Amount, string Direction, string? Reference, string? Description, string? BankTransactionId);

/// <summary>C23 -- import a parsed bank statement; a second import of the same file is refused (<c>StatementDuplicate</c>).</summary>
public sealed class ImportBankStatementCommand : Command
{
    public required Guid StatementId { get; init; }
    public required Guid BranchId { get; init; }
    public required string BankAccountLabel { get; init; }
    public required DateOnly PeriodFrom { get; init; }
    public required DateOnly PeriodTo { get; init; }
    public required string SourceFileName { get; init; }
    public required string SourceSha256 { get; init; }
    public required IReadOnlyList<StatementLineInput> Lines { get; init; }
}

/// <summary>C24 -- match every statement credit that has exactly one candidate transfer payment; the rest are left for a person.</summary>
public sealed class AutoMatchBankStatementCommand : Command
{
    public required Guid StatementId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C25 -- a person matches one statement credit to one transfer payment.</summary>
public sealed class MatchBankLineCommand : Command
{
    public required Guid MatchId { get; init; }
    public required Guid StatementId { get; init; }
    public required Guid StatementLineId { get; init; }
    public required Guid PaymentId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>C25 -- undo a match (the row is kept: who, when, why).</summary>
public sealed class UnmatchBankLineCommand : Command
{
    public required Guid MatchId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}
