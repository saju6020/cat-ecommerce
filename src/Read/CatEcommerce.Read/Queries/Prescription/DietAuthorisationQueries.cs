using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Prescription;

// HO-PHM-001 §12.6 (US-PHM-08). A query never writes.

/// <summary>A cat diet authorisations; <c>Scope</c> is <c>active</c> (default -- in date and not revoked) or <c>all</c>.</summary>
public sealed class GetCatDietAuthorisationsQuery : Query<IReadOnlyList<DietAuthorisationViewModel>>
{
    public required Guid CatId { get; init; }
    public string Scope { get; init; } = "active";
}

/// <summary>HO-EMR-001 contract: the diet authorisations made during one visit (all statuses).</summary>
public sealed class GetVisitDietAuthorisationsQuery : Query<IReadOnlyList<DietAuthorisationViewModel>>
{
    public required Guid VisitId { get; init; }
}

public sealed class SearchDietProductsQuery : Query<IReadOnlyList<DietProductViewModel>>
{
    public string? Search { get; init; }
}

/// <summary>The owner authorisations, only for the caller CURRENT cats. <c>Scope</c> is <c>active</c> (default) or <c>past</c>.</summary>
public sealed class GetCustomerDietAuthorisationsQuery : Query<IReadOnlyList<CustomerDietAuthorisationViewModel>>
{
    public required Guid CustomerId { get; init; }
    public string Scope { get; init; } = "active";
    public Guid? CatId { get; init; }
}

/// <summary>Worker: ids of authorisations still stored as Active whose last day has passed (the command re-checks).</summary>
public sealed class GetExpirableDietAuthorisationsQuery : Query<IReadOnlyList<Guid>>
{
    public int Limit { get; init; } = 200;
}
