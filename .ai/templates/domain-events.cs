using Platform.Infrastructure.Core.Events;

namespace {{RootNamespace}}.Domain.Events.[ContextName];

/// <summary>
/// Template for Domain Events.
/// Events must extend Platform.Infrastructure.Core.Events.Event.
/// They are used to trigger projections in the Read side or orchestrate sagas.
/// </summary>
public sealed class [EventName]Event : Event
{
    // Payload required for Read Projections.
    // Example:
    // public Guid ResourceId { get; set; }
    // public string Status { get; set; } = string.Empty;
}
