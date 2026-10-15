using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 section 8.11 / 11.4 C16 -- U-G1..U-G3: a campaign prices sterilisations only, applies to a booking only when every condition holds (each refusal its own reason),
/// and its prices lock once a surgery is booked under it.</summary>
public sealed class SurgeryCampaignAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid Spay = Guid.NewGuid();
    private static readonly Guid Neuter = Guid.NewGuid();
    private static readonly Guid Dental = Guid.NewGuid();
    private static readonly Guid BranchA = Guid.NewGuid();
    private static readonly Guid BranchB = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 11, 1);
    private static readonly DateOnly To = new(2026, 11, 30);

    private static Money Bdt(decimal amount) => new(amount, "BDT");

    private static SurgeryCampaignDto Dto(
        string code = "TNR-NOV", IReadOnlyList<CampaignPriceInput>? prices = null, IReadOnlyList<Guid>? branches = null, int? maxCases = null, DateOnly? from = null, DateOnly? to = null) =>
        new(code, "Community cats November", "Free-roaming cats", from ?? From, to ?? To, branches ?? [], prices ?? [new CampaignPriceInput(Spay, true, Bdt(500m)), new CampaignPriceInput(Neuter, true, Bdt(300m))],
            maxCases, Now, TestUserContext.Create(), Guid.NewGuid());

    private static SurgeryCampaignAggregate Created(SurgeryCampaignDto? dto = null)
    {
        var campaign = new SurgeryCampaignAggregate(Guid.NewGuid());
        campaign.Create(dto ?? Dto());
        return campaign;
    }

    private static string CodeOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    /// <summary>U-G1 -- a campaign on a dental service is refused.</summary>
    [Fact]
    public void U_G1_ACampaignPricesSterilisationsOnly()
    {
        var campaign = new SurgeryCampaignAggregate(Guid.NewGuid());

        var code = CodeOf(() => campaign.Create(Dto(prices: [new CampaignPriceInput(Spay, true, Bdt(500m)), new CampaignPriceInput(Dental, false, Bdt(900m))])));

        Assert.Equal(BusinessViolationCodes.CampaignServiceNotSterilisation, code);
        Assert.Empty(campaign.Prices);
    }

    [Fact]
    public void ACampaign_IsCreatedActive_WithNormalisedCode_AndRaisesAnEvent()
    {
        var campaign = Created(Dto(code: " tnr-nov "));

        Assert.Equal("TNR-NOV", campaign.Code);
        Assert.True(campaign.IsActive);
        Assert.Equal(2, campaign.Prices.Count);
        Assert.Contains(campaign.Events, e => e is SurgeryCampaignChangedEvent { IsActive: true } changed && changed.CampaignId == campaign.Id);
    }

    [Fact]
    public void ACampaign_MustBeWellFormed()
    {
        var c = new SurgeryCampaignAggregate(Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(code: "bad code!"))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(code: ""))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(from: To, to: From))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(maxCases: 0))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(prices: []))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(prices: [new CampaignPriceInput(Spay, true, Bdt(1m)), new CampaignPriceInput(Spay, true, Bdt(2m))]))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(prices: [new CampaignPriceInput(Spay, true, Bdt(-1m))]))));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => c.Create(Dto(prices: [new CampaignPriceInput(Spay, true, Bdt(1m)), new CampaignPriceInput(Neuter, true, new Money(1m, "USD"))]))));
    }

    /// <summary>U-G2 -- every reason a campaign does not apply is its own message; a free (zero) price is allowed.</summary>
    [Fact]
    public void U_G2_EveryRefusalHasItsOwnReason()
    {
        var campaign = Created(Dto(branches: [BranchA], maxCases: 2));
        var inside = new DateOnly(2026, 11, 10);

        Assert.Contains("runs from 2026-11-01 to 2026-11-30", campaign.Evaluate(BranchA, Spay, new DateOnly(2026, 12, 1), "TNR volunteer", 0).RefusalReason);
        Assert.Contains("runs from", campaign.Evaluate(BranchA, Spay, new DateOnly(2026, 10, 31), "TNR volunteer", 0).RefusalReason);
        Assert.Contains("does not cover this branch", campaign.Evaluate(BranchB, Spay, inside, "TNR volunteer", 0).RefusalReason);
        Assert.Contains("no price for this procedure", campaign.Evaluate(BranchA, Dental, inside, "TNR volunteer", 0).RefusalReason);
        Assert.Contains("limit of 2 case", campaign.Evaluate(BranchA, Spay, inside, "TNR volunteer", 2).RefusalReason);
        Assert.Contains("eligibility note", campaign.Evaluate(BranchA, Spay, inside, null, 0).RefusalReason);
        Assert.Contains("eligibility note", campaign.Evaluate(BranchA, Spay, inside, "   ", 0).RefusalReason);
        Assert.Contains("eligibility note", campaign.Evaluate(BranchA, Spay, inside, new string('x', SurgeryRules.EligibilityNoteMax + 1), 0).RefusalReason);

        var applies = campaign.Evaluate(BranchA, Neuter, inside, "Volunteer carer with a letter", 1);
        Assert.True(applies.Applies);
        Assert.Equal(Bdt(300m), applies.Price);

        // The boundary days are inside; the cap is on count, so one case short of it still applies.
        Assert.True(campaign.Evaluate(BranchA, Spay, From, "x", 1).Applies);
        Assert.True(campaign.Evaluate(BranchA, Spay, To, "x", 1).Applies);

        campaign.SetActive(false, TestUserContext.Create(), Guid.NewGuid());
        Assert.Contains("not active", campaign.Evaluate(BranchA, Spay, inside, "x", 0).RefusalReason);
    }

    [Fact]
    public void ACampaign_WithNoBranchesListed_CoversEveryBranch()
    {
        var campaign = Created();

        Assert.True(campaign.Evaluate(BranchA, Spay, new DateOnly(2026, 11, 10), "x", 0).Applies);
        Assert.True(campaign.Evaluate(BranchB, Spay, new DateOnly(2026, 11, 10), "x", 0).Applies);
    }

    /// <summary>U-G3 -- a price change after one booking is locked; everything else may still change.</summary>
    [Fact]
    public void U_G3_ThePrices_AreLockedOnceASurgeryIsBooked()
    {
        var campaign = Created();

        var changed = Dto(prices: [new CampaignPriceInput(Spay, true, Bdt(550m)), new CampaignPriceInput(Neuter, true, Bdt(300m))]);
        Assert.Equal(BusinessViolationCodes.CampaignPriceLocked, CodeOf(() => campaign.Update(changed, casesBooked: 1)));
        Assert.Equal(Bdt(500m), campaign.Prices.First(p => p.ServiceId == Spay).Price);

        var fewer = Dto(prices: [new CampaignPriceInput(Spay, true, Bdt(500m))]);
        Assert.Equal(BusinessViolationCodes.CampaignPriceLocked, CodeOf(() => campaign.Update(fewer, casesBooked: 1)));

        // Unchanged prices with other changes are fine, locked or not.
        var sameIdsBefore = campaign.Prices.Select(p => p.Id).ToList();
        campaign.Update(Dto(from: From, to: To.AddDays(14), maxCases: 50), casesBooked: 1);
        Assert.Equal(To.AddDays(14), campaign.ValidTo);
        Assert.Equal(50, campaign.MaxCases);
        Assert.Equal(sameIdsBefore, campaign.Prices.Select(p => p.Id).ToList());

        // With no booking the prices may change.
        campaign.Update(changed, casesBooked: 0);
        Assert.Equal(Bdt(550m), campaign.Prices.First(p => p.ServiceId == Spay).Price);
    }

    [Fact]
    public void TheCode_NeverChanges_AndTheCapCannotDropBelowTheCasesBooked()
    {
        var campaign = Created();

        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => campaign.Update(Dto(code: "OTHER"), casesBooked: 0)));
        Assert.Equal(BusinessViolationCodes.SurgeryCampaignInvalid, CodeOf(() => campaign.Update(Dto(maxCases: 2), casesBooked: 3)));
        campaign.Update(Dto(maxCases: 3), casesBooked: 3);
        Assert.Equal(3, campaign.MaxCases);
    }

    [Fact]
    public void SetActive_IsIdempotent_AndRaisesAnEventOnlyOnAChange()
    {
        var campaign = Created();
        var before = campaign.Events.Count;
        var version = campaign.Version;

        campaign.SetActive(true, TestUserContext.Create(), Guid.NewGuid());
        Assert.Equal(before, campaign.Events.Count);
        Assert.Equal(version, campaign.Version);

        campaign.SetActive(false, TestUserContext.Create(), Guid.NewGuid());
        Assert.False(campaign.IsActive);
        Assert.Contains(campaign.Events, e => e is SurgeryCampaignChangedEvent { IsActive: false });
        campaign.SetActive(true, TestUserContext.Create(), Guid.NewGuid());
        Assert.True(campaign.IsActive);
    }
}
