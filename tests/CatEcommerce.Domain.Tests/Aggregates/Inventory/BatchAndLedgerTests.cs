using System.Reflection;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Aggregates.Inventory;

/// <summary>HO-IMP-001 §15 phase D -- U-D7 (the batch state machine), U-D8 (the sign of every movement) and U-D9 (the ledger entries are
/// immutable in the application).</summary>
public sealed class BatchAndLedgerTests
{
    private static InventoryActor Actor() => new(TestUserContext.Create(), Guid.NewGuid());

    private static BatchAggregate NewBatch(BatchStatus? initialStatus = null, DateOnly? expiresOn = null)
    {
        var initial = initialStatus ?? BatchStatus.Available;
        var batch = new BatchAggregate(Guid.NewGuid());
        batch.Create(new NewBatchDto(
            batch.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(), new SkuSnapshot("SKU-1", "Product", "each", StockClass.Food), "LOT-1",
            null, expiresOn, "DE", DateTimeOffset.UtcNow, 10m, 5m, "BDT", 5m, initial, Actor()));
        return batch;
    }

    private static BatchAggregate In(BatchStatus status)
    {
        var batch = NewBatch();
        if (status == BatchStatus.Quarantined) batch.Quarantine("Inspection.", DateTimeOffset.UtcNow, Actor());
        if (status == BatchStatus.Recalled) Recall(batch);
        if (status == BatchStatus.WrittenOff) batch.WriteOff("Damaged.", DateTimeOffset.UtcNow, Actor());
        return batch;
    }

    private static void Recall(BatchAggregate batch) =>
        batch.Recall(new BatchRecallDto("RCL-2026-000001", "Contamination.", "Stop feeding it.", DateTimeOffset.UtcNow, Actor()), "LOT-1");

    // ── U-D7 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>§8.6.1's arrows, transcribed independently of <see cref="BatchTransitionRules"/>.</summary>
    private static readonly IReadOnlyDictionary<(string Action, BatchStatus From), BatchStatus> Arrows = new Dictionary<(string, BatchStatus), BatchStatus>
    {
        [("quarantine", BatchStatus.Available)] = BatchStatus.Quarantined,
        [("release", BatchStatus.Quarantined)] = BatchStatus.Available,
        [("recall", BatchStatus.Available)] = BatchStatus.Recalled,
        [("recall", BatchStatus.Quarantined)] = BatchStatus.Recalled,
        [("withdraw-recall", BatchStatus.Recalled)] = BatchStatus.Quarantined,
        [("write-off", BatchStatus.Available)] = BatchStatus.WrittenOff,
        [("write-off", BatchStatus.Quarantined)] = BatchStatus.WrittenOff,
        [("write-off", BatchStatus.Recalled)] = BatchStatus.WrittenOff,
    };

    public static IEnumerable<object[]> ActionByStatus() =>
        from action in BatchTransitionRules.Actions
        from status in Enumeration.GetAll<BatchStatus>()
        select new object[] { action, status };

    [Theory]
    [MemberData(nameof(ActionByStatus))]
    public void U_D7_BatchTransitions_AreExactlyThoseOfSection8_6_1(string action, BatchStatus status)
    {
        Arrows.TryGetValue((action, status), out var expected);

        Assert.Equal(Arrows.ContainsKey((action, status)) ? expected : null, BatchTransitionRules.Next(status, action));
    }

    [Theory]
    [MemberData(nameof(ActionByStatus))]
    public void U_D7_TheAggregateAppliesTheSameTable(string action, BatchStatus status)
    {
        var batch = In(status);

        void Act()
        {
            switch (action)
            {
                case "quarantine": batch.Quarantine("Inspection.", DateTimeOffset.UtcNow, Actor()); break;
                case "release": batch.Release(DateTimeOffset.UtcNow, Actor()); break;
                case "recall": Recall(batch); break;
                case "withdraw-recall": batch.WithdrawRecall("Declared by mistake.", DateTimeOffset.UtcNow, Actor()); break;
                default: batch.WriteOff("Damaged.", DateTimeOffset.UtcNow, Actor()); break;
            }
        }

        if (Arrows.TryGetValue((action, status), out var expected)) { Act(); Assert.Equal(expected, batch.Status); }
        else Assert.Equal(BusinessViolationCodes.InvalidBatchTransition, Assert.Throws<DomainRuleViolationException>(Act).ErrorCode);
    }

