using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-BRD-001 §8.14: <c>IBoardingVetSpendingPolicy.GetHeadroomAsync</c>'s result, "for the clinic to
/// decide whether a charge is within the limit" -- <c>limit = VetVisitAuthorised ? VetSpendingLimit
/// : 0</c> + approved increases; <c>committed</c> = Σ <c>VetCommitments</c>; <c>headroom = limit −
/// committed</c>. No handover writes this as a full record; fields inferred directly from that
/// formula's three named quantities plus the gating flag. Flagged as inferred in
/// docs/handover/CONTRACTS.md.
/// </summary>
public sealed record VetSpendingHeadroomResult(bool VetVisitAuthorised, Money Limit, Money Committed, Money Headroom);
