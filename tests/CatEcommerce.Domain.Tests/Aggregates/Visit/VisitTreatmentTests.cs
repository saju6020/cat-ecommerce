using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Visit;

/// <summary>Builders shared by the visit tests: a treatment as the handler would resolve it.</summary>
internal static class TreatmentTestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    public static TreatmentDto Consumable(
        decimal quantity = 2m, decimal? deducted = null, bool billable = false, Money? price = null, Guid? taxClassId = null, Guid? lineId = null) =>
        new(lineId ?? Guid.NewGuid(), TreatmentKind.Consumable, "Syringe 2 ml", null, Guid.NewGuid(), "Syringe", "each",
            Guid.NewGuid(), "LOT-1", new DateOnly(2027, 1, 1), Guid.NewGuid(),
            quantity, deducted ?? quantity, quantity - (deducted ?? quantity), null, null, billable, price, taxClassId, null,
            Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

    public static TreatmentDto Procedure(bool billable = false, Money? price = null, Guid? taxClassId = null, Guid? serviceId = null) =>
        new(Guid.NewGuid(), TreatmentKind.Procedure, "Wound dressing", serviceId, null, null, null, null, null, null, null,
            1m, 0m, 0m, null, null, billable, price, taxClassId, null, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());

    public static TreatmentReversalDto Reversal(Guid lineId, string reason = "Wrong item") =>
        new(lineId, reason, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid());
}

