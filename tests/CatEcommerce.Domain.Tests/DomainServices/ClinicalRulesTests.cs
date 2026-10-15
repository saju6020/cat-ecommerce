using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-EMR-001 §15 U-B7, U-D1, U-G1, U-C11 -- the pure clinical domain services.</summary>
public sealed class ClinicalRulesTests
{
    // ── VitalRangeCheck (U-B7) ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Temperature", 35.0, false)]
    [InlineData("Temperature", 41.5, false)]
    [InlineData("Weight", 0.30, false)]
    [InlineData("Weight", 12.00, false)]
    [InlineData("Temperature", 34.9, true)]
    [InlineData("Weight", 12.01, true)]
    public void VitalRangeCheck_PlausibleBoundsAreInclusive(string sign, double value, bool warns)
    {
        var vital = Enumeration.FromDisplayName<VitalSign>(sign);

        var result = VitalRangeCheck.Evaluate(new Dictionary<VitalSign, decimal> { [vital] = (decimal)value });

        Assert.Empty(result.AbsoluteViolations);
        Assert.Equal(warns, result.PlausibleWarnings.Contains(vital));
    }

    [Theory]
    [InlineData("Temperature", 24.9)]
    [InlineData("Temperature", 45.1)]
    [InlineData("Weight", 0.09)]
    [InlineData("Weight", 30.01)]
    [InlineData("HeartRate", 19)]
    [InlineData("BodyConditionScore", 10)]
    [InlineData("PainScore", 11)]
    public void VitalRangeCheck_OutsideAbsolute_IsAnAbsoluteViolationOnly(string sign, double value)
    {
        var vital = Enumeration.FromDisplayName<VitalSign>(sign);

        var result = VitalRangeCheck.Evaluate(new Dictionary<VitalSign, decimal> { [vital] = (decimal)value });

        Assert.Equal([vital], result.AbsoluteViolations);
        Assert.Empty(result.PlausibleWarnings);
    }

    [Fact]
    public void VitalRangeCheck_ScoresHaveNoConfirmTier()
    {
        var result = VitalRangeCheck.Evaluate(new Dictionary<VitalSign, decimal> { [VitalSign.BodyConditionScore] = 1, [VitalSign.PainScore] = 0 });

        Assert.True(result.IsClean);
    }

    [Fact]
    public void VitalSignRules_FormatsAtTheStatedPrecision_AndCarriesAUnit()
    {
        Assert.Equal("4.20", VitalSignRules.Format(VitalSign.Weight, 4.2m));
        Assert.Equal("38.6", VitalSignRules.Format(VitalSign.Temperature, 38.6m));
        Assert.Equal("180", VitalSignRules.Format(VitalSign.HeartRate, 180m));
        Assert.All(VitalSignRules.All, r => Assert.False(string.IsNullOrWhiteSpace(r.Unit)));
        Assert.Equal(6, VitalSignRules.All.Count);
    }

    [Fact]
    public void VitalSignRules_WeightAbsoluteRange_MatchesTheCatWeightHistory()
    {
        var weight = VitalSignRules.For(VitalSign.Weight);

        Assert.Equal(CatEcommerce.Domain.ValueObjects.Weight.MinKilograms, weight.AbsoluteMin);
        Assert.Equal(CatEcommerce.Domain.ValueObjects.Weight.MaxKilograms, weight.AbsoluteMax);
    }

    // ── ReferenceRangeClassifier (U-D1) ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(4.9, 5.0, 10.0, "Low")]
    [InlineData(5.0, 5.0, 10.0, "Normal")]
    [InlineData(10.0, 5.0, 10.0, "Normal")]
    [InlineData(10.1, 5.0, 10.0, "High")]
    public void Classifier_Numeric_BoundsAreInclusive(double value, double low, double high, string expected) =>
        Assert.Equal(expected, ReferenceRangeClassifier.ClassifyNumeric((decimal)value, (decimal)low, (decimal)high).DisplayName);

    [Fact]
    public void Classifier_Numeric_NoRange_IsNotAssessed_AndAHalfOpenRangeStillJudges()
    {
        Assert.Equal(ResultFlag.NotAssessed, ReferenceRangeClassifier.ClassifyNumeric(7m, null, null));
        Assert.Equal(ResultFlag.High, ReferenceRangeClassifier.ClassifyNumeric(7m, null, 5m));
        Assert.Equal(ResultFlag.Normal, ReferenceRangeClassifier.ClassifyNumeric(3m, null, 5m));
        Assert.Equal(ResultFlag.Low, ReferenceRangeClassifier.ClassifyNumeric(1m, 2m, null));
    }

