using Platform.Infrastructure.Common.Security;

namespace {{RootNamespace}}.Domain.Models;

/// <summary>
/// Template for Domain DTOs used to pass data from the Application layer into Aggregate methods.
/// Rules:
/// 1. Always use immutable `record` types.
/// 2. Always include UserContext and CorrelationId as the last parameters.
/// 3. ScopeCorrelationId is optional (used in saga contexts).
/// 4. Property names should match the domain language, not the command properties.
/// </summary>
public record [EntityName]CreationDto(
    Guid [EntityName]Id,
    // Add domain-relevant properties here
    // Example:
    // string Name,
    // string Code,
    // Guid ParentId,
    // bool IsActive,
    UserContext UserContext,
    Guid CorrelationId,
    Guid? ScopeCorrelationId = null);
