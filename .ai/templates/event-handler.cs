using {{RootNamespace}}.Domain.Events.[ContextName];
using {{RootNamespace}}.Read.ViewModels;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;

namespace {{RootNamespace}}.Read.EventHandlers.[ContextName];

/// <summary>
/// Template for a read-side Event Handler (projection). Working reference:
/// src/Read/Bits.Procurement.Read/EventHandlers/Vendor/VendorRegisteredEventHandler.cs.
/// Registered as an EventConsumerAdapter&lt;[EventName]Event&gt; in the EventWorker host only —
/// see .ai/host-composition.md and .ai/di-registration-guide.md.
///
/// Standard responsibilities:
/// 1. Implement IEventHandlerAsync&lt;TEvent&gt; directly — there is no base class.
/// 2. Inject IReadRepository and ILogger via primary constructor. There is no notifier to
///    inject: success notification is declarative, not something this handler calls. If
///    [EventName]Event implements INotifiedByEventHandler (Platform.Infrastructure.Core.Notification),
///    the platform's NotificationHandlerMiddleware delivers it on its own once the event is
///    published — see VendorRegisteredEvent for the shape.
/// 3. Build or update the read-side ViewModel via IReadRepository.CreateAsync/UpdateAsync inside a
///    try/catch that logs and rethrows, so the message is retried/dead-lettered rather than
///    silently dropped.
/// 4. Per ADR-006: any parent scalar field this projection updates must be assigned straight from
///    the event, never accumulated (no `+= `), so redelivery of the same event converges instead
///    of compounding.
/// </summary>
public sealed class [EventName]EventHandler(
    IReadRepository readRepository,
    ILogger<[EventName]EventHandler> logger) : IEventHandlerAsync<[EventName]Event>
{
    public async Task HandleAsync([EventName]Event @event)
    {
        logger.LogInformation(
            "Projecting [EventName]Event for [EntityName]Id {[EntityName]Id}. CorrelationId: {CorrelationId}",
            @event.[EntityName]Id,
            @event.CorrelationId);

        try
        {
            var view = new [EntityName]ViewModel();
            view.Map(@event);
            view.SetDefaultValue(@event.UserContext);

            await readRepository.CreateAsync(view);

            logger.LogInformation(
                "[EntityName] {[EntityName]Id} projected to read store. CorrelationId: {CorrelationId}",
                @event.[EntityName]Id,
                @event.CorrelationId);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error projecting [EventName]Event for [EntityName]Id {[EntityName]Id}. CorrelationId: {CorrelationId}",
                @event.[EntityName]Id,
                @event.CorrelationId);
            throw;
        }
    }
}
