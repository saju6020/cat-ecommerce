namespace CatEcommerce.Domain.DomainServices;

/// <summary>A stay with charges that Billing has not invoiced yet and whose service date has reached a cycle boundary (US-BRD-20).</summary>
public sealed record StayDueForInvoicing(Guid StayId, Guid BranchId, Guid CustomerId, int Cycle, DateOnly ServiceDateTo);

/// <summary>
/// HO-BRD-001 section 8.15 -- Boarding's READ-ONLY view of the charges it pushed to Billing (the push model of merged Billing, mismatch 2 of section 1.1 Amended): which charges exist
/// for a stay, which are voided and which an invoice already carries. Implemented over Billing's tables in <c>Infrastructure/Boarding</c>; Boarding never writes a charge except through
/// <c>IBillableChargeRecorder</c> in its own command handlers.
/// </summary>
public interface IBoardingChargeLedger
{
    Task<IReadOnlyList<BoardingChargeRow>> GetRowsAsync(Guid stayId, CancellationToken ct = default);

    /// <summary>
    /// Checked-in stays that have an UNINVOICED, active charge dated on or before a cycle boundary the stay has reached: the boundary of cycle <c>k</c> is <c>arrival + 30k - 1</c> and the stay
    /// has reached it when <c>today &gt;= arrival + 30k</c>. One row per stay: the highest cycle due.
    /// </summary>
    Task<IReadOnlyList<StayDueForInvoicing>> GetStaysDueForInvoicingAsync(DateTimeOffset now, CancellationToken ct = default);
}