    [Fact]
    public void U_D7_WithdrawingARecallGoesToQuarantined_NeverStraightBackToSale()
    {
        var batch = In(BatchStatus.Recalled);

        batch.WithdrawRecall("Declared by mistake.", DateTimeOffset.UtcNow, Actor());

        Assert.Equal(BatchStatus.Quarantined, batch.Status);
        Assert.Equal(2, batch.StatusHistory.Count);
    }

    [Fact]
    public void U_D7_ExpiredIsNeverAStatus_AndABatchIsCreatedAvailableOrQuarantinedOnly()
    {
        Assert.DoesNotContain(Enumeration.GetAll<BatchStatus>(), s => s.DisplayName.Contains("Expired", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Available", "Quarantined", "Recalled", "WrittenOff"], Enumeration.GetAll<BatchStatus>().Select(s => s.DisplayName).OrderBy(n => n));

        Assert.Equal(BatchStatus.Quarantined, NewBatch(BatchStatus.Quarantined).Status);
        Assert.Throws<DomainRuleViolationException>(() => NewBatch(BatchStatus.Recalled));
    }

    [Fact]
    public void U_D7_ARecallNeedsTheLotNumberTyped_AndAReasonAndAdvice()
    {
        var batch = NewBatch();

        Assert.Equal(BusinessViolationCodes.RecallConfirmationMismatch, Assert.Throws<DomainRuleViolationException>(() =>
            batch.Recall(new BatchRecallDto("RCL-1", "Reason", "Advice", DateTimeOffset.UtcNow, Actor()), "lot-2")).ErrorCode);
        Assert.Equal(BatchStatus.Available, batch.Status);
    }

    [Fact]
    public void U_D7_ABatchIsNotBornExpired_AndExpiryIsDerivedFromTheDate()
    {
        var batch = NewBatch(expiresOn: new DateOnly(2026, 10, 12));

        Assert.False(BatchExpiryRules.IsExpired(batch.ExpiresOn, new DateOnly(2026, 10, 12)));
        Assert.True(BatchExpiryRules.IsExpired(batch.ExpiresOn, new DateOnly(2026, 10, 13)));
        Assert.Equal(BatchStatus.Available, batch.Status);
    }

    // ── U-D8 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<MovementKind, int> ExpectedSigns = new Dictionary<MovementKind, int>
    {
        [MovementKind.Receipt] = +1, [MovementKind.TransferIn] = +1, [MovementKind.AdjustmentIn] = +1, [MovementKind.CountGain] = +1, [MovementKind.Return] = +1,
        [MovementKind.TransferOut] = -1, [MovementKind.AdjustmentOut] = -1, [MovementKind.CountLoss] = -1, [MovementKind.Wastage] = -1,
        [MovementKind.Sale] = -1, [MovementKind.Dispense] = -1, [MovementKind.ClinicalUse] = -1, [MovementKind.BoardingUse] = -1,
    };

    public static IEnumerable<object[]> Kinds() => Enumeration.GetAll<MovementKind>().Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(Kinds))]
    public void U_D8_MovementKindRules_SignOfEveryKind(MovementKind kind)
    {
        Assert.Equal(ExpectedSigns[kind], MovementKindRules.Sign(kind));
        Assert.True(MovementKindRules.IsValidDelta(kind, ExpectedSigns[kind] * 2m));
        Assert.False(MovementKindRules.IsValidDelta(kind, -ExpectedSigns[kind] * 2m));
        Assert.False(MovementKindRules.IsValidDelta(kind, 0m));
    }

    [Fact]
    public void U_D8_EveryKindIsClassifiedExactlyOnce_AndTheConsumingKindsAreTheFour()
    {
        Assert.Equal(13, Enumeration.GetAll<MovementKind>().Count());
        Assert.Equal(MovementKindRules.Incoming.Count + MovementKindRules.Outgoing.Count, Enumeration.GetAll<MovementKind>().Count());
        Assert.Empty(MovementKindRules.Incoming.Intersect(MovementKindRules.Outgoing));
        Assert.Equal([MovementKind.BoardingUse, MovementKind.ClinicalUse, MovementKind.Dispense, MovementKind.Sale], MovementKindRules.Consuming.OrderBy(k => k.DisplayName));
    }

