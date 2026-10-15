namespace CatEcommerce.Domain.DomainServices;

/// <summary>What Billing shows for the deposit of one stay: the accepted total and the total awaiting a second approver (BR-17).</summary>
public sealed record DepositLedgerEntry(decimal Accepted, decimal PendingApproval)
{
    public static readonly DepositLedgerEntry None = new(0m, 0m);
}

/// <summary>
/// HO-BRD-001 section 8.10/12.8 P1 (ADR-223, renumbered ADR-356) -- a READ port over HO-BIL-001's payments. Staff record the deposit in Billing as a payment
/// earmarked <c>{ Kind = BoardingStay, SourceId = stayId }</c> (merged Billing's mechanism; the handover's <c>BoardingStayDeposit</c> reference is that earmark);
/// Boarding confirms only after reading what Billing accepted. Nothing here writes Billing data and nothing captures money.
/// </summary>
public interface IBoardingDepositLedger
{
    Task<DepositLedgerEntry> GetAsync(Guid stayId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, DepositLedgerEntry>> GetManyAsync(IReadOnlyCollection<Guid> stayIds, CancellationToken ct = default);

    /// <summary>
    /// Stays of a branch that ENDED without a check-out and were not forfeited (cancelled, lapsed or expired -- a lapse is a cancellation with a reason) and still hold an accepted, unreleased deposit of
    /// their own customer and branch: the deposits to give back. No look-back window -- the ledger is the filter.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetRefundableAsync(Guid branchId, CancellationToken ct = default);

    /// <summary>The stay a payment is earmarked for, or <c>null</c> when it is not a boarding deposit.</summary>
    Task<Guid?> GetStayIdOfPaymentAsync(Guid paymentId, CancellationToken ct = default);
}
