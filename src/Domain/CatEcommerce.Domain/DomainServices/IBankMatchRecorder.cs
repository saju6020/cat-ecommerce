using CatEcommerce.Domain.Aggregates.Billing;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 section 11.4 C24 (ADR-189) -- records one bank match and answers <c>false</c> instead of failing when another request already
/// matched the same statement line or the same payment (the unique indexes on active matches are the guard). Two auto-matches run at
/// once therefore produce no duplicate and no error: the loser skips what the winner took. A port because only the data layer can
/// forget a failed insert.
/// </summary>
public interface IBankMatchRecorder
{
    Task<bool> TryRecordAsync(BankMatchAggregate match, CancellationToken ct = default);
}
