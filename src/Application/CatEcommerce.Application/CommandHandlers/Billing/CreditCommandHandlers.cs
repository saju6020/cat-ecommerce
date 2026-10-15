using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
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

/// <summary>C16's result; <see cref="Replayed"/> is true when an <c>Idempotency-Key</c> found the credit note already issued (ADR-187).</summary>
public sealed record IssuedCreditNoteResult(Guid CreditNoteId, string Number, decimal ReleasedToCustomerCredit, bool Replayed = false);

/// <summary>C19's result.</summary>
public sealed record IssuedRefundResult(Guid RefundId, bool Replayed = false);

/// <summary>
/// HO-BIL-001 §8.9 / §11.4 C16 (US-BIL-05 AC2, ADR-184, 185, 186) -- one posting: the credit note (numbered from its own gapless
/// series, with the bounded numbering retry), the credit applied to the invoice and the excess released from the invoice's
/// payments, newest allocation first. Per-line limits are the calculator's; the totals are the invoice's.
/// </summary>
public sealed class IssueCreditNoteCommandHandler(
    IAggregateRootRepository<CreditNoteAggregate> creditNoteRepository,
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IBillingFinder finder,
    IDocumentNumbering numbering,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<IssueCreditNoteCommand>
{
    public async Task<CommandResponse> HandleAsync(IssueCreditNoteCommand command)
    {
        if (command.IdempotencyKey is { } key)
        {
            var prior = await finder.FindCreditNoteByIdempotencyKeyAsync(command.UserContext!.UserId, key);
            if (prior is not null)
                return prior.InvoiceId == command.InvoiceId
                    ? Success(new IssuedCreditNoteResult(prior.CreditNoteId, prior.Number, prior.ReleasedToCustomerCredit, Replayed: true))
                    : Violation(BusinessViolationCodes.IdempotencyKeyConflict, nameof(command.IdempotencyKey), BillingMessages.IdempotencyKeyConflict);
        }

        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        if (invoice.Status == InvoiceStatus.Credited)
            return Violation(BusinessViolationCodes.NothingToCredit, nameof(command.InvoiceId), "There is nothing left to credit on this invoice.");

        if (!InvoiceActionRules.CanApplyCredit(invoice.Status))
            return Violation(BusinessViolationCodes.InvoiceNotIssued, nameof(command.InvoiceId), "A credit note can only be issued against an issued invoice.");

        var branch = await branchRepository.GetByIdAsync(invoice.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var actor = command.ToBillingActor();
        var now = timeProvider.GetUtcNow();

        var built = await BuildLinesAsync(command, invoice);
        if (built.Violation is not null) return built.Violation;
        var lines = built.Lines;
        var total = lines.Sum(l => l.GrossAmount);

        if (total > invoice.Total - invoice.CreditedTotal)
            return Violation(BusinessViolationCodes.CreditExceedsInvoice, nameof(command.InvoiceId), "The credit is more than the invoice total.");

        // Step 3: an invoice that would be over-paid after the credit gives the excess back, newest allocation first.
        var excess = Math.Max(0m, invoice.PaidTotal - (invoice.Total - (invoice.CreditedTotal + total)));
        var released = 0m;
        var participants = new List<IPostingParticipant>();
        try
        {
            if (excess > 0m)
            {
                foreach (var paymentId in await finder.FindPaymentIdsAllocatedToInvoiceAsync(invoice.Id))
                {
                    if (excess <= 0m) break;

                    var payment = await paymentRepository.GetByIdAsync(paymentId);
                    if (payment is null) continue;

                    var part = Math.Min(payment.AllocatedTo(invoice.Id), excess);
                    if (part <= 0m) continue;

                    payment.Release(new PaymentReleaseDto(invoice.Id, part, AllocationReleaseReason.CreditNoteExcess, now, actor));
                    invoice.ReleasePayment(part, actor);
                    participants.Add(BillingPosting.Of(paymentRepository, payment));
                    excess -= part;
                    released += part;
                }

                if (excess > 0m)
                    return Violation(BusinessViolationCodes.CreditExceedsInvoice, nameof(command.InvoiceId), "The payments applied to this invoice do not cover the credit; contact support.");
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId);
        var issueDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var financialYear = FinancialYearRules.For(issueDate, settings.GetInt(SettingKeys.BillingFinancialYearStartMonth));

        DocumentNumberSeriesAggregate series;
        try
        {
            series = await numbering.EnsureSeriesAsync(invoice.BranchId, financialYear, BillingDocumentKind.CreditNote);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        var sequence = series.Take();
        var creditNote = new CreditNoteAggregate(command.CreditNoteId);
        try
        {
            creditNote.Issue(new CreditNoteIssueDto(
                invoice.Id, invoice.BranchId, invoice.CustomerId, invoice.CurrencyCode,
                DocumentNumberFormat.Format(BillingDocumentKind.CreditNote, branch.Code, financialYear, sequence), financialYear, sequence,
                BillingMapper.ParseEnum<CreditNoteKind>(command.Kind), command.Reason, lines, released, now, issueDate, command.IdempotencyKey, actor));
            invoice.ApplyCredit(total, actor);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        participants.Add(BillingPosting.Of(invoiceRepository, invoice));
        await numbering.SaveWithRetryAsync(
            series,
            () => BillingPosting.CommitNewAsync(creditNoteRepository, creditNote, [.. participants]),
            collided => creditNote.Renumber(DocumentNumberFormat.Format(BillingDocumentKind.CreditNote, branch.Code, financialYear, collided), collided));

        return Success(new IssuedCreditNoteResult(creditNote.Id, creditNote.Number, creditNote.ReleasedToCustomerCredit));
    }

    private sealed record Built(IReadOnlyList<CreditNoteLineInput> Lines, CommandResponse? Violation);

    /// <summary>Step 1 and 2: what is left on each line, then the credit at the line's frozen rate.</summary>
    private async Task<Built> BuildLinesAsync(IssueCreditNoteCommand command, InvoiceAggregate invoice)
    {
        var credited = await finder.GetCreditedSoFarAsync(invoice.Id);
        var bases = invoice.Lines.OrderBy(l => l.SortOrder).ToDictionary(l => l.Id, l =>
        {
            var so = credited.TryGetValue(l.Id, out var c) ? c : new CreditedSoFar(l.Id, 0m, 0m, 0m, 0m);
            return new CreditLineBasis(
                l.Id, l.Description, l.Quantity, l.NetAmount, l.TaxAmount, l.GrossAmount, l.TaxRatePercent, so.Quantity, so.Net, so.Tax, so.Gross);
        });

        var result = new List<CreditNoteLineInput>();
        try
        {
            if (command.FullCredit)
            {
                foreach (var basis in bases.Values)
                {
                    var rest = InvoiceCalculator.Remainder(basis);
                    if (rest.Gross <= 0m) continue;
                    result.Add(new CreditNoteLineInput(basis.InvoiceLineId, basis.Description, null, null, rest.Net, basis.TaxRatePercent, rest.Tax, rest.Gross));
                }
            }
            else
            {
                foreach (var request in command.Lines ?? [])
                {
                    if (!bases.TryGetValue(request.InvoiceLineId, out var basis))
                        return new Built([], Violation(BusinessViolationCodes.InvoiceLineNotFound, "Lines", "A line was not found on this invoice."));

                    var amounts = request.Quantity is { } quantity
                        ? InvoiceCalculator.CreditByQuantity(basis, quantity)
                        : InvoiceCalculator.CreditByAmount(basis, request.Amount!.Value);

                    result.Add(new CreditNoteLineInput(
                        basis.InvoiceLineId, basis.Description, request.Quantity, request.Amount, amounts.Net, basis.TaxRatePercent, amounts.Tax, amounts.Gross));

                    // The same line named twice is credited cumulatively.
                    bases[basis.InvoiceLineId] = basis with
                    {
                        CreditedQuantity = basis.CreditedQuantity + (request.Quantity ?? 0m), CreditedNet = basis.CreditedNet + amounts.Net,
                        CreditedTax = basis.CreditedTax + amounts.Tax, CreditedGross = basis.CreditedGross + amounts.Gross,
                    };
                }
            }
        }
        catch (DomainRuleViolationException ex)
        {
            return new Built([], Violation(ex.ErrorCode, ex.PropertyName, ex.Message));
        }

        return result.Count == 0
            ? new Built([], Violation(BusinessViolationCodes.NothingToCredit, "Lines", "There is nothing left to credit on this invoice."))
            : new Built(result, null);
    }
}

/// <summary>HO-BIL-001 §8.9 C17 (US-BIL-05 AC2) -- a draft copying the credited invoice's lines, linked both ways to the invoice and
/// the credit note. One live corrected draft per credit note.</summary>
public sealed class CreateCorrectedDraftCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IAggregateRootRepository<CreditNoteAggregate> creditNoteRepository,
    IBillingFinder finder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CreateCorrectedDraftCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateCorrectedDraftCommand command)
    {
        var source = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (source is null || source.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        var creditNote = await creditNoteRepository.GetByIdAsync(command.CreditNoteId);
        if (creditNote is null || creditNote.InvoiceId != source.Id)
            return Violation(BusinessViolationCodes.CreditNoteNotFound, nameof(command.CreditNoteId), "That credit note does not belong to this invoice.");

        if (await finder.FindCorrectingInvoiceIdAsync(creditNote.Id) is not null)
            return Violation(BusinessViolationCodes.CorrectedDraftInvalid, nameof(command.CreditNoteId), "A corrected invoice already exists for this credit note.");

        var lines = source.Lines.OrderBy(l => l.SortOrder).Select(l => new DraftInvoiceLineInput(
            null, l.Id, l.BusinessLine, l.ItemKind, l.SourceKind, l.SourceId, l.BillingGroupKind, l.BillingGroupId, l.CatId,
            l.Description, l.Quantity, l.UnitPrice, l.LineDiscount, l.TaxClassId)).ToList();

        var draft = new InvoiceAggregate(command.NewInvoiceId);
        try
        {
            draft.CreateDraft(new DraftInvoiceDto(
                source.BranchId, source.CustomerId, source.CurrencyCode, lines, null, source.Id, creditNote.Id, timeProvider.GetUtcNow(), command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await invoiceRepository.SaveAsync(draft, EventPublishOption.InMemoryPublishOnly);
        return Success(draft.Id);
    }
}

/// <summary>HO-BIL-001 §8.9 C18 -- edit a corrected draft's lines (description, quantity, unit price, discount, tax class).</summary>
public sealed class SetCorrectedDraftLinesCommandHandler(
    IAggregateRootRepository<InvoiceAggregate> invoiceRepository,
    IBillingPricingInputs pricing,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SetCorrectedDraftLinesCommand>
{
    public async Task<CommandResponse> HandleAsync(SetCorrectedDraftLinesCommand command)
    {
        var invoice = await invoiceRepository.GetByIdAsync(command.InvoiceId);
        if (invoice is null || invoice.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.InvoiceNotFound, nameof(command.InvoiceId), "Invoice not found.");

        if (invoice.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This invoice has changed since you loaded it. Reload and try again.");

        if (invoice.Status != InvoiceStatus.Draft)
            return Violation(BusinessViolationCodes.InvoiceNotDraft, nameof(command.InvoiceId), BillingMessages.InvoiceNotDraft);

        if (invoice.CorrectsInvoiceId is null || command.Lines.Count != invoice.Lines.Count)
            return Violation(BusinessViolationCodes.CorrectedDraftInvalid, nameof(command.Lines), "A corrected draft keeps the same lines; only their details change.");

        var rates = await pricing.GetTaxRatesAsync(command.Lines.Select(l => l.TaxClassId).Distinct().ToList(), timeProvider.GetUtcNow());
        var existing = invoice.Lines.OrderBy(l => l.SortOrder).ToList();
        var inputs = new List<DraftInvoiceLineInput>();
        for (var i = 0; i < existing.Count; i++)
        {
            var edit = command.Lines[i];
            var line = existing[i];

            if (!rates.ContainsKey(edit.TaxClassId))
                return Violation(BusinessViolationCodes.TaxClassNotFound, nameof(command.Lines), "Tax class not found.");

            if (edit.LineDiscount > InvoiceCalculator.Round(edit.Quantity * edit.UnitPrice))
                return Violation(BusinessViolationCodes.DiscountExceedsAmount, nameof(command.Lines), "A discount cannot exceed the line amount.");

            inputs.Add(new DraftInvoiceLineInput(
                null, line.CorrectsInvoiceLineId, line.BusinessLine, line.ItemKind, line.SourceKind, line.SourceId, line.BillingGroupKind,
                line.BillingGroupId, line.CatId, edit.Description.Trim(), edit.Quantity, edit.UnitPrice, edit.LineDiscount, edit.TaxClassId));
        }

        try
        {
            invoice.ReviseCorrectedLines(inputs, command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await invoiceRepository.UpdateAsync(invoice, EventPublishOption.InMemoryPublishOnly);
        return Success(invoice.Id);
    }
}

/// <summary>
/// HO-BIL-001 §8.9 C19 (FR-BIL-5, FR-ORD-15) -- a refund drawn from an accepted payment's unallocated amount: method must be one the
/// original method allows, the refund and <c>payment.RecordRefund</c> commit together, the message is enqueued before the save and
/// never carries the reason.
/// </summary>
public sealed class IssueRefundCommandHandler(
    IAggregateRootRepository<RefundAggregate> refundRepository,
    IAggregateRootRepository<PaymentAggregate> paymentRepository,
    IAggregateRootRepository<CreditNoteAggregate> creditNoteRepository,
    IBillingFinder finder,
    INotificationOutbox outbox,
    TimeProvider timeProvider,
    IAggregateRootRepository<BranchAggregate> branchRepository)
    : ICommandHandlerAsync<IssueRefundCommand>
{
    public async Task<CommandResponse> HandleAsync(IssueRefundCommand command)
    {
        if (command.IdempotencyKey is { } key)
        {
            var prior = await finder.FindRefundByIdempotencyKeyAsync(command.UserContext!.UserId, key);
            if (prior is not null)
                return prior.PaymentId == command.PaymentId
                    ? Success(new IssuedRefundResult(prior.RefundId, Replayed: true))
                    : Violation(BusinessViolationCodes.IdempotencyKeyConflict, nameof(command.IdempotencyKey), BillingMessages.IdempotencyKeyConflict);
        }

        var payment = await paymentRepository.GetByIdAsync(command.PaymentId);
        if (payment is null || payment.BranchId != command.BranchId)
            return Violation(BusinessViolationCodes.PaymentNotFound, nameof(command.PaymentId), "Payment not found.");

        var method = BillingMapper.ParseEnum<RefundMethod>(command.Method);
        if (!PaymentMethodRules.IsAllowedRefundMethod(payment.Method, method))
            return Violation(BusinessViolationCodes.RefundMethodNotAllowed, nameof(command.Method), $"A {payment.Method.DisplayName} payment cannot be refunded by {method.DisplayName}.");

        string? creditNoteNumber = null;
        if (command.CreditNoteId is { } creditNoteId)
        {
            var creditNote = await creditNoteRepository.GetByIdAsync(creditNoteId);
            if (creditNote is null || creditNote.CustomerId != payment.CustomerId)
                return Violation(BusinessViolationCodes.CreditNoteNotFound, nameof(command.CreditNoteId), "Credit note not found.");
            creditNoteNumber = creditNote.Number;
        }

        var branch = await branchRepository.GetByIdAsync(payment.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var now = timeProvider.GetUtcNow();
        var businessDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId)).DateTime);
        var actor = command.ToBillingActor();

        var refund = new RefundAggregate(command.RefundId);
        try
        {
            refund.Issue(new RefundIssueDto(
                payment.Id, command.CreditNoteId, payment.BranchId, payment.CustomerId, payment.CurrencyCode, method, command.Amount,
                command.Reference, command.Reason, now, businessDate, command.IdempotencyKey, actor));
            payment.RecordRefund(command.Amount);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        BillingNotifications.EnqueueRefundIssued(
            outbox, payment.CustomerId, payment.BranchId, command.Amount, payment.CurrencyCode, method, creditNoteNumber, command.CorrelationId, command.UserContext!);

        await BillingPosting.CommitNewAsync(refundRepository, refund, BillingPosting.Of(paymentRepository, payment));
        return Success(new IssuedRefundResult(refund.Id));
    }
}
