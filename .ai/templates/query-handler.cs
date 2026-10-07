using {{RootNamespace}}.Read.Queries.[ContextName];
using {{RootNamespace}}.Read.ViewModels;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Queries;

namespace {{RootNamespace}}.Read.QueryHandlers.[ContextName];

/// <summary>
/// Template for a CQRS Query Handler on the Read side.
/// Responsibilities: Fetch ViewModels from MongoDB via IReadRepository asynchronously.
/// </summary>
public class Get[ResourceName]QueryHandler : IQueryHandlerAsync<Get[ResourceName]Query, [ResourceName]ViewModel?>
{
    private readonly ILogger<Get[ResourceName]QueryHandler> _logger;
    private readonly IReadRepository _readRepository;

    public Get[ResourceName]QueryHandler(
        ILogger<Get[ResourceName]QueryHandler> logger,
        IReadRepository readRepository)
    {
        _logger = logger;
        _readRepository = readRepository;
    }

    public async Task<QueryResponse<[ResourceName]ViewModel?>> HandleAsync(Get[ResourceName]Query query)
    {
        _logger.LogInformation("Fetching [ResourceName] for Id = {Id}", query.Id);

        var viewModel = await _readRepository.GetItemAsync<[ResourceName]ViewModel>(
            t => t.Id == query.Id
        );

        if (viewModel == null)
        {
            _logger.LogWarning("[ResourceName] not found for Id = {Id}", query.Id);
            return new QueryResponse<[ResourceName]ViewModel?> { Result = null };
        }

        return new QueryResponse<[ResourceName]ViewModel?> { Result = viewModel };
    }
}
