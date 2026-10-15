using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Tests.TestSupport;
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

/// <summary>HO-BIL-001 section 15 U-E1..U-E5 at the handler level -- credit limits, the newest-first release of an over-payment, an unpaid
/// invoice releasing nothing, the refund method rule and the refund cap; plus the refund message and the idempotent replays.</summary>
public sealed class CreditCommandHandlerTests
{
    private readonly FakeAggregateRootRepository<CreditNoteAggregate> _creditNotes = new();
    private readonly FakeAggregateRootRepository<RefundAggregate> _refunds = new();
    private readonly FakeAggregateRootRepository<InvoiceAggregate> _invoices = new();
    private readonly FakeAggregateRootRepository<PaymentAggregate> _payments = new();
    private readonly FakeAggregateRootRepository<BranchAggregate> _branches = new();
    private readonly FakeBillingFinder _finder = new();
    private readonly FakeNotificationOutbox _outbox = new();
    private readonly FakeDocumentNumbering _numbering = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero));
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly UserContext _staff = TestUserContext.Create();

    public CreditCommandHandlerTests()
    {
        var branch = new BranchAggregate(_branchId);
        branch.Create(new BranchCreateDto("MAIN", "Main", "Asia/Dhaka", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        _branches.Seed(branch);
    }

    private BillingActor Actor => new(_staff, Guid.NewGuid());

    private InvoiceAggregate SeedIssuedInvoice(decimal total = 1000m)
    {
        var taxClass = Guid.NewGuid();
        var invoice = new InvoiceAggregate(Guid.NewGuid());
        invoice.CreateDraft(new DraftInvoiceDto(
            _branchId, _customerId, "BDT",
            [new DraftInvoiceLineInput(Guid.NewGuid(), null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, Guid.NewGuid(), null, null, null, "Item", 1m, total, 0m, taxClass)],
            null, null, null, _time.GetUtcNow(), Actor));
        invoice.Issue(new InvoiceIssueDto(
            "INV-MAIN-2627-000001", "2026-27", 1, new Dictionary<Guid, TaxRateInfo> { [taxClass] = new(taxClass, "ZERO", 0m) },
            new SellerSnapshot("Seller", "TIN", "Addr", null, null, null, "[]"), new BillToSnapshot("Jane", null, null, null), _time.GetUtcNow(), "Asia/Dhaka", 0,
            new DateOnly(2026, 10, 7), null, Actor));
        _invoices.Seed(invoice);
        return invoice;
    }

    private PaymentAggregate SeedPayment(decimal amount, PaymentMethod? method = null, InvoiceAggregate? allocateTo = null)
    {
        var payment = new PaymentAggregate(Guid.NewGuid());
        payment.Record(new PaymentRecordDto(
            _branchId, _customerId, method ?? PaymentMethod.CashInStore, amount, "BDT", new DateOnly(2026, 10, 7),
            method == PaymentMethod.BankTransfer ? "TRX-1" : null, null, null, null, 50_000m, [], new DateOnly(2026, 10, 7), _time.GetUtcNow(), null, Actor, 1));

        if (allocateTo is not null)
        {
            payment.Allocate(new PaymentAllocateDto(allocateTo.Id, amount, true, _time.GetUtcNow(), Actor));
            allocateTo.ApplyPayment(amount, Actor);
        }

        _payments.Seed(payment);
        return payment;
    }

    private IssueCreditNoteCommandHandler CreditHandler() => new(_creditNotes, _invoices, _payments, _branches, _finder, _numbering, BillingTestSettings.Defaults(), _time);

    private IssueRefundCommandHandler RefundHandler() => new(_refunds, _payments, _creditNotes, _finder, _outbox, _time, _branches);

    private IssueCreditNoteCommand Credit(Guid invoiceId, bool full = false, IReadOnlyList<CreditLineRequest>? lines = null, string? key = null) => new()
    {
        CreditNoteId = Guid.NewGuid(), InvoiceId = invoiceId, BranchId = _branchId, Kind = "Return", Reason = "Returned", FullCredit = full, Lines = lines,
        IdempotencyKey = key, UserContext = _staff, CorrelationId = Guid.NewGuid(),
    };

    private IssueRefundCommand Refund(Guid paymentId, decimal amount, string method = "CashInStore", string? reference = null, string? key = null) => new()
    {
        RefundId = Guid.NewGuid(), PaymentId = paymentId, BranchId = _branchId, Method = method, Amount = amount, Reference = reference, Reason = "secret reason text",
        IdempotencyKey = key, UserContext = _staff, CorrelationId = Guid.NewGuid(),
    };

    private static string CodeOf(CommandResponse response) => response.ValidationResult!.Errors.Single().ErrorCode;

    // U-E1
    [Fact]
    public async Task U_E1_ACreditAboveWhatTheLineStillHolds_IsRefused()
    {
        var invoice = SeedIssuedInvoice(1000m);
        var line = invoice.Lines.Single();
        _finder.CreditedSoFar[line.Id] = new CreditedSoFar(line.Id, 0m, 800m, 0m, 800m);

        var response = await CreditHandler().HandleAsync(Credit(invoice.Id, lines: [new CreditLineRequest(line.Id, Amount: 300m)]));

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.CreditExceedsLine, CodeOf(response));
        Assert.Empty(_creditNotes.Saved);
    }

    [Fact]
    public async Task U_E1_AFullCreditAfterEverythingIsCredited_HasNothingToCredit_AndAnUnknownLineIsNotFound()
    {
        var invoice = SeedIssuedInvoice(1000m);
        var line = invoice.Lines.Single();
        _finder.CreditedSoFar[line.Id] = new CreditedSoFar(line.Id, 1m, 1000m, 0m, 1000m);

        Assert.Equal(BusinessViolationCodes.NothingToCredit, CodeOf(await CreditHandler().HandleAsync(Credit(invoice.Id, full: true))));

        _finder.CreditedSoFar.Clear();
        Assert.Equal(BusinessViolationCodes.InvoiceLineNotFound,
            CodeOf(await CreditHandler().HandleAsync(Credit(invoice.Id, lines: [new CreditLineRequest(Guid.NewGuid(), Amount: 10m)]))));
    }

    // U-E2
    [Fact]
    public async Task U_E2_APaidInvoiceCredited500_ReleasesTheNewestPaymentFirst()
    {
        var invoice = SeedIssuedInvoice(1000m);
        var older = SeedPayment(600m, allocateTo: invoice);
        var newer = SeedPayment(400m, allocateTo: invoice);
        _finder.PaymentsNewestFirst.AddRange([newer.Id, older.Id]);

        var response = await CreditHandler().HandleAsync(Credit(invoice.Id, lines: [new CreditLineRequest(invoice.Lines.Single().Id, Amount: 500m)]));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<IssuedCreditNoteResult>(response.Result);
        Assert.Equal(500m, result.ReleasedToCustomerCredit);
        Assert.Equal(0m, newer.AllocatedTo(invoice.Id));
        Assert.Equal(500m, older.AllocatedTo(invoice.Id)); // 100 released from the older payment
        Assert.Equal(400m, newer.Unallocated);
        Assert.Equal(100m, older.Unallocated);
        Assert.Equal(500m, invoice.PaidTotal);
        Assert.Equal(500m, invoice.CreditedTotal);
        Assert.Equal(0m, invoice.Outstanding);
        Assert.Equal("CN-MAIN-2627-000001", result.Number);
        Assert.Contains(invoice, _invoices.Updated);
        Assert.Contains(newer, _payments.Updated);
    }

    // U-E3
    [Fact]
    public async Task U_E3_AnUnpaidInvoiceCredited300_ReleasesNothing_AndTheOutstandingFalls()
    {
        var invoice = SeedIssuedInvoice(1000m);

        var response = await CreditHandler().HandleAsync(Credit(invoice.Id, lines: [new CreditLineRequest(invoice.Lines.Single().Id, Amount: 300m)]));

        Assert.True(response.IsSuccess);
        Assert.Equal(0m, Assert.IsType<IssuedCreditNoteResult>(response.Result).ReleasedToCustomerCredit);
        Assert.Equal(700m, invoice.Outstanding);
        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.Empty(_payments.Updated);
        Assert.Equal(300m, Assert.Single(_creditNotes.Saved).Total);
    }

    [Fact]
    public async Task ACreditNote_FromTheSameKey_IsReplayed_AndAnotherInvoiceUnderTheKeyConflicts()
    {
        var invoice = SeedIssuedInvoice(1000m);
        _finder.IdempotentCreditNotes[(_staff.UserId, "k1")] = new IssuedCreditNoteRef(Guid.NewGuid(), invoice.Id, _customerId, "CN-1", 5m);

        var replay = await CreditHandler().HandleAsync(Credit(invoice.Id, full: true, key: "k1"));
        Assert.True(Assert.IsType<IssuedCreditNoteResult>(replay.Result).Replayed);
        Assert.Empty(_creditNotes.Saved);

        var other = SeedIssuedInvoice(50m);
        Assert.Equal(BusinessViolationCodes.IdempotencyKeyConflict, CodeOf(await CreditHandler().HandleAsync(Credit(other.Id, full: true, key: "k1"))));
    }

    [Fact]
    public async Task ACreditNote_OnAnotherBranchesInvoice_IsNotFound_AndAFullCreditSumsEveryLine()
    {
        var invoice = SeedIssuedInvoice(1000m);

        var response = await CreditHandler().HandleAsync(new IssueCreditNoteCommand
        {
            CreditNoteId = Guid.NewGuid(), InvoiceId = invoice.Id, BranchId = Guid.NewGuid(), Kind = "Return", Reason = "x", FullCredit = true,
            UserContext = _staff, CorrelationId = Guid.NewGuid(),
        });
        Assert.Equal(BusinessViolationCodes.InvoiceNotFound, CodeOf(response));

        var full = await CreditHandler().HandleAsync(Credit(invoice.Id, full: true));
        Assert.True(full.IsSuccess);
        Assert.Equal(InvoiceStatus.Credited, invoice.Status);
        Assert.Equal(1000m, _creditNotes.Saved.Single().Total);
    }

    // U-E4
    [Fact]
    public async Task U_E4_ACashRefundOfATransferPayment_IsNotAllowed()
    {
        var payment = SeedPayment(500m, PaymentMethod.BankTransfer);

        var response = await RefundHandler().HandleAsync(Refund(payment.Id, 100m, "CashInStore"));

        Assert.False(response.IsSuccess);
        Assert.Equal(BusinessViolationCodes.RefundMethodNotAllowed, CodeOf(response));
        Assert.Empty(_refunds.Saved);
        Assert.Empty(_outbox.Requests);
    }

    // U-E5
    [Fact]
    public async Task U_E5_ARefundAboveWhatIsUnallocated_NamesTheAvailableAmount()
    {
        var invoice = SeedIssuedInvoice(1000m);
        var payment = SeedPayment(500m, allocateTo: invoice); // fully allocated; nothing available
        var free = SeedPayment(100m);

        var response = await RefundHandler().HandleAsync(Refund(free.Id, 100.01m));

        Assert.Equal(BusinessViolationCodes.RefundExceedsAvailable, CodeOf(response));
        Assert.Contains("BDT 100.00", response.ValidationResult!.Errors.Single().ErrorMessage);
        Assert.Equal(BusinessViolationCodes.RefundExceedsAvailable, CodeOf(await RefundHandler().HandleAsync(Refund(payment.Id, 1m))));
        Assert.Empty(_refunds.Saved);
    }

    [Fact]
    public async Task ARefund_DrawsDownThePayment_EnqueuesAMessageWithoutTheReason_AndReplaysByKey()
    {
        var payment = SeedPayment(300m);

        var response = await RefundHandler().HandleAsync(Refund(payment.Id, 200m, key: "rk"));

        Assert.True(response.IsSuccess);
        Assert.Equal(100m, payment.Unallocated);
        Assert.Equal(200m, payment.RefundedTotal);
        Assert.Contains(payment, _payments.Updated);
        var refund = Assert.Single(_refunds.Saved);
        Assert.Equal("secret reason text", refund.Reason);

        var message = Assert.Single(_outbox.Requests);
        Assert.Equal(NotificationTemplateCatalogue.BillingRefundIssued, message.TemplateKey);
        Assert.Equal("BDT 200.00", message.Variables["amountText"]);
        Assert.Equal("—", message.Variables["creditNoteNumber"]);
        Assert.DoesNotContain(message.Variables.Values, v => v.Contains("secret"));

        _finder.IdempotentRefunds[(_staff.UserId, "rk")] = new IssuedRefundRef(refund.Id, payment.Id, _customerId);
        var replay = await RefundHandler().HandleAsync(Refund(payment.Id, 200m, key: "rk"));
        Assert.True(Assert.IsType<IssuedRefundResult>(replay.Result).Replayed);
        Assert.Single(_refunds.Saved);
    }

    [Fact]
    public async Task ABankTransferRefund_NeedsAReference_AndACardNumberIsRefused()
    {
        var payment = SeedPayment(500m, PaymentMethod.BankTransfer);

        Assert.Equal(BusinessViolationCodes.ReferenceRequired, CodeOf(await RefundHandler().HandleAsync(Refund(payment.Id, 10m, "BankTransfer"))));
        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber,
            CodeOf(await RefundHandler().HandleAsync(Refund(payment.Id, 10m, "BankTransfer", "4111 1111 1111 1111"))));
        Assert.True((await RefundHandler().HandleAsync(Refund(payment.Id, 10m, "BankTransfer", "REF-9"))).IsSuccess);
    }
}