/// <summary>HO-EMR-001 section 8.6 / 15 U-F5, U-F6, U-F8, U-F9 at aggregate level -- the visit's own treatment rules (the stock decisions belong to
/// the handler and are tested in the Application tests).</summary>
public sealed class VisitTreatmentTests
{
    private static VisitAggregate OpenVisit()
    {
        var visit = new VisitAggregate(Guid.NewGuid());
        visit.Open(new VisitOpeningDto(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), AppointmentStatus.CheckedIn, CatLifeStatus.Alive,
            Guid.NewGuid(), "Consultation", ServiceCategory.Consultation, TreatmentTestData.Now, TestUserContext.Create(), Guid.NewGuid()));
        return visit;
    }

    private static DomainRuleViolationException Violation(Action action) => Assert.Throws<DomainRuleViolationException>(action);

    [Fact]
    public void RecordTreatment_StoresTheLine_WithTheBatchAndQuantities_AndRaisesTheEvent()
    {
        var visit = OpenVisit();
        var dto = TreatmentTestData.Consumable(quantity: 3m);

        var line = visit.RecordTreatment(dto);

        Assert.Same(line, Assert.Single(visit.Treatments));
        Assert.Equal(dto.BatchId, line.BatchId);
        Assert.Equal("LOT-1", line.LotNumber);
        Assert.Equal(3m, line.DeductedQuantity);
        Assert.Equal(0m, line.ShortfallQuantity);
        Assert.Equal(TreatmentLineStatus.Recorded, line.Status);
        var recorded = Assert.Single(visit.Events.OfType<TreatmentRecordedEvent>());
        Assert.Equal(line.Id, recorded.LineId);
        Assert.Empty(visit.Events.OfType<ClinicalStockShortfallRecordedEvent>());
    }

    [Fact]
    public void RecordTreatment_WithAShortfall_RaisesTheShortfallEvent() // U-F5
    {
        var visit = OpenVisit();

        var line = visit.RecordTreatment(TreatmentTestData.Consumable(quantity: 5m, deducted: 3m));

        Assert.Equal(3m, line.DeductedQuantity);
        Assert.Equal(2m, line.ShortfallQuantity);
        var shortfall = Assert.Single(visit.Events.OfType<ClinicalStockShortfallRecordedEvent>());
        Assert.Equal(2m, shortfall.ShortfallQuantity);
        Assert.Equal(3m, shortfall.DeductedQuantity);
        Assert.Equal(visit.BranchId, shortfall.BranchId);
    }

    [Fact]
    public void RecordTreatment_NothingDeducted_WholeQuantityShort_IsAllowed_WithNoBatch() // U-F6
    {
        var visit = OpenVisit();
        var dto = TreatmentTestData.Consumable(quantity: 2m, deducted: 0m) with { BatchId = null, LotNumber = null, BatchExpiresOn = null };

        var line = visit.RecordTreatment(dto);

        Assert.Equal(0m, line.DeductedQuantity);
        Assert.Equal(2m, line.ShortfallQuantity);
        Assert.Null(line.BatchId);
        Assert.Single(visit.Events.OfType<ClinicalStockShortfallRecordedEvent>());
    }

    [Fact]
    public void RecordTreatment_QuantitiesThatDoNotAddUp_AreRefused()
    {
        var visit = OpenVisit();
        var dto = TreatmentTestData.Consumable(quantity: 5m, deducted: 3m) with { ShortfallQuantity = 1m };

        Assert.Equal(BusinessViolationCodes.InvalidQuantity, Violation(() => visit.RecordTreatment(dto)).ErrorCode);
        Assert.Empty(visit.Treatments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RecordTreatment_NonPositiveQuantity_IsRefused(int quantity)
    {
        var visit = OpenVisit();
        var dto = TreatmentTestData.Consumable() with { Quantity = quantity, DeductedQuantity = quantity };

        Assert.Equal(BusinessViolationCodes.InvalidQuantity, Violation(() => visit.RecordTreatment(dto)).ErrorCode);
    }

    [Fact]
    public void RecordTreatment_AProcedure_TakesNoStock()
    {
        var visit = OpenVisit();

        Assert.Equal(BusinessViolationCodes.InvalidQuantity,
            Violation(() => visit.RecordTreatment(TreatmentTestData.Procedure() with { SkuId = Guid.NewGuid() })).ErrorCode);

        var line = visit.RecordTreatment(TreatmentTestData.Procedure());
        Assert.Equal(TreatmentKind.Procedure, line.Kind);
        Assert.Null(line.BatchId);
    }

    [Fact]
    public void RecordTreatment_AStockItemWithoutASku_IsRefused()
    {
        var visit = OpenVisit();

        Assert.Equal(BusinessViolationCodes.SkuNotFound,
            Violation(() => visit.RecordTreatment(TreatmentTestData.Consumable() with { SkuId = null })).ErrorCode);
    }

    [Fact]
    public void RecordTreatment_Billable_SnapshotsThePriceAndTaxClass() // U-F9
    {
        var visit = OpenVisit();
        var tax = Guid.NewGuid();

        var line = visit.RecordTreatment(TreatmentTestData.Consumable(billable: true, price: new Money(120.50m, "BDT"), taxClassId: tax));

        Assert.True(line.IsBillable);
        Assert.Equal(new Money(120.50m, "BDT"), line.UnitPrice);
        Assert.Equal(tax, line.TaxClassId);
    }

    [Fact]
    public void RecordTreatment_NotBillable_KeepsNoPriceSnapshot()
    {
        var visit = OpenVisit();

        var line = visit.RecordTreatment(TreatmentTestData.Consumable(billable: false, price: new Money(10m, "BDT"), taxClassId: Guid.NewGuid()));

        Assert.False(line.IsBillable);
        Assert.Null(line.UnitPrice);
        Assert.Null(line.TaxClassId);
    }

    [Fact]
    public void RecordTreatment_BillableWithoutAPrice_IsRefused_NamingWhatIsMissing()
    {
        var visit = OpenVisit();

        Assert.Equal(BusinessViolationCodes.SkuNotPriced, Violation(() => visit.RecordTreatment(TreatmentTestData.Consumable(billable: true))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.ServiceNotPriced, Violation(() => visit.RecordTreatment(TreatmentTestData.Procedure(billable: true))).ErrorCode);
    }

    [Fact]
    public void RecordTreatment_TheHundredAndFirstLine_IsRefused()
    {
        var visit = OpenVisit();
        for (var i = 0; i < ClinicalNoteRules.MaxTreatmentLines; i++) visit.RecordTreatment(TreatmentTestData.Procedure());

        Assert.Equal(BusinessViolationCodes.ClinicalLimitExceeded, Violation(() => visit.RecordTreatment(TreatmentTestData.Procedure())).ErrorCode);
    }

    [Fact]
    public void RecordTreatment_ALineIdUsedTwice_IsRefused()
    {
        var visit = OpenVisit();
        var dto = TreatmentTestData.Procedure();
        visit.RecordTreatment(dto);

        Assert.Equal(BusinessViolationCodes.TreatmentLineAlreadyRecorded, Violation(() => visit.RecordTreatment(dto)).ErrorCode);
    }

    [Fact]
    public void RecordTreatment_ABlankOrOverlongDescription_IsRefused()
    {
        var visit = OpenVisit();

        Assert.Equal(BusinessViolationCodes.TreatmentDescriptionRequired,
            Violation(() => visit.RecordTreatment(TreatmentTestData.Procedure() with { Description = "  " })).ErrorCode);
        Assert.Equal(BusinessViolationCodes.ClinicalTextTooLong,
            Violation(() => visit.RecordTreatment(TreatmentTestData.Procedure() with { Description = new string('x', 301) })).ErrorCode);
    }

    [Fact]
    public void ReverseTreatment_MarksTheLineReversed_NeverRemovesIt() // U-F8
    {
        var visit = OpenVisit();
        var line = visit.RecordTreatment(TreatmentTestData.Consumable());

        visit.ReverseTreatment(TreatmentTestData.Reversal(line.Id));

        Assert.Same(line, Assert.Single(visit.Treatments));
        Assert.Equal(TreatmentLineStatus.Reversed, line.Status);
        Assert.Equal("Wrong item", line.ReversalReason);
        Assert.True(Assert.Single(visit.Events.OfType<TreatmentReversedEvent>()).StockReturned);
    }

    [Fact]
    public void ReverseTreatment_OfAProcedure_ReturnsNoStock()
    {
        var visit = OpenVisit();
        var line = visit.RecordTreatment(TreatmentTestData.Procedure());

        visit.ReverseTreatment(TreatmentTestData.Reversal(line.Id));

        Assert.False(Assert.Single(visit.Events.OfType<TreatmentReversedEvent>()).StockReturned);
    }

    [Fact]
    public void ReverseTreatment_Twice_AnUnknownLine_AndABlankReason_AreRefused() // U-F8
    {
        var visit = OpenVisit();
        var line = visit.RecordTreatment(TreatmentTestData.Consumable());

        Assert.Equal(BusinessViolationCodes.ReversalReasonRequired, Violation(() => visit.ReverseTreatment(TreatmentTestData.Reversal(line.Id, " "))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.TreatmentLineNotFound, Violation(() => visit.ReverseTreatment(TreatmentTestData.Reversal(Guid.NewGuid()))).ErrorCode);

        visit.ReverseTreatment(TreatmentTestData.Reversal(line.Id));
        Assert.Equal(BusinessViolationCodes.TreatmentAlreadyReversed, Violation(() => visit.ReverseTreatment(TreatmentTestData.Reversal(line.Id))).ErrorCode);
    }

    [Fact]
    public void RecordTreatment_AQuantityBeyondTheColumn_IsRefused_ForAProcedureToo() // SEC 48
    {
        var visit = OpenVisit();

        var tooMany = TreatmentTestData.Procedure() with { Quantity = ClinicalNoteRules.MaxTreatmentQuantity + 1m };
        Assert.Equal(BusinessViolationCodes.InvalidQuantity, Violation(() => visit.RecordTreatment(tooMany)).ErrorCode);

        var most = TreatmentTestData.Procedure() with { Quantity = ClinicalNoteRules.MaxTreatmentQuantity };
        Assert.Equal(ClinicalNoteRules.MaxTreatmentQuantity, visit.RecordTreatment(most).Quantity);
    }
}
