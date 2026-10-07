using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.Queries.Product;

public sealed record SkuPriceHistoryEntryViewModel(Guid SkuPriceId, CatEcommerce.Read.ViewModels.MoneyViewModel Amount, DateTimeOffset EffectiveFrom, Guid SetBy);

public sealed record SkuPriceHistoryViewModel(
    SkuPriceHistoryEntryViewModel? Current,
    IReadOnlyList<SkuPriceHistoryEntryViewModel> Scheduled,
    IReadOnlyList<SkuPriceHistoryEntryViewModel> Past);

/// <summary>HO-SHP-001 section 12.4.</summary>
public sealed class GetSkuPriceHistoryQuery : Query<SkuPriceHistoryViewModel?>
{
    public required Guid SkuId { get; init; }
}
