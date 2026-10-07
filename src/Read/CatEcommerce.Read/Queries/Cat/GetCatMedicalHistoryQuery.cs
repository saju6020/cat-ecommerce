using Platform.Infrastructure.Core.Queries;
using CatEcommerce.Read.ViewModels;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>Q5 -- feeds the medical history PDF (US-CAT-10).</summary>
public sealed class GetCatMedicalHistoryQuery : Query<CatMedicalHistoryViewModel?>
{
    public required Guid CatId { get; init; }
}
