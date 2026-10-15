using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Events.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Aggregates.Billing;

/// <summary>HO-BIL-001 §15 group D (U-D2, U-D4…U-D12) -- a payment is a record of money received: attributable,
/// approved by a different person above the threshold, never edited, reversed and re-entered (ADR-182, ADR-183).</summary>
public sealed class PaymentAggregateTests
{
    private static string ErrorOf(Action act) => Assert.Throws<DomainRuleViolationException>(act).ErrorCode;

    private static PaymentCollector Staff() => new(CollectorKind.Staff, Guid.NewGuid(), null, null);

    private static PaymentCollector Courier() => new(CollectorKind.Courier, null, "PATHAO", "Pathao Courier");

    private static PaymentAllocateDto Allocate(Guid invoiceId, decimal amount, bool matches = true) =>
        new(invoiceId, amount, matches, DateTimeOffset.UtcNow, BillingTestData.Actor());

    private static PaymentReleaseDto Release(Guid invoiceId, decimal amount) =>
        new(invoiceId, amount, AllocationReleaseReason.CreditNoteExcess, DateTimeOffset.UtcNow, BillingTestData.Actor());

    // ── U-D2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D2_AMethodThatRequiresAReference_RefusesWithoutOne()
    {
        Assert.Equal(BusinessViolationCodes.ReferenceRequired, ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CardInStore))));
        Assert.Equal(BusinessViolationCodes.ReferenceRequired, ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.BankTransfer))));
        BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CardInStore, reference: "004512"));
    }

    [Fact]
    public void U_D2_CashOnDelivery_RequiresACollector_StaffOrCourier()
    {
        Assert.Equal(BusinessViolationCodes.CollectorRequired, ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CashOnDelivery))));
        Assert.Equal(BusinessViolationCodes.CollectorRequired,
            ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CashOnDelivery, collector: new PaymentCollector(CollectorKind.Courier, null, null, null)))));

        Assert.Equal(CollectorKind.Staff, BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CashOnDelivery, collector: Staff())).Collector!.Kind);
        Assert.Equal("PATHAO", BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CashOnDelivery, collector: Courier())).Collector!.CourierCode);
    }

    [Fact]
    public void CashInStore_NeedsNeitherReferenceNorCollector()
    {
        var payment = BillingTestData.Payment();
        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.Null(payment.ExternalReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void AnAmountMustBePositive(int amount)
    {
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(amount: amount))));
    }

    [Fact]
    public void AnAmountHasAtMostTwoDecimals()
    {
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(amount: 10.005m))));
    }

    // ── C-5 ─────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ACardNumberInTheReferenceOrTheNotes_IsRefused_WithoutEchoingIt()
    {
        var inReference = Assert.Throws<DomainRuleViolationException>(() =>
            BillingTestData.Payment(BillingTestData.PaymentDto(method: PaymentMethod.CardInStore, reference: "4111 1111 1111 1111")));
        var inNotes = Assert.Throws<DomainRuleViolationException>(() =>
            BillingTestData.Payment(BillingTestData.PaymentDto(notes: "customer card 4111111111111111")));

        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, inReference.ErrorCode);
        Assert.Equal(BusinessViolationCodes.ReferenceLooksLikeCardNumber, inNotes.ErrorCode);
        Assert.DoesNotContain("4111", inReference.Message);
        Assert.DoesNotContain("4111", inNotes.Message);
        Assert.Equal(BillingMessages.ReferenceLooksLikeCardNumber, inReference.Message);
    }

    // ── U-D4 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D4_AnAmountAtTheThreshold_IsAcceptedImmediately()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(50_000m, threshold: 50_000m));

        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.NotNull(payment.AcceptedOn);
        Assert.NotNull(payment.BusinessDate);
        Assert.Contains(payment.Events, e => e is PaymentAcceptedEvent);
    }

    // FU1 ORD-52/53: the waiver is recorded with its reason, and only cash-like methods with the matching reason may use it.
    [Fact]
    public void AWaivedApproval_AcceptsAboveTheThreshold_AndRecordsTheReasonAndAnEvent()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(
            60_000m, PaymentMethod.CashInStore, threshold: 50_000m, waiverReason: ApprovalWaiverReasons.CounterTender));

        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.Equal(ApprovalWaiverReasons.CounterTender, payment.ApprovalWaiverReason);
        Assert.Contains(payment.Events, e => e is PaymentApprovalWaivedEvent);
    }

    [Fact]
    public void AnOrdinaryPayment_HasNoWaiverReasonAndNoWaiverEvent()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(100m));

        Assert.Null(payment.ApprovalWaiverReason);
        Assert.DoesNotContain(payment.Events, e => e is PaymentApprovalWaivedEvent);
    }

    [Theory]
    [InlineData(nameof(PaymentMethod.BankTransfer), ApprovalWaiverReasons.CodCollection)]
    [InlineData(nameof(PaymentMethod.BankTransfer), ApprovalWaiverReasons.CounterTender)]
    [InlineData(nameof(PaymentMethod.CashOnDelivery), ApprovalWaiverReasons.CounterTender)]
    [InlineData(nameof(PaymentMethod.CashInStore), ApprovalWaiverReasons.CodCollection)]
    [InlineData(nameof(PaymentMethod.CashInStore), "Anything")]
    public void AWaiver_ForAnotherMethodOrAnUnknownReason_IsRefused(string method, string reason)
    {
        var parsed = Enumeration.FromDisplayName<PaymentMethod>(method);
        var dto = BillingTestData.PaymentDto(60_000m, parsed, reference: "REF-1", collector: parsed == PaymentMethod.CashOnDelivery ? Staff() : null, threshold: 50_000m, waiverReason: reason);

        Assert.Equal(BusinessViolationCodes.ApprovalWaiverNotAllowed, ErrorOf(() => BillingTestData.Payment(dto)));
    }

    [Fact]
    public void U_D4_OnePaisaAboveTheThreshold_WaitsForApproval_WithRequestedAllocationsAndNothingApplied()
    {
        var invoiceId = Guid.NewGuid();
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(50_000.01m, threshold: 50_000m, allocations: [new AllocationRequest(invoiceId, 40_000m)]));

        Assert.Equal(PaymentStatus.PendingApproval, payment.Status);
        Assert.Null(payment.AcceptedOn);
        Assert.Empty(payment.Entries);
        var requested = Assert.Single(payment.RequestedAllocations);
        Assert.Equal((invoiceId, 40_000m), (requested.InvoiceId, requested.Amount));
        Assert.Contains(payment.Events, e => e is PaymentRecordedEvent);
        Assert.DoesNotContain(payment.Events, e => e is PaymentAcceptedEvent);
    }

    [Fact]
    public void RequestedAllocations_CannotExceedTheAmount()
    {
        Assert.Equal(BusinessViolationCodes.AllocationExceedsUnallocated,
            ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(100m, allocations: [new AllocationRequest(Guid.NewGuid(), 100.01m)]))));
    }

    // ── U-D5, U-D6 ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D5_TheRecorderCannotApproveOrRejectTheirOwnPayment()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(60_000m, threshold: 50_000m));
        var actor = BillingTestData.Actor();

        Assert.Equal(BusinessViolationCodes.ApproverMustDiffer, ErrorOf(() => payment.Approve(payment.RecordedBy, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), actor)));
        Assert.Equal(BusinessViolationCodes.ApproverMustDiffer, ErrorOf(() => payment.Reject(payment.RecordedBy, "no", DateTimeOffset.UtcNow, actor)));
        Assert.Equal(PaymentStatus.PendingApproval, payment.Status);
    }

    [Fact]
    public void U_D6_Approval_ReturnsTheRequestedAllocations_AcceptsAndClearsThem()
    {
        var invoiceId = Guid.NewGuid();
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(60_000m, threshold: 50_000m, allocations: [new AllocationRequest(invoiceId, 55_000m)]));
        payment.ClearEvents();

        var requested = payment.Approve(Guid.NewGuid(), DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), BillingTestData.Actor());

        Assert.Equal(PaymentStatus.Accepted, payment.Status);
        Assert.Equal(55_000m, Assert.Single(requested).Amount);
        Assert.Empty(payment.RequestedAllocations);
        Assert.Contains(payment.Events, e => e is PaymentApprovedEvent);
        Assert.Contains(payment.Events, e => e is PaymentAcceptedEvent);
        Assert.Equal(60_000m, payment.Unallocated); // the handler applies each, capped at the invoice outstanding
    }

    [Fact]
    public void Reject_NeedsAReason_AndLeavesNothingApplied()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(60_000m, threshold: 50_000m));
        var approver = Guid.NewGuid();

        Assert.Equal(BusinessViolationCodes.ReasonRequired, ErrorOf(() => payment.Reject(approver, " ", DateTimeOffset.UtcNow, BillingTestData.Actor())));

        payment.Reject(approver, "Wrong amount", DateTimeOffset.UtcNow, BillingTestData.Actor());
        Assert.Equal(PaymentStatus.Rejected, payment.Status);
        Assert.Equal("Wrong amount", payment.RejectionReason);
        Assert.Equal(BusinessViolationCodes.PaymentNotPending, ErrorOf(() => payment.Approve(approver, DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), BillingTestData.Actor())));
    }

    // ── U-D7, U-D8 ──────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D7_Unallocated_IsAmountMinusEffectiveAllocationsMinusRefunds()
    {
        var invoiceId = Guid.NewGuid();
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(1000m));

        payment.Allocate(Allocate(invoiceId, 600m));
        payment.Release(Release(invoiceId, 100m));
        payment.RecordRefund(200m);

        Assert.Equal(500m, payment.AllocatedTo(invoiceId));
        Assert.Equal(300m, payment.Unallocated);
        Assert.Equal(200m, payment.RefundedTotal);
    }

    [Fact]
    public void U_D8_AllocatingMoreThanTheUnallocated_NamesTheAvailableAmount()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(1000m));
        payment.Allocate(Allocate(Guid.NewGuid(), 700m));

        var ex = Assert.Throws<DomainRuleViolationException>(() => payment.Allocate(Allocate(Guid.NewGuid(), 300.01m)));

        Assert.Equal(BusinessViolationCodes.AllocationExceedsUnallocated, ex.ErrorCode);
        Assert.Contains("BDT 300.00", ex.Message);
    }

    [Fact]
    public void Release_CannotExceedWhatWasAllocatedToThatInvoice()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(1000m));
        var invoiceId = Guid.NewGuid();
        payment.Allocate(Allocate(invoiceId, 100m));

        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => payment.Release(Release(invoiceId, 100.01m))));
        Assert.Equal(BusinessViolationCodes.InvalidAmount, ErrorOf(() => payment.Release(Release(Guid.NewGuid(), 1m))));
    }

    [Fact]
    public void Refund_CannotExceedTheUnallocated_AndNamesTheAvailableAmount()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(1000m));
        payment.Allocate(Allocate(Guid.NewGuid(), 800m));

        var ex = Assert.Throws<DomainRuleViolationException>(() => payment.RecordRefund(200.01m));

        Assert.Equal(BusinessViolationCodes.RefundExceedsAvailable, ex.ErrorCode);
        Assert.Contains("BDT 200.00", ex.Message);
    }

    // ── U-D9 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D9_AnEarmarkedPayment_AllocatesOnlyToAMatchingInvoice_UntilTheEarmarkIsReleased()
    {
        var earmark = new PaymentEarmark(EarmarkKind.BoardingStay, Guid.NewGuid(), "Deposit - boarding 12-19 Oct");
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(500m, earmark: earmark));
        var other = Guid.NewGuid();

        Assert.False(payment.IsFreeCredit);
        Assert.Equal(BusinessViolationCodes.EarmarkMismatch, ErrorOf(() => payment.Allocate(Allocate(other, 100m, matches: false))));

        payment.Allocate(Allocate(Guid.NewGuid(), 100m, matches: true));

        Assert.Equal(BusinessViolationCodes.ReasonRequired, ErrorOf(() => payment.ReleaseEarmark(" ", DateTimeOffset.UtcNow, BillingTestData.Actor())));
        payment.ReleaseEarmark("Stay cancelled", DateTimeOffset.UtcNow, BillingTestData.Actor());

        Assert.True(payment.IsFreeCredit);
        payment.Allocate(Allocate(other, 100m, matches: false));
        Assert.Equal(300m, payment.Unallocated);
        Assert.Contains(payment.Events, e => e is PaymentEarmarkReleasedEvent);
    }

    [Fact]
    public void ReleasingAnEarmarkTwice_OrOnAPaymentWithoutOne_IsRefused()
    {
        var plain = BillingTestData.Payment();
        Assert.Equal(BusinessViolationCodes.PaymentNotEarmarked, ErrorOf(() => plain.ReleaseEarmark("x", DateTimeOffset.UtcNow, BillingTestData.Actor())));
    }

    // ── U-D10 ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D10_ReversingAPaymentWithRefunds_IsRefused()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(1000m));
        payment.RecordRefund(10m);

        Assert.Equal(BusinessViolationCodes.PaymentHasRefunds,
            ErrorOf(() => payment.Reverse("Wrong customer", DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), BillingTestData.Actor())));
        Assert.Equal(PaymentStatus.Accepted, payment.Status);
    }

    [Fact]
    public void U_D10_Reversal_ReleasesEveryEffectiveAllocation_AndRecordsWhyWhenAndOnWhichBusinessDate()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(1000m));
        payment.Allocate(Allocate(first, 600m));
        payment.Allocate(Allocate(second, 300m));
        payment.Release(Release(second, 100m));
        var today = new DateOnly(2026, 10, 7);

        var released = payment.Reverse("Entered against the wrong invoice", DateTimeOffset.UtcNow, today, BillingTestData.Actor());

        Assert.Equal(PaymentStatus.Reversed, payment.Status);
        Assert.Equal(new[] { (first, 600m), (second, 200m) }, released.Select(r => (r.InvoiceId, r.Amount)).ToArray());
        Assert.Equal(0m, payment.AllocatedTo(first));
        Assert.Equal(0m, payment.AllocatedTo(second));
        Assert.Equal("Entered against the wrong invoice", payment.ReversalReason);
        Assert.Equal(today, payment.ReversalBusinessDate);
        Assert.Contains(payment.Events, e => e is PaymentReversedEvent { Amount: 1000m });

        Assert.Equal(BusinessViolationCodes.PaymentNotAccepted, ErrorOf(() => payment.Allocate(Allocate(first, 1m))));
    }

    [Fact]
    public void Reversal_NeedsAReason()
    {
        var payment = BillingTestData.Payment();
        Assert.Equal(BusinessViolationCodes.ReasonRequired,
            ErrorOf(() => payment.Reverse("", DateTimeOffset.UtcNow, DateOnly.FromDateTime(DateTime.UtcNow), BillingTestData.Actor())));
    }

    // ── U-D11 ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D11_NoPublicSetter_OnAmountMethodReferenceReceivedDateCollectorOrRecorder()
    {
        var protectedProperties = new[]
        {
            nameof(PaymentAggregate.Amount), nameof(PaymentAggregate.Method), nameof(PaymentAggregate.ExternalReference),
            nameof(PaymentAggregate.ReceivedOn), nameof(PaymentAggregate.Collector), nameof(PaymentAggregate.RecordedBy),
            nameof(PaymentAggregate.RecordedOn), nameof(PaymentAggregate.CurrencyCode), nameof(PaymentAggregate.ReceiptNumber),
        };

        foreach (var name in protectedProperties)
        {
            var property = typeof(PaymentAggregate).GetProperty(name)!;
            Assert.True(property.SetMethod is null || !property.SetMethod.IsPublic, $"{name} must have no public setter.");
        }

        Assert.DoesNotContain(typeof(PaymentAggregate).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly),
            p => p.SetMethod is { IsPublic: true });
    }

    // ── U-D12 ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void U_D12_TheDateReceived_CannotBeInTheFuture_OrMoreThan31DaysAgo()
    {
        var today = new DateOnly(2026, 10, 7);

        Assert.Equal(BusinessViolationCodes.ReceivedDateInFuture,
            ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(receivedOn: today.AddDays(1), todayLocal: today))));
        Assert.Equal(BusinessViolationCodes.ReceivedDateTooOld,
            ErrorOf(() => BillingTestData.Payment(BillingTestData.PaymentDto(receivedOn: today.AddDays(-32), todayLocal: today))));

        BillingTestData.Payment(BillingTestData.PaymentDto(receivedOn: today.AddDays(-31), todayLocal: today));
        BillingTestData.Payment(BillingTestData.PaymentDto(receivedOn: today, todayLocal: today));
    }

    [Fact]
    public void AllocationEntries_AreAppendOnly_AndBounded()
    {
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(10_000m));
        for (var i = 0; i < BillingRules.MaxAllocationEntries; i++) payment.Allocate(Allocate(Guid.NewGuid(), 1m));

        Assert.Equal(BusinessViolationCodes.TooManyAllocations, ErrorOf(() => payment.Allocate(Allocate(Guid.NewGuid(), 1m))));
    }

    [Fact]
    public void ARecordedPayment_RaisesRecordedAndAccepted_WithTheEarmarkOnTheAcceptedEvent()
    {
        var stay = Guid.NewGuid();
        var payment = BillingTestData.Payment(BillingTestData.PaymentDto(500m, earmark: new PaymentEarmark(EarmarkKind.BoardingStay, stay, "Deposit")));

        var accepted = Assert.Single(payment.Events.OfType<PaymentAcceptedEvent>());
        Assert.Equal(EarmarkKind.BoardingStay, accepted.EarmarkKind);
        Assert.Equal(stay, accepted.EarmarkId);
        Assert.Equal(500m, accepted.Amount);
    }
}
