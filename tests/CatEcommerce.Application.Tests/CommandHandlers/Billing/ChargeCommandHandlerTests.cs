using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DomainEventHandlers.Billing;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Billing;

/// <summary>HO-BIL-001 §15 group B (U-B1, U-B5, U-B6) at the handler level -- manual charges, voids and the capture
/// pipeline (ADR-178).</summary>
public sealed class ChargeCommandHandlerTests
{
    private static readonly DateOnly ServiceDate = new(2026, 10, 7);

    private readonly FakeAggregateRootRepository<CustomerAccountAggregate> _customers = new();
    private readonly FakeAggregateRootRepository<ChargeAggregate> _charges = new();
    private readonly FakeBillingPricingInputs _pricing = new();
    private readonly FakeBillingFinder _finder = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero));
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _taxClassId = Guid.NewGuid();

    public ChargeCommandHandlerTests() => _pricing.AddTaxClass(_taxClassId);

    private Guid SeedCustomer(bool merged = false)
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Jane", MobileNumber.Create("0171" + Random.Shared.NextInt64(1_000_000, 9_999_999)), TestUserContext.Create(), Guid.NewGuid()));
        _customers.Seed(customer);
        return customer.Id;
    }

    private RecordManualChargeCommandHandler ManualHandler() => new(_customers, _charges, _pricing, BillingTestSettings.Defaults(), _time);

    private RecordManualChargeCommand Fee(Guid customerId, decimal price = 250m, decimal quantity = 2m, decimal discount = 0m, string? line = null) => new()
    {
        ChargeId = Guid.NewGuid(), CustomerId = customerId, BranchId = _branchId, Description = "Late pickup fee", UnitPrice = price,
        TaxClassId = _taxClassId, Quantity = quantity, LineDiscount = discount, ServiceDate = ServiceDate, BusinessLine = line,
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
    };

    // ── C2 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task C2_AFreeTextFee_IsRecordedAsAFeeLine_AtTheEnteredPrice()
    {
        var customerId = SeedCustomer();

        var response = await ManualHandler().HandleAsync(Fee(customerId));

        Assert.True(response.IsSuccess);
        var charge = Assert.Single(_charges.Saved);
        Assert.Equal(ItemKind.Fee, charge.ItemKind);
        Assert.Equal(ChargeSourceKind.Manual, charge.SourceKind);
        Assert.Equal(charge.Id, charge.SourceId);
        Assert.Equal(BusinessLine.Clinic, charge.BusinessLine);
        Assert.Equal(250m, charge.UnitPriceAmount);
        Assert.Equal(_branchId, charge.BranchId);
    }

    [Fact]
    public async Task C2_AServiceCharge_ReadsThePriceAndTaxClassAtNow()
    {
        var customerId = SeedCustomer();
        var serviceId = Guid.NewGuid();
        _pricing.Services[serviceId] = new ServicePriceInfo(serviceId, "Vaccination", true, new Money(800m, "BDT"), _taxClassId);

        var command = new RecordManualChargeCommand
        {
            ChargeId = Guid.NewGuid(), CustomerId = customerId, BranchId = _branchId, ServiceId = serviceId, Quantity = 1m, ServiceDate = ServiceDate,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        };

        var response = await ManualHandler().HandleAsync(command);

        Assert.True(response.IsSuccess);
        var charge = Assert.Single(_charges.Saved);
        Assert.Equal("Vaccination", charge.Description);
        Assert.Equal(ItemKind.Service, charge.ItemKind);
        Assert.Equal(800m, charge.UnitPriceAmount);
        Assert.Equal(_taxClassId, charge.TaxClassId);
    }

    [Fact]
    public async Task C2_AnUnknownOrInactiveOrUnpricedService_IsRefused()
    {
        var customerId = SeedCustomer();
        var inactive = Guid.NewGuid();
        var unpriced = Guid.NewGuid();
        _pricing.Services[inactive] = new ServicePriceInfo(inactive, "Old", false, new Money(1m, "BDT"), _taxClassId);
        _pricing.Services[unpriced] = new ServicePriceInfo(unpriced, "New", true, null, _taxClassId);

        async Task<string> CodeFor(Guid serviceId) => (await ManualHandler().HandleAsync(new RecordManualChargeCommand
        {
            ChargeId = Guid.NewGuid(), CustomerId = customerId, BranchId = _branchId, ServiceId = serviceId, Quantity = 1m, ServiceDate = ServiceDate,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        })).ValidationResult!.Errors.Single().ErrorCode;

        Assert.Equal(BusinessViolationCodes.ServiceNotFound, await CodeFor(Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.ServiceNotFound, await CodeFor(inactive));
        Assert.Equal(BusinessViolationCodes.ServiceNotPriced, await CodeFor(unpriced));
        Assert.Empty(_charges.Saved);
    }

    [Fact]
    public async Task C2_AnUnknownCustomer_AnUnknownTaxClass_AndABadQuantity_AreReturnedNotThrown()
    {
        var customerId = SeedCustomer();

        var noCustomer = await ManualHandler().HandleAsync(Fee(Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.CustomerNotFound, noCustomer.ValidationResult!.Errors.Single().ErrorCode);

        _pricing.TaxClasses.Clear();
        var missingClass = await ManualHandler().HandleAsync(Fee(customerId));
        Assert.Equal(BusinessViolationCodes.TaxClassNotFound, missingClass.ValidationResult!.Errors.Single().ErrorCode);

        _pricing.AddTaxClass(_taxClassId);
        var badQuantity = await ManualHandler().HandleAsync(Fee(customerId, quantity: 0.0001m));
        Assert.Equal(BusinessViolationCodes.InvalidQuantity, badQuantity.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task C2_OnlyAManualChargeMayChooseItsBusinessLine()
    {
        var customerId = SeedCustomer();

        var response = await ManualHandler().HandleAsync(Fee(customerId, line: "Shop"));

        Assert.True(response.IsSuccess);
        Assert.Equal(BusinessLine.Shop, _charges.Saved.Single().BusinessLine);
    }

    [Fact]
    public void C2_TheValidator_RequiresExactlyOneForm_AndBoundsTheShape()
    {
        var validator = new RecordManualChargeCommandValidator(_time);
        var ok = Fee(Guid.NewGuid());
        Assert.True(validator.Validate(ok).IsValid);

        Assert.False(validator.Validate(Fee(Guid.NewGuid(), price: -1m)).IsValid);
        Assert.False(validator.Validate(Fee(Guid.NewGuid(), price: 10.005m)).IsValid);
        Assert.False(validator.Validate(Fee(Guid.NewGuid(), quantity: 0m)).IsValid);
        Assert.False(validator.Validate(Fee(Guid.NewGuid(), quantity: 1.0001m)).IsValid);
        Assert.False(validator.Validate(Fee(Guid.NewGuid(), line: "Nowhere")).IsValid);

        var both = new RecordManualChargeCommand
        {
            ChargeId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), BranchId = _branchId, ServiceId = Guid.NewGuid(), Description = "x", UnitPrice = 1m,
            TaxClassId = _taxClassId, Quantity = 1m, ServiceDate = ServiceDate, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        };
        Assert.False(validator.Validate(both).IsValid);

        var neither = new RecordManualChargeCommand
        {
            ChargeId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), BranchId = _branchId, Quantity = 1m, ServiceDate = ServiceDate,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        };
        Assert.False(validator.Validate(neither).IsValid);
    }

    // ── C3 ──────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task C3_VoidingAnotherBranchsCharge_IsNotFound_AndAnActiveOneIsVoidedWithItsReason()
    {
        var customerId = SeedCustomer();
        await ManualHandler().HandleAsync(Fee(customerId));
        var charge = _charges.Saved.Single();
        var handler = new VoidChargeCommandHandler(_finder, _charges, _time);

        var elsewhere = await handler.HandleAsync(new VoidChargeCommand { ChargeId = charge.Id, BranchId = Guid.NewGuid(), Reason = "x", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        Assert.Equal(BusinessViolationCodes.ChargeNotFound, elsewhere.ValidationResult!.Errors.Single().ErrorCode);
        Assert.Equal(ChargeStatus.Active, charge.Status);

        var voided = await handler.HandleAsync(new VoidChargeCommand { ChargeId = charge.Id, BranchId = _branchId, Reason = "Entered twice", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        Assert.True(voided.IsSuccess);
        Assert.Equal(ChargeStatus.Voided, charge.Status);

        var again = await handler.HandleAsync(new VoidChargeCommand { ChargeId = charge.Id, BranchId = _branchId, Reason = "Again", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });
        Assert.Equal(BusinessViolationCodes.ChargeNotActive, again.ValidationResult!.Errors.Single().ErrorCode);
    }

    // ── C4 ──────────────────────────────────────────────────────────────────────────────────────────
    private BillableSourceItem Item(Guid customerId, Guid? taxClass, string lineRef = "service", decimal price = 800m) =>
        new(customerId, _branchId, Guid.NewGuid(), ItemKind.Service, "Consultation", 1m, new Money(price, "BDT"), new Money(0m, "BDT"), taxClass, ServiceDate, lineRef, null, null);

    private CaptureSourceChargesCommandHandler CaptureHandler(IBillableItemSource source) =>
        new(new FakeBillableItemSourceResolver(source), _charges, _pricing, BillingTestSettings.Defaults(), _time);

    private static CaptureSourceChargesCommand Capture(Guid sourceId, Guid? branchId = null) => new()
    { SourceKind = "Appointment", SourceId = sourceId, BranchId = branchId, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

    [Fact]
    public async Task C4_CapturesTheMissingItems_AndAReRunRecordsNone_U_B6()
    {
        var sourceId = Guid.NewGuid();
        var source = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.Billable([Item(SeedCustomer(), _taxClassId)]));
        var handler = CaptureHandler(source);

        var first = (CaptureSourceChargesResult)(await handler.HandleAsync(Capture(sourceId))).Result;
        var second = (CaptureSourceChargesResult)(await handler.HandleAsync(Capture(sourceId))).Result;

        Assert.Equal(1, first.Captured);
        Assert.Equal(0, second.Captured);
        var charge = Assert.Single(_charges.Saved);
        Assert.Equal(ChargeSourceKind.Appointment, charge.SourceKind);
        Assert.Equal(sourceId, charge.SourceId);
        Assert.Equal("service", charge.SourceLineRef);
        Assert.Equal(BusinessLine.Clinic, charge.BusinessLine);
        Assert.Equal(800m, charge.UnitPriceAmount);
    }

    [Fact]
    public async Task C4_AnItemWhoseTaxClassIsMissing_IsSkippedAndReported_NeverGuessed()
    {
        var source = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.Billable(
            [Item(SeedCustomer(), Guid.NewGuid(), "a"), Item(SeedCustomer(), null, "b"), Item(SeedCustomer(), _taxClassId, "c")]));

        var result = (CaptureSourceChargesResult)(await CaptureHandler(source).HandleAsync(Capture(Guid.NewGuid()))).Result;

        Assert.Equal(1, result.Captured);
        Assert.Equal(2, result.Skipped.Count);
        Assert.All(result.Skipped, s => Assert.Equal(BusinessViolationCodes.TaxClassNotFound, s.Reason));
        Assert.Equal("c", Assert.Single(_charges.Saved).SourceLineRef);
    }

    [Fact]
    public async Task C4_AFreeItem_IsCaptured_AsAZeroLine()
    {
        var source = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.Billable([Item(SeedCustomer(), _taxClassId, price: 0m)]));

        var result = (CaptureSourceChargesResult)(await CaptureHandler(source).HandleAsync(Capture(Guid.NewGuid()))).Result;

        Assert.Equal(1, result.Captured);
        Assert.Equal(0m, _charges.Saved.Single().UnitPriceAmount);
    }

    [Fact]
    public async Task C4_ASourceNotYetBillable_SucceedsWithZeroCapturedAndTheReason()
    {
        var source = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.NotBillable("AppointmentNotCompleted"));

        var response = await CaptureHandler(source).HandleAsync(Capture(Guid.NewGuid()));

        Assert.True(response.IsSuccess);
        var result = (CaptureSourceChargesResult)response.Result;
        Assert.Equal(0, result.Captured);
        Assert.Equal("AppointmentNotCompleted", Assert.Single(result.Skipped).Reason);
        Assert.Empty(_charges.Saved);
    }

    [Fact]
    public async Task C4_ASourceThatDoesNotExist_OrBelongsToAnotherBranch_IsSourceNotFound()
    {
        var gone = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.NotFound);
        var response = await CaptureHandler(gone).HandleAsync(Capture(Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.SourceNotFound, response.ValidationResult!.Errors.Single().ErrorCode);

        var other = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.Billable([Item(SeedCustomer(), _taxClassId)]));
        var wrongBranch = await CaptureHandler(other).HandleAsync(Capture(Guid.NewGuid(), branchId: Guid.NewGuid()));
        Assert.Equal(BusinessViolationCodes.SourceNotFound, wrongBranch.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task C4_AKindBillingCannotCapture_IsRefused()
    {
        var handler = new CaptureSourceChargesCommandHandler(new FakeBillableItemSourceResolver(), _charges, _pricing, BillingTestSettings.Defaults(), _time);

        var response = await handler.HandleAsync(new CaptureSourceChargesCommand { SourceKind = "Order", SourceId = Guid.NewGuid(), UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(BusinessViolationCodes.SourceKindNotCapturable, response.ValidationResult!.Errors.Single().ErrorCode);
    }

    [Fact]
    public async Task C4_AnItemInAnotherCurrency_IsSkipped_WithTheViolationCode()
    {
        var foreign = Item(SeedCustomer(), _taxClassId) with { UnitPrice = new Money(10m, "USD"), LineDiscount = new Money(0m, "USD") };
        var source = new FakeBillableItemSource(ChargeSourceKind.Appointment, BillableSourceResult.Billable([foreign]));

        var result = (CaptureSourceChargesResult)(await CaptureHandler(source).HandleAsync(Capture(Guid.NewGuid()))).Result;

        Assert.Equal(0, result.Captured);
        Assert.Equal(BusinessViolationCodes.CurrencyMismatch, Assert.Single(result.Skipped).Reason);
    }

    // ── U-B5 ────────────────────────────────────────────────────────────────────────────────────────
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

    private sealed class CapturingLogger : ILogger<CaptureChargesOnAppointmentCompletedHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + (exception is null ? "" : " | " + exception)));
    }

    [Fact]
    public async Task U_B5_ACaptureThatThrows_IsSwallowed_AndLoggedAsAnErrorWithIdsOnly()
    {
        var logger = new CapturingLogger();
        var handler = new CaptureChargesOnAppointmentCompletedHandler(new ThrowingScopeFactory(), logger);
        var appointmentId = Guid.NewGuid();

        await handler.HandleAsync(new AppointmentCompletedEvent
        {
            AppointmentId = appointmentId, BranchId = Guid.NewGuid(), CustomerId = Guid.NewGuid(), CatId = Guid.NewGuid(),
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        });

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(appointmentId.ToString(), entry.Message);
        Assert.DoesNotContain("secret detail", entry.Message);
    }
}
