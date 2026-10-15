using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-ORD-001 §11.3 -- the whole definition of a discount code as the create and update commands carry it. <c>CategoryIds</c> are
/// <c>ProductCategory</c> enumeration values (Shop's top-level categories are code-defined, ADR-078, not rows).</summary>
public sealed record DiscountDefinitionDto(
    string Code,
    string? Description,
    DiscountKind Kind,
    decimal? Percent,
    decimal? Amount,
    string CurrencyCode,
    decimal? MinimumSpend,
    DiscountScope Scope,
    IReadOnlyList<Guid> ProductIds,
    IReadOnlyList<int> CategoryIds,
    bool FirstOrderOnly,
    int? UsageLimit,
    int? PerCustomerLimit,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsActive,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>A discount code as the pure <c>DiscountRules</c> sees it (a snapshot of the aggregate's rule fields).</summary>
public sealed record DiscountRuleSet(
    Guid DiscountCodeId,
    string Code,
    DiscountKind Kind,
    long? PercentHundredths,
    MinorUnits? Amount,
    MinorUnits? MinimumSpend,
    DiscountScope Scope,
    IReadOnlySet<Guid> ProductIds,
    IReadOnlySet<int> CategoryIds,
    bool FirstOrderOnly,
    int? UsageLimit,
    int TimesRedeemed,
    int? PerCustomerLimit,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool IsActive,
    string CurrencyCode);

/// <summary>A cart line as the discount rules need it: its identity, product, category and subtotal in minor units.</summary>
public sealed record DiscountCartLine(Guid LineRef, Guid ProductId, int CategoryId, MinorUnits Subtotal);

/// <summary>What the order history says about the customer, for the per-customer and first-order rules (US-ORD-05).</summary>
public sealed record CustomerDiscountFacts(int TimesUsedByCustomer, bool HasPriorNonCancelledOrder);

/// <summary>The amounts a code takes off a cart: per-line discounts (scoped codes), the order discount and its apportionment (unscoped codes).</summary>
public sealed record DiscountApplication(
    IReadOnlyDictionary<Guid, MinorUnits> LineDiscounts,
    MinorUnits OrderDiscount,
    IReadOnlyDictionary<Guid, MinorUnits> OrderDiscountShares)
{
    public static readonly DiscountApplication None = new(new Dictionary<Guid, MinorUnits>(), MinorUnits.Zero, new Dictionary<Guid, MinorUnits>());

    public MinorUnits LineDiscountTotal => new(LineDiscounts.Values.Sum(v => v.Value));
}

/// <summary>The outcome of evaluating a code: applied (with its amounts) or the first failing rule's code and plain message (US-ORD-05 AC2).</summary>
public sealed record DiscountEvaluation(bool Applied, DiscountApplication Application, string? ReasonCode, string? Message)
{
    public static DiscountEvaluation Success(DiscountApplication application) => new(true, application, null, null);

    public static DiscountEvaluation Failure(string code, string message) => new(false, DiscountApplication.None, code, message);
}
