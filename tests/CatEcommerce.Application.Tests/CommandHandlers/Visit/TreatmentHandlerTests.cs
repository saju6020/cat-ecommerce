using CatEcommerce.Application.CommandHandlers.Visit;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.Application.Tests.CommandHandlers.Visit;

/// <summary>HO-EMR-001 section 15 Phase F -- U-F1 ... U-F9: the record-treatment and reverse-treatment handlers against a scripted ledger. The
/// ledger is a hand-written fake with a shelf in memory (no mocking library is in scope); what is asserted is the decision the handler makes and
/// the calls it makes to the published port, never Inventory's own rules (those are HO-IMP-001's tests).</summary>
public sealed class TreatmentHandlerTests
{
    private static readonly DateTimeOffset Now = ClinicalTestData.Now;
    private static readonly DateOnly Today = new(2026, 10, 7);

    private sealed class World
    {
        public World()
        {
            Ledger = new ScriptedStockLedger(Today);
            Cat = ClinicalTestData.Cat();
            Cats.Seed(Cat);
            var appointment = ClinicalTestData.CheckedInAppointment(Branch, Cat);
            Visit = ClinicalTestData.OpenVisit(Branch, appointment.Id, Cat.Id);
            Visits.Seed(Visit);
            FirstSku = Skus.Add();
        }

        public Guid Branch { get; } = Guid.NewGuid();
        public CatAggregate Cat { get; }
        public VisitAggregate Visit { get; }
        public ScriptedStockLedger Ledger { get; }
        public FakeAggregateRootRepository<VisitAggregate> Visits { get; } = new();
        public FakeAggregateRootRepository<CatAggregate> Cats { get; } = new();
        public FakeClinicalSkuReader Skus { get; } = new();
        public FakeSkuPriceReader SkuPrices { get; } = new();
        public FakeServicePricing ServicePricing { get; } = new();
        public FakeTimeProvider Clock { get; } = new(Now);

        public RecordTreatmentCommandHandler Handler() =>
            new(Visits, Cats, Ledger, new DispensaryBatchPicker(Ledger), Skus, SkuPrices, ServicePricing, Clock);

        public ReverseTreatmentLineCommandHandler ReverseHandler() => new(Visits, Cats, Ledger, Clock);

        public ClinicalSku NewSku(string name = "Meloxicam", bool controlled = false, bool fractional = false) => Skus.Add(name, controlled, fractional: fractional);

        public RecordTreatmentCommand Record(
            ClinicalSku sku, decimal quantity, Guid? batchId = null, bool acceptShortfall = false, string? overrideReason = null, bool billable = false,
            string kind = "Consumable", string? description = null) => new()
        {
            VisitId = Visit.Id, LineId = Guid.NewGuid(), BranchId = Branch, Kind = kind, Description = description, SkuId = sku.SkuId, BatchId = batchId,
            Quantity = quantity, IsBillable = billable, AcceptShortfall = acceptShortfall, BatchOverrideReason = overrideReason,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };

        public ClinicalSku FirstSku { get; }
    }

    private static string Code(Platform.Infrastructure.Core.Commands.CommandResponse response) => response.ValidationResult!.Errors.Single().ErrorCode;

    private static World WorldWithOneSku(out ClinicalSku sku)
    {
        var world = new World();
        sku = world.FirstSku;
        return world;
    }

