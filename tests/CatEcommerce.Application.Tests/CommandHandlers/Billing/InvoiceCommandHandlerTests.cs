using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.Application.Tests.CommandHandlers.Billing;

/// <summary>HO-BIL-001 §11.4 C5, C6, C9 -- the pre-checks every invoice-creating handler shares (charges are active, belong to
/// one customer at one branch, are in the invoice currency and are not already billed), and the C9 selector shape.</summary>
public sealed class InvoiceCommandHandlerTests
{
    private readonly FakeAggregateRootRepository<InvoiceAggregate> _invoices = new();
    private readonly FakeAggregateRootRepository<ChargeAggregate> _charges = new();
    private readonly FakeAggregateRootRepository<CustomerAccountAggregate> _customers = new();
    private readonly FakeBillingFinder _finder = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero));
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _taxClassId = Guid.NewGuid();
    private readonly Guid _customerId;

    public InvoiceCommandHandlerTests()
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Jane", MobileNumber.Create("01712345678"), TestUserContext.Create(), Guid.NewGuid()));
        _customers.Seed(customer);
        _customerId = customer.Id;
    }

    private ChargeAggregate SeedCharge(Guid? customerId = null, Guid? branchId = null, string currency = "BDT", bool voided = false)
    {
        var charge = new ChargeAggregate(Guid.NewGuid());
        var actor = new BillingActor(TestUserContext.Create(), Guid.NewGuid());
        charge.Record(new ChargeRequestDto(
            customerId ?? _customerId, branchId ?? _branchId, null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, Guid.NewGuid(), "main", null, null,
            "Cat food", 1m, new Money(100m, currency), new Money(0m, currency), _taxClassId, new DateOnly(2026, 10, 7)), new ChargeRecordContext(currency, _time.GetUtcNow(), actor));
        if (voided) charge.Void("test", _time.GetUtcNow(), actor);
        _charges.Seed(charge);
        return charge;
    }

    private CreateDraftInvoiceCommandHandler CreateHandler() => new(_invoices, _charges, _customers, _finder, BillingTestSettings.Defaults(), _time);

    private CreateDraftInvoiceCommand Create(params Guid[] chargeIds) => new()
    {
        InvoiceId = Guid.NewGuid(), CustomerId = _customerId, BranchId = _branchId, ChargeIds = chargeIds,
        UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
    };

    private static string CodeOf(Platform.Infrastructure.Core.Commands.CommandResponse response) => response.ValidationResult!.Errors.Single().ErrorCode;

    [Fact]
    public async Task C5_ADraftIsCreatedFromActiveChargesOfTheCustomer_EachLineASnapshot()
    {
        var charge = SeedCharge();

        var response = await CreateHandler().HandleAsync(Create(charge.Id));

        Assert.True(response.IsSuccess);
        var invoice = Assert.Single(_invoices.Saved);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(charge.Id, line.ChargeId);
        Assert.Equal(100m, line.UnitPrice);
        Assert.Equal(_taxClassId, line.TaxClassId);
        Assert.Equal("BDT", invoice.CurrencyCode);
    }

    [Fact]
    public async Task C5_RefusesAChargeThatIsVoided_Missing_OfAnotherCustomer_OfAnotherBranch_OrBilled()
    {
        Assert.Equal(BusinessViolationCodes.ChargeNotActive, CodeOf(await CreateHandler().HandleAsync(Create(SeedCharge(voided: true).Id))));
        Assert.Equal(BusinessViolationCodes.ChargeNotFound, CodeOf(await CreateHandler().HandleAsync(Create(Guid.NewGuid()))));
        Assert.Equal(BusinessViolationCodes.ChargeNotFound, CodeOf(await CreateHandler().HandleAsync(Create(SeedCharge(branchId: Guid.NewGuid()).Id))));
        Assert.Equal(BusinessViolationCodes.ChargesMixedCustomerOrBranch, CodeOf(await CreateHandler().HandleAsync(Create(SeedCharge(customerId: Guid.NewGuid()).Id))));

        var billed = SeedCharge();
        _finder.BilledChargeIds.Add(billed.Id);
        Assert.Equal(BusinessViolationCodes.ChargeAlreadyInvoiced, CodeOf(await CreateHandler().HandleAsync(Create(billed.Id))));

        Assert.Equal(BusinessViolationCodes.CurrencyMismatch, CodeOf(await CreateHandler().HandleAsync(Create(SeedCharge(currency: "USD").Id))));
        Assert.Empty(_invoices.Saved);
    }

    [Fact]
    public async Task C5_RefusesNoCharges_AndMoreThanFiveHundred()
    {
        Assert.Equal(BusinessViolationCodes.ChargeNotFound, CodeOf(await CreateHandler().HandleAsync(Create())));

        var tooMany = Enumerable.Range(0, BillingRules.MaxInvoiceLines + 1).Select(_ => Guid.NewGuid()).ToArray();
        Assert.Equal(BusinessViolationCodes.InvoiceTooManyLines, CodeOf(await CreateHandler().HandleAsync(Create(tooMany))));
    }

    [Fact]
    public async Task C5_AnUnknownOrMergedCustomer_IsCustomerNotFound()
    {
        var charge = SeedCharge();
        var response = await CreateHandler().HandleAsync(Create(charge.Id) is var c ? new CreateDraftInvoiceCommand
        {
            InvoiceId = c.InvoiceId, CustomerId = Guid.NewGuid(), BranchId = _branchId, ChargeIds = c.ChargeIds, UserContext = c.UserContext, CorrelationId = c.CorrelationId,
        } : c);

        Assert.Equal(BusinessViolationCodes.CustomerNotFound, CodeOf(response));
    }

    // ── C6 ──────────────────────────────────────────────────────────────────────────────────────────
    private SetDraftInvoiceChargesCommandHandler SetHandler() => new(_invoices, _charges, _finder, BillingTestSettings.Defaults(), new TestLoyalty(_invoices).Dependencies, _time);

    private async Task<InvoiceAggregate> SeededDraftAsync(params ChargeAggregate[] charges)
    {
        await CreateHandler().HandleAsync(Create(charges.Select(c => c.Id).ToArray()));
        return _invoices.Saved.Last();
    }

    [Fact]
    public async Task C6_ReplacesTheCharges_OfADraft_AtTheCurrentVersion_IgnoringItsOwnCharges()
    {
        var a = SeedCharge();
        var b = SeedCharge();
        var draft = await SeededDraftAsync(a);
        _finder.BilledChargeIds.Add(a.Id); // a is "billed" -- on THIS draft; keeping it must not be refused

        var response = await SetHandler().HandleAsync(new SetDraftInvoiceChargesCommand
        {
            InvoiceId = draft.Id, BranchId = _branchId, ExpectedVersion = draft.Version, ChargeIds = [a.Id, b.Id],
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(2, draft.Lines.Count);
    }

    [Fact]
    public async Task C6_AStaleVersion_AnotherBranch_AndAnIssuedInvoice_AreRefused()
    {
        var a = SeedCharge();
        var draft = await SeededDraftAsync(a);

        async Task<string> Attempt(Guid branchId, int version) => CodeOf(await SetHandler().HandleAsync(new SetDraftInvoiceChargesCommand
        {
            InvoiceId = draft.Id, BranchId = branchId, ExpectedVersion = version, ChargeIds = [a.Id], UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        }));

        Assert.Equal(BusinessViolationCodes.StaleVersion, await Attempt(_branchId, draft.Version + 5));
        Assert.Equal(BusinessViolationCodes.InvoiceNotFound, await Attempt(Guid.NewGuid(), draft.Version));
    }

    // ── Discard ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Discard_MarksTheDraft_AndRefusesAnotherBranchOrASecondDiscard()
    {
        var draft = await SeededDraftAsync(SeedCharge());
        var handler = new DiscardDraftInvoiceCommandHandler(_invoices, new TestLoyalty(_invoices).Dependencies, _time);

        DiscardDraftInvoiceCommand Discard(Guid branchId) => new() { InvoiceId = draft.Id, BranchId = branchId, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

        Assert.Equal(BusinessViolationCodes.InvoiceNotFound, CodeOf(await handler.HandleAsync(Discard(Guid.NewGuid()))));
        Assert.True((await handler.HandleAsync(Discard(_branchId))).IsSuccess);
        Assert.Equal(InvoiceStatus.Discarded, draft.Status);
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, CodeOf(await handler.HandleAsync(Discard(_branchId))));
    }

    // ── C9 selector ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void C9_TheValidator_RequiresExactlyOneSelectorForm_AndBoundsEachOne()
    {
        var validator = new IssueInvoiceFromChargesCommandValidator();
        IssueInvoiceFromChargesCommand With(InvoiceChargeSelector selector, string? key = null) => new()
        {
            InvoiceId = Guid.NewGuid(), CustomerId = _customerId, BranchId = _branchId, Selector = selector, IdempotencyKey = key,
            UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid(),
        };

        Assert.True(validator.Validate(With(new InvoiceChargeSelector(ChargeIds: [Guid.NewGuid()]))).IsValid);
        Assert.True(validator.Validate(With(new InvoiceChargeSelector(Source: new SourceChargeSelector("Order", Guid.NewGuid())))).IsValid);
        Assert.True(validator.Validate(With(new InvoiceChargeSelector(BillingGroup: new BillingGroupChargeSelector("BoardingStay", Guid.NewGuid(), new DateOnly(2026, 10, 7))))).IsValid);

        Assert.False(validator.Validate(With(new InvoiceChargeSelector())).IsValid);
        Assert.False(validator.Validate(With(new InvoiceChargeSelector(ChargeIds: [Guid.NewGuid()], Source: new SourceChargeSelector("Order", Guid.NewGuid())))).IsValid);
        Assert.False(validator.Validate(With(new InvoiceChargeSelector(Source: new SourceChargeSelector("Nonsense", Guid.NewGuid())))).IsValid);
        Assert.False(validator.Validate(With(new InvoiceChargeSelector(BillingGroup: new BillingGroupChargeSelector("Nope", Guid.NewGuid())))).IsValid);
        Assert.False(validator.Validate(With(new InvoiceChargeSelector(ChargeIds: []))).IsValid);
        Assert.False(validator.Validate(With(new InvoiceChargeSelector(ChargeIds: [Guid.NewGuid()]), key: new string('k', BillingRules.MaxIdempotencyKeyLength + 1))).IsValid);
    }

    [Fact]
    public void TheDraftValidators_RejectEmptyIdsAndTooManyCharges()
    {
        var create = new CreateDraftInvoiceCommandValidator();
        Assert.True(create.Validate(Create(Guid.NewGuid())).IsValid);
        Assert.False(create.Validate(Create()).IsValid);
        Assert.False(create.Validate(Create(Guid.Empty)).IsValid);
        Assert.False(create.Validate(Create(Enumerable.Range(0, BillingRules.MaxChargeIdsPerRequest + 1).Select(_ => Guid.NewGuid()).ToArray())).IsValid);
    }
}
