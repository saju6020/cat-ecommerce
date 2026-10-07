using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetCountriesQueryHandler : IQueryHandlerAsync<GetCountriesQuery, IReadOnlyList<CountryViewModel>>
{
    public Task<QueryResponse<IReadOnlyList<CountryViewModel>>> HandleAsync(GetCountriesQuery query)
    {
        var items = CountryCodes.All.OrderBy(kv => kv.Value, StringComparer.Ordinal)
            .Select(kv => new CountryViewModel(kv.Key, kv.Value))
            .ToList();

        return Task.FromResult(new QueryResponse<IReadOnlyList<CountryViewModel>> { Result = items });
    }
}
