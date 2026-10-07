using CatEcommerce.Read.ViewModels;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetStaffProductDetailQuery : Query<StaffProductDetailViewModel?>
{
    public required Guid ProductId { get; init; }
}
