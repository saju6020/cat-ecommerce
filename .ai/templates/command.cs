using {{RootNamespace}}.Shared.SharedDto.[ContextName];
using Platform.Infrastructure.Core.Commands;

namespace {{RootNamespace}}.Application.Commands.[ContextName]
{
    /// <summary>
    /// Template for a typical CQRS Command.
    /// Commands must extend Platform.Infrastructure.Core.Commands.Command.
    /// Commands should be immutable (using init properties) and contain only data.
    /// </summary>
    public class [ActionName]Command : Command
    {
        // Add your primitive and DTO payload properties here.
        // Example:
        // public Guid ResourceId { get; init; }
        // public string ResourceName { get; init; } = string.Empty;
    }
}
