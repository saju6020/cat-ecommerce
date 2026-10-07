using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record SubcategoryViewModel(string Value, int ProductCount);
public sealed record CategoryViewModel(string Value, int ProductCount, IReadOnlyList<SubcategoryViewModel> Subcategories);

/// <summary>HO-SHP-001 section 8.2/12.4 -- the code-defined taxonomy with product counts.</summary>
public sealed class GetCategoriesQuery : Query<IReadOnlyList<CategoryViewModel>>;
