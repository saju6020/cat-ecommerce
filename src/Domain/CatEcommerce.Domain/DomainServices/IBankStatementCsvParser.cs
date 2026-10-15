using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

public sealed record BankStatementParseError(int RowNumber, string Message);

/// <param name="TooLarge">More than <c>BillingRules.MaxStatementBytes</c> (or lines) -- <c>413 StatementTooLarge</c>.</param>
/// <param name="Sha256">Of the raw bytes, lower-case hex -- the duplicate-import key (ADR-189).</param>
public sealed record BankStatementParseResult(
    bool TooLarge, string Sha256, IReadOnlyList<BankStatementLineInput> Lines, IReadOnlyList<BankStatementParseError> Errors)
{
    public bool IsValid => !TooLarge && Errors.Count == 0;
}

/// <summary>
/// HO-BIL-001 §12.2 / OQ-10 -- the canonical statement CSV
/// (<c>date,amount,direction,reference,description,bankTransactionId</c>): header required, RFC 4180
/// quoting, dates <c>yyyy-MM-dd</c>, amounts invariant with <c>.</c> and ≤ 2 decimals, direction
/// <c>C</c>/<c>D</c>; rejects files over 1 MiB and non-UTF-8; a formula-looking cell stays text.
/// Parsed <b>in memory</b>, never stored as a file (NFR-SEC-5 -- nothing to scan or serve).
/// </summary>
public interface IBankStatementCsvParser
{
    BankStatementParseResult Parse(ReadOnlyMemory<byte> content);
}
