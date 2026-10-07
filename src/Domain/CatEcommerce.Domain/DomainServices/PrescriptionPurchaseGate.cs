using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>The result of <see cref="PrescriptionPurchaseGate"/>: whether the prescription rule
/// lets this caller buy, why not, and for which cats.</summary>
public sealed record PrescriptionDecision(bool IsPermitted, PurchaseBlockReason? BlockReason, IReadOnlyList<AuthorisedCat> EligibleCats);

/// <summary>
/// HO-SHP-001 section 8.9 / ADR-084 -- pure. Shop consumes HO-PHM-001's
/// <c>IDietAuthorisationGate</c> (ADR-149), whose own rule decides which cats are authorised: a
/// cat that is deceased, no longer this customer's, or whose authorisation lapsed never appears in
/// <c>GetAuthorisedCatsAsync</c>, and <c>CheckAsync</c> names the reason (HO-SHP-001 section 1
/// Amended, 2026-10-04 -- this replaces the section 8.9 draft port, which carried the raw
/// authorisations for Shop to filter). This class owns what is still Shop's: the guest rule, the
/// non-prescription pass-through and the mapping from the port's outcome to a purchase block.
/// </summary>
public static class PrescriptionPurchaseGate
{
    public static PrescriptionDecision Evaluate(bool prescriptionRequired, Guid? customerId, IReadOnlyList<AuthorisedCat> authorisedCats)
    {
        if (!prescriptionRequired) return new PrescriptionDecision(true, null, []);
        if (customerId is null) return new PrescriptionDecision(false, PurchaseBlockReason.PrescriptionSignInRequired, []);

        return authorisedCats.Count > 0
            ? new PrescriptionDecision(true, null, authorisedCats)
            : new PrescriptionDecision(false, PurchaseBlockReason.PrescriptionAuthorisationRequired, []);
    }

    /// <summary>The add-to-cart / checkout eligibility contract (section 8.9, US-SHP-10 AC2/AC3):
    /// one outcome mapping shared by both call sites, so the two checks cannot drift.</summary>
    public static PrescriptionDecision FromOutcome(DietGateOutcome outcome)
    {
        if (outcome == DietGateOutcome.NotRequired || outcome == DietGateOutcome.Authorised)
            return new PrescriptionDecision(true, null, []);

        return new PrescriptionDecision(false, PurchaseBlockReason.PrescriptionAuthorisationRequired, []);
    }
}
