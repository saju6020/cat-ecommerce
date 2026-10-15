using CatEcommerce.Application.CommandHandlers.Billing;
using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DomainEventHandlers.Billing;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Tests.CommandHandlers.Billing;

/// <summary>HO-BIL-001 section 15 U-I2..U-I6 at the handler level -- points accrued once, redeemed within the rules, taken back by a credit note, and the
/// customer credit drawn oldest free payment first, an earmarked deposit untouched (US-BIL-08, US-BIL-10; ADR-191).</summary>
public sealed class LoyaltyHandlerTests
{
    private readonly FakeAggregateRootRepository<InvoiceAggregate> _invoices = new();
    private readonly FakeAggregateRootRepository<PaymentAggregate> _payments = new();
    private readonly FakeAggregateRootRepository<CreditNoteAggregate> _creditNotes = new();
    private readonly FakeAggregateRootRepository<CustomerAccountAggregate> _customers = new();
    private readonly FakeBillingFinder _finder = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero));
    private readonly TestLoyalty _loyalty;
    private readonly Guid _branchId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly UserContext _staff = TestUserContext.Create();

    public LoyaltyHandlerTests() => _loyalty = new TestLoyalty(_invoices);

    private BillingActor Actor => new(_staff, Guid.NewGuid());

    private IBusinessSettings Settings(bool enabled = true) => BillingTestSettings.Defaults(new Dictionary<string, object>
    {
        [SettingKeys.BillingLoyaltyEnabled] = enabled,
        [SettingKeys.BillingLoyaltySpendPerPoint] = 100m,
        [SettingKeys.BillingLoyaltyPointValue] = 1m,
        [SettingKeys.BillingLoyaltyMinimumRedemptionPoints] = 100,
    });

    private LoyaltyAccountAggregate SeedAccount(int balance)
    {
        var account = new LoyaltyAccountAggregate(_customerId);
        account.Open(_staff);
        if (balance > 0) account.Accrue(balance);
        _loyalty.Accounts.Seed(account);
        return account;
    }

    private InvoiceAggregate SeedInvoice(decimal price, ItemKind kind = default!, bool issue = false)
    {
        var taxClass = Guid.NewGuid();
        var invoice = new InvoiceAggregate(Guid.NewGuid());
        invoice.CreateDraft(new DraftInvoiceDto(
            _branchId, _customerId, "BDT",
            [new DraftInvoiceLineInput(Guid.NewGuid(), null, BusinessLine.Shop, kind ?? ItemKind.Product, ChargeSourceKind.Order, Guid.NewGuid(), null, null, null, "Item", 1m, price, 0m, taxClass)],
            null, null, null, _time.GetUtcNow(), Actor));

        if (issue)
            invoice.Issue(new InvoiceIssueDto(
                "INV-MAIN-2627-000001", "2026-27", 1, new Dictionary<Guid, TaxRateInfo> { [taxClass] = new(taxClass, "ZERO", 0m) },
                new SellerSnapshot("Seller", "TIN", "Addr", null, null, null, "[]"), new BillToSnapshot("Jane", null, null, null), _time.GetUtcNow(), "Asia/Dhaka", 0,
                new DateOnly(2026, 10, 7), null, Actor));

        _invoices.Seed(invoice);
        return invoice;
    }

    private ApplyLoyaltyRedemptionCommandHandler Redeem(bool enabled = true) => new(_invoices, _loyalty.Dependencies, Settings(enabled), _time);

    private RemoveLoyaltyRedemptionCommandHandler Remove() => new(_invoices, _loyalty.Dependencies, _time);

    private ApplyLoyaltyRedemptionCommand RedeemCommand(Guid invoiceId, int points) => new()
    { InvoiceId = invoiceId, BranchId = _branchId, Points = points, UserContext = _staff, CorrelationId = Guid.NewGuid() };

    private static string CodeOf(CommandResponse response) => response.ValidationResult!.Errors.Single().ErrorCode;

    private InvoiceStatusChangedEvent Paid(Guid invoiceId) => new()
    { InvoiceId = invoiceId, From = InvoiceStatus.PartPaid, To = InvoiceStatus.Paid, UserContext = _staff, CorrelationId = Guid.NewGuid() };

    private ReverseLoyaltyOnCreditNoteIssuedHandler Reversal(bool enabled = true) => new(_creditNotes, _invoices, _customers, _loyalty.Dependencies, _finder, Settings(enabled), _time);

    private AccrueLoyaltyOnInvoicePaidHandler Accrual(bool enabled = true) => new(_invoices, _customers, _loyalty.Dependencies, _finder, Settings(enabled), Reversal(enabled), _time);

    // ── C27 ─────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task C27_ARedemption_DiscountsTheDraft_SpendsThePoints_AndWritesTheEntry()
    {
        var account = SeedAccount(500);
        var draft = SeedInvoice(1000m);

        var response = await Redeem().HandleAsync(RedeemCommand(draft.Id, 200));

        Assert.True(response.IsSuccess);
        Assert.Equal(200, draft.LoyaltyRedemption!.Points);
        Assert.Equal(200m, draft.LoyaltyRedemption.Value);
        Assert.Equal(300, account.Balance);
        var entry = Assert.Single(_loyalty.Entries.Saved);
        Assert.Equal(LoyaltyEntryKind.Redemption, entry.Kind);
        Assert.Equal(draft.Id, entry.InvoiceId);
        Assert.Equal(entry.Id, draft.LoyaltyRedemption.LoyaltyEntryId);
        Assert.Contains(draft, _loyalty.Invoices.Updated);
        Assert.Contains(account, _loyalty.Accounts.Updated);
    }

    [Fact]
    public async Task U_I3_BelowTheMinimum_AboveTheBalance_AndAboveTheQualifyingNet_AreRefused()
    {
        SeedAccount(500);
        var big = SeedInvoice(1000m);
        var small = SeedInvoice(150m);

        Assert.Equal(BusinessViolationCodes.LoyaltyBelowMinimum, CodeOf(await Redeem().HandleAsync(RedeemCommand(big.Id, 99))));
        Assert.Equal(BusinessViolationCodes.LoyaltyInsufficientPoints, CodeOf(await Redeem().HandleAsync(RedeemCommand(big.Id, 501))));
        Assert.Equal(BusinessViolationCodes.LoyaltyRedemptionExceedsNet, CodeOf(await Redeem().HandleAsync(RedeemCommand(small.Id, 200))));

        Assert.Null(big.LoyaltyRedemption);
        Assert.Null(small.LoyaltyRedemption);
        Assert.Empty(_loyalty.Entries.Saved);
        Assert.Equal(500, (await _loyalty.Accounts.GetByIdAsync(_customerId)).Balance);
    }

    [Fact]
    public async Task U_I5_WhenLoyaltyIsOff_ARedemptionIsRefused_AndAccrualDoesNothing()
    {
        SeedAccount(500);
        var draft = SeedInvoice(1000m);
        Assert.Equal(BusinessViolationCodes.LoyaltyNotEnabled, CodeOf(await Redeem(enabled: false).HandleAsync(RedeemCommand(draft.Id, 200))));

        var issued = SeedInvoice(1234m, issue: true);
        await Accrual(enabled: false).HandleAsync(Paid(issued.Id));
        Assert.Empty(_loyalty.Entries.Saved);
    }

    [Fact]
    public async Task C27_ARedemptionOnAnIssuedInvoiceOrAnotherBranch_IsRefused_AndASecondOneNeedsTheFirstRemoved()
    {
        SeedAccount(900);
        var issued = SeedInvoice(1000m, issue: true);
        Assert.Equal(BusinessViolationCodes.InvoiceNotDraft, CodeOf(await Redeem().HandleAsync(RedeemCommand(issued.Id, 200))));

        var draft = SeedInvoice(1000m);
        var otherBranch = new ApplyLoyaltyRedemptionCommand { InvoiceId = draft.Id, BranchId = Guid.NewGuid(), Points = 200, UserContext = _staff, CorrelationId = Guid.NewGuid() };
        Assert.Equal(BusinessViolationCodes.InvoiceNotFound, CodeOf(await Redeem().HandleAsync(otherBranch)));

        Assert.True((await Redeem().HandleAsync(RedeemCommand(draft.Id, 200))).IsSuccess);
        Assert.False((await Redeem().HandleAsync(RedeemCommand(draft.Id, 100))).IsSuccess);
    }

    [Fact]
    public async Task C27_RemovingARedemption_ReturnsThePoints_AndRemovingNothingIsANoOp()
    {
        var account = SeedAccount(500);
        var draft = SeedInvoice(1000m);
        await Redeem().HandleAsync(RedeemCommand(draft.Id, 200));
        Assert.Equal(300, account.Balance);

        var remove = new RemoveLoyaltyRedemptionCommand { InvoiceId = draft.Id, BranchId = _branchId, UserContext = _staff, CorrelationId = Guid.NewGuid() };
        Assert.True((await Remove().HandleAsync(remove)).IsSuccess);

        Assert.Null(draft.LoyaltyRedemption);
        Assert.Equal(500, account.Balance);
        Assert.Equal(new[] { LoyaltyEntryKind.Redemption, LoyaltyEntryKind.RedemptionReversal }, _loyalty.Entries.Saved.Select(e => e.Kind).ToArray());

        Assert.True((await Remove().HandleAsync(remove)).IsSuccess);
        Assert.Equal(2, _loyalty.Entries.Saved.Count);
    }

    // ── Accrual and reversal ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task U_I1_Accrual_On1234Net_At100PerPoint_Is12Points()
    {
        var invoice = SeedInvoice(1234m, issue: true);

        await Accrual().HandleAsync(Paid(invoice.Id));

        var entry = Assert.Single(_loyalty.Entries.Saved);
        Assert.Equal(LoyaltyEntryKind.Accrual, entry.Kind);
        Assert.Equal(12, entry.Points);
        Assert.Equal(invoice.Id, entry.InvoiceId);
        Assert.Equal(12, (await _loyalty.Accounts.GetByIdAsync(_customerId)).Balance);
    }

    [Fact]
    public async Task U_I2_TheSameInvoiceTwice_AccruesOnce()
    {
        var invoice = SeedInvoice(1234m, issue: true);
        await Accrual().HandleAsync(Paid(invoice.Id));
        _finder.LoyaltyEntries.Add((LoyaltyEntryKind.Accrual, invoice.Id)); // what the unique index and the lookup see after the first save

        await Accrual().HandleAsync(Paid(invoice.Id));

        Assert.Single(_loyalty.Entries.Saved);
        Assert.Equal(12, (await _loyalty.Accounts.GetByIdAsync(_customerId)).Balance);
    }

    [Fact]
    public async Task U_I1_FeeLines_DoNotEarnPoints_AndOtherStatusesDoNotAccrue()
    {
        var fee = SeedInvoice(5000m, ItemKind.Fee, issue: true);
        await Accrual().HandleAsync(Paid(fee.Id));
        Assert.Empty(_loyalty.Entries.Saved);

        var issued = SeedInvoice(1000m, issue: true);
        await Accrual().HandleAsync(new InvoiceStatusChangedEvent { InvoiceId = issued.Id, From = InvoiceStatus.Issued, To = InvoiceStatus.PartPaid, UserContext = _staff, CorrelationId = Guid.NewGuid() });
        Assert.Empty(_loyalty.Entries.Saved);
    }

    private CreditNoteAggregate SeedCreditNote(InvoiceAggregate invoice, decimal gross)
    {
        var line = invoice.Lines.Single();
        var note = new CreditNoteAggregate(Guid.NewGuid());
        note.Issue(new CreditNoteIssueDto(
            invoice.Id, _branchId, _customerId, "BDT", "CN-MAIN-2627-000001", "2026-27", 1, CreditNoteKind.Return, "Returned",
            [new CreditNoteLineInput(line.Id, "Item", null, gross, gross, 0m, 0m, gross)], 0m, _time.GetUtcNow(), new DateOnly(2026, 10, 7), null, Actor));
        _creditNotes.Seed(note);
        return note;
    }

    [Fact]
    public async Task ACreditNote_TakesBackThePointsItsNetEarned_CappedAtTheAccrual_AndOnce()
    {
        var invoice = SeedInvoice(1234m, issue: true);
        SeedAccount(12);
        _finder.AccruedNotReversed = 12;
        var note = SeedCreditNote(invoice, 350m);

        await Reversal().ReverseForAsync(note.Id, _staff, Guid.NewGuid());

        var entry = Assert.Single(_loyalty.Entries.Saved);
        Assert.Equal(LoyaltyEntryKind.AccrualReversal, entry.Kind);
        Assert.Equal(3, entry.Points);
        Assert.Equal(note.Id, entry.CreditNoteId);
        Assert.Equal(0, entry.Shortfall);
        Assert.Equal(9, (await _loyalty.Accounts.GetByIdAsync(_customerId)).Balance);

        _finder.LoyaltyEntries.Add((LoyaltyEntryKind.AccrualReversal, note.Id));
        await Reversal().ReverseForAsync(note.Id, _staff, Guid.NewGuid());
        Assert.Single(_loyalty.Entries.Saved);
    }

    [Fact]
    public async Task U_I4_ACreditNoteLargerThanTheBalance_EmptiesIt_AndRecordsTheShortfall()
    {
        var invoice = SeedInvoice(1234m, issue: true);
        SeedAccount(1); // the customer already spent most of the 12 points
        _finder.AccruedNotReversed = 12;
        var note = SeedCreditNote(invoice, 1234m);

        await Reversal().HandleAsync(new CreditNoteIssuedEvent
        { CreditNoteId = note.Id, InvoiceId = invoice.Id, Total = 1234m, ReleasedToCustomerCredit = 0m, UserContext = _staff, CorrelationId = Guid.NewGuid() });

        var entry = Assert.Single(_loyalty.Entries.Saved);
        Assert.Equal(12, entry.Points);
        Assert.Equal(11, entry.Shortfall);
        Assert.Equal(0, (await _loyalty.Accounts.GetByIdAsync(_customerId)).Balance);
    }

    [Fact]
    public async Task ACreditNoteBeforeAnyAccrual_DoesNothing_AndTheLaterAccrualAppliesIt()
    {
        var invoice = SeedInvoice(1234m, issue: true);
        var note = SeedCreditNote(invoice, 350m);
        _finder.CreditNoteIds.Add(note.Id);
        _finder.AccruedNotReversed = 0;

        await Reversal().ReverseForAsync(note.Id, _staff, Guid.NewGuid());
        Assert.Empty(_loyalty.Entries.Saved);

        _finder.AccruedNotReversed = 12; // after the accrual below the invoice holds 12 points
        await Accrual().HandleAsync(Paid(invoice.Id));

        Assert.Equal(new[] { LoyaltyEntryKind.Accrual, LoyaltyEntryKind.AccrualReversal }, _loyalty.Entries.Saved.Select(e => e.Kind).ToArray());
        Assert.Equal(9, (await _loyalty.Accounts.GetByIdAsync(_customerId)).Balance);
    }


    // ── Merge transfer (ADR-184: one save) ──────────────────────────────────────────────────────────
    [Fact]
    public async Task AMergeTransfer_IsOneLedgerPosting_SoEveryBalanceEqualsItsLedger_AndARedeliveryDoesNothing()
    {
        var duplicateId = Guid.NewGuid();
        var survivorId = Guid.NewGuid();
        var duplicate = new LoyaltyAccountAggregate(duplicateId);
        duplicate.Open(_staff);
        duplicate.Accrue(70);
        _loyalty.Accounts.Seed(duplicate);
        var survivor = new LoyaltyAccountAggregate(survivorId);
        survivor.Open(_staff);
        survivor.Accrue(5);
        _loyalty.Accounts.Seed(survivor);

        var handler = new ReassignBillingOnCustomerMergedHandler(
            _finder, new FakeAggregateRootRepository<ChargeAggregate>(), _invoices, _loyalty.Dependencies, _time);
        var merged = new CustomerMergedEvent
        {
            DuplicateCustomerId = duplicateId, SurvivingCustomerId = survivorId, Reason = "Same person", IsReapplication = false,
            UserContext = _staff, CorrelationId = Guid.NewGuid(),
        };

        await handler.HandleAsync(merged);

        Assert.Equal(0, duplicate.Balance);
        Assert.Equal(75, survivor.Balance);
        var entries = _loyalty.Entries.Saved;
        Assert.Equal(2, entries.Count);
        Assert.Equal(new[] { LoyaltyEntryKind.MergeTransferOut, LoyaltyEntryKind.MergeTransferIn }.Order(), entries.Select(e => e.Kind).Order());
        Assert.All(entries, e => Assert.Equal(duplicateId, e.SourceCustomerId));

        // The ledger moves exactly what the accounts moved: out of the duplicate, into the survivor.
        int Moved(Guid customer) => entries.Where(e => e.CustomerId == customer)
            .Sum(e => e.Kind == LoyaltyEntryKind.MergeTransferIn ? e.Points : -e.Points);
        Assert.Equal(-70, Moved(duplicateId));
        Assert.Equal(70, Moved(survivorId));
        Assert.Equal(5 + Moved(survivorId), survivor.Balance);
        Assert.Equal(70 + Moved(duplicateId), duplicate.Balance);

        // A redelivered event finds the first fact written and moves nothing.
        _finder.LoyaltyEntries.Add((LoyaltyEntryKind.MergeTransferOut, duplicateId));
        await handler.HandleAsync(merged);
        Assert.Equal(2, _loyalty.Entries.Saved.Count);
        Assert.Equal(75, survivor.Balance);
    }
    // ── C13 ─────────────────────────────────────────────────────────────────────────────────────────
    private PaymentAggregate SeedPayment(decimal amount, PaymentEarmark? earmark = null)
    {
        var payment = new PaymentAggregate(Guid.NewGuid());
        payment.Record(new PaymentRecordDto(
            _branchId, _customerId, PaymentMethod.CashInStore, amount, "BDT", new DateOnly(2026, 10, 7), null, null, earmark, null, 50_000m, [],
            new DateOnly(2026, 10, 7), _time.GetUtcNow(), null, Actor, 1));
        _payments.Seed(payment);
        return payment;
    }

    private ApplyCustomerCreditCommandHandler Credit() => new(_invoices, _payments, _customers, _finder, _time);

    private ApplyCustomerCreditCommand CreditCommand(Guid invoiceId, decimal? amount = null) => new()
    { CustomerId = _customerId, InvoiceId = invoiceId, BranchId = _branchId, Amount = amount, UserContext = _staff, CorrelationId = Guid.NewGuid() };

    [Fact]
    public async Task U_I6_CustomerCredit_IsDrawnOldestFreePaymentFirst_AndAnEarmarkedDepositIsUntouched()
    {
        var older = SeedPayment(300m);
        var newer = SeedPayment(200m);
        var deposit = SeedPayment(1000m, new PaymentEarmark(EarmarkKind.BoardingStay, Guid.NewGuid(), "Stay deposit"));
        _finder.FreeCreditPaymentsOldestFirst.AddRange([older.Id, newer.Id]);
        var invoice = SeedInvoice(400m, issue: true);

        var response = await Credit().HandleAsync(CreditCommand(invoice.Id));

        Assert.True(response.IsSuccess);
        var result = Assert.IsType<AppliedCustomerCreditResult>(response.Result);
        Assert.Equal(2, result.Applied.Count);
        Assert.Equal(300m, result.Applied[0].Amount);
        Assert.Equal(100m, result.Applied[1].Amount);
        Assert.Equal(0m, result.InvoiceOutstanding);
        Assert.Equal(100m, result.FreeCreditRemaining);
        Assert.Equal(0m, older.Unallocated);
        Assert.Equal(100m, newer.Unallocated);
        Assert.Equal(1000m, deposit.Unallocated);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Contains(invoice, _invoices.Updated);
    }

    [Fact]
    public async Task U_I6_ACreditAmount_AppliesPartially_ThenFully_AndNeverMoreThanOutstanding()
    {
        var payment = SeedPayment(1000m);
        _finder.FreeCreditPaymentsOldestFirst.Add(payment.Id);
        var invoice = SeedInvoice(400m, issue: true);

        var part = Assert.IsType<AppliedCustomerCreditResult>((await Credit().HandleAsync(CreditCommand(invoice.Id, 150m))).Result);
        Assert.Equal(250m, part.InvoiceOutstanding);
        Assert.Equal(850m, part.FreeCreditRemaining);

        Assert.Equal(BusinessViolationCodes.AllocationExceedsOutstanding, CodeOf(await Credit().HandleAsync(CreditCommand(invoice.Id, 251m))));

        var rest = Assert.IsType<AppliedCustomerCreditResult>((await Credit().HandleAsync(CreditCommand(invoice.Id))).Result);
        Assert.Equal(0m, rest.InvoiceOutstanding);
        Assert.Equal(600m, rest.FreeCreditRemaining);
    }

    [Fact]
    public async Task CustomerCredit_NeedsCredit_AnIssuedInvoiceNotSettled_AndTheCustomersOwn()
    {
        var invoice = SeedInvoice(400m, issue: true);
        Assert.Equal(BusinessViolationCodes.CustomerCreditUnavailable, CodeOf(await Credit().HandleAsync(CreditCommand(invoice.Id))));

        var draft = SeedInvoice(400m);
        _finder.FreeCreditPaymentsOldestFirst.Add(SeedPayment(100m).Id);
        Assert.Equal(BusinessViolationCodes.InvoiceNotIssued, CodeOf(await Credit().HandleAsync(CreditCommand(draft.Id))));

        var stranger = new ApplyCustomerCreditCommand { CustomerId = Guid.NewGuid(), InvoiceId = invoice.Id, BranchId = _branchId, UserContext = _staff, CorrelationId = Guid.NewGuid() };
        Assert.Equal(BusinessViolationCodes.AllocationInvoiceCustomerMismatch, CodeOf(await Credit().HandleAsync(stranger)));
    }
}
