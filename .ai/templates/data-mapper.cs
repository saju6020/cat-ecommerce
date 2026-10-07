using {{RootNamespace}}.Application.Commands.[ContextName];
using {{RootNamespace}}.Domain.Models;

namespace {{RootNamespace}}.Application.DataMappers;

/// <summary>
/// Template for mapping Commands to Domain DTOs.
/// DataMappers are static classes with extension methods.
/// Always pass UserContext and CorrelationId through to the domain DTO.
/// </summary>
public static class [EntityName]Mapper
{
    public static [EntityName]CreationDto ToDomainDto(this Create[EntityName]Command command)
    {
        return new [EntityName]CreationDto(
            [EntityName]Id: command.[EntityName]Id,
            // Map other command properties to domain DTO properties here
            // Example:
            // Name: command.Name,
            // Code: command.Code,
            UserContext: command.UserContext,
            CorrelationId: command.CorrelationId);
    }

    // Add additional mapper methods for Update, Toggle, etc.
    // public static [EntityName]UpdateDto ToDomainDto(this Update[EntityName]Command command)
    // {
    //     return new [EntityName]UpdateDto(...);
    // }
}
