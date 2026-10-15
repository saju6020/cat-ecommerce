using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Prescription;

// HO-PHM-001 §12.6. A query never writes.

public sealed class GetFormularyMedicinesQuery : Query<PagedResult<FormularyMedicineSummaryViewModel>>
{
    public string? Search { get; init; }
    public bool? IsControlled { get; init; }
    public bool? IsActive { get; init; }
}

public sealed class GetFormularyMedicineQuery : Query<FormularyMedicineDetailViewModel?>
{
    public required Guid MedicineId { get; init; }
}

public sealed class SearchFormularySkusQuery : Query<IReadOnlyList<FormularySkuSearchResultViewModel>>
{
    public string? Term { get; init; }
}
