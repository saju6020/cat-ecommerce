using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Tests.CommandHandlers.Billing;

/// <summary>HO-BIL-001 §11.4 C10, C11, C12, C14, C15 at the handler level -- the posting sequence, idempotency, the threshold and the
/// approver rule, allocation caps at approval, reversal releasing invoices, and the message rules (ADR-183, 187, 193).</summary>
public sealed class PaymentCommandHandlerTests
{
    private readonly FakeAggregateRootRepository<PaymentAggregate> _payments = new();
    private readonly FakeAggregateRootRepository<InvoiceAggregate> _invoices = new();
    private readonly FakeAggregateRootRepository<CustomerAccountAggregate> _customers = new();
    private readonly FakeAggregateRootRepository<StaffAccountAggregate> _staff = new();
    private readonly FakeAggregateRootRepository<BranchAggregate> _branches = new();
    private readonly FakeBillingFinder _finder = new();
    private readonly FakeNotificationOutbox _outbox = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero));
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId;
    private readonly UserContext _recorder = TestUserContext.Create();

    public PaymentCommandHandlerTests()
    {
        var branch = new BranchAggregate(_branchId);
        branch.Create(new BranchCreateDto("MAIN", "Main", "Asia/Dhaka", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        _branches.Seed(branch);

        _customerId = SeedCustomer().Id;
    }

    private CustomerAccountAggregate SeedCustomer(Guid? mergedInto = null)
    {
        var customer = new CustomerAccountAggregate(Guid.NewGuid());
        customer.RegisterWalkIn(new WalkInCustomerRegistrationDto("Jane", MobileNumber.Create("0171" + Random.Shared.NextInt64(1_000_000, 9_999_999)), TestUserContext.Create(), Guid.NewGuid()));
        _customers.Seed(customer);
        return customer;
    }

    private InvoiceAggregate SeedIssuedInvoice(Guid? customerId = null, decimal total = 1000m, string number = "INV-MAIN-2627-000001", ChargeSourceKind? source = null, Guid? sourceId = null)
    {
        var taxClass = Guid.NewGuid();
        var invoice = new InvoiceAggregate(Guid.NewGuid());
        var actor = new BillingActor(TestUserContext.Create(), Guid.NewGuid());
        invoice.CreateDraft(new DraftInvoiceDto(
            _branchId, customerId ?? _customerId, "BDT",
            [new DraftInvoiceLineInput(Guid.NewGuid(), null, BusinessLine.Shop, ItemKind.Product, source ?? ChargeSourceKind.Order, sourceId ?? Guid.NewGuid(), null, null, null, "Item", 1m, total, 0m, taxClass)],
            null, null, null, _time.GetUtcNow(), actor));
        invoice.Issue(new InvoiceIssueDto(
            number, "2026-27", 1, new Dictionary<Guid, TaxRateInfo> { [taxClass] = new(taxClass, "ZERO", 0m) },
            new SellerSnapshot("Seller", "TIN", "Addr", null, null, null, "[]"), new BillToSnapshot("Jane", null, null, null), _time.GetUtcNow(), "Asia/Dhaka", 0,
            new DateOnly(2026, 10, 7), null, actor));
        _invoices.Seed(invoice);
        return invoice;
    }

    private RecordPaymentCommandHandler Handler(decimal threshold = 50_000m) => new(
        _payments, _invoices, _customers, _staff, _branches, _finder,
        BillingTestSettings.Defaults(new Dictionary<string, object> { [SettingKeys.BillingPaymentApprovalThreshold] = threshold }),
        _outbox, new StubLinks(), _time);

    private RecordPaymentCommand Record(decimal amount = 400m, string method = "CashInStore", string? reference = null, IReadOnlyList<PaymentAllocationInput>? allocations = null,
        PaymentCollectorInput? collector = null, PaymentEarmarkInput? earmark = null, string? key = null, Guid? customerId = null) => new()
    {
        PaymentId = Guid.NewGuid(), CustomerId = customerId ?? _customerId, BranchId = _branchId, Method = method, Amount = amount, ReceivedOn = new DateOnly(2026, 10, 7),
        ExternalReference = reference, Collector = collector, Earmark = earmark, Allocations = allocations ?? [], IdempotencyKey = key,
        UserContext = _recorder, CorrelationId = Guid.NewGuid(),
    };

    private static string CodeOf(CommandResponse response) => response.ValidationResult!.Errors.Single().ErrorCode;

    private sealed class StubLinks : INotificationLinkBuilder
    {
        public string PasswordResetLink(string token) => "";
        public string CatCareLink(Guid catId, string category) => "";
        public string UnsubscribeLink(string token) => "";
        public string OneClickUnsubscribeUrl(string token) => "";
        public string AppointmentLink(Guid appointmentId) => "";
        public string VisitSummaryLink(Guid visitId) => "";
        public string BookingLink() => "";
        public string ProductLink(string slug, Guid skuId) => "";
        public string InvoiceLink(Guid invoiceId) => $"https://portal.example.test/portal/billing/invoices/{invoiceId}";
        public string ReceiptLink(Guid paymentId) => $"https://portal.example.test/portal/billing/payments/{paymentId}";
        public string OrderLink(Guid orderId) => "";
        public string PrescriptionLink(Guid prescriptionId) => "";
        public string RepeatRequestLink(Guid prescriptionId) => "";
        public string DietAuthorisationsLink() => "";
    }

    // ── C10 ─────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task C10_AnAcceptedPayment_AppliesItsAllocationsInTheSameSave_AndEnqueuesTheReceiptMessage()
    {
        var invoice = SeedIssuedInvoice(total: 1000m);

        var response = await Handler().HandleAsync(Record(400m, allocations: [new PaymentAllocationInput(invoice.Id, 400m)]));

        Assert.True(response.IsSuccess);
        var payment = Assert.Single(_payments.Saved);
        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.Equal(1L, payment.ReceiptNumber);
        Assert.Equal(600m, invoice.Outstanding);
        Assert.Equal(InvoiceStatus.PartPaid, invoice.Status);
        Assert.Equal(0m, payment.Unallocated);
        Assert.Contains(invoice, _invoices.Updated); // its events are dispatched by the follow-up call (F31(b))

        var message = Assert.Single(_outbox.Requests);
        Assert.Equal(NotificationTemplateCatalogue.BillingPaymentReceived, message.TemplateKey);
        Assert.Equal("BDT 400.00", message.Variables["amountText"]);
        Assert.Equal("R-000001", message.Variables["receiptNumber"]);
        Assert.Equal("INV-MAIN-2627-000001", message.Variables["purposeText"]);
        Assert.Equal("CashInStore", message.Variables["methodName"]);
        Assert.Equal(_customerId, message.RecipientId);
    }

    [Fact]
    public async Task C10_APaymentWithNoAllocation_IsCredit_AndSaysSo()
    {
        await Handler().HandleAsync(Record(250m));

        Assert.Equal("credit on your account", Assert.Single(_outbox.Requests).Variables["purposeText"]);
        Assert.Equal(250m, _payments.Saved.Single().Unallocated);
    }

    [Fact]
    public async Task C10_ADepositsMessage_SaysTheEarmarkLabel()
    {
        await Handler().HandleAsync(Record(500m, earmark: new PaymentEarmarkInput("BoardingStay", Guid.NewGuid(), "Deposit — boarding 12–19 Oct")));

        Assert.Equal("Deposit — boarding 12–19 Oct", Assert.Single(_outbox.Requests).Variables["purposeText"]);
    }

    [Fact]
    public async Task C10_AboveTheThreshold_WaitsForApproval_AppliesNothing_AndEnqueuesNothing()
    {
        var invoice = SeedIssuedInvoice(total: 90_000m);

        var response = await Handler(threshold: 50_000m).HandleAsync(Record(60_000m, allocations: [new PaymentAllocationInput(invoice.Id, 60_000m)]));

        Assert.True(response.IsSuccess);
        var payment = Assert.Single(_payments.Saved);
        Assert.Equal(PaymentStatus.PendingApproval, payment.Status);
        Assert.Equal(90_000m, invoice.Outstanding);
        Assert.Single(payment.RequestedAllocations);
        Assert.Empty(payment.Entries);
        Assert.Empty(_outbox.Requests);
        Assert.Empty(_invoices.Updated);
    }

    [Fact]
    public async Task C10_TheThresholdIsTheBranchesSetting_NotAConstant()
    {
        await Handler(threshold: 100m).HandleAsync(Record(101m));
        await Handler(threshold: 100m).HandleAsync(Record(100m));

        Assert.Equal([PaymentStatus.PendingApproval, PaymentStatus.Accepted], _payments.Saved.Select(p => p.Status).ToArray());
    }

    [Fact]
    public async Task C10_TheSameKeyTwice_ReturnsTheStoredPayment_AndAKeyOnADifferentCustomerConflicts()
    {
        var existing = new RecordedPaymentRef(Guid.NewGuid(), _customerId);
        _finder.IdempotentPayments[(_recorder.UserId, "k1")] = existing;

        var replay = await Handler().HandleAsync(Record(key: "k1"));
        Assert.True(replay.IsSuccess);
        var result = Assert.IsType<RecordPaymentResult>(replay.Result);
        Assert.Equal((existing.PaymentId, true), (result.PaymentId, result.Replayed));
        Assert.Empty(_payments.Saved);

        var other = SeedCustomer();
        var conflict = await Handler().HandleAsync(Record(key: "k1", customerId: other.Id));
        Assert.Equal(BusinessViolationCodes.IdempotencyKeyConflict, CodeOf(conflict));
    }

    [Fact]
    public async Task C10_RefusesAnUnknownCustomer_AnUnknownStaffCollector_AndAnInvoiceOfAnotherCustomer()
    {
        Assert.Equal(BusinessViolationCodes.CustomerNotFound, CodeOf(await Handler().HandleAsync(Record(customerId: Guid.NewGuid()))));

        var missingStaff = new PaymentCollectorInput("Staff", Guid.NewGuid());
        Assert.Equal(BusinessViolationCodes.CollectorRequired, CodeOf(await Handler().HandleAsync(Record(method: "CashOnDelivery", collector: missingStaff))));

        var someoneElses = SeedIssuedInvoice(customerId: SeedCustomer().Id);
        var mismatch = await Handler().HandleAsync(Record(400m, allocations: [new PaymentAllocationInput(someoneElses.Id, 400m)]));
        Assert.Equal(BusinessViolationCodes.AllocationInvoiceCustomerMismatch, CodeOf(mismatch));
        Assert.Equal(1000m, someoneElses.Outstanding);
    }

    [Fact]
    public async Task C10_AnInvoiceOfACustomerMergedIntoThePayer_IsAllowed()
    {
        var survivor = SeedCustomer();
        var duplicate = SeedCustomer();
        duplicate.MergeInto(new CustomerMergeDto(survivor.Id, "Duplicate", duplicate.MobileNumber, [], null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        var invoice = SeedIssuedInvoice(customerId: duplicate.Id);

        var response = await Handler().HandleAsync(Record(400m, customerId: survivor.Id, allocations: [new PaymentAllocationInput(invoice.Id, 400m)]));

        Assert.True(response.IsSuccess);
        Assert.Equal(600m, invoice.Outstanding);
    }

    [Fact]
    public async Task C10_AnAllocationAboveTheOutstanding_NamesTheAvailableAmount_AndSavesNothing()
    {
        var invoice = SeedIssuedInvoice(total: 300m);

        var response = await Handler().HandleAsync(Record(400m, allocations: [new PaymentAllocationInput(invoice.Id, 350m)]));

        var error = response.ValidationResult!.Errors.Single();
        Assert.Equal(BusinessViolationCodes.AllocationExceedsOutstanding, error.ErrorCode);
        Assert.Contains("BDT 300.00", error.ErrorMessage);
        Assert.Empty(_payments.Saved);
    }

    [Fact]
    public async Task C10_ACardLookingReference_IsRefused_AndNeverEchoed()
    {
        var response = await Handler().HandleAsync(Record(method: "CardInStore", reference: "4111 1111 1111 1111"));

        var error = response.ValidationResult!.Errors.Single();
        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, error.ErrorCode);
        Assert.DoesNotContain("4111", error.ErrorMessage);
        Assert.Empty(_payments.Saved);
        Assert.Empty(_outbox.Requests);
    }

    [Fact]
    public async Task C10_ADepositAllocatesOnlyToItsOwnStaysInvoice()
    {
        var stay = Guid.NewGuid();
        var stayInvoice = SeedIssuedInvoice(source: ChargeSourceKind.BoardingStay, sourceId: stay);
        var otherInvoice = SeedIssuedInvoice();
        var earmark = new PaymentEarmarkInput("BoardingStay", stay, "Deposit");

        var refused = await Handler().HandleAsync(Record(300m, earmark: earmark, allocations: [new PaymentAllocationInput(otherInvoice.Id, 100m)]));
        Assert.Equal(BusinessViolationCodes.EarmarkMismatch, CodeOf(refused));

        var allowed = await Handler().HandleAsync(Record(300m, earmark: earmark, allocations: [new PaymentAllocationInput(stayInvoice.Id, 100m)]));
        Assert.True(allowed.IsSuccess);
        Assert.Equal(900m, stayInvoice.Outstanding);
    }

    // ── C11 ─────────────────────────────────────────────────────────────────────────────────────────
    private ApprovePaymentCommandHandler Approver() => new(_payments, _invoices, _customers, _branches, _outbox, new StubLinks(), _time);

    private async Task<PaymentAggregate> PendingPaymentAsync(InvoiceAggregate invoice, decimal amount = 60_000m, decimal requested = 55_000m)
    {
        await Handler(50_000m).HandleAsync(Record(amount, allocations: [new PaymentAllocationInput(invoice.Id, requested)]));
        return _payments.Saved.Last();
    }

    private ApprovePaymentCommand Approve(Guid paymentId, UserContext? approver = null) => new()
    { PaymentId = paymentId, BranchId = _branchId, UserContext = approver ?? TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

    [Fact]
    public async Task C11_TheRecorderCannotApproveTheirOwnPayment()
    {
        var invoice = SeedIssuedInvoice(total: 90_000m);
        var payment = await PendingPaymentAsync(invoice);

        var response = await Approver().HandleAsync(Approve(payment.Id, _recorder));

        Assert.Equal(BusinessViolationCodes.ApproverMustDiffer, CodeOf(response));
        Assert.Equal(PaymentStatus.PendingApproval, payment.Status);
        Assert.Equal(90_000m, invoice.Outstanding);
    }

    [Fact]
    public async Task C11_AnApproval_AppliesTheRequestedAllocations_Enqueues_AndReportsWhatIsLeft()
    {
        var invoice = SeedIssuedInvoice(total: 90_000m);
        var payment = await PendingPaymentAsync(invoice);

        var response = await Approver().HandleAsync(Approve(payment.Id));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<ApprovePaymentResult>(response.Result);
        Assert.Equal(55_000m, Assert.Single(result.Applied).Amount);
        Assert.Equal(5_000m, result.LeftUnallocated);
        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.Equal(35_000m, invoice.Outstanding);
        Assert.Equal(NotificationTemplateCatalogue.BillingPaymentReceived, Assert.Single(_outbox.Requests).TemplateKey);
    }

    /// <summary>U-D6 -- the invoice's outstanding fell below the requested allocation while the payment waited: the application is
    /// capped, the remainder stays unallocated, and it is reported.</summary>
    [Fact]
    public async Task C11_WhenTheOutstandingFellBelowTheRequest_TheApplicationIsCapped_TheRemainderIsReported()
    {
        var invoice = SeedIssuedInvoice(total: 90_000m);
        var payment = await PendingPaymentAsync(invoice, requested: 55_000m);
        invoice.ApplyPayment(50_000m, new BillingActor(TestUserContext.Create(), Guid.NewGuid())); // a cash payment landed meanwhile

        var response = await Approver().HandleAsync(Approve(payment.Id));

        var result = Assert.IsType<ApprovePaymentResult>(response.Result);
        Assert.Equal(40_000m, Assert.Single(result.Applied).Amount);   // capped at the 40 000 still outstanding
        Assert.Equal(20_000m, result.LeftUnallocated);                  // 60 000 - 40 000, never silently lost
        Assert.Equal(0m, invoice.Outstanding);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [Fact]
    public async Task C11_AnotherBranchsPayment_IsNotFound()
    {
        var payment = await PendingPaymentAsync(SeedIssuedInvoice(total: 90_000m));

        var response = await Approver().HandleAsync(new ApprovePaymentCommand { PaymentId = payment.Id, BranchId = Guid.NewGuid(), UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(BusinessViolationCodes.PaymentNotFound, CodeOf(response));
    }

    [Fact]
    public async Task C11_ARejection_NeedsAReasonAndADifferentPerson_AndAppliesNothing()
    {
        var invoice = SeedIssuedInvoice(total: 90_000m);
        var payment = await PendingPaymentAsync(invoice);
        var handler = new RejectPaymentCommandHandler(_payments, _time);

        RejectPaymentCommand Reject(UserContext by, string reason) => new() { PaymentId = payment.Id, BranchId = _branchId, Reason = reason, UserContext = by, CorrelationId = Guid.NewGuid() };

        Assert.Equal(BusinessViolationCodes.ApproverMustDiffer, CodeOf(await handler.HandleAsync(Reject(_recorder, "no"))));
        Assert.Equal(BusinessViolationCodes.ReasonRequired, CodeOf(await handler.HandleAsync(Reject(TestUserContext.Create(), " "))));

        Assert.True((await handler.HandleAsync(Reject(TestUserContext.Create(), "Amount looks wrong"))).IsSuccess);
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal(90_000m, invoice.Outstanding);
        Assert.Empty(_outbox.Requests);
    }

    // ── C12 / C14 / C15 ─────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task C12_AnAcceptedPaymentsCredit_CanBeAllocatedLater_NeverBeyondItsUnallocated()
    {
        var invoice = SeedIssuedInvoice(total: 1000m);
        await Handler().HandleAsync(Record(600m));
        var payment = _payments.Saved.Single();
        var handler = new AllocatePaymentCommandHandler(_payments, _invoices, _customers, _time);

        AllocatePaymentCommand Allocate(decimal amount) => new()
        { PaymentId = payment.Id, BranchId = _branchId, Allocations = [new PaymentAllocationInput(invoice.Id, amount)], UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

        Assert.Equal(BusinessViolationCodes.AllocationExceedsUnallocated, CodeOf(await handler.HandleAsync(Allocate(600.01m))));
        Assert.Equal(1000m, invoice.Outstanding);

        var response = await handler.HandleAsync(Allocate(250m));
        Assert.True(response.IsSuccess);
        Assert.Equal(350m, Assert.IsType<AllocatePaymentResult>(response.Result).Unallocated);
        Assert.Equal(750m, invoice.Outstanding);
    }

    [Fact]
    public async Task C12_APendingPayment_CannotBeAllocated()
    {
        var invoice = SeedIssuedInvoice(total: 90_000m);
        var payment = await PendingPaymentAsync(invoice);
        var handler = new AllocatePaymentCommandHandler(_payments, _invoices, _customers, _time);

        var response = await handler.HandleAsync(new AllocatePaymentCommand
        { PaymentId = payment.Id, BranchId = _branchId, Allocations = [new PaymentAllocationInput(invoice.Id, 1m)], UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.Equal(BusinessViolationCodes.PaymentNotAccepted, CodeOf(response));
    }

    [Fact]
    public async Task C15_AReversal_ReleasesEveryAllocation_FromEveryInvoice_InOneSave()
    {
        var first = SeedIssuedInvoice(total: 500m, number: "INV-MAIN-2627-000001");
        var second = SeedIssuedInvoice(total: 500m, number: "INV-MAIN-2627-000002");
        await Handler().HandleAsync(Record(800m, allocations: [new PaymentAllocationInput(first.Id, 500m), new PaymentAllocationInput(second.Id, 300m)]));
        var payment = _payments.Saved.Single();
        Assert.Equal(InvoiceStatus.Paid, first.Status);

        var handler = new ReversePaymentCommandHandler(_payments, _invoices, _branches, _time);
        var response = await handler.HandleAsync(new ReversePaymentCommand
        { PaymentId = payment.Id, BranchId = _branchId, Reason = "Entered against the wrong customer", UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() });

        Assert.True(response.IsSuccess);
        Assert.Equal(PaymentStatus.Reversed, payment.Status);
        Assert.Equal(500m, first.Outstanding);
        Assert.Equal(InvoiceStatus.Issued, first.Status);
        Assert.Equal(500m, second.Outstanding);
        Assert.Contains(first, _invoices.Updated);
        Assert.Contains(second, _invoices.Updated);
    }

    [Fact]
    public async Task C15_AReversalNeedsAReason_AndAnotherBranchCannotDoIt()
    {
        await Handler().HandleAsync(Record(100m));
        var payment = _payments.Saved.Single();
        var handler = new ReversePaymentCommandHandler(_payments, _invoices, _branches, _time);

        ReversePaymentCommand Reverse(Guid branch, string reason) => new() { PaymentId = payment.Id, BranchId = branch, Reason = reason, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

        Assert.Equal(BusinessViolationCodes.PaymentNotFound, CodeOf(await handler.HandleAsync(Reverse(Guid.NewGuid(), "x"))));
        Assert.Equal(BusinessViolationCodes.ReasonRequired, CodeOf(await handler.HandleAsync(Reverse(_branchId, ""))));
        Assert.Equal(PaymentStatus.Accepted, payment.Status);
    }

    [Fact]
    public async Task C14_ReleasingAnEarmark_NeedsAReason_AndMakesItFreeCredit()
    {
        await Handler().HandleAsync(Record(500m, earmark: new PaymentEarmarkInput("Order", Guid.NewGuid(), "Order #12")));
        var payment = _payments.Saved.Single();
        var handler = new ReleasePaymentEarmarkCommandHandler(_payments, _time);

        ReleasePaymentEarmarkCommand Release(string reason) => new() { PaymentId = payment.Id, BranchId = _branchId, Reason = reason, UserContext = TestUserContext.Create(), CorrelationId = Guid.NewGuid() };

        Assert.Equal(BusinessViolationCodes.ReasonRequired, CodeOf(await handler.HandleAsync(Release(" "))));
        Assert.False(payment.IsFreeCredit);

        Assert.True((await handler.HandleAsync(Release("Order cancelled"))).IsSuccess);
        Assert.True(payment.IsFreeCredit);
    }

    // ── Validators ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TheValidators_BoundTheShapeOfEveryPaymentCommand()
    {
        var record = new RecordPaymentCommandValidator(_time);
        Assert.True(record.Validate(Record()).IsValid);
        Assert.False(record.Validate(Record(amount: 0m)).IsValid);
        Assert.False(record.Validate(Record(amount: 10.005m)).IsValid);
        Assert.False(record.Validate(Record(method: "Bitcoin")).IsValid);
        Assert.False(record.Validate(Record(reference: new string('x', 101))).IsValid);
        Assert.False(record.Validate(Record(collector: new PaymentCollectorInput("Alien"))).IsValid);
        Assert.False(record.Validate(Record(earmark: new PaymentEarmarkInput("Stay", Guid.NewGuid(), "x"))).IsValid);
        Assert.False(record.Validate(Record(earmark: new PaymentEarmarkInput("Order", Guid.NewGuid(), ""))).IsValid);
        Assert.False(record.Validate(Record(allocations: Enumerable.Range(0, 21).Select(_ => new PaymentAllocationInput(Guid.NewGuid(), 1m)).ToList())).IsValid);
        Assert.False(record.Validate(Record(allocations: [new PaymentAllocationInput(Guid.NewGuid(), 0m)])).IsValid);

        var tomorrowByUtcPlusTwo = new RecordPaymentCommand
        {
            PaymentId = Guid.NewGuid(), CustomerId = _customerId, BranchId = _branchId, Method = "CashInStore", Amount = 1m,
            ReceivedOn = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime).AddDays(2), UserContext = _recorder, CorrelationId = Guid.NewGuid(),
        };
        Assert.False(record.Validate(tomorrowByUtcPlusTwo).IsValid);

        var reverse = new ReversePaymentCommandValidator();
        Assert.False(reverse.Validate(new ReversePaymentCommand { PaymentId = Guid.NewGuid(), BranchId = _branchId, Reason = "", UserContext = _recorder, CorrelationId = Guid.NewGuid() }).IsValid);
        Assert.True(reverse.Validate(new ReversePaymentCommand { PaymentId = Guid.NewGuid(), BranchId = _branchId, Reason = "Wrong customer", UserContext = _recorder, CorrelationId = Guid.NewGuid() }).IsValid);
    }
}
