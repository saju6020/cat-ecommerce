using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>HO-BIL-001 section 11.4 C26 (ADR-190) -- writes one <see cref="AccountingExportRunAggregate"/>; the audit row is written by the event
/// handler (<c>AuditBillingDecisionHandler</c>) in the same request.</summary>
public sealed class RecordAccountingExportRunCommandHandler(
    IAggregateRootRepository<AccountingExportRunAggregate> runRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordAccountingExportRunCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordAccountingExportRunCommand command)
    {
        var run = new AccountingExportRunAggregate(command.RunId);
        try
        {
            run.Record(new AccountingExportRunDto(
                command.From, command.To, command.BranchId, command.InvoiceCount, command.InvoiceLineCount, command.PaymentCount, command.AllocationCount,
                command.CreditNoteCount, command.RefundCount, command.Sha256, timeProvider.GetUtcNow(), command.ToBillingActor()));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await runRepository.SaveAsync(run, EventPublishOption.InMemoryPublishOnly);
        return Success(run.Id);
    }
}
