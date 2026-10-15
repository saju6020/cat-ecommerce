using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>HO-BIL-001 §8.1/§11.4 C23 -- one row of an imported statement. The description is stored
/// <b>card-masked</b> (<c>CardNumberGuard.MaskCardNumbers</c>, C-5); the raw text is never kept.
/// Immutable after import (<c>trg_bank_statement_line_immutable</c>).</summary>
public sealed class BankStatementLine
{
    public Guid Id { get; private set; }
    public int LineNumber { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public decimal Amount { get; private set; }
    public BankEntryDirection Direction { get; private set; } = default!;
    public string? Reference { get; private set; }
    public string? Description { get; private set; }
    public string? BankTransactionId { get; private set; }

    private BankStatementLine() { }

    public BankStatementLine(
        Guid id, int lineNumber, DateOnly transactionDate, decimal amount, BankEntryDirection direction,
        string? reference, string? description, string? bankTransactionId)
    {
        Id = id;
        LineNumber = lineNumber;
        TransactionDate = transactionDate;
        Amount = amount;
        Direction = direction;
        Reference = reference;
        Description = description;
        BankTransactionId = bankTransactionId;
    }
}
