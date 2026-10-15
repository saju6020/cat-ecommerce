using System.Reflection;
using System.Text.RegularExpressions;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-IMP-001 §15 phase E -- U-E1 ... U-E11: FEFO, the one expiry predicate, and every rule <c>IStockLedger</c> applies (decided by the pure
/// <see cref="StockDemandPlanner"/>, so they are exercised without a database).</summary>
public sealed class FefoAndStockDemandTests
{
    private static readonly DateOnly Today = new(2026, 10, 7);
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Sku = Guid.NewGuid();
    private readonly FefoAllocator _fefo = new();
    private StockDemandPlanner Planner => new(_fefo);

    private static FefoCandidate Candidate(DateOnly? expires, int receivedDay = 1, decimal free = 10m, BatchStatus? status = null, int priority = 2) =>
        new(Guid.NewGuid(), Guid.NewGuid(), priority, expires, new DateTimeOffset(2026, 9, receivedDay, 0, 0, 0, TimeSpan.Zero), status ?? BatchStatus.Available, free);

    // ── U-E1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E1_FefoOrder_IsSoonestExpiryFirst_EarlierReceiptBreaksATie_UndatedGoodsLast()
    {
        var march = Candidate(new DateOnly(2027, 3, 31));
        var decemberLate = Candidate(new DateOnly(2026, 12, 31), receivedDay: 20);
        var undated = Candidate(null, receivedDay: 2);
        var decemberEarly = Candidate(new DateOnly(2026, 12, 31), receivedDay: 5);

        var order = FefoAllocator.Order([march, decemberLate, undated, decemberEarly]).ToList();

        Assert.Equal([decemberEarly, decemberLate, march, undated], order);
    }

    [Fact]
    public void U_E1_ALocationPriorityBreaksTheLastTie_WarehouseBeforeShopFloor()
    {
        var expiry = new DateOnly(2026, 12, 31);
        var shop = Candidate(expiry, priority: StockLocationRules.FefoPriority(StockLocationKind.ShopFloor));
        var warehouse = Candidate(expiry, priority: StockLocationRules.FefoPriority(StockLocationKind.Warehouse));

        Assert.Equal([warehouse, shop], FefoAllocator.Order([shop, warehouse]));
    }

    [Fact]
    public void U_E1_TheAllocationIsDeterministic_WhateverTheInputOrder()
    {
        var candidates = Enumerable.Range(0, 6).Select(i => Candidate(Today.AddDays(30 + i % 3), receivedDay: 1 + i % 2, free: 3m)).ToList();

        var forward = _fefo.Allocate(10m, candidates, Today);
        var reversed = _fefo.Allocate(10m, Enumerable.Reverse(candidates).ToList(), Today);

        Assert.Equal(forward.Lines, reversed.Lines);
    }

    // ── U-E2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E2_ABatchIsUsableThroughItsWholeExpiryDate_AndExpiredFromTheNextBranchLocalDay()
    {
        var expiry = new DateOnly(2026, 10, 12);
        // 17:00Z is 23:00 in Dhaka on the 12th; 18:30Z is 00:30 on the 13th.
        var onTheDay = BranchCalendar.LocalDateOf(new DateTimeOffset(2026, 10, 12, 17, 0, 0, TimeSpan.Zero), "Asia/Dhaka");
        var nextDay = BranchCalendar.LocalDateOf(new DateTimeOffset(2026, 10, 12, 18, 30, 0, TimeSpan.Zero), "Asia/Dhaka");

        Assert.False(BatchExpiryRules.IsExpired(expiry, onTheDay));
        Assert.True(BatchExpiryRules.IsExpired(expiry, nextDay));
        Assert.False(BatchExpiryRules.IsExpired(null, nextDay));
        Assert.Equal(">", BatchExpiryRules.SqlComparison);
    }

    // ── U-E3 / U-E4 / U-E5 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E3_Fefo_SkipsExpiredQuarantinedRecalledWrittenOffAndNothingFreeCandidates()
    {
        var good = Candidate(Today.AddDays(60));
        var candidates = new[]
        {
            Candidate(Today.AddDays(-1)),
            Candidate(Today.AddDays(5), status: BatchStatus.Quarantined),
            Candidate(Today.AddDays(5), status: BatchStatus.Recalled),
            Candidate(Today.AddDays(5), status: BatchStatus.WrittenOff),
            Candidate(Today.AddDays(5), free: 0m),
            good,
        };

        var allocation = _fefo.Allocate(4m, candidates, Today);

        var line = Assert.Single(allocation.Lines);
        Assert.Equal(good.BatchId, line.BatchId);
        Assert.Equal(0m, allocation.Shortfall);
    }

    [Fact]
    public void U_E4_Demand15OverBatchesWith10And8Free_Takes10ThenTheNext5_SplitAcrossBatches()
    {
        var first = Candidate(Today.AddDays(30), free: 10m);
        var second = Candidate(Today.AddDays(90), free: 8m);

        var allocation = _fefo.Allocate(15m, [second, first], Today);

        Assert.Equal([(first.BatchId, 10m), (second.BatchId, 5m)], allocation.Lines.Select(l => (l.BatchId, l.Quantity)));
        Assert.Equal(0m, allocation.Shortfall);
    }

    [Fact]
    public void U_E5_Demand30Over18Free_LeavesAShortfallOf12_AndThePlannerRefusesWithInsufficientStockMentioning18()
    {
        var candidates = new[] { Candidate(Today.AddDays(30), free: 10m), Candidate(Today.AddDays(60), free: 8m) };
        Assert.Equal(12m, _fefo.Allocate(30m, candidates, Today).Shortfall);

        var stock = candidates.Select(c => new StockCandidate(c.BatchId, "LOT", c.ExpiresOn, c.ReceivedOn, c.Status, c.LocationId, StockLocationKind.Warehouse, c.Free, 0m)).ToList();
        var result = Planner.Plan(Demand(30m), Reference(), Party(customer: true), SkuProfile(), stock, new Dictionary<Guid, StockLocationFact>(), Today, allowShortfall: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(BusinessViolationCodes.InsufficientStock, result.ViolationCode);
        Assert.Contains("18", result.Message);
    }

    [Fact]
    public void U_E5_APlanOnlyPreviewReportsTheShortfallInsteadOfFailing()
    {
        var stock = new[] { Stock(Today.AddDays(30), onHand: 18m) };

        var result = Planner.Plan(Demand(30m), null, null, SkuProfile(), stock, new Dictionary<Guid, StockLocationFact>(), Today, allowShortfall: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(12m, result.Value!.Plan.Shortfall);
        Assert.Equal(18m, result.Value.Plan.Lines.Sum(l => l.Quantity));
    }

    // ── planner fixtures ────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static SkuStockProfile SkuProfile(bool fractional = false, bool active = true) =>
        new(Sku, Guid.NewGuid(), "SKU-1", "Kitten food", null, StockClass.Food, true, "each", fractional, null, active);

    private static StockDemand Demand(
        decimal quantity, MovementKind? purpose = null, IReadOnlyList<(Guid, Guid, decimal)>? explicitLines = null, string? reason = null, IReadOnlyList<Guid>? locations = null) =>
        new(Branch, Sku, quantity, purpose ?? MovementKind.Sale, locations, explicitLines, reason);

    private static StockReference Reference(StockReferenceKind? kind = null) => new(kind ?? StockReferenceKind.CustomerOrder, Guid.NewGuid(), null, "Order PC-001234");

    private static StockParty Party(bool customer = false, bool cat = false) => new(customer ? Guid.NewGuid() : null, cat ? Guid.NewGuid() : null);

    private static StockCandidate Stock(
        DateOnly? expires, decimal onHand = 10m, decimal reserved = 0m, BatchStatus? status = null, StockLocationKind? kind = null, int receivedDay = 1) =>
        new(Guid.NewGuid(), "LOT-" + Guid.NewGuid().ToString("N")[..4], expires, new DateTimeOffset(2026, 9, receivedDay, 0, 0, 0, TimeSpan.Zero),
            status ?? BatchStatus.Available, Guid.NewGuid(), kind ?? StockLocationKind.Warehouse, onHand, reserved);

    private StockResult<PlannedDemand> PlanExplicit(
        IReadOnlyList<StockCandidate> stock, IReadOnlyList<(Guid, Guid, decimal)> lines, string? reason = null, decimal? quantity = null, MovementKind? purpose = null)
    {
        var locations = stock.ToDictionary(s => s.LocationId, s => new StockLocationFact(s.LocationId, Branch, "L", s.LocationKind, true));
        return Planner.Plan(Demand(quantity ?? lines.Sum(l => l.Item3), purpose, lines, reason), Reference(), Party(customer: true, cat: true), SkuProfile(), stock, locations, Today, allowShortfall: false);
    }

    // ── U-E6 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E6_AnExplicitPlanEqualToFefo_IsAcceptedUnflagged_WithNoAuditOverride()
    {
        var soon = Stock(Today.AddDays(30));
        var later = Stock(Today.AddDays(90));

        var result = PlanExplicit([later, soon], [(soon.BatchId, soon.LocationId, 4m)]);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Overridden);
        Assert.False(Assert.Single(result.Value.Plan.Lines).FefoOverridden);
    }

    [Fact]
    public void U_E6_AnExplicitPlanThatDiffersFromFefoNeedsAReason_AndWithOneIsFlagged()
    {
        var soon = Stock(Today.AddDays(30));
        var later = Stock(Today.AddDays(90));

        var without = PlanExplicit([soon, later], [(later.BatchId, later.LocationId, 4m)]);
        Assert.Equal(BusinessViolationCodes.AllocationOverrideReasonRequired, without.ViolationCode);

        var tooShort = PlanExplicit([soon, later], [(later.BatchId, later.LocationId, 4m)], reason: "no");
        Assert.Equal(BusinessViolationCodes.AllocationOverrideReasonRequired, tooShort.ViolationCode);

        var with = PlanExplicit([soon, later], [(later.BatchId, later.LocationId, 4m)], reason: "Customer asked for the later date.");
        Assert.True(with.IsSuccess);
        Assert.True(with.Value!.Overridden);
        Assert.Equal("Customer asked for the later date.", with.Value.OverrideReason);
        Assert.True(Assert.Single(with.Value.Plan.Lines).FefoOverridden);
        Assert.Equal(soon.BatchId, Assert.Single(with.Value.FefoLines).BatchId);
    }

    // ── U-E7 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E7_AnExplicitPlanNamingAnExpiredBatch_IsBatchExpired_NoReasonChangesIt()
    {
        var expired = Stock(Today.AddDays(-1));
        var good = Stock(Today.AddDays(60));

        var withReason = PlanExplicit([expired, good], [(expired.BatchId, expired.LocationId, 2m)], reason: "The vet says it is fine, I am sure of it.");

        Assert.Equal(BusinessViolationCodes.BatchExpired, withReason.ViolationCode);
        Assert.Contains(expired.LotNumber, withReason.Message);
        Assert.Contains((Today.AddDays(-1)).ToString("yyyy-MM-dd"), withReason.Message);
    }

    [Theory]
    [InlineData("Quarantined")]
    [InlineData("Recalled")]
    [InlineData("WrittenOff")]
    public void U_E7_AnExplicitPlanNamingAnUnusableBatch_IsBatchNotUsable(string status)
    {
        var held = Stock(Today.AddDays(60), status: Enumeration.FromDisplayName<BatchStatus>(status));

        var result = PlanExplicit([held], [(held.BatchId, held.LocationId, 1m)], reason: "Please, just this once.");

        Assert.Equal(BusinessViolationCodes.BatchNotUsable, result.ViolationCode);
    }

    [Fact]
    public void U_E7_AnExplicitLineBeyondWhatIsFree_IsInsufficientStock_AndOtherCustomersReservationsAreNotFree()
    {
        var reservedMostly = Stock(Today.AddDays(30), onHand: 10m, reserved: 8m);

        var result = PlanExplicit([reservedMostly], [(reservedMostly.BatchId, reservedMostly.LocationId, 3m)]);

        Assert.Equal(BusinessViolationCodes.InsufficientStock, result.ViolationCode);
        Assert.Contains("2", result.Message);
    }

    // ── U-E8 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_E8_ASaleAtADispensary_AndADispenseAtTheShopFloor_AreNotPermittedAtThatLocation()
    {
        var dispensary = new StockLocationFact(Guid.NewGuid(), Branch, "DISP", StockLocationKind.ClinicDispensary, true);
        var shop = new StockLocationFact(Guid.NewGuid(), Branch, "SHOP", StockLocationKind.ShopFloor, true);
        var locations = new Dictionary<Guid, StockLocationFact> { [dispensary.LocationId] = dispensary, [shop.LocationId] = shop };

        var sale = Planner.Plan(Demand(1m, MovementKind.Sale, locations: [dispensary.LocationId]), Reference(), Party(customer: true), SkuProfile(), [], locations, Today, false);
        var dispense = Planner.Plan(Demand(1m, MovementKind.Dispense, locations: [shop.LocationId]), Reference(), Party(customer: true, cat: true), SkuProfile(), [], locations, Today, false);

        Assert.Equal(BusinessViolationCodes.MovementNotPermittedAtLocation, sale.ViolationCode);
        Assert.Equal(BusinessViolationCodes.MovementNotPermittedAtLocation, dispense.ViolationCode);
    }

    [Fact]
    public void U_E8_WithNoLocationNamed_OnlyLocationsThatPermitThePurposeAreConsidered()
    {
        var dispensaryStock = Stock(Today.AddDays(30), kind: StockLocationKind.ClinicDispensary);
        var shopStock = Stock(Today.AddDays(90), kind: StockLocationKind.ShopFloor);

        var result = Planner.Plan(Demand(2m, MovementKind.Sale), Reference(), Party(customer: true), SkuProfile(), [dispensaryStock, shopStock], new Dictionary<Guid, StockLocationFact>(), Today, false);

        Assert.True(result.IsSuccess);
        Assert.Equal(shopStock.BatchId, Assert.Single(result.Value!.Plan.Lines).BatchId);
    }

    // ── U-E9 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly Regex Forbidden = new("force|bypass|ignore|allowExpired|override(?!Reason)", RegexOptions.IgnoreCase);

    private static IEnumerable<Type> ReachableTypes(params Type[] roots)
    {
        var seen = new HashSet<Type>();
        var pending = new Queue<Type>(roots);
        while (pending.Count > 0)
        {
            var type = pending.Dequeue();
            if (type == typeof(void) || !seen.Add(type)) continue;
            yield return type;

            if (type.IsGenericType) foreach (var argument in type.GetGenericArguments()) pending.Enqueue(argument);
            if (type.IsArray) pending.Enqueue(type.GetElementType()!);

            // Only the types of THIS solution's Domain are walked into (never System/Platform internals).
            if (type.Namespace?.StartsWith("CatEcommerce.Domain.Models", StringComparison.Ordinal) != true && !type.IsInterface) continue;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) pending.Enqueue(property.PropertyType);
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                pending.Enqueue(method.ReturnType);
                foreach (var parameter in method.GetParameters()) pending.Enqueue(parameter.ParameterType);
            }
        }
    }

    [Fact]
    public void U_E9_NoMemberOfTheStockPortsOrAnyTypeTheyTake_IsNamedForceBypassIgnoreAllowExpiredOrOverride()
    {
        var offenders = new List<string>();
        foreach (var type in ReachableTypes(typeof(IStockLedger), typeof(IInventoryLedgerWriter)).Where(t => t.Namespace?.StartsWith("CatEcommerce", StringComparison.Ordinal) == true))
        {
            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (Forbidden.IsMatch(member.Name)) offenders.Add($"{type.Name}.{member.Name}");
                if (member is MethodBase method)
                    foreach (var parameter in method.GetParameters().Where(p => p.Name is not null && Forbidden.IsMatch(p.Name)))
                        offenders.Add($"{type.Name}.{member.Name}({parameter.Name})");
            }
        }

        Assert.Empty(offenders);
        Assert.Contains(typeof(StockDemand), ReachableTypes(typeof(IStockLedger))); // the walk really did reach the DTOs
    }

    [Fact]
    public void U_E9_TheOnlyOverrideThereIsIsTheReasonForADifferentAllocation()
    {
        Assert.Equal(["OverrideReason"], typeof(StockDemand).GetProperties().Select(p => p.Name).Where(n => n.Contains("Override")).ToArray());
    }

    // ── U-E10 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Dispense", "CustomerOrder", false, true, "StockPartyRequired")]   // Dispense without a cat
    [InlineData("Dispense", "CustomerOrder", true, false, "StockPartyRequired")]   // Dispense without a customer
    [InlineData("Sale", "CustomerOrder", false, false, "StockPartyRequired")]      // an order sale with no customer
    [InlineData("Sale", "PosSale", false, false, null)]                            // an anonymous counter sale is allowed
    [InlineData("Sale", "CustomerOrder", true, false, null)]
    [InlineData("BoardingUse", "BoardingStay", true, false, "StockPartyRequired")]
    [InlineData("BoardingUse", "BoardingStay", true, true, null)]
    [InlineData("ClinicalUse", "Visit", false, false, "StockPartyRequired")]
    [InlineData("ClinicalUse", "Visit", false, true, null)]
    public void U_E10_ThePartyStampRules(string purpose, string referenceKind, bool customer, bool cat, string? expected)
    {
        var refusal = StockDemandPlanner.PartyRefusal(Enumeration.FromDisplayName<MovementKind>(purpose), Enumeration.FromDisplayName<StockReferenceKind>(referenceKind), Party(customer, cat));

        Assert.Equal(expected, refusal?.Code);
    }

    [Fact]
    public void U_E10_ThePlannerAppliesThePartyRulesBeforeTouchingStock()
    {
        var stock = new[] { Stock(Today.AddDays(30)) };

        var result = Planner.Plan(Demand(1m, MovementKind.Dispense), Reference(StockReferenceKind.Prescription), Party(customer: true), SkuProfile(), stock, new Dictionary<Guid, StockLocationFact>(), Today, false);

        Assert.Equal(BusinessViolationCodes.StockPartyRequired, result.ViolationCode);
    }

    // ── U-E11 ───────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Recalled")]
    [InlineData("Quarantined")]
    public void U_E11_ConsumingAReservedBatchThatWasRecalledAfterReserving_IsBatchNotUsable(string status)
    {
        var refusal = StockDemandPlanner.UsabilityRefusal(Enumeration.FromDisplayName<BatchStatus>(status), Today.AddDays(30), "LOT-1", Today);

        Assert.Equal(BusinessViolationCodes.BatchNotUsable, refusal?.Code);
    }

    [Fact]
    public void U_E11_AnExpiredBatchIsBatchExpired_WithTheLotAndDateInTheMessage_AndAnAvailableUnexpiredOneIsUsable()
    {
        var expired = StockDemandPlanner.UsabilityRefusal(BatchStatus.Available, new DateOnly(2026, 10, 1), "LOT-7", Today);

        Assert.Equal(BusinessViolationCodes.BatchExpired, expired?.Code);
        Assert.Equal("Batch LOT-7 expired on 2026-10-01 and cannot be sold, dispensed or fed.", expired?.Message);
        Assert.Null(StockDemandPlanner.UsabilityRefusal(BatchStatus.Available, Today, "LOT-7", Today));
        Assert.Null(StockDemandPlanner.UsabilityRefusal(BatchStatus.Available, null, "LOT-7", Today));
    }

    [Fact]
    public void U_E1_AFractionalDemandOnAWholeOnlySkuIsRefused_AndAnInactiveOrUnknownSkuToo()
    {
        var stock = new[] { Stock(Today.AddDays(30)) };

        Assert.Equal(BusinessViolationCodes.FractionalQuantityNotAllowed,
            Planner.Plan(Demand(1.5m), null, null, SkuProfile(), stock, new Dictionary<Guid, StockLocationFact>(), Today, false).ViolationCode);
        Assert.Equal(BusinessViolationCodes.SkuInactive,
            Planner.Plan(Demand(1m), null, null, SkuProfile(active: false), stock, new Dictionary<Guid, StockLocationFact>(), Today, false).ViolationCode);
        Assert.Equal(BusinessViolationCodes.SkuNotFound,
            Planner.Plan(Demand(1m), null, null, null, stock, new Dictionary<Guid, StockLocationFact>(), Today, false).ViolationCode);
        Assert.True(Planner.Plan(Demand(1.5m), null, null, SkuProfile(fractional: true), stock, new Dictionary<Guid, StockLocationFact>(), Today, false).IsSuccess);
    }

    [Fact]
    public void U_E1_OnlyAConsumingPurposeCanBePlanned()
    {
        var result = Planner.Plan(Demand(1m, MovementKind.Receipt), null, null, SkuProfile(), [], new Dictionary<Guid, StockLocationFact>(), Today, false);

        Assert.Equal(BusinessViolationCodes.MovementKindNotAllowed, result.ViolationCode);
    }
}
