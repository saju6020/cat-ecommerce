using CatEcommerce.Application.Commands.Administration;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Administration;

/// <summary>Owner decision 2026-09-26 (supersedes the earlier owner decision (b) and the
/// <c>ReservedPolicyKeys</c> prefix check it was narrowed to): every policy document's create and
/// publish is Administrator-only, for any key, regardless of <c>RequiresAcceptance</c>. That
/// decision is enforced entirely at the authorization layer -- <c>[RequireAction(Actions.Policies
/// .Manage)]</c> on <c>AdminPoliciesController.Create</c>/<c>PublishVersion</c>, an action held only
/// by Administrator in <c>ActionRoles.Administration.cs</c> -- so this handler orchestrates only: it
/// does not decide who may call it (rule 20). A caller who lacks the action never reaches
/// <c>HandleAsync</c> at all.</summary>
public sealed class CreatePolicyDocumentCommandHandler(IAggregateRootRepository<PolicyDocumentAggregate> repository) : ICommandHandlerAsync<CreatePolicyDocumentCommand>
{
    public async Task<CommandResponse> HandleAsync(CreatePolicyDocumentCommand command)
    {
        if (await repository.ExistAsync(p => p.Key == command.Key))
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.PolicyKeyAlreadyExists, nameof(command.Key), $"Policy key '{command.Key}' is already in use.");

        var document = new PolicyDocumentAggregate(command.PolicyDocumentId);
        try
        {
            document.Create(command.ToDomainDto());
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await repository.SaveAsync(document, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = document.Id };
    }
}

/// <summary>§11.4 (C13) / ADR-052's recorded rule-1 exception: next version number = current + 1;
/// two writes -- SaveAsync(version) then UpdateAsync(document). See
/// <see cref="CreatePolicyDocumentCommandHandler"/>'s doc comment: publishing is Administrator-only
/// via <c>[RequireAction(Actions.Policies.Manage)]</c>, not a check in this handler.</summary>
public sealed class PublishPolicyVersionCommandHandler(
    IAggregateRootRepository<PolicyVersionAggregate> versionRepository,
    IAggregateRootRepository<PolicyDocumentAggregate> documentRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PublishPolicyVersionCommand>
{
    public async Task<CommandResponse> HandleAsync(PublishPolicyVersionCommand command)
    {
        var document = await documentRepository.GetByFilterAsync(d => d.Key == command.PolicyKey);
        if (document is null) return CreateBranchCommandHandler.Violation(BusinessViolationCodes.PolicyNotFound, nameof(command.PolicyKey), "Policy document not found.");

        var nextVersionNumber = (document.CurrentVersionNumber ?? 0) + 1;
        var now = timeProvider.GetUtcNow();

        var version = new PolicyVersionAggregate(command.PolicyVersionId);
        try
        {
            version.Create(command.ToDomainDto(nextVersionNumber, now, document.RequiresAcceptance));
        }
        catch (DomainRuleViolationException ex)
        {
            return CreateBranchCommandHandler.Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await versionRepository.SaveAsync(version, EventPublishOption.InMemoryPublishOnly);

        document.SetCurrentVersion(new PolicyDocumentSetCurrentVersionDto(nextVersionNumber, command.UserContext, command.CorrelationId));
        await documentRepository.UpdateAsync(document, EventPublishOption.DoNotPublish);

        return new CommandResponse { IsSuccess = true, Result = version.Id };
    }
}

/// <summary>§11.4: version exists and is the current version; already accepted -&gt; success, no
/// write (idempotent, unique (customer, key, version) index).</summary>
public sealed class AcceptPolicyVersionCommandHandler(
    IAggregateRootRepository<PolicyAcceptanceAggregate> acceptanceRepository,
    IAggregateRootRepository<PolicyDocumentAggregate> documentRepository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AcceptPolicyVersionCommand>
{
    public async Task<CommandResponse> HandleAsync(AcceptPolicyVersionCommand command)
    {
        var document = await documentRepository.GetByFilterAsync(d => d.Key == command.PolicyKey);
        if (document is null || document.CurrentVersionNumber != command.VersionNumber)
            return CreateBranchCommandHandler.Violation(BusinessViolationCodes.PolicyVersionNotCurrent, nameof(command.VersionNumber), "This is not the current policy version.");

        var alreadyAccepted = await acceptanceRepository.ExistAsync(a =>
            a.CustomerAccountId == command.UserContext.UserId && a.PolicyKey == command.PolicyKey && a.VersionNumber == command.VersionNumber);
        if (alreadyAccepted) return new CommandResponse { IsSuccess = true };

        var acceptance = new PolicyAcceptanceAggregate(Guid.NewGuid());
        acceptance.Create(command.ToDomainDto(timeProvider.GetUtcNow()));

        // Create() now raises PolicyVersionAcceptedEvent (gate fix 2026-09-26) -- publish it.
        await acceptanceRepository.SaveAsync(acceptance, EventPublishOption.InMemoryPublishOnly);
        return new CommandResponse { IsSuccess = true, Result = acceptance.Id };
    }
}
