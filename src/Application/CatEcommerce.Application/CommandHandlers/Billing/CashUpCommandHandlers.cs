using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>
/// HO-BIL-001 section 11.4 C20 (US-BIL-12, US-BIL-06 AC3) -- load-or-create the cash-up of <c>(branch, date)</c> and record the count. The
/// aggregate decides (the date is not in the future, the day is open); a recount replaces the earlier count.
/// </summary>
public sealed class RecordCashCountCommandHandler(
    IAggregateRootRepository<CashUpAggregate> cashUpRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordCashCountCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCashCountCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var now = timeProvider.GetUtcNow();
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId)).DateTime);

        var id = CashUpIds.For(command.BranchId, command.BusinessDate);
        var cashUp = await cashUpRepository.GetByIdAsync(id);
        var isNew = cashUp is null;
        if (cashUp is null)
        {
            cashUp = new CashUpAggregate(id);
            cashUp.Open(command.BranchId, command.BusinessDate, command.UserContext!);
        }

        try
        {
            cashUp.RecordCount(command.CountedCash, command.Explanation, todayLocal, now, command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew) await cashUpRepository.SaveAsync(cashUp, EventPublishOption.InMemoryPublishOnly);
        else await cashUpRepository.UpdateAsync(cashUp, EventPublishOption.InMemoryPublishOnly);

        return Success(cashUp.Id);
    }
}

/// <summary>
/// C21 -- close the day. The expected totals come from the same <see cref="CashUpCalculator"/> the read uses over the same rows, are frozen
/// in the aggregate, and a non-zero variance needs an explanation (US-BIL-12 AC2). A payment still awaiting approval that day blocks the
/// close: its cash is in the drawer but not yet counted as received (<c>PendingApprovalsExist</c>).
/// </summary>
public sealed class CloseCashUpCommandHandler(
    IAggregateRootRepository<CashUpAggregate> cashUpRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IBillingFinder finder,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CloseCashUpCommand>
{
    public async Task<CommandResponse> HandleAsync(CloseCashUpCommand command)
    {
        var branch = await branchRepository.GetByIdAsync(command.BranchId);
        if (branch is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var cashUp = await cashUpRepository.GetByIdAsync(CashUpIds.For(command.BranchId, command.BusinessDate));
        if (cashUp is null)
            return Violation(BusinessViolationCodes.CountRequired, nameof(command.BusinessDate), "Count the cash before closing the day.");

        if (cashUp.Version != command.ExpectedVersion)
            return Violation(BusinessViolationCodes.StaleVersion, nameof(command.ExpectedVersion), "This day has changed since you loaded it. Reload and try again.");

        if (await finder.CountPendingApprovalsAsync(command.BranchId, command.BusinessDate, branch.TimeZoneId) > 0)
            return Violation(BusinessViolationCodes.PendingApprovalsExist, nameof(command.BusinessDate), "Payments waiting for approval must be approved or rejected before the day is closed.");

        var snapshot = CashUpCalculator.Calculate(await finder.GetCashUpRowsAsync(command.BranchId, command.BusinessDate));
        try
        {
            cashUp.Close(snapshot, timeProvider.GetUtcNow(), command.ToBillingActor());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await cashUpRepository.UpdateAsync(cashUp, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
