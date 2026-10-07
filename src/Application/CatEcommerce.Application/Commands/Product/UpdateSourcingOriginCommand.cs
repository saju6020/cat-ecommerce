using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C9.</summary>
public sealed class UpdateSourcingOriginCommand : Command
{
    public required string CountryCode { get; init; }
    public string? SourcingNotes { get; init; }
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
}
