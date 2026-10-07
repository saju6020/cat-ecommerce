using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Cat;

/// <summary>
/// HO-ADM-001 §12.3 / ADR-043. Used only by <c>RequireActionFilter</c>'s customer-ownership check --
/// never exposed on a controller. Returns the owner id, or <c>null</c> when the cat does not exist
/// (the filter treats both "not found" and "owned by someone else" as a 404 -- U-A5).
/// </summary>
public sealed class GetCatOwnerQuery : Query<Guid?>
{
    public required Guid CatId { get; init; }
}
