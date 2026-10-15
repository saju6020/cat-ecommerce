using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>C10's result. The controller builds the response body from the stored payment (a query), so the first answer and a
/// replay (<see cref="Replayed"/>, ADR-187) are the same shape.</summary>
public sealed record RecordPaymentResult(Guid PaymentId, bool Replayed = false);

/// <summary>C11's result: what the approval applied, and what stayed unallocated (reported, not hidden -- U-D6).</summary>
public sealed record ApprovePaymentResult(IReadOnlyList<AppliedAllocation> Applied, decimal LeftUnallocated);

/// <summary>C12's result.</summary>
public sealed record AllocatePaymentResult(IReadOnlyList<AppliedAllocation> Applied, decimal Unallocated);

/// <summary>
/// HO-BIL-001 §11.4 C10 (US-BIL-11, 03, 04; ADR-182, 183, 185, 187). The sequence: caller's idempotency key, customer and
/// branch, then the aggregate decides (method flags, received date, card-number guard, threshold); an accepted payment applies
/// its allocations in the <b>same save</b>, a pending one stores them and applies nothing and enqueues nothing. The
/// payment-received message is enqueued here, before the save (never from an event handler -- ADR-193).
/// </summary>
public sealed class RecordPaymentCommandHandler(
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<StaffAccountAggregate> staffRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IBillingFinder finder,
    IBusinessSettings businessSettings,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordPaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordPaymentCommand command)
    {
        if (command.IdempotencyKey is { } key)
        {
            var prior = await finder.FindPaymentByIdempotencyKeyAsync(command.UserContext!.UserId, key);
            if (prior is not null)
                return prior.CustomerId == command.CustomerId
                    ? Success(new RecordPaymentResult(prior.PaymentId, Replayed: true))
                    : Violation(BusinessViolationCodes.IdempotencyKeyConflict, nameof(command.IdempotencyKey), BillingMessages.IdempotencyKeyConflict);
        }

        var customer = await customerRepository.GetByIdAsync(command.CustomerId);
        if (customer is null || customer.Status != CustomerAccountStatus.Active)
            return Violation(BusinessViolationCodes.CustomerNotFound, nameof(command.CustomerId), "Customer not found.");

        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var method = BillingMapper.ParseEnum<PaymentMethod>(command.Method);

        PaymentCollector? collector = null;
        if (command.Collector is { } collectorInput)
        {
            collector = new PaymentCollector(
                BillingMapper.ParseEnum<CollectorKind>(collectorInput.Kind), collectorInput.StaffAccountId, Clean(collectorInput.CourierCode), Clean(collectorInput.CourierName));

            if (collector.Kind == CollectorKind.Staff && collector.StaffAccountId is { } staffId && await staffRepository.GetByIdAsync(staffId) is null)
                return Violation(BusinessViolationCodes.CollectorRequired, nameof(command.Collector), "The staff member who collected the money was not found.");
        }

        var earmark = command.Earmark is { } earmarkInput
            ? new PaymentEarmark(BillingMapper.ParseEnum<EarmarkKind>(earmarkInput.Kind), earmarkInput.SourceId, earmarkInput.Label.Trim())
            : null;

        var now = timeProvider.GetUtcNow();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId)).DateTime);
        var actor = command.ToBillingActor();

        var payment = new PaymentAggregate(command.PaymentId);
        try
        {
            payment.Record(new PaymentRecordDto(
                command.BranchId, command.CustomerId, method, command.Amount, businessSettings.GetString(SettingKeys.CommerceCurrencyCode), command.ReceivedOn,
                command.ExternalReference, collector, earmark, command.Notes,
                businessSettings.GetDecimal(SettingKeys.BillingPaymentApprovalThreshold, command.BranchId),
                command.Allocations.Select(a => new AllocationRequest(a.InvoiceId, a.Amount)).ToList(), todayLocal, now, command.IdempotencyKey, actor,
                await finder.NextReceiptNumberAsync(), command.ApprovalWaiverReason));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        IReadOnlyList<IPostingParticipant> participants = [];
        var applied = (IReadOnlyList<AppliedAllocation>)[];
        if (payment.Status == PaymentStatus.Accepted && command.Allocations.Count > 0)
        {
            var outcome = await PaymentAllocating.ApplyAsync(
                invoiceRepository, customerRepository, payment, command.Allocations.Select(a => new AllocationRequest(a.InvoiceId, a.Amount)).ToList(),
                capAtOutstanding: false, now, actor);
            if (outcome.Violation is not null) return outcome.Violation;

            applied = outcome.Applied;
            participants = outcome.Participants;
        }

        if (payment.Status == PaymentStatus.Accepted)
            BillingNotifications.EnqueuePaymentReceived(outbox, links, payment, PaymentAllocating.PurposeText(payment, applied), command.CorrelationId, command.UserContext!);

        await BillingPosting.CommitNewAsync(paymentRepository, payment, [.. participants]);
        return Success(new RecordPaymentResult(payment.Id));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>HO-BIL-001 §11.4 C11 -- approve (the approver must differ from the recorder, in the aggregate) and apply the requested
/// allocations, each capped at the invoice's outstanding <b>at approval time</b>.</summary>
public sealed class ApprovePaymentCommandHandler(
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApprovePaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(ApprovePaymentCommand command)
    {
        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        var branch = await branchRepository.GetByIdAsync(payment.BranchId);
        var now = timeProvider.GetUtcNow();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(branch?.TimeZoneId ?? "UTC")).DateTime);
        var actor = command.ToBillingActor();

        IReadOnlyList<AllocationRequest> requested;
        try
        {
            requested = payment.Approve(command.UserContext!.UserId, now, todayLocal, actor);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var outcome = await PaymentAllocating.ApplyAsync(invoiceRepository, customerRepository, payment, requested, capAtOutstanding: true, now, actor);
        if (outcome.Violation is not null) return outcome.Violation;

        var customer = await customerRepository.GetByIdAsync(payment.CustomerId);
        if (customer?.Status == CustomerAccountStatus.Active)
            BillingNotifications.EnqueuePaymentReceived(outbox, links, payment, PaymentAllocating.PurposeText(payment, outcome.Applied), command.CorrelationId, command.UserContext!);

        await BillingPosting.CommitAsync(paymentRepository, payment, [.. outcome.Participants]);
        return Success(new ApprovePaymentResult(outcome.Applied, payment.Unallocated));
    }
}

