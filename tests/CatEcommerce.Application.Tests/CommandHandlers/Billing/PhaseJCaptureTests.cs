using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DomainEventHandlers.Billing;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Prescription;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Domain.Events.Visit;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Billing;

/// <summary>
/// HO-BIL-001 Phase J (owner decisions 2026-10-14) at the handler level: a visit, a dispensing or a surgery is captured once per line however often it is
/// re-read (idempotent per source), what a source could not turn into an item is reported, and a capture that breaks never fails the source and logs ids only.
/// </summary>
public sealed class PhaseJCaptureTests
{
    private static readonly DateOnly ServiceDate = new(2026, 10, 14);

    private readonly FakeAggregateRootRepository<ChargeAggregate> _charges = new();
    private readonly FakeBillingPricingInputs _pricing = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 6, 0, 0, TimeSpan.Zero));
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _taxClassId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    public PhaseJCaptureTests() => _pricing.AddTaxClass(_taxClassId, rate: 0m);

    private BillableSourceItem Item(string lineRef, ItemKind kind, decimal quantity = 1m, decimal price = 100m, Guid? taxClass = null) =>
        new(_customerId, _branchId, Guid.NewGuid(), kind, "Line " + lineRef, quantity, new Money(price, "BDT"), new Money(0m, "BDT"), taxClass ?? _taxClassId,
            ServiceDate, lineRef, null, null);

    private CaptureSourceChargesCommandHandler Handler(IBillableItemSource source) =>
        new(new FakeBillableItemSourceResolver(source), _charges, _pricing, BillingTestSettings.Defaults(), _time);

    private static CaptureSourceChargesCommand Capture(ChargeSourceKind kind, Guid sourceId) => new()
    { SourceKind = kind.DisplayName, SourceId = sourceId, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

    // ── idempotent per source ────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(PhaseJKinds))]
    public async Task EachPhaseJKind_IsCapturedOncePerLine_AReplayRecordsNone_AndEveryChargeIsClinicBDT(ChargeSourceKind kind)
    {
        var sourceId = Guid.NewGuid();
        var source = new FakeBillableItemSource(kind, BillableSourceResult.Billable([
            Item("treatment:a", ItemKind.Service), Item("treatment:b", ItemKind.Product, 2m, 12.5m), Item("diagnostic:c", ItemKind.Service, price: 0m)]));
        var handler = Handler(source);

        var first = (CaptureSourceChargesResult)(await handler.HandleAsync(Capture(kind, sourceId))).Result;
        var replay = (CaptureSourceChargesResult)(await handler.HandleAsync(Capture(kind, sourceId))).Result;
        var again = (CaptureSourceChargesResult)(await handler.HandleAsync(Capture(kind, sourceId))).Result;

        Assert.Equal(3, first.Captured);
        Assert.Equal(0, replay.Captured);
        Assert.Equal(0, again.Captured);
        Assert.Equal(3, _charges.Saved.Count);
        Assert.Equal(3, _charges.Saved.Select(c => c.SourceLineRef).Distinct().Count());
        Assert.All(_charges.Saved, c =>
        {
            Assert.Equal(kind, c.SourceKind);
            Assert.Equal(sourceId, c.SourceId);
            Assert.Equal(BusinessLine.Clinic, c.BusinessLine); // ChargeRules.DefaultBusinessLine: Visit / Dispense / Surgery are Clinic
            Assert.Equal("BDT", c.CurrencyCode);
            Assert.Equal(ServiceDate, c.ServiceDate);
            Assert.Equal(_customerId, c.CustomerId);
        });
    }

    public static IEnumerable<object[]> PhaseJKinds() =>
    [
        [ChargeSourceKind.Visit], [ChargeSourceKind.Dispense], [ChargeSourceKind.Surgery],
    ];

    [Fact]
    public async Task AReplayAfterAPartialCapture_RecordsOnlyTheMissingLines()
    {
        var sourceId = Guid.NewGuid();
        var full = new FakeBillableItemSource(ChargeSourceKind.Visit, BillableSourceResult.Billable([Item("treatment:a", ItemKind.Service), Item("treatment:b", ItemKind.Product)]));
        var partial = new FakeBillableItemSource(ChargeSourceKind.Visit, BillableSourceResult.Billable([Item("treatment:a", ItemKind.Service)]));

        await Handler(partial).HandleAsync(Capture(ChargeSourceKind.Visit, sourceId));
        var rest = (CaptureSourceChargesResult)(await Handler(full).HandleAsync(Capture(ChargeSourceKind.Visit, sourceId))).Result;

        Assert.Equal(1, rest.Captured);
        Assert.Equal(["treatment:a", "treatment:b"], _charges.Saved.Select(c => c.SourceLineRef).Order().ToArray());
    }

    // ── nothing is invented ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WhatTheSourceCouldNotPrice_IsReportedBesideTheHandlersOwnSkips_NotCaptured()
    {
        var source = new FakeBillableItemSource(ChargeSourceKind.Dispense, BillableSourceResult.Billable(
            [Item("line:a", ItemKind.Medicine), Item("line:b", ItemKind.Medicine, taxClass: Guid.NewGuid())],
            [BusinessViolationCodes.SkuNotPriced]));

        var result = (CaptureSourceChargesResult)(await Handler(source).HandleAsync(Capture(ChargeSourceKind.Dispense, Guid.NewGuid()))).Result;

        Assert.Equal(1, result.Captured);
        Assert.Equal([BusinessViolationCodes.SkuNotPriced, BusinessViolationCodes.TaxClassNotFound], result.Skipped.Select(s => s.Reason).Order().ToArray());
        Assert.Equal("line:a", Assert.Single(_charges.Saved).SourceLineRef);
    }

    [Fact]
    public async Task ASourceWithNothingToBill_SucceedsWithZeroCaptured()
    {
        var source = new FakeBillableItemSource(ChargeSourceKind.Visit, BillableSourceResult.Billable([]));

        var response = await Handler(source).HandleAsync(Capture(ChargeSourceKind.Visit, Guid.NewGuid()));

        Assert.True(response.IsSuccess);
        Assert.Equal(0, ((CaptureSourceChargesResult)response.Result).Captured);
        Assert.Empty(_charges.Saved);
    }

    [Fact]
    public async Task AVisitNotFinalisedOrWithoutARecordedOwner_IsNotBillable_WithTheReason()
    {
        foreach (var reason in new[] { "VisitNotFinalised", "VisitCustomerNotRecorded" })
        {
            var source = new FakeBillableItemSource(ChargeSourceKind.Visit, BillableSourceResult.NotBillable(reason));

            var result = (CaptureSourceChargesResult)(await Handler(source).HandleAsync(Capture(ChargeSourceKind.Visit, Guid.NewGuid()))).Result;

            Assert.Equal(0, result.Captured);
            Assert.Equal(reason, Assert.Single(result.Skipped).Reason);
        }

        Assert.Empty(_charges.Saved);
    }

    [Fact]
    public async Task APhaseJLine_KeepsItsSnapshotUnitPriceAndQuantity_SoTheInvoiceRoundsOnceAtTheLine()
    {
        // 0.5 x 0.01 = 0.005 -> 0.01 (half away from zero, ADR-181); the charge stores the factors, never a pre-rounded total.
        var source = new FakeBillableItemSource(ChargeSourceKind.Visit, BillableSourceResult.Billable([Item("treatment:r", ItemKind.Product, 0.5m, 0.01m)]));

        await Handler(source).HandleAsync(Capture(ChargeSourceKind.Visit, Guid.NewGuid()));

        var charge = Assert.Single(_charges.Saved);
        Assert.Equal(0.5m, charge.Quantity);
        Assert.Equal(0.01m, charge.UnitPriceAmount);
        Assert.Equal(0.01m, InvoiceCalculator.Round(charge.Quantity * charge.UnitPriceAmount));
    }

    // ── a capture that breaks never fails the source, and logs ids only ──────────────────────────

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new ThrowingScope();

        private sealed class ThrowingScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ThrowingProvider();
            public void Dispose() { }
        }

        private sealed class ThrowingProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => throw new InvalidOperationException("secret detail that must never be logged");
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + (exception is null ? "" : " | " + exception)));
    }

    private static void AssertLoggedIdsOnly<T>(CapturingLogger<T> logger, Guid sourceId)
    {
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(sourceId.ToString(), entry.Message);
        Assert.DoesNotContain("secret detail", entry.Message);
    }

    [Fact]
    public async Task AVisitFinalisedCapture_ThatThrows_IsSwallowed_AndLoggedWithIdsOnly()
    {
        var logger = new CapturingLogger<CaptureChargesOnVisitFinalisedHandler>();
        var visitId = Guid.NewGuid();

        await new CaptureChargesOnVisitFinalisedHandler(new ThrowingScopeFactory(), logger).HandleAsync(new VisitFinalisedEvent
        {
            VisitId = visitId, AppointmentId = Guid.NewGuid(), CatId = Guid.NewGuid(), BranchId = _branchId, CustomerId = _customerId,
            FinalisedBy = Guid.NewGuid(), FinalisedOn = _time.GetUtcNow(), UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        });

        AssertLoggedIdsOnly(logger, visitId);
    }

    [Fact]
    public async Task ADispensedCapture_ThatThrows_IsSwallowed_AndLoggedWithIdsOnly()
    {
        var logger = new CapturingLogger<CaptureChargesOnPrescriptionDispensedHandler>();
        var dispensingId = Guid.NewGuid();

        await new CaptureChargesOnPrescriptionDispensedHandler(new ThrowingScopeFactory(), logger).HandleAsync(new PrescriptionDispensedEvent
        {
            DispensingId = dispensingId, PrescriptionId = Guid.NewGuid(), BranchId = _branchId, CustomerId = _customerId, CatId = Guid.NewGuid(),
            VisitId = Guid.NewGuid(), SupplyNumber = 1, Lines = [], UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        });

        AssertLoggedIdsOnly(logger, dispensingId);
    }

    [Fact]
    public async Task ASurgeryDischargedCapture_ThatThrows_IsSwallowed_AndLoggedWithIdsOnly()
    {
        var logger = new CapturingLogger<CaptureChargesOnSurgeryDischargedHandler>();
        var surgeryId = Guid.NewGuid();

        await new CaptureChargesOnSurgeryDischargedHandler(new ThrowingScopeFactory(), logger).HandleAsync(new SurgeryDischargedEvent
        {
            SurgeryId = surgeryId, CatId = Guid.NewGuid(), BranchId = _branchId, CustomerId = _customerId, ServiceId = Guid.NewGuid(),
            DischargedOn = _time.GetUtcNow(), PricingBasis = SurgeryPricingBasis.Standard, CatWasDeceased = false,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        });

        AssertLoggedIdsOnly(logger, surgeryId);
    }

    [Fact]
    public async Task AReversalVoid_ThatThrows_IsSwallowed_AndLoggedWithIdsOnly()
    {
        var logger = new CapturingLogger<VoidChargesOnDispensingReversedHandler>();
        var dispensingId = Guid.NewGuid();

        await new VoidChargesOnDispensingReversedHandler(new ThrowingScopeFactory(), logger).HandleAsync(new DispensingReversedEvent
        {
            DispensingId = dispensingId, PrescriptionId = Guid.NewGuid(), BranchId = _branchId, CustomerId = _customerId, CatId = Guid.NewGuid(), SupplyNumber = 1,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        });

        AssertLoggedIdsOnly(logger, dispensingId);
    }
}
