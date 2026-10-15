using System.Reflection;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-PHM-001 §15 group E -- the dispensing rules (US-PHM-05, BR-3, BR-4; ADR-143, ADR-147): FEFO planning, validation, label and dose wording.</summary>
public sealed class PharmacyDispensingTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);
    private static readonly Guid Sku = Guid.NewGuid();

    private static BatchOffer Offer(DateOnly? expires, decimal available, string lot, bool dispensable = true, string? reason = null, Guid? sku = null) =>
        new(sku ?? Sku, Guid.NewGuid(), lot, expires, available, dispensable, reason, Guid.NewGuid());

    private static List<BatchOffer> ThreeBatches() =>
    [
        Offer(new DateOnly(2026, 12, 10), 10m, "DEC"),
        Offer(new DateOnly(2026, 11, 5), 5m, "NOV"),
        Offer(new DateOnly(2026, 3, 1), 20m, "MAR-EXPIRED"),
    ];

    private static DispenseLineInput Line(BatchOffer offer, decimal quantity) => new(offer.SkuId, offer.BatchId, quantity);

    [Fact]
    public void U_E1_TheFefoSuggestion_TakesTheEarliestFirst_AndNeverAnExpiredBatch()
    {
        var offers = ThreeBatches();

        var plan = DispensingBatchPlanner.Plan(8m, offers, Today);

        Assert.Equal(2, plan.SuggestedLines.Count);
        Assert.Equal(offers[1].BatchId, plan.SuggestedLines[0].BatchId);
        Assert.Equal(5m, plan.SuggestedLines[0].Quantity);
        Assert.Equal(offers[0].BatchId, plan.SuggestedLines[1].BatchId);
        Assert.Equal(3m, plan.SuggestedLines[1].Quantity);
        Assert.DoesNotContain(plan.SuggestedLines, l => l.BatchId == offers[2].BatchId);
        Assert.False(plan.OnlyExpiredStockAvailable);
        Assert.Equal(0m, plan.Shortfall);
    }

    [Fact]
    public void U_E2_OnlyExpiredStock_IsReportedAsSuch_NotSuggested()
    {
        var plan = DispensingBatchPlanner.Plan(8m, [Offer(new DateOnly(2026, 3, 1), 20m, "OLD")], Today);

        Assert.Empty(plan.SuggestedLines);
        Assert.True(plan.OnlyExpiredStockAvailable);
        Assert.NotNull(plan.ExpiredExample);
        Assert.Equal(8m, plan.Shortfall);
    }

    [Fact]
    public void U_E3_NamingAnExpiredBatch_IsBatchExpired_AndNothingCanOverrideIt()
    {
        var offers = ThreeBatches();
        var expired = offers[2];

        var violations = DispensingBatchPlanner.Validate([Line(expired, 8m)], 8m, offers, [Sku], Today, fefoOverrideReason: "The administrator insists on this lot");

        Assert.Equal(BusinessViolationCodes.BatchExpired, Assert.Single(violations).Code);

        // No parameter of any planner method is a flag, a role or an override of expiry -- the only free text is the FEFO reason.
        var parameters = typeof(DispensingBatchPlanner).GetMethods(BindingFlags.Public | BindingFlags.Static).SelectMany(m => m.GetParameters()).ToList();
        Assert.DoesNotContain(parameters, p => p.ParameterType == typeof(bool));
        Assert.DoesNotContain(parameters, p => System.Text.RegularExpressions.Regex.IsMatch(p.Name!, "role|admin|force|allowExpired|ignoreExpiry", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public void U_E4_ABatchOtherThanTheFefoChoice_NeedsAReason()
    {
        var early = Offer(new DateOnly(2026, 11, 5), 20m, "NOV");
        var late = Offer(new DateOnly(2026, 12, 10), 20m, "DEC");
        List<BatchOffer> offers = [early, late];

        var without = DispensingBatchPlanner.Validate([Line(late, 8m)], 8m, offers, [Sku], Today, null);
        Assert.Equal(BusinessViolationCodes.FefoOverrideReasonRequired, Assert.Single(without).Code);

        var tooShort = DispensingBatchPlanner.Validate([Line(late, 8m)], 8m, offers, [Sku], Today, "abc");
        Assert.Equal(BusinessViolationCodes.FefoOverrideReasonRequired, Assert.Single(tooShort).Code);

        Assert.Empty(DispensingBatchPlanner.Validate([Line(late, 8m)], 8m, offers, [Sku], Today, "Owner wants the later lot"));
        Assert.Empty(DispensingBatchPlanner.Validate([Line(early, 8m)], 8m, offers, [Sku], Today, null));
    }

    [Fact]
    public void U_E5_TheQuantityMustMatch_AtMostFiveLines_AndTheSkuMustBeLinked()
    {
        var offers = new List<BatchOffer> { Offer(new DateOnly(2026, 11, 5), 20m, "NOV") };

        var mismatch = DispensingBatchPlanner.Validate([Line(offers[0], 7m)], 8m, offers, [Sku], Today, null);
        Assert.Equal(BusinessViolationCodes.DispenseQuantityMismatch, Assert.Single(mismatch).Code);

        var six = Enumerable.Range(0, 6).Select(_ => Offer(new DateOnly(2026, 11, 5), 5m, "L")).ToList();
        var tooMany = DispensingBatchPlanner.Validate(six.Select(o => Line(o, 1m)).ToList(), 6m, six, [Sku], Today, null);
        Assert.Equal(BusinessViolationCodes.TooManyDispensingLines, Assert.Single(tooMany).Code);

        var unlinked = DispensingBatchPlanner.Validate([Line(offers[0], 8m)], 8m, offers, [Guid.NewGuid()], Today, null);
        Assert.Equal(BusinessViolationCodes.SkuNotLinkedToMedicine, Assert.Single(unlinked).Code);
    }

    [Fact]
    public void U_E6_ABatchInventoryWillNotRelease_IsNotDispensable_WithInventorysOwnReason()
    {
        var quarantined = Offer(new DateOnly(2026, 11, 5), 20m, "Q", dispensable: false, reason: "That batch is in quarantine.");

        var violation = Assert.Single(DispensingBatchPlanner.Validate([Line(quarantined, 8m)], 8m, [quarantined], [Sku], Today, null));

        Assert.Equal(BusinessViolationCodes.BatchNotDispensable, violation.Code);
        Assert.Equal("That batch is in quarantine.", violation.Message);
    }

    [Fact]
    public void U_E7_TheLabel_CarriesEveryFieldOfUsPhm05Ac1_WithTheEarliestExpiry()
    {
        var label = MedicineLabelComposer.Compose(new MedicineLabelInput(
            "Mitu", "Rahim Uddin", "Meloxicam", "1.5 mg/ml", false, new Dose(0.3m, DoseUnit.Ml), AdministrationRoute.Oral, DoseFrequency.OnceDaily, 5, "Give with food.",
            DispensingUnit.Millilitre, 10m, Today, [new DateOnly(2026, 12, 10), new DateOnly(2026, 11, 5), null], "RX-2026-000042", "Dr Karim", "Cat Clinic", "01700000000", "House 1, Dhaka"));

        Assert.Equal("Mitu", label.CatName);
        Assert.Equal("Rahim Uddin", label.OwnerName);
        Assert.Equal("Meloxicam 1.5 mg/ml", label.MedicineLine);
        Assert.Equal("Give 0.3 ml by mouth once daily for 5 days", label.DoseLine);
        Assert.Equal("Give with food.", label.Instructions);
        Assert.Equal(Today, label.DispensedOnLocal);
        Assert.Equal(new DateOnly(2026, 11, 5), label.ExpiresOn);
        Assert.Equal("Quantity: 10 ml", label.QuantityLine);
        Assert.Equal("RX-2026-000042", label.PrescriptionNumber);
        Assert.Null(label.ControlledNotice);
        Assert.False(string.IsNullOrWhiteSpace(label.StandardWarnings));
    }

    [Fact]
    public void U_E7b_AControlledMedicineLabel_CarriesTheControlledNotice_AndNoExpiryWhenNoLineHasOne()
    {
        var label = MedicineLabelComposer.Compose(new MedicineLabelInput(
            "Mitu", "Rahim", "Ketamine", "100 mg/ml", true, new Dose(1m, DoseUnit.Tablet), AdministrationRoute.Oral, DoseFrequency.TwiceDaily, 1, "x",
            DispensingUnit.Tablet, 1m, Today, [null], "RX-1", "Dr K", "Clinic", "0", null));

        Assert.Equal(LabelRules.ControlledNotice, label.ControlledNotice);
        Assert.Null(label.ExpiresOn);
        Assert.Equal("Give 1 tablet by mouth twice daily for 1 day", label.DoseLine);
        Assert.Equal("Quantity: 1 tablet", label.QuantityLine);
    }

    [Theory]
    [InlineData("Once", "once")]
    [InlineData("OnceDaily", "once daily")]
    [InlineData("TwiceDaily", "twice daily")]
    [InlineData("ThreeTimesDaily", "three times daily")]
    [InlineData("FourTimesDaily", "four times daily")]
    [InlineData("EveryOtherDay", "every other day")]
    [InlineData("Weekly", "once weekly")]
    [InlineData("AsNeeded", "as needed")]
    public void U_E8_EveryFrequencyHasItsLabelWording(string frequency, string wording)
    {
        var text = DoseFormatter.Format(new Dose(2m, DoseUnit.Ml), AdministrationRoute.Oral, Enumeration.FromDisplayName<DoseFrequency>(frequency), 7);

        Assert.Equal($"Give 2 ml by mouth {wording} for 7 days", text);
    }

    [Fact]
    public void U_E8b_EveryFrequencyIsCoveredByATheoryRow() =>
        Assert.Equal(8, Enumeration.GetAll<DoseFrequency>().Count());
}