public sealed class RejectPaymentCommandHandler(IAggregateRootRepository<PaymentAggregate> paymentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RejectPaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectPaymentCommand command)
    {
        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        try
        {
            payment.Reject(command.UserContext!.UserId, command.Reason, timeProvider.GetUtcNow(), command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await paymentRepository.UpdateAsync(payment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-BIL-001 §11.4 C12 -- allocate an accepted payment to invoices; staff cannot "move" money: a wrong allocation is
/// corrected by reversal and re-entry.</summary>
public sealed class AllocatePaymentCommandHandler(
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<CustomerAccountAggregate> customerRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AllocatePaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(AllocatePaymentCommand command)
    {
        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        if (payment.Status != PaymentStatus.Accepted)
            return Violation(BusinessViolationCodes.PaymentNotAccepted, nameof(command.PaymentId), "This payment is not an accepted payment.");

        var outcome = await PaymentAllocating.ApplyAsync(
            invoiceRepository, customerRepository, payment, command.Allocations.Select(a => new AllocationRequest(a.InvoiceId, a.Amount)).ToList(),
            capAtOutstanding: false, timeProvider.GetUtcNow(), command.ToBillingActor());
        if (outcome.Violation is not null) return outcome.Violation;

        await BillingPosting.CommitAsync(paymentRepository, payment, [.. outcome.Participants]);
        return Success(new AllocatePaymentResult(outcome.Applied, payment.Unallocated));
    }
}

public sealed class ReleasePaymentEarmarkCommandHandler(IAggregateRootRepository<PaymentAggregate> paymentRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<ReleasePaymentEarmarkCommand>
{
    public async Task<CommandResponse> HandleAsync(ReleasePaymentEarmarkCommand command)
    {
        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        try
        {
            payment.ReleaseEarmark(command.Reason, timeProvider.GetUtcNow(), command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await paymentRepository.UpdateAsync(payment, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>HO-BIL-001 §11.4 C15 (US-BIL-11 AC3) -- reverse: every effective allocation is released and each invoice's
/// <c>ReleasePayment</c> applied in the <b>same save</b>; a payment with refunds cannot be reversed.</summary>
public sealed class ReversePaymentCommandHandler(
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReversePaymentCommand>
{
    public async Task<CommandResponse> HandleAsync(ReversePaymentCommand command)
    {
        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        var branch = await branchRepository.GetByIdAsync(payment.BranchId);
        var now = timeProvider.GetUtcNow();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(branch?.TimeZoneId ?? "UTC")).DateTime);
        var actor = command.ToBillingActor();

        IReadOnlyList<AllocationRequest> released;
        try
        {
            released = payment.Reverse(command.Reason, now, todayLocal, actor);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var participants = new List<IPostingParticipant>();
        foreach (var release in released)
        {
            var invoice = await invoiceRepository.GetByIdAsync(release.InvoiceId);
            if (invoice is null) return Violation(BusinessViolationCodes.InvoiceNotFound, "Allocations", "An invoice this payment was applied to was not found.");

            try
            {
                invoice.ReleasePayment(release.Amount, actor);
            }
            catch (DomainRuleViolationException ex)
            {
                return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
            }

            participants.Add(BillingPosting.Of(invoiceRepository, invoice));
        }

        await BillingPosting.CommitAsync(paymentRepository, payment, [.. participants]);
        return Success();
    }
}
