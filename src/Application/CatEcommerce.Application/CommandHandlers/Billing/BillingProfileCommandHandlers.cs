using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Billing.BillingHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>HO-BIL-001 §11.4 C1 -- load-or-create the profile by id == branch id, then one mutation.</summary>
public sealed class SetBillingProfileCommandHandler(
    IAggregateRootRepository<BranchAggregate> branchRepository,
    IAggregateRootRepository<BillingProfileAggregate> profileRepository)
    : ICommandHandlerAsync<SetBillingProfileCommand>
{
    public async Task<CommandResponse> HandleAsync(SetBillingProfileCommand command)
    {
        if (await branchRepository.GetByIdAsync(command.BranchId) is null)
            return Violation(BusinessViolationCodes.BranchNotFound, nameof(command.BranchId), "Branch not found.");

        var profile = await profileRepository.GetByIdAsync(command.BranchId);
        var isNew = profile is null;
        if (isNew)
        {
            profile = new BillingProfileAggregate(command.BranchId);
            profile.Open(command.UserContext!);
        }

        try
        {
            profile!.Set(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (isNew) await profileRepository.SaveAsync(profile, EventPublishOption.InMemoryPublishOnly);
        else await profileRepository.UpdateAsync(profile, EventPublishOption.InMemoryPublishOnly);

        return Success(command.BranchId);
    }
}
