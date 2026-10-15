using System.Reflection;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.Surgery;

/// <summary>HO-SRG-001 §8.8 -- U-D1..U-D4 and the append-only shape (ADR-162): an entry is created once, attributed and timestamped from the context, and nothing changes it.</summary>
public sealed class AnaesthesiaLogEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Admitted = Now.AddMinutes(-30);

    private static AnaesthesiaEntryContext Context(DateTimeOffset? occurredOn = null, DateTimeOffset? admittedOn = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), admittedOn ?? Admitted, occurredOn ?? Now.AddMinutes(-5), Now, TestUserContext.Create(), Guid.NewGuid());

    private static AnaesthesiaDrugDto Drug(bool controlled = false, Guid? administeredBy = null, Guid? witness = null, decimal? wasted = 0m, Guid? registerEntry = null) => new(
        Guid.NewGuid(), "Ketamine", controlled, 0.5m, DoseUnit.Ml, AdministrationRoute.Intravenous, administeredBy ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "LOT-1", Guid.NewGuid(), 2m,
        wasted, "Vial", witness, registerEntry, false);

    private static AnaesthesiaVitalsDto Vitals(int? hr = 120, decimal? temperature = null, bool confirm = false) =>
        new(hr, null, null, null, temperature, null, null, null, confirm);

    /// <summary>U-D1 -- a drug entry records who logged it and when, from the context and the clock (US-SRG-04 AC1).</summary>
    [Fact]
    public void U_D1_ADrugEntry_RecordsLoggedBy_AndLoggedOn_FromTheContext()
    {
        var context = Context();
        var entry = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());

        entry.LogDrug(context, Drug());

        Assert.Equal(AnaesthesiaEntryKind.Drug, entry.Kind);
        Assert.Equal(context.UserContext.UserId, entry.LoggedBy);
        Assert.Equal(Now, entry.LoggedOn);
        Assert.Equal(context.OccurredOn, entry.OccurredOn);
        Assert.Equal(context.SurgeryId, entry.SurgeryId);
        var logged = Assert.Single(entry.Events.OfType<AnaesthesiaEntryLoggedEvent>());
        Assert.Equal(AnaesthesiaEntryKind.Drug, logged.Kind);
        Assert.False(logged.IsControlled);
    }

    /// <summary>U-D2 -- five minutes in the future, and an hour and a minute before the admission, are refused; the 2-minute tolerance and the 1-hour grace are inside.</summary>
    [Theory]
    [InlineData(5, false)]
    [InlineData(2, true)]
    [InlineData(-91, false)] // admitted 30 minutes ago; -91 minutes is more than an hour before that
    [InlineData(-89, true)]
    public void U_D2_TheOccurredOnWindow_IsEnforced(int minutesFromNow, bool allowed)
    {
        var entry = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        var context = Context(Now.AddMinutes(minutesFromNow));

        if (allowed)
        {
            entry.LogVitals(context, Vitals());
            Assert.Equal(AnaesthesiaEntryKind.Vitals, entry.Kind);
        }
        else
        {
            var ex = Assert.Throws<DomainRuleViolationException>(() => entry.LogVitals(context, Vitals()));
            Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry, ex.ErrorCode);
        }
    }

    /// <summary>U-D3 -- none set: VitalsRequired; heart rate 300 unconfirmed: ImplausibleVitalValue; confirmed: stored with the flag.</summary>
    [Fact]
    public void U_D3_Vitals_NeedAValue_AndAnImplausibleOneNeedsConfirmation()
    {
        var none = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.VitalsRequired, Assert.Throws<DomainRuleViolationException>(() => none.LogVitals(Context(), Vitals(hr: null))).ErrorCode);

        var unconfirmed = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => unconfirmed.LogVitals(Context(), Vitals(hr: 300)));
        Assert.Equal(BusinessViolationCodes.ImplausibleVitalValue, ex.ErrorCode);
        Assert.Contains("heart rate 300", ex.Message);

        var confirmed = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        confirmed.LogVitals(Context(), Vitals(hr: 300, confirm: true));
        Assert.Equal(300, confirmed.HeartRateBpm);
        Assert.True(confirmed.ImplausibleConfirmed);

        var plausible = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        plausible.LogVitals(Context(), Vitals(hr: 120, confirm: true));
        Assert.False(plausible.ImplausibleConfirmed); // a confirmation nobody needed is not recorded as one
    }

    [Theory]
    [InlineData(59, false)]
    [InlineData(60, true)]
    [InlineData(260, true)]
    [InlineData(261, false)]
    public void TheFelineHeartRateRange_IsSixtyToTwoSixty(int heartRate, bool plausible)
    {
        var entry = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        if (plausible) entry.LogVitals(Context(), Vitals(hr: heartRate));
        else Assert.Equal(BusinessViolationCodes.ImplausibleVitalValue, Assert.Throws<DomainRuleViolationException>(() => entry.LogVitals(Context(), Vitals(hr: heartRate))).ErrorCode);
    }

    /// <summary>U-D4 (aggregate half) -- a controlled drug needs a witness who is not the administering vet, the wastage (zero allowed) and its register entry.</summary>
    [Fact]
    public void U_D4_AControlledDrug_NeedsAWitness_WhoIsSomeoneElse_TheWastage_AndTheRegisterEntry()
    {
        var vet = Guid.NewGuid();
        Assert.Equal(BusinessViolationCodes.WitnessRequired,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug(true, vet, null, 0m, Guid.NewGuid()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.WitnessSameAsAdministrator,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug(true, vet, vet, 0m, Guid.NewGuid()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.WastageRequired,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug(true, vet, Guid.NewGuid(), null, Guid.NewGuid()))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug(true, vet, Guid.NewGuid(), 0m, null))).ErrorCode);

        var ok = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        ok.LogDrug(Context(), Drug(true, vet, Guid.NewGuid(), 0m, Guid.NewGuid()));
        Assert.True(Assert.Single(ok.Events.OfType<AnaesthesiaEntryLoggedEvent>()).IsControlled);
    }

    [Fact]
    public void ADrug_NeedsAPositiveDose_AndWastedCannotExceedDrawn()
    {
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug() with { DoseAmount = 0m })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug() with { DoseAmount = 0.0001m })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug() with { QuantityDrawn = 1m, QuantityWasted = 2m })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).LogDrug(Context(), Drug() with { QuantityDrawn = -1m })).ErrorCode);
    }

    /// <summary>US-SRG-08 AC2 -- a correction is a NEW entry linked to the one it corrects, with a reason of 10 to 500 characters and the corrected values of the corrected kind.</summary>
    [Fact]
    public void ACorrection_IsLinked_NeedsAReason_AndCarriesTheValuesOfTheCorrectedKind()
    {
        var target = Guid.NewGuid();
        var entry = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());

        entry.Correct(Context(), new AnaesthesiaCorrectionDto(target, AnaesthesiaEntryKind.Vitals, "Typed 210 instead of 120", null, Vitals(hr: 120), null));

        Assert.Equal(AnaesthesiaEntryKind.Correction, entry.Kind);
        Assert.Equal(target, entry.CorrectsEntryId);
        Assert.Equal(AnaesthesiaEntryKind.Vitals, entry.CorrectedKind);
        Assert.Equal(120, entry.HeartRateBpm);
        Assert.Equal("Typed 210 instead of 120", entry.CorrectionReason);

        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).Correct(Context(), new AnaesthesiaCorrectionDto(target, AnaesthesiaEntryKind.Vitals, "typo", null, Vitals(), null))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).Correct(Context(), new AnaesthesiaCorrectionDto(target, AnaesthesiaEntryKind.Drug, "Wrong kind of values", null, Vitals(), null))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidAnaesthesiaEntry,
            Assert.Throws<DomainRuleViolationException>(() => new AnaesthesiaLogEntryAggregate(Guid.NewGuid()).Correct(Context(), new AnaesthesiaCorrectionDto(target, AnaesthesiaEntryKind.Vitals, "No values at all", null, null, null))).ErrorCode);
    }

    /// <summary>ADR-162 -- an entry is created once: a second Log call on the same instance is refused, and the class has no public mutator beyond the four creators.</summary>
    [Fact]
    public void AnEntry_IsAppendOnly_NoSecondWrite_NoSetter_NoUpdateOrDelete()
    {
        var entry = new AnaesthesiaLogEntryAggregate(Guid.NewGuid());
        entry.LogVitals(Context(), Vitals());

        Assert.Throws<DomainRuleViolationException>(() => entry.LogVitals(Context(), Vitals(hr: 100)));

        var creators = new HashSet<string> { "LogDrug", "LogVitals", "LogMilestone", "Correct" };
        var publicMethods = typeof(AnaesthesiaLogEntryAggregate).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Select(m => m.Name).ToList();
        Assert.Equal(creators.Order().ToArray(), publicMethods.Order().ToArray());

        var publicSetters = typeof(AnaesthesiaLogEntryAggregate).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => p.SetMethod is { IsPublic: true }).Select(p => p.Name).ToList();
        Assert.True(publicSetters.Count == 0, "Public setters on the append-only entry: " + string.Join(", ", publicSetters));
    }
}
