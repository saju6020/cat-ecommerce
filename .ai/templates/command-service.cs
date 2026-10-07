using {{RootNamespace}}.Application.CommandServices.Abstractions;
using {{RootNamespace}}.Domain.Aggregates;
using Platform.Infrastructure.Core.Domain;

namespace {{RootNamespace}}.Application.CommandServices;

/// <summary>
/// Template for a CommandService implementation.
/// Responsibilities:
/// 1. Encapsulate cross-entity database lookups (existence checks, uniqueness checks).
/// 2. Inject IStateRepository for querying non-aggregate entities and IAggregateRootRepository for aggregates.
/// 3. Use Primary Constructor for DI.
/// 4. Always filter with !x.IsMarkedToDelete.
/// </summary>
public class [EntityName]CommandService(
    IStateRepository stateRepository,
    IAggregateRootRepository<[EntityName]Aggregate> aggregateRepository) : I[EntityName]CommandService
{
    private readonly IStateRepository _stateRepository = stateRepository;
    private readonly IAggregateRootRepository<[EntityName]Aggregate> _aggregateRepository = aggregateRepository;

    public async Task<[EntityName]Aggregate?> GetAsync(Guid id)
    {
        return await _aggregateRepository.GetByFilterAsync(
            x => !x.IsMarkedToDelete && x.Id == id);
    }

    public async Task<bool> IsDuplicateNameAsync(Guid parentId, string name, Guid? currentId = null)
    {
        var nameIdx = name.Trim().ToLowerInvariant().Replace(" ", string.Empty);

        var duplicate = await _aggregateRepository.GetByFilterAsync(x =>
            !x.IsMarkedToDelete &&
            x.Id != currentId &&
            x.[EntityName]NameIdx == nameIdx);

        return duplicate is not null;
    }

    // Add more cross-entity validation methods as needed:
    // public async Task<bool> IsDuplicateCodeAsync(...) { ... }
    // public async Task<int> GetNextOrderAsync(...) { ... }
}
