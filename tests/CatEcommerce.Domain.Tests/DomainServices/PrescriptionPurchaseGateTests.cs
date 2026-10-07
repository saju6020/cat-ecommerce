using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>
/// HO-SHP-001 §15 Phase F -- U-F1 (BR-2) and U-F5 (US-SHP-08). Shop consumes HO-PHM-001's
/// <c>IDietAuthorisationGate</c> (HO-SHP-001 §1 Amended, 2026-10-04): ownership, liveness, expiry
/// and the "other variant" rule are the gate's own (an authorisation that fails any of them never
/// reaches Shop as an authorised cat; <c>CheckAsync</c> reports its outcome). Those §15 U-F1 rows
/// are therefore proved here as the outcome mapping, and end to end by I-F2 against a controllable
/// gate that applies the same rules over the real cat rows.
/// </summary>
public sealed class PrescriptionPurchaseGateTests
{
    private static readonly Guid Customer = Guid.NewGuid();
    private static AuthorisedCat Cat(string name = "Mitu") => new(Guid.NewGuid(), name, Guid.NewGuid(), new DateOnly(2027, 1, 1));

    [Fact]
    public void U_F1_NonPrescription_IsPurchasable()
    {
        var decision = PrescriptionPurchaseGate.Evaluate(false, Customer, []);
        Assert.True(decision.IsPermitted);
        Assert.Null(decision.BlockReason);
    }

    [Fact]
    public void U_F1_NonPrescription_Guest_IsPurchasable()
    {
        Assert.True(PrescriptionPurchaseGate.Evaluate(false, null, []).IsPermitted);
    }

    [Fact]
    public void U_F1_Prescription_Guest_RequiresSignIn()
    {
        var decision = PrescriptionPurchaseGate.Evaluate(true, null, [Cat()]);
        Assert.False(decision.IsPermitted);
        Assert.Equal(PurchaseBlockReason.PrescriptionSignInRequired, decision.BlockReason);
        Assert.Empty(decision.EligibleCats);
    }

    [Fact]
    public void U_F1_Prescription_NoAuthorisation_RequiresAuthorisation()
    {
        var decision = PrescriptionPurchaseGate.Evaluate(true, Customer, []);
        Assert.False(decision.IsPermitted);
        Assert.Equal(PurchaseBlockReason.PrescriptionAuthorisationRequired, decision.BlockReason);
    }

    [Fact]
    public void U_F1_Prescription_WithAnAuthorisedCat_IsEligibleForThatCatOnly()
    {
        var cat = Cat();
        var decision = PrescriptionPurchaseGate.Evaluate(true, Customer, [cat]);

        Assert.True(decision.IsPermitted);
        Assert.Equal([cat], decision.EligibleCats);
    }

    [Theory]
    [InlineData(nameof(DietGateOutcome.NoAuthorisation))]
    [InlineData(nameof(DietGateOutcome.AuthorisationExpired))]
    [InlineData(nameof(DietGateOutcome.CatDeceased))]
    [InlineData(nameof(DietGateOutcome.CatNotOwned))]
    public void U_F1_GateOutcomeThatIsNotAnAuthorisation_RequiresAuthorisation(string outcomeName)
    {
        var outcome = Enumeration.FromDisplayName<DietGateOutcome>(outcomeName);
        var decision = PrescriptionPurchaseGate.FromOutcome(outcome);

        Assert.False(decision.IsPermitted);
        Assert.Equal(PurchaseBlockReason.PrescriptionAuthorisationRequired, decision.BlockReason);
    }

    [Theory]
    [InlineData(nameof(DietGateOutcome.NotRequired))]
    [InlineData(nameof(DietGateOutcome.Authorised))]
    public void U_F1_GateOutcomeThatPermits_IsEligible(string outcomeName)
    {
        var outcome = Enumeration.FromDisplayName<DietGateOutcome>(outcomeName);
        Assert.True(PrescriptionPurchaseGate.FromOutcome(outcome).IsPermitted);
    }

    // ── U-F5 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_F5_OutOfStock_Excluded() =>
        Assert.False(RecommendationExclusionPolicy.IsIncluded(true, inStock: false, false, false, false, false));

    [Fact]
    public void U_F5_PrescriptionForGuest_Excluded() =>
        Assert.False(RecommendationExclusionPolicy.IsIncluded(true, true, false, prescriptionRequired: true, isGuest: true, callerHasEligibleCat: false));

    [Fact]
    public void U_F5_PrescriptionForCustomerWithNoEligibleCat_Excluded() =>
        Assert.False(RecommendationExclusionPolicy.IsIncluded(true, true, false, true, isGuest: false, callerHasEligibleCat: false));

    [Fact]
    public void U_F5_PrescriptionForCustomerWithAnEligibleCat_Included() =>
        Assert.True(RecommendationExclusionPolicy.IsIncluded(true, true, false, true, isGuest: false, callerHasEligibleCat: true));

    [Fact]
    public void U_F5_TheProductItself_Excluded() =>
        Assert.False(RecommendationExclusionPolicy.IsIncluded(true, true, isSameProduct: true, false, false, false));

    [Fact]
    public void U_F5_NotOnStorefront_Excluded() =>
        Assert.False(RecommendationExclusionPolicy.IsIncluded(isActiveAndListed: false, true, false, false, false, false));

    [Fact]
    public void U_F5_OrdinaryInStockProduct_Included() =>
        Assert.True(RecommendationExclusionPolicy.IsIncluded(true, true, false, false, true, false));
}
