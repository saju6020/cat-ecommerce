using {{RootNamespace}}.Domain.Aggregates;

namespace {{RootNamespace}}.Application.CommandServices.Abstractions;

/// <summary>
/// Template for a CommandService interface.
/// Define methods for cross-entity validation and data retrieval.
/// Common methods:
/// - GetParentEntityAsync: Retrieve parent entity info.
/// - GetAsync: Retrieve the target entity.
/// - IsDuplicateNameAsync: Uniqueness checks.
/// </summary>
public interface I[EntityName]CommandService
{
    Task<[EntityName]Aggregate?> GetAsync(Guid id);
    Task<bool> IsDuplicateNameAsync(Guid parentId, string name, Guid? currentId = null);

    // Add more methods as needed:
    // Task<bool> IsDuplicateCodeAsync(Guid parentId, string code, Guid? currentId = null);
    // Task<int> GetNextOrderAsync(Guid parentId);
}