    // ── U-F1 FEFO default ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_WithNoBatchGiven_UsesTheFefoFirstBatch() // U-F1
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);
        var early = world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 2));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<RecordTreatmentResult>(response.Result);
        Assert.Equal(early.BatchId, result.Batch!.Id);
        Assert.Equal("EARLY", result.Batch.LotNumber);
        Assert.Equal(2m, result.DeductedQuantity);
        Assert.Equal(0m, result.ShortfallQuantity);

        var (demand, reference, party) = Assert.Single(world.Ledger.Deducted);
        Assert.Equal(MovementKind.ClinicalUse, demand.Purpose);
        Assert.Equal(2m, demand.Quantity);
        Assert.Null(demand.OverrideReason);
        var line = Assert.Single(demand.ExplicitLines!);
        Assert.Equal(early.BatchId, line.BatchId);
        Assert.Equal(StockReferenceKind.Visit, reference.ReferenceKind);
        Assert.Equal(world.Visit.Id, reference.ReferenceId);
        Assert.Equal(result.LineId, reference.ReferenceLineId);
        Assert.Equal(world.Cat.Id, party.CatId);
        Assert.Equal(world.Cat.OwnerId, party.CustomerId);

        Assert.Same(world.Visit, Assert.Single(world.Visits.Updated));
        var stored = Assert.Single(world.Visit.Treatments);
        Assert.Equal(early.BatchId, stored.BatchId);
        Assert.Equal("EARLY", stored.LotNumber);
        Assert.Equal(sku.Name, stored.Description);
        Assert.Equal(sku.Name, stored.SkuName);
        Assert.Empty(world.Ledger.Shortfalls);
    }

    [Fact]
    public async Task Record_TheFefoChoice_IgnoresABatchInventoryWouldNotUse()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "RECALLED", new DateOnly(2026, 12, 1), 10, status: BatchStatus.Recalled);
        world.Ledger.Stock(sku.SkuId, "EXPIRED", new DateOnly(2026, 9, 1), 10);
        var good = world.Ledger.Stock(sku.SkuId, "GOOD", new DateOnly(2027, 3, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1));

        Assert.Equal(good.BatchId, Assert.IsType<RecordTreatmentResult>(response.Result).Batch!.Id);
    }

    // ── U-F2 BR-4 override ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_ANonFefoBatch_WithoutAReason_IsRefused() // U-F2
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 10);
        var late = world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: late.BatchId));

        Assert.Equal(BusinessViolationCodes.BatchOverrideReasonRequired, Code(response));
        Assert.Empty(world.Ledger.Deducted);
        Assert.Empty(world.Visits.Updated);
        Assert.Empty(world.Visit.Treatments);
    }

    [Fact]
    public async Task Record_ANonFefoBatch_WithAReason_IsRecorded_AndTheReasonIsKept() // U-F2
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 10);
        var late = world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: late.BatchId, overrideReason: "Early lot is a different strength"));

        Assert.True(response.IsSuccess);
        Assert.Equal("Early lot is a different strength", Assert.Single(world.Ledger.Deducted).Demand.OverrideReason);
        Assert.Equal("Early lot is a different strength", Assert.Single(world.Visit.Treatments).BatchOverrideReason);
        Assert.Equal(late.BatchId, Assert.Single(world.Visit.Treatments).BatchId);
    }

    [Fact]
    public async Task Record_TheFefoBatchNamedExplicitly_NeedsNoReason()
    {
        var world = WorldWithOneSku(out var sku);
        var early = world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 10);
        world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: early.BatchId));

        Assert.True(response.IsSuccess);
        Assert.Null(world.Visit.Treatments[0].BatchOverrideReason);
    }

    [Fact]
    public async Task Record_ABatchThatIsNotInTheDispensary_IsNotIssuable()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.BatchNotIssuable, Code(response));
    }

    // ── U-F3 BR-3 expired: no override ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("The owner is waiting and it is only one day out of date")]
    public async Task Record_AnExpiredBatch_IsRefused_WithAndWithoutAReason(string? reason) // U-F3
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "FRESH", new DateOnly(2027, 1, 1), 10);
        var expired = world.Ledger.Stock(sku.SkuId, "OLD", new DateOnly(2026, 10, 6), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: expired.BatchId, overrideReason: reason));

        Assert.Equal(BusinessViolationCodes.BatchExpired, Code(response));
        Assert.Contains("OLD", response.ValidationResult!.Errors.Single().ErrorMessage);
        Assert.Empty(world.Ledger.Deducted);
        Assert.Empty(world.Visits.Updated);
    }

    [Fact]
    public async Task Record_ABatchInventoryHasQuarantined_IsNotIssuable()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "FRESH", new DateOnly(2027, 1, 1), 10);
        var held = world.Ledger.Stock(sku.SkuId, "HELD", new DateOnly(2027, 2, 1), 10, status: BatchStatus.Quarantined);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: held.BatchId, overrideReason: "needed urgently"));

        Assert.Equal(BusinessViolationCodes.BatchNotIssuable, Code(response));
    }

    // ── U-F4 / U-F5 / U-F6 shortfall ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_MoreThanAvailable_WithoutAcceptShortfall_ReportsTheAvailableQuantityAndUnit() // U-F4
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 3);

        var response = await world.Handler().HandleAsync(world.Record(sku, 5));

        Assert.Equal(BusinessViolationCodes.InsufficientStock, Code(response));
        var detail = Assert.IsType<InsufficientStockDetail>(response.Result);
        Assert.Equal(3m, detail.AvailableQuantity);
        Assert.Equal("each", detail.Unit);
        Assert.Contains("Only 3 each of Meloxicam are in the dispensary", response.ValidationResult!.Errors.Single().ErrorMessage);
        Assert.Empty(world.Ledger.Deducted);
        Assert.Empty(world.Ledger.Shortfalls);
        Assert.Empty(world.Visits.Updated);
        Assert.Empty(world.Visit.Treatments);
    }

    [Fact]
    public async Task Record_MoreThanAvailable_WithAcceptShortfall_DeductsWhatIsThere_AndRecordsTheRest() // U-F5
    {
        var world = WorldWithOneSku(out var sku);
        var batch = world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 3);

        var response = await world.Handler().HandleAsync(world.Record(sku, 5, acceptShortfall: true));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<RecordTreatmentResult>(response.Result);
        Assert.Equal(3m, result.DeductedQuantity);
        Assert.Equal(2m, result.ShortfallQuantity);
        Assert.Equal(batch.BatchId, result.Batch!.Id);

        var (demand, _, _) = Assert.Single(world.Ledger.Deducted);
        Assert.Equal(3m, demand.Quantity);

        var shortfall = Assert.Single(world.Ledger.Shortfalls);
        Assert.Equal(sku.SkuId, shortfall.SkuId);
        Assert.Equal(2m, shortfall.Quantity);
        Assert.Equal(world.Ledger.DispensaryLocationId, shortfall.LocationId);
        Assert.Equal(world.Visit.Id, shortfall.ReferenceId);
        Assert.Equal(result.LineId, shortfall.ReferenceLineId);

        // The deduction is enlisted before the shortfall, and both before the single save.
        Assert.Equal([nameof(IStockLedger.DeductAsync), nameof(IStockLedger.RecordShortfall)], world.Ledger.Calls.Where(c => c is nameof(IStockLedger.DeductAsync) or nameof(IStockLedger.RecordShortfall)));
        Assert.Single(world.Visits.Updated);

        var raised = Assert.Single(world.Visit.Events.OfType<ClinicalStockShortfallRecordedEvent>());
        Assert.Equal(2m, raised.ShortfallQuantity);
        Assert.Equal(3m, raised.DeductedQuantity);
    }

    [Fact]
    public async Task Record_NothingAvailable_WithAcceptShortfall_DeductsNothing_AndRecordsTheShortfall() // U-F6
    {
        var world = WorldWithOneSku(out var sku);

        var response = await world.Handler().HandleAsync(world.Record(sku, 2, acceptShortfall: true));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<RecordTreatmentResult>(response.Result);
        Assert.Equal(0m, result.DeductedQuantity);
        Assert.Equal(2m, result.ShortfallQuantity);
        Assert.Null(result.Batch);

        Assert.Empty(world.Ledger.Deducted);
        Assert.DoesNotContain(nameof(IStockLedger.DeductAsync), world.Ledger.Calls);
        var shortfall = Assert.Single(world.Ledger.Shortfalls);
        Assert.Equal(2m, shortfall.Quantity);
        Assert.Equal(world.Ledger.DispensaryLocationId, shortfall.LocationId);
        Assert.Single(world.Visit.Events.OfType<ClinicalStockShortfallRecordedEvent>());
        Assert.Null(world.Visit.Treatments[0].BatchId);
    }

    [Fact]
    public async Task Record_NothingAvailable_WithoutAcceptShortfall_ReportsZero()
    {
        var world = WorldWithOneSku(out var sku);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1));

        Assert.Equal(0m, Assert.IsType<InsufficientStockDetail>(response.Result).AvailableQuantity);
    }

    [Fact]
    public async Task Record_ReservedUnits_AreNotAvailable()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), onHand: 5, reserved: 4);

        var response = await world.Handler().HandleAsync(world.Record(sku, 2));

        Assert.Equal(1m, Assert.IsType<InsufficientStockDetail>(response.Result).AvailableQuantity);
    }

    [Fact]
    public async Task Record_WhenTheLedgerLosesTheRace_IsInsufficientStockWithoutDetail_AndNothingIsSaved() // IMP-5
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 5);
        world.Ledger.DeductFailure = Domain_Failure(BusinessViolationCodes.InsufficientStock);

        var response = await world.Handler().HandleAsync(world.Record(sku, 2));

        Assert.Equal(BusinessViolationCodes.InsufficientStock, Code(response));
        Assert.Null(response.Result);
        Assert.Empty(world.Visits.Updated);
        Assert.Empty(world.Ledger.Shortfalls);
    }

    private static CatEcommerce.Domain.Models.StockResult<IReadOnlyList<CatEcommerce.Domain.Models.StockMovementLine>> Domain_Failure(string code) =>
        CatEcommerce.Domain.Models.StockResult<IReadOnlyList<CatEcommerce.Domain.Models.StockMovementLine>>.Failure(code, "lost the race", "Quantity");

    // ── U-F7 controlled drug ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_AControlledSku_IsRefused_BeforeAnyStockIsRead() // U-F7
    {
        var world = new World();
        var controlled = world.NewSku("Ketamine", controlled: true);
        world.Ledger.Stock(controlled.SkuId, "K1", new DateOnly(2027, 1, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(controlled, 1, kind: "Medicine"));

        Assert.Equal(BusinessViolationCodes.ControlledDrugNotSupportedHere, Code(response));
        Assert.Empty(world.Ledger.Calls);
        Assert.Empty(world.Visits.Updated);
    }

    // ── U-F8 reversal ──────────────────────────────────────────────────────────────────────────

    private static ReverseTreatmentLineCommand Reverse(World world, Guid lineId, string reason = "Wrong item chosen") => new()
    {
        VisitId = world.Visit.Id, LineId = lineId, BranchId = world.Branch, Reason = reason,
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public async Task Reverse_ALineWithADeduction_ReturnsTheStock_AndMarksTheLine() // U-F8
    {
        var world = WorldWithOneSku(out var sku);
        var batch = world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        var recorded = (RecordTreatmentResult)(await world.Handler().HandleAsync(world.Record(sku, 3))).Result;
        world.Visits.Updated.Clear();

        var response = await world.ReverseHandler().HandleAsync(Reverse(world, recorded.LineId));

        Assert.True(response.IsSuccess);
        var (stockReturn, reference, party) = Assert.Single(world.Ledger.Returned);
        Assert.Equal(batch.BatchId, stockReturn.BatchId);
        Assert.Equal(3m, stockReturn.Quantity);
        Assert.Equal(sku.SkuId, stockReturn.SkuId);
        Assert.Equal(world.Branch, stockReturn.BranchId);
        Assert.Equal(StockReferenceKind.Visit, reference.ReferenceKind);
        Assert.Equal(recorded.LineId, reference.ReferenceLineId);
        Assert.Equal(world.Cat.Id, party.CatId);

        var line = Assert.Single(world.Visit.Treatments);
        Assert.Equal(TreatmentLineStatus.Reversed, line.Status);
        Assert.Equal("Wrong item chosen", line.ReversalReason);
        Assert.Single(world.Visits.Updated);
        Assert.True(Assert.Single(world.Visit.Events.OfType<TreatmentReversedEvent>()).StockReturned);
    }

    [Fact]
    public async Task Reverse_Twice_IsRefused_AndReturnsNothingTheSecondTime() // U-F8
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        var recorded = (RecordTreatmentResult)(await world.Handler().HandleAsync(world.Record(sku, 3))).Result;
        await world.ReverseHandler().HandleAsync(Reverse(world, recorded.LineId));

        var second = await world.ReverseHandler().HandleAsync(Reverse(world, recorded.LineId));

        Assert.Equal(BusinessViolationCodes.TreatmentAlreadyReversed, Code(second));
        Assert.Single(world.Ledger.Returned);
    }

    [Fact]
    public async Task Reverse_AnUnknownLine_AndABlankReason_AreRefused()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        var recorded = (RecordTreatmentResult)(await world.Handler().HandleAsync(world.Record(sku, 1))).Result;

        Assert.Equal(BusinessViolationCodes.TreatmentLineNotFound, Code(await world.ReverseHandler().HandleAsync(Reverse(world, Guid.NewGuid()))));
        Assert.Equal(BusinessViolationCodes.ReversalReasonRequired, Code(await world.ReverseHandler().HandleAsync(Reverse(world, recorded.LineId, " "))));
        Assert.Empty(world.Ledger.Returned);
    }

    [Fact]
    public async Task Reverse_AProcedure_ReturnsNoStock()
    {
        var world = new World();
        var record = new RecordTreatmentCommand
        {
            VisitId = world.Visit.Id, LineId = Guid.NewGuid(), BranchId = world.Branch, Kind = "Procedure", Description = "Wound dressing", Quantity = 1,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };
        await world.Handler().HandleAsync(record);

        var response = await world.ReverseHandler().HandleAsync(Reverse(world, record.LineId));

        Assert.True(response.IsSuccess);
        Assert.Empty(world.Ledger.Returned);
    }

    [Fact]
    public async Task Reverse_OnAFinalisedVisit_IsRefused()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        var recorded = (RecordTreatmentResult)(await world.Handler().HandleAsync(world.Record(sku, 1))).Result;
        FinaliseTheVisit(world);

        var response = await world.ReverseHandler().HandleAsync(Reverse(world, recorded.LineId));

        Assert.Equal(BusinessViolationCodes.VisitFinalised, Code(response));
        Assert.Empty(world.Ledger.Returned);
    }

    private static void FinaliseTheVisit(World world)
    {
        world.Visit.SaveNote(new CatEcommerce.Domain.Models.SoapNoteDto(
            "S", "O", "A", "P", "Rest.",
            [new CatEcommerce.Domain.Models.ProblemDto(Guid.NewGuid(), Guid.NewGuid(), "NAD", "No abnormality", true, ProblemCertainty.Confirmed, null)],
            null, null, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        world.Visit.Finalise(new CatEcommerce.Domain.Models.VisitFinalisationDto([StaffRoles.Vet], Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid(), Guid.NewGuid()));
    }

    // ── U-F9 price snapshots ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_ABillableService_SnapshotsThePriceAtNow() // U-F9
    {
        var world = new World();
        var serviceId = Guid.NewGuid();
        var taxClass = Guid.NewGuid();
        world.ServicePricing.Services[serviceId] = new ServicePriceInfo(serviceId, "Dental scale", true, new Money(1500m, "BDT"), taxClass);
        var command = new RecordTreatmentCommand
        {
            VisitId = world.Visit.Id, LineId = Guid.NewGuid(), BranchId = world.Branch, Kind = "Procedure", Description = "Scale and polish", ServiceId = serviceId,
            Quantity = 1, IsBillable = true, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };

        var response = await world.Handler().HandleAsync(command);

        Assert.True(response.IsSuccess);
        var line = Assert.Single(world.Visit.Treatments);
        Assert.Equal(new Money(1500m, "BDT"), line.UnitPrice);
        Assert.Equal(taxClass, line.TaxClassId);
        Assert.Equal(serviceId, line.ClinicServiceId);
        Assert.Equal(Now, Assert.Single(world.ServicePricing.Asked).At);
        Assert.Empty(world.Ledger.Calls);
    }

    [Fact]
    public async Task Record_ABillableSku_SnapshotsThePriceAndTaxClassAtNow() // U-F9
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        world.SkuPrices.Prices[sku.SkuId] = new Money(75.50m, "BDT");

        var response = await world.Handler().HandleAsync(world.Record(sku, 2, billable: true));

        Assert.True(response.IsSuccess);
        var line = Assert.Single(world.Visit.Treatments);
        Assert.True(line.IsBillable);
        Assert.Equal(new Money(75.50m, "BDT"), line.UnitPrice);
        Assert.Equal(sku.TaxClassId, line.TaxClassId);
        Assert.Equal(Now, Assert.Single(world.SkuPrices.Asked).At);
    }

    [Fact]
    public async Task Record_ABillableSkuWithNoPrice_IsRefused_AndNothingIsWritten()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, billable: true));

        Assert.Equal(BusinessViolationCodes.SkuNotPriced, Code(response));
        Assert.Empty(world.Ledger.Deducted);
        Assert.Empty(world.Visits.Updated);
    }

    [Fact]
    public async Task Record_ANotBillableSku_ReadsNoPrice()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);

        await world.Handler().HandleAsync(world.Record(sku, 1, billable: false));

        Assert.Empty(world.SkuPrices.Asked);
        Assert.Null(world.Visit.Treatments[0].UnitPrice);
    }

    [Fact]
    public async Task Record_ABillableProcedureWithoutAService_IsRefused()
    {
        var world = new World();
        var command = new RecordTreatmentCommand
        {
            VisitId = world.Visit.Id, LineId = Guid.NewGuid(), BranchId = world.Branch, Kind = "Procedure", Description = "Dressing", Quantity = 1, IsBillable = true,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid()
        };

        Assert.Equal(BusinessViolationCodes.ServiceNotPriced, Code(await world.Handler().HandleAsync(command)));
    }

    // ── guards ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Record_OnAnotherBranchsVisit_IsNotFound()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        var command = world.Record(sku, 1);

        var other = new RecordTreatmentCommand
        {
            VisitId = command.VisitId, LineId = command.LineId, BranchId = Guid.NewGuid(), Kind = command.Kind, SkuId = command.SkuId, Quantity = 1,
            UserContext = command.UserContext, CorrelationId = command.CorrelationId
        };

        Assert.Equal(BusinessViolationCodes.VisitNotFound, Code(await world.Handler().HandleAsync(other)));
        Assert.Empty(world.Ledger.Calls);
    }

    [Fact]
    public async Task Record_OnAFinalisedVisit_IsRefused_BeforeAnyStockIsRead()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);
        FinaliseTheVisit(world);

        var response = await world.Handler().HandleAsync(world.Record(sku, 1));

        Assert.Equal(BusinessViolationCodes.VisitFinalised, Code(response));
        Assert.Empty(world.Ledger.Calls);
    }

    [Fact]
    public async Task Record_ABranchWithNoDispensary_IsDispensaryNotConfigured()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.HasDispensary = false;

        var response = await world.Handler().HandleAsync(world.Record(sku, 1, acceptShortfall: true));

        Assert.Equal(BusinessViolationCodes.DispensaryNotConfigured, Code(response));
        Assert.Empty(world.Ledger.Shortfalls);
    }

    [Fact]
    public async Task Record_AnUnknownOrInactiveSku_IsRefused()
    {
        var world = new World();
        var inactive = world.Skus.Add("Old", active: false);
        var unknown = new ClinicalSku(Guid.NewGuid(), "Ghost", "each", true, false, Guid.NewGuid(), false);

        Assert.Equal(BusinessViolationCodes.SkuInactive, Code(await world.Handler().HandleAsync(world.Record(inactive, 1))));
        Assert.Equal(BusinessViolationCodes.SkuNotFound, Code(await world.Handler().HandleAsync(world.Record(unknown, 1))));
    }

    [Fact]
    public async Task Record_AFractionalQuantityOfAWholeUnitSku_IsRefused_NeverShortfalled() // whole units (OQ-19)
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "L1", new DateOnly(2027, 1, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 0.5m, acceptShortfall: true));

        Assert.Equal(BusinessViolationCodes.FractionalQuantityNotAllowed, Code(response));
        Assert.Empty(world.Ledger.Calls);
    }

    [Fact]
    public async Task Record_AFractionalQuantityOfAFractionalSku_IsAccepted()
    {
        var world = new World();
        var syrup = world.NewSku("Syrup", fractional: true);
        world.Ledger.Stock(syrup.SkuId, "S1", new DateOnly(2027, 1, 1), 10m);

        var response = await world.Handler().HandleAsync(world.Record(syrup, 0.5m));

        Assert.True(response.IsSuccess);
        Assert.Equal(0.5m, Assert.Single(world.Visit.Treatments).DeductedQuantity);
    }

    // ── owner decision C (2026-10-13): auto-split across batches ──────────────────────────────

    [Fact]
    public async Task Record_MoreThanTheFefoBatchHolds_DrawsOnTheNextBatches_OneLinePerBatchUsed()
    {
        var world = WorldWithOneSku(out var sku);
        var early = world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 3);
        var middle = world.Ledger.Stock(sku.SkuId, "MIDDLE", new DateOnly(2027, 3, 1), 4);
        world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 9, 1), 50);

        var response = await world.Handler().HandleAsync(world.Record(sku, 6));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<RecordTreatmentResult>(response.Result);
        Assert.Equal(6m, result.DeductedQuantity);
        Assert.Equal(0m, result.ShortfallQuantity);
        Assert.Equal([early.BatchId, middle.BatchId], world.Visit.Treatments.Select(t => t.BatchId!.Value).ToList());
        Assert.Equal([3m, 3m], world.Visit.Treatments.Select(t => t.DeductedQuantity).ToList());
        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(world.Visit.Treatments.Select(t => t.Id).ToList(), result.Lines.Select(l => l.LineId).ToList());

        // One ledger call, one explicit line per batch, FEFO order, no override (the split IS the FEFO plan).
        var (demand, reference, _) = Assert.Single(world.Ledger.Deducted);
        Assert.Equal(6m, demand.Quantity);
        Assert.Equal([early.BatchId, middle.BatchId], demand.ExplicitLines!.Select(l => l.BatchId).ToList());
        Assert.Null(demand.OverrideReason);
        Assert.Equal(result.LineId, reference.ReferenceLineId);
        Assert.Empty(world.Ledger.Shortfalls);
    }

    [Fact]
    public async Task Record_TheWarning_AndTheShortfall_UseTheWholeDispensary_NotOneBatch()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 3);
        world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);

        var warned = await world.Handler().HandleAsync(world.Record(sku, 20));
        Assert.Equal(13m, Assert.IsType<InsufficientStockDetail>(warned.Result).AvailableQuantity);
        Assert.Empty(world.Visit.Treatments);

        var accepted = await world.Handler().HandleAsync(world.Record(sku, 20, acceptShortfall: true));

        var result = Assert.IsType<RecordTreatmentResult>(accepted.Result);
        Assert.Equal(13m, result.DeductedQuantity);
        Assert.Equal(7m, result.ShortfallQuantity);
        Assert.Equal(2, world.Visit.Treatments.Count);
        Assert.Equal(0m, world.Visit.Treatments[0].ShortfallQuantity);
        Assert.Equal(7m, world.Visit.Treatments[1].ShortfallQuantity);
        Assert.Equal(17m, world.Visit.Treatments[1].Quantity);
        Assert.Equal(7m, Assert.Single(world.Ledger.Shortfalls).Quantity);
    }

    [Fact]
    public async Task Record_AShortfallOverStockThatIsOnTheShelf_IsNotAPhantom()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 3);
        world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);

        var response = await world.Handler().HandleAsync(world.Record(sku, 5, acceptShortfall: true));

        Assert.Equal(0m, Assert.IsType<RecordTreatmentResult>(response.Result).ShortfallQuantity);
        Assert.Empty(world.Ledger.Shortfalls);
    }

    [Fact]
    public async Task Record_ANamedNonFefoBatch_IsDrawnFirst_ThenTheDrawContinuesInFefoOrderSkippingIt()
    {
        var world = WorldWithOneSku(out var sku);
        var early = world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 10);
        var middle = world.Ledger.Stock(sku.SkuId, "MIDDLE", new DateOnly(2027, 3, 1), 10);
        var late = world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 9, 1), 4);

        var response = await world.Handler().HandleAsync(world.Record(sku, 6, batchId: late.BatchId, overrideReason: "Keep the early lots for the surgery list"));

        Assert.True(response.IsSuccess);
        Assert.Equal([late.BatchId, early.BatchId], world.Visit.Treatments.Select(t => t.BatchId!.Value).ToList());
        Assert.Equal([4m, 2m], world.Visit.Treatments.Select(t => t.DeductedQuantity).ToList());
        Assert.Equal("Keep the early lots for the surgery list", world.Visit.Treatments[0].BatchOverrideReason);
        Assert.Null(world.Visit.Treatments[1].BatchOverrideReason);
        var (demand, _, _) = Assert.Single(world.Ledger.Deducted);
        Assert.Equal("Keep the early lots for the surgery list", demand.OverrideReason);
        Assert.DoesNotContain(world.Visit.Treatments, t => t.BatchId == middle.BatchId);
    }

    [Fact]
    public async Task Record_TheExpiredBatchRefusal_StillHoldsInASplit()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "FRESH", new DateOnly(2027, 1, 1), 2);
        var expired = world.Ledger.Stock(sku.SkuId, "OLD", new DateOnly(2026, 10, 6), 50);

        // Not named: an expired batch is never drawn from, so only 2 are available.
        var warned = await world.Handler().HandleAsync(world.Record(sku, 10));
        Assert.Equal(2m, Assert.IsType<InsufficientStockDetail>(warned.Result).AvailableQuantity);

        var named = await world.Handler().HandleAsync(world.Record(sku, 1, batchId: expired.BatchId, overrideReason: "please"));
        Assert.Equal(BusinessViolationCodes.BatchExpired, Code(named));
    }

    // ── idempotency: a retry with the client line id deducts nothing again ────────────────────

    [Fact]
    public async Task Record_ARetryWithTheSameLineId_ReturnsTheSameLines_AndDeductsNothingAgain()
    {
        var world = WorldWithOneSku(out var sku);
        world.Ledger.Stock(sku.SkuId, "EARLY", new DateOnly(2027, 1, 1), 3);
        world.Ledger.Stock(sku.SkuId, "LATE", new DateOnly(2027, 6, 1), 10);
        var command = world.Record(sku, 5);

        var first = Assert.IsType<RecordTreatmentResult>((await world.Handler().HandleAsync(command)).Result);
        var retry = await world.Handler().HandleAsync(command);

        Assert.True(retry.IsSuccess);
        var again = Assert.IsType<RecordTreatmentResult>(retry.Result);
        Assert.True(again.Replayed);
        Assert.False(first.Replayed);
        Assert.Equal(first.Lines.Select(l => l.LineId), again.Lines.Select(l => l.LineId));
        Assert.Equal(2, world.Visit.Treatments.Count);
        Assert.Single(world.Ledger.Deducted);
        Assert.Equal(5m, world.Visit.Treatments.Sum(t => t.DeductedQuantity));
    }
}