    [Theory]
    [InlineData("negative", "Negative", "Normal")]
    [InlineData("  NEGATIVE ", "Negative", "Normal")]
    [InlineData("Positive", "Negative", "Abnormal")]
    [InlineData("Positive", null, "NotAssessed")]
    [InlineData("Positive", "  ", "NotAssessed")]
    public void Classifier_Qualitative_ComparesTrimmedIgnoringCase(string value, string? normal, string expected) =>
        Assert.Equal(expected, ReferenceRangeClassifier.ClassifyQualitative(value, normal).DisplayName);

    // ── FollowUpSchedule (U-G1) ────────────────────────────────────────────────────────────────

    [Fact]
    public void FollowUpSchedule_SendAfter_Is0900BranchLocalOnDueMinusLead()
    {
        var now = new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

        var sendAfter = FollowUpSchedule.SendAfter(new DateOnly(2026, 10, 20), 3, now, "Asia/Dhaka");

        Assert.Equal(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.FromHours(6)), sendAfter);
    }

    [Fact]
    public void FollowUpSchedule_SendAfter_IfAlreadyPast_IsNow()
    {
        var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(now, FollowUpSchedule.SendAfter(new DateOnly(2026, 10, 20), 3, now, "Asia/Dhaka"));
        // Due tomorrow with a 3-day lead: long past, so now.
        var tomorrow = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(tomorrow, FollowUpSchedule.SendAfter(new DateOnly(2026, 10, 20), 3, tomorrow, "Asia/Dhaka"));
    }

    [Fact]
    public void FollowUpSchedule_NotAfter_IsTheEndOfTheDueDateLocal()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.FromHours(6)), FollowUpSchedule.NotAfter(new DateOnly(2026, 10, 20), "Asia/Dhaka"));
    }

    // ── VisitAllowedActions (U-C11) ────────────────────────────────────────────────────────────

    [Fact]
    public void AllowedActions_Nurse_VitalsOnlyAndOnlyWhileOpen()
    {
        Assert.Equal([VisitAllowedActions.RecordVitals], VisitAllowedActions.For(VisitStatus.Open, [StaffRoles.Nurse], AppointmentStatus.InConsultation));
        Assert.Empty(VisitAllowedActions.For(VisitStatus.Finalised, [StaffRoles.Nurse], AppointmentStatus.InConsultation));
    }

    [Fact]
    public void AllowedActions_Vet_EverythingWhileOpen_AndAnAddendumOnceFinalised()
    {
        var open = VisitAllowedActions.For(VisitStatus.Open, [StaffRoles.Vet], AppointmentStatus.InConsultation);

        Assert.Equal(
            [VisitAllowedActions.RecordVitals, VisitAllowedActions.EditNote, VisitAllowedActions.RecordTreatment, VisitAllowedActions.OrderDiagnostic, VisitAllowedActions.AttachPhoto,
             VisitAllowedActions.SetFollowUp, VisitAllowedActions.Finalise],
            open);
        Assert.Equal([VisitAllowedActions.AddAddendum], VisitAllowedActions.For(VisitStatus.Finalised, [StaffRoles.Vet], AppointmentStatus.InConsultation));
    }

    [Fact]
    public void AllowedActions_Administrator_AndReception_HaveNone()
    {
        Assert.Empty(VisitAllowedActions.For(VisitStatus.Open, [StaffRoles.Administrator], AppointmentStatus.InConsultation));
        Assert.Empty(VisitAllowedActions.For(VisitStatus.Finalised, [StaffRoles.Administrator], AppointmentStatus.Completed));
        Assert.Empty(VisitAllowedActions.For(VisitStatus.Open, [StaffRoles.Reception], AppointmentStatus.CheckedIn));
    }

    [Fact]
    public void AllowedActions_ACancelledAppointmentsVisit_CannotBeWorkedOn()
    {
        Assert.Empty(VisitAllowedActions.For(VisitStatus.Open, [StaffRoles.Vet], AppointmentStatus.Cancelled));
    }
}
