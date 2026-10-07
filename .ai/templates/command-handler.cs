using {{RootNamespace}}.Application.Commands.[ContextName];
using {{RootNamespace}}.Application.CommandServices.Abstractions;
using {{RootNamespace}}.Domain.Aggregates;
using {{RootNamespace}}.Domain.Events.ErrorEvents;
using {{RootNamespace}}.Shared.Common.Constants;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Bus;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;

namespace {{RootNamespace}}.Application.CommandHandlers.[ContextName];

/// <summary>
/// Template for a CommandHandler. Working reference:
/// src/Application/Bits.Procurement.Application/CommandHandlers/Vendor/RegisterVendorCommandHandler.cs
/// (paired with RegisterVendorCommandValidator). Registered as
/// ICommandHandlerAsync&lt;[ActionName]Command&gt; in the CommandWorker host only — see
/// .ai/host-composition.md and .ai/di-registration-guide.md. It is never registered in the Api
/// host (ADR-004): the Api sends the command to the bus, CommandWorker consumes and executes it.
///
/// Standard responsibilities:
/// 1. Implement ICommandHandlerAsync&lt;TCommand&gt; directly. There is no base class — no
///    BaseCommandHandler, no GetValidator() override. Shape validation is a separately registered
///    IValidator&lt;TCommand&gt; (FluentValidation) resolved by the platform's IValidationService
///    before HandleAsync ever runs.
/// 2. Inject I[ActionName]CommandService for cross-entity/database lookups (existence,
///    uniqueness) — the handler itself never queries the repository for that.
/// 3. Inject IAggregateRootRepository&lt;TAggregate&gt; to load/save the aggregate. Persist with
///    SaveAsync/UpdateAsync, always passing EventPublishOption.QueuePublishOnly.
/// 4. On a business rule violation: publish a rejection event via
///    IBusMessageDispatcher.PublishAsync (ADR-005 — .ai/decisions/adr.md) *and* return
///    CommandResponse { IsSuccess = false, ValidationResult = ... } built from
///    ValidationResponse.AddError(...). There is no NotifyBusinessViolationAsync — notification is
///    declarative, carried by the rejection event implementing INotifiedByEventHandler.
/// 5. Delegate all state mutation to the aggregate. The handler never sets aggregate state
///    directly.
/// </summary>
public sealed class [ActionName]CommandHandler(
    ILogger<[ActionName]CommandHandler> logger,
    I[ActionName]CommandService commandService,
    IBusMessageDispatcher busMessageDispatcher,
    IAggregateRootRepository<[ActionName]Aggregate> aggregateRepository)
    : ICommandHandlerAsync<[ActionName]Command>
{
    private const string ActionName = "[ActionName]";

    public async Task<CommandResponse> HandleAsync([ActionName]Command command)
    {
        logger.LogInformation(
            "Executing [ActionName] for [EntityName] {EntityId}. CorrelationId: {CorrelationId}",
            command.[EntityName]Id,
            command.CorrelationId);

        // 1. Cross-entity validation (database lookups via CommandService).
        // if (await commandService.IsDuplicateNameAsync(command.ParentId, command.Name))
        // {
        //     var violationMessage = $"A [EntityName] named '{command.Name}' already exists.";
        //
        //     await busMessageDispatcher.PublishAsync(new [ContextName]CommandRejectedEvent
        //     {
        //         [EntityName]Id = command.[EntityName]Id,
        //         ActionName = ActionName,
        //         ViolationCode = BusinessViolationCodes.ResourceAlreadyExists,
        //         ViolationMessage = violationMessage,
        //         PropertyName = nameof(command.Name),
        //         CorrelationId = command.CorrelationId,
        //         UserContext = command.UserContext
        //     });
        //
        //     var validationResponse = new ValidationResponse();
        //     validationResponse.AddError(
        //         errorMessage: violationMessage,
        //         propertyName: nameof(command.Name),
        //         errorCode: BusinessViolationCodes.ResourceAlreadyExists,
        //         resourceName: nameof([ActionName]Aggregate));
        //
        //     return new CommandResponse { IsSuccess = false, ValidationResult = validationResponse };
        // }

        // 2. Aggregate operations — state changes only inside the aggregate's own mutation method.
        // var aggregate = new [ActionName]Aggregate();
        // aggregate.[ActionName](command.ToDomainDto());

        // 3. Persist. Always QueuePublishOnly — the domain events raised above are queued for
        //    EventWorker to project and notify from, not published in-process.
        // await aggregateRepository.SaveAsync(aggregate, EventPublishOption.QueuePublishOnly);

        return new CommandResponse
        {
            IsSuccess = true
            // Result = aggregate.Id
        };
    }
}
