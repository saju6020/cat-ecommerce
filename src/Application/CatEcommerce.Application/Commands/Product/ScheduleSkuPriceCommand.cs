using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Product;

/// <summary>HO-SHP-001 section 11.1 C6.</summary>
public sealed class ScheduleSkuPriceCommand : Command
{
    public required Guid SkuPriceId { get; init; }
    public required Guid SkuId { get; init; }
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}