    private static StockLedgerEntryDto Entry(MovementKind kind, decimal delta, bool consumes = false) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), kind, delta, consumes, StockReferenceKind.GoodsReceipt, Guid.NewGuid(), null, "GRN-2026-000012",
            null, null, false, null, Guid.NewGuid(), null, DateTimeOffset.UtcNow, TestUserContext.Create());

    [Fact]
    public void U_D8_ALedgerEntryWithTheWrongSignForItsKindIsRefused()
    {
        Assert.Equal(BusinessViolationCodes.InvalidQuantity, Assert.Throws<DomainRuleViolationException>(() => StockLedgerEntryAggregate.Record(Entry(MovementKind.Receipt, -1m))).ErrorCode);
        Assert.Equal(BusinessViolationCodes.InvalidQuantity, Assert.Throws<DomainRuleViolationException>(() => StockLedgerEntryAggregate.Record(Entry(MovementKind.Sale, 1m))).ErrorCode);
        Assert.Throws<DomainRuleViolationException>(() => StockLedgerEntryAggregate.Record(Entry(MovementKind.Wastage, 0m)));

        var entry = StockLedgerEntryAggregate.Record(Entry(MovementKind.Sale, -3m, consumes: true));
        Assert.Equal(-3m, entry.QuantityDelta);
        Assert.True(entry.ConsumesReservation);
    }

    [Fact]
    public void U_D8_OnlyASaleCanConsumeAReservation()
    {
        Assert.Throws<DomainRuleViolationException>(() => StockLedgerEntryAggregate.Record(Entry(MovementKind.Dispense, -1m, consumes: true)));
    }

    // ── U-D9 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(typeof(StockLedgerEntryAggregate))]
    [InlineData(typeof(StockReservationEntryAggregate))]
    public void U_D9_LedgerAndReservationEntries_HaveNoPublicSetterAndNoPublicMethodButTheFactory(Type type)
    {
        var setters = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(p => p.SetMethod is { IsPublic: true });
        var instanceMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName);
        var staticMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).Select(m => m.Name);

        Assert.Empty(setters);
        Assert.Empty(instanceMethods);
        Assert.Equal(["Record"], staticMethods.ToArray());
        Assert.Empty(type.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void U_D9_ALedgerEntryRaisesNoDomainEvent()
    {
        Assert.Empty(StockLedgerEntryAggregate.Record(Entry(MovementKind.Receipt, 5m)).Events);
    }

    // ── U-I1 / U-I4 (phase I) ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_I1_ARecallFromWrittenOffIsRefused_AndTheLotNumberMustMatchExactly()
    {
        var written = In(BatchStatus.WrittenOff);
        var dto = new BatchRecallDto("RCL-2026-000001", "Contamination.", "Stop feeding it.", DateTimeOffset.UtcNow, Actor());

        Assert.Equal(BusinessViolationCodes.InvalidBatchTransition, Assert.Throws<DomainRuleViolationException>(() => written.Recall(dto, "LOT-1")).ErrorCode);
        Assert.Equal(BusinessViolationCodes.RecallConfirmationMismatch, Assert.Throws<DomainRuleViolationException>(() => NewBatch().Recall(dto, "lot-1")).ErrorCode);
        Assert.Equal(BusinessViolationCodes.RecallConfirmationMismatch, Assert.Throws<DomainRuleViolationException>(() => NewBatch().Recall(dto, "LOT-2")).ErrorCode);
    }

    [Fact]
    public void U_I4_TheRecallTemplateIsTransactionalAndUrgent_AndItsDefaultsPassTheTemplateRules()
    {
        var definition = NotificationTemplateCatalogue.Find(NotificationTemplateCatalogue.InventoryBatchRecall);

        Assert.NotNull(definition);
        Assert.Equal(NotificationCategory.Transactional, definition!.Category);
        Assert.Equal(NotificationUrgency.Urgent, definition.Urgency);
        var contents = definition.Defaults.Select(d => new TemplateContentInput(d.Channel, d.Subject, d.Title, d.Body, d.LinkPath)).ToList();
        Assert.Null(TemplateRules.Validate(definition, contents));
    }
}
