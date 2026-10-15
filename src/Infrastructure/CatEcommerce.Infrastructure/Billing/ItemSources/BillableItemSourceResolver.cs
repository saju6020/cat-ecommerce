using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Billing.ItemSources;

/// <summary>HO-BIL-001 §12.7 -- the item sources, keyed by <see cref="ChargeSourceKind"/>. A kind Billing
/// cannot capture (Order, BoardingStay, Manual -- those arrive by the push port) resolves to null.</summary>
public sealed class BillableItemSourceResolver(IEnumerable<IBillableItemSource> sources) : IBillableItemSourceResolver
{
    private readonly Dictionary<int, IBillableItemSource> _byKind = sources.ToDictionary(s => s.Kind.Value);

    public IBillableItemSource? Resolve(ChargeSourceKind kind) => _byKind.GetValueOrDefault(kind.Value);
}
