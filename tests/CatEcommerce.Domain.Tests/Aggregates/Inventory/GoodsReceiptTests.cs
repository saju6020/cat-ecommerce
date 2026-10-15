using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Inventory;

/// <summary>HO-IMP-001 §15 phase D -- U-D1 ... U-D6: the receipt line rules of §8.6, each with its own code, and the rule that EVERY violation is
/// returned together.</summary>
public sealed class GoodsReceiptTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Location = Guid.NewGuid();

    private static InventoryActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    private static SkuStockProfile Sku(bool expiryRequired, StockClass? stockClass = null, bool fractional = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product", null, stockClass ?? (expiryRequired ? StockClass.Food : StockClass.General), expiryRequired, "each", fractional, "DE", true);

    private static GoodsReceiptValidationContext Context(
        IEnumerable<SkuStockProfile> skus, IEnumerable<PurchaseOrderLineFact>? poLines = null, StockLocationKind? locationKind = null,
        bool hasConsignment = false, IReadOnlyDictionary<string, decimal>? rates = null) =>
        new(Today, "BDT", skus.ToDictionary(s => s.SkuId),
            new Dictionary<Guid, StockLocationFact> { [Location] = new(Location, Branch, "WH", locationKind ?? StockLocationKind.Warehouse, true) },
            (poLines ?? []).ToDictionary(l => l.LineId), hasConsignment, rates ?? new Dictionary<string, decimal>());

    private static GoodsReceiptLineDto Line(
        SkuStockProfile sku, decimal accepted = 10m, decimal rejected = 0m, DateOnly? expires = null, DateOnly? manufactured = null, string lot = "LOT-1",
        RejectionReason? reason = null, Guid? poLine = null, decimal? cost = 5m, string? currency = "BDT", decimal? rate = null) =>
        new(Guid.NewGuid(), sku.SkuId, poLine, lot, manufactured, expires, accepted, rejected, reason, null, Location, false, cost, currency, rate, Guid.NewGuid());

    private static GoodsReceiptPostDto Dto(params GoodsReceiptLineDto[] lines) =>
        new("GRN-2026-000001", Branch, null, null, null, null, DateTimeOffset.UtcNow, lines, Actor());

    private static IReadOnlyList<GoodsReceiptViolation> Violations(GoodsReceiptPostDto dto, GoodsReceiptValidationContext context) =>
        GoodsReceiptAggregate.Validate(dto, context);

    // ── U-D1 / U-D2 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D1_AFoodSkuAccepted10WithNoExpiry_IsExpiryDateRequired_OnTheLinesExpiresOnProperty()
    {
        var food = Sku(expiryRequired: true);

        var violations = Violations(Dto(Line(food)), Context([food]));

        var violation = Assert.Single(violations);
        Assert.Equal(BusinessViolationCodes.ExpiryDateRequired, violation.Code);
        Assert.Equal("Lines[0].ExpiresOn", violation.PropertyName);
        Assert.Equal("Food and medicine must be received with an expiry date.", violation.Message);
    }

    [Fact]
    public void U_D1_AMedicineStyleSkuThatTracksExpiryIsRefusedToo_ButARejectedOnlyLineNeedsNoExpiry()
    {
        var medicine = Sku(expiryRequired: true, stockClass: StockClass.Medicine);

        Assert.Contains(Violations(Dto(Line(medicine)), Context([medicine])), v => v.Code == BusinessViolationCodes.ExpiryDateRequired);
        Assert.Empty(Violations(Dto(Line(medicine, accepted: 0m, rejected: 3m, reason: RejectionReason.Damaged)), Context([medicine])));
    }

    [Fact]
    public void U_D2_AGeneralSkuWithNoExpiry_IsAccepted()
    {
        var general = Sku(expiryRequired: false);

        Assert.Empty(Violations(Dto(Line(general)), Context([general])));
    }

    // ── U-D3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D3_ExpiryYesterdayInTheBranchsLocalDate_IsBatchAlreadyExpired_AndTodayIsStillAcceptable()
    {
        var food = Sku(expiryRequired: true);

        Assert.Contains(Violations(Dto(Line(food, expires: Today.AddDays(-1))), Context([food])), v => v.Code == BusinessViolationCodes.BatchAlreadyExpired && v.PropertyName == "Lines[0].ExpiresOn");
        Assert.Empty(Violations(Dto(Line(food, expires: Today)), Context([food])));
    }

    [Fact]
    public void U_D3_ExpiredGoodsCanBeRejectedWithTheReasonExpired_ButNeverAccepted()
    {
        var food = Sku(expiryRequired: true);

        Assert.Empty(Violations(Dto(Line(food, accepted: 0m, rejected: 5m, expires: Today.AddDays(-30), reason: RejectionReason.Expired)), Context([food])));
    }

    [Fact]
    public void U_D3_ManufacturedAfterExpiry_AndManufacturedTomorrow_AreBothInvalidBatchDates()
    {
        var food = Sku(expiryRequired: true);

        var afterExpiry = Violations(Dto(Line(food, expires: Today.AddDays(10), manufactured: Today.AddDays(11))), Context([food]));
        var tomorrow = Violations(Dto(Line(food, expires: Today.AddDays(100), manufactured: Today.AddDays(1))), Context([food]));

        Assert.Contains(afterExpiry, v => v.Code == BusinessViolationCodes.InvalidBatchDates);
        Assert.Contains(tomorrow, v => v.Code == BusinessViolationCodes.InvalidBatchDates);
    }

    // ── U-D4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D4_ARejectedQuantityWithoutAReason_IsRejectionReasonRequired()
    {
        var general = Sku(expiryRequired: false);

        var violations = Violations(Dto(Line(general, accepted: 8m, rejected: 2m)), Context([general]));

        Assert.Contains(violations, v => v.Code == BusinessViolationCodes.RejectionReasonRequired && v.PropertyName == "Lines[0].RejectionReason");
    }

    [Fact]
    public void U_D4_ARejectedOnlyLine_MakesNoBatch_AndRecordsTheReasonOnTheLine()
    {
        var general = Sku(expiryRequired: false);
        var dto = Dto(Line(general, accepted: 0m, rejected: 4m, reason: RejectionReason.WrongItem, lot: ""));
        var context = Context([general]);

        var receipt = new GoodsReceiptAggregate(Guid.NewGuid());
        receipt.Post(dto, context);

        var line = Assert.Single(receipt.Lines);
        Assert.Null(line.BatchId);
        Assert.Equal(RejectionReason.WrongItem, line.RejectionReason);
        Assert.Equal(4m, line.QuantityRejected);
        Assert.Empty(receipt.Events.OfType<Events.Inventory.GoodsReceiptPostedEvent>().SelectMany(e => e.BatchIds));
    }

    [Fact]
    public void U_D4_AnAcceptedLineNeedsALot_AndALineMustReceiveOrRejectSomething()
    {
        var general = Sku(expiryRequired: false);

        Assert.Contains(Violations(Dto(Line(general, lot: "  ")), Context([general])), v => v.Code == BusinessViolationCodes.LotNumberRequired);
        Assert.Contains(Violations(Dto(Line(general, accepted: 0m, rejected: 0m)), Context([general])), v => v.Code == BusinessViolationCodes.InvalidQuantity);
    }

    // ── U-D5 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D5_AcceptedBeyondTheOutstandingQuantity_IsReceiptExceedsOutstanding_WithOneUnitOfTolerance_None()
    {
        var general = Sku(expiryRequired: false);
        var poLine = new PurchaseOrderLineFact(Guid.NewGuid(), general.SkuId, OrderedQuantity: 100m, PreviouslyAccepted: 60m, UnitCostAmount: 5m, CurrencyCode: "BDT");

        var over = Violations(Dto(Line(general, accepted: 41m, poLine: poLine.LineId)), Context([general], [poLine]));
        var exact = Violations(Dto(Line(general, accepted: 40m, poLine: poLine.LineId)), Context([general], [poLine]));

        Assert.Contains(over, v => v.Code == BusinessViolationCodes.ReceiptExceedsOutstanding);
        Assert.Empty(exact);
    }

    [Fact]
    public void U_D5_TwoLinesAgainstTheSamePoLine_AreSummedAgainstWhatIsOutstanding()
    {
        var general = Sku(expiryRequired: false);
        var poLine = new PurchaseOrderLineFact(Guid.NewGuid(), general.SkuId, 10m, 0m, 5m, "BDT");

        var violations = Violations(
            Dto(Line(general, accepted: 6m, poLine: poLine.LineId, lot: "A"), Line(general, accepted: 6m, poLine: poLine.LineId, lot: "B")), Context([general], [poLine]));

        Assert.Contains(violations, v => v.Code == BusinessViolationCodes.ReceiptExceedsOutstanding && v.PropertyName == "Lines[1].QuantityAccepted");
    }

    [Fact]
    public void U_D5_APoLineOfAnotherSkuIsAMismatch_AndTheCostComesFromThePoLine()
    {
        var general = Sku(expiryRequired: false);
        var other = Sku(expiryRequired: false);
        var poLine = new PurchaseOrderLineFact(Guid.NewGuid(), other.SkuId, 10m, 0m, 7.5m, "BDT");

        Assert.Contains(Violations(Dto(Line(general, poLine: poLine.LineId)), Context([general, other], [poLine])), v => v.Code == BusinessViolationCodes.PurchaseOrderLineMismatch);

        var matching = new PurchaseOrderLineFact(Guid.NewGuid(), general.SkuId, 10m, 0m, 7.5m, "BDT");
        var cost = GoodsReceiptAggregate.ResolveCost(Line(general, poLine: matching.LineId, cost: 1m), Context([general], [matching]));
        Assert.Equal(7.5m, cost!.Value.Amount);
    }

    // ── cost, rate, location, duplicates ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D5_WithoutAPoLineAUnitCostIsRequired_AndAForeignCurrencyNeedsARate()
    {
        var general = Sku(expiryRequired: false);

        Assert.Contains(Violations(Dto(Line(general, cost: null, currency: null)), Context([general])), v => v.Code == BusinessViolationCodes.UnitCostRequired);
        Assert.Contains(Violations(Dto(Line(general, cost: 2.5m, currency: "USD")), Context([general])), v => v.Code == BusinessViolationCodes.ExchangeRateMissing);
        Assert.Empty(Violations(Dto(Line(general, cost: 2.5m, currency: "USD", rate: 118.5m)), Context([general])));
        Assert.Empty(Violations(Dto(Line(general, cost: 2.5m, currency: "USD")), Context([general], hasConsignment: true, rates: new Dictionary<string, decimal> { ["USD"] = 118.5m })));
    }

    [Fact]
    public void U_D5_TheBaseCostIsTheCostTimesTheRate_To4dp()
    {
        var general = Sku(expiryRequired: false);

        var cost = GoodsReceiptAggregate.ResolveCost(Line(general, cost: 2.5m, currency: "USD", rate: 118.5m), Context([general]))!.Value;

        Assert.Equal(296.25m, cost.Base);
        Assert.Equal("USD", cost.Currency);
    }

    [Fact]
    public void U_D5_ALocationThatCannotReceiveOrAnUnknownOneIsRefused_AndADuplicateLotInOneReceiptIs()
    {
        var general = Sku(expiryRequired: false);
        var dispensaryOnly = Context([general], locationKind: StockLocationKind.ClinicDispensary);

        // Every kind permits Receipt (§8.3), so a location the receipt does not know is the refusal.
        var unknown = Line(general) with { LocationId = Guid.NewGuid() };
        Assert.Contains(Violations(Dto(unknown), dispensaryOnly), v => v.Code == BusinessViolationCodes.StockLocationNotFound);

        var duplicate = Violations(Dto(Line(general, lot: "SAME", expires: Today.AddDays(5)), Line(general, lot: "SAME", expires: Today.AddDays(5))), Context([general]));
        Assert.Contains(duplicate, v => v.Code == BusinessViolationCodes.DuplicateReceiptLine && v.PropertyName == "Lines[1].LotNumber");
    }

    // ── U-D6 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_D6_ReceiptWithThreeInvalidLines_ReturnsAllThreeViolationsTogether()
    {
        var food = Sku(expiryRequired: true);
        var general = Sku(expiryRequired: false);

        var violations = Violations(
            Dto(
                Line(food),                                                         // no expiry
                Line(general, accepted: 5m, rejected: 1m),                          // rejection without a reason
                Line(food, expires: Today.AddDays(-2))),                            // expired
            Context([food, general]));

        Assert.Equal(3, violations.Count);
        Assert.Contains(violations, v => v.Code == BusinessViolationCodes.ExpiryDateRequired && v.PropertyName == "Lines[0].ExpiresOn");
        Assert.Contains(violations, v => v.Code == BusinessViolationCodes.RejectionReasonRequired && v.PropertyName == "Lines[1].RejectionReason");
        Assert.Contains(violations, v => v.Code == BusinessViolationCodes.BatchAlreadyExpired && v.PropertyName == "Lines[2].ExpiresOn");
    }

    [Fact]
    public void U_D6_PostRefusesAnInvalidReceiptEvenIfTheCallerSkippedValidate()
    {
        var food = Sku(expiryRequired: true);
        var receipt = new GoodsReceiptAggregate(Guid.NewGuid());

        var ex = Assert.Throws<Domain.Exceptions.DomainRuleViolationException>(() => receipt.Post(Dto(Line(food)), Context([food])));

        Assert.Equal(BusinessViolationCodes.ExpiryDateRequired, ex.ErrorCode);
    }

    [Fact]
    public void U_D6_APostedReceiptHasNoMutatingMethod()
    {
        var methods = typeof(GoodsReceiptAggregate).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Select(m => m.Name);

        Assert.Equal(["Post"], methods.ToArray());
    }

    [Fact]
    public void U_D1_AFractionalQuantityOnAWholeOnlySkuIsRefused_AndOnAFractionalOneAccepted()
    {
        var whole = Sku(expiryRequired: false);
        var litres = Sku(expiryRequired: false, fractional: true);

        Assert.Contains(Violations(Dto(Line(whole, accepted: 2.5m)), Context([whole])), v => v.Code == BusinessViolationCodes.FractionalQuantityNotAllowed);
        Assert.Empty(Violations(Dto(Line(litres, accepted: 2.5m)), Context([litres])));
    }
}
