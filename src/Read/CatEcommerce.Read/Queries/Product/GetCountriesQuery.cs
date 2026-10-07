using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record CountryViewModel(string Code, string Name);

/// <summary>HO-SHP-001 section 12.4 -- <c>CountryCodes</c> as a list, for the product editor.</summary>
public sealed class GetCountriesQuery : Query<IReadOnlyList<CountryViewModel>>;
