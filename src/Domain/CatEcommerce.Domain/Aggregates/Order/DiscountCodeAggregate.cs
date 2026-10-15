using System.Text.RegularExpressions;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.15 (ADR-207), US-ORD-05 -- a discount code: percentage or fixed amount, minimum spend, scope (the whole order, listed products or
/// listed categories), first-order-only, overall and per-customer limits and a validity window. <b>The overall limit is a counter on the code</b>
/// (<see cref="TimesRedeemed"/>) whose <c>xmin</c> serialises concurrent redemptions: two placements racing for the last use collide on the
/// code's row version and one is told to re-quote (<c>409 CheckoutQuoteChanged</c>). The per-customer count is derived from the customer's orders,
/// so no redemption table exists. The code text is stored upper-case and is unique (a handler check and a unique index). All the
/// <i>rules</i> (which code applies to what) live in the pure <c>DiscountRules</c>; this aggregate owns the definition's shape and the counter.
/// </summary>
public sealed partial class DiscountCodeAggregate : AggregateRoot
{

    public string Code { get; private set; } = default!;
    public string? Description { get; private set; }
    public DiscountKind Kind { get; private set; } = default!;
    public decimal? Percent { get; private set; }
    public decimal? Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public decimal? MinimumSpend { get; private set; }
    public DiscountScope Scope { get; private set; } = default!;
    public IReadOnlyList<Guid> ProductIds { get; private set; } = [];
    public IReadOnlyList<int> CategoryIds { get; private set; } = [];
    public bool FirstOrderOnly { get; private set; }
    public int? UsageLimit { get; private set; }
    public int? PerCustomerLimit { get; private set; }
    public DateTimeOffset? ValidFrom { get; private set; }
    public DateTimeOffset? ValidTo { get; private set; }
    public bool IsActive { get; private set; }
    public int TimesRedeemed { get; private set; }

    private DiscountCodeAggregate() { }

    public DiscountCodeAggregate(Guid id) : base(id) { }

    /// <summary>The text a code is stored and compared as: trimmed and upper-case.</summary>
    public static string Normalise(string code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public void Create(DiscountDefinitionDto dto)
    {
        Apply(dto, isNew: true);
        TimesRedeemed = 0;
        SetDefaultValue(dto.UserContext);
        Raise(dto.UserContext, dto.CorrelationId);
    }

    /// <summary>Edits everything but the code. The overall limit cannot be set below the uses already made.</summary>
    public void Update(DiscountDefinitionDto dto)
    {
        if (Normalise(dto.Code) != Code)
            throw Invalid("A discount code's text cannot be changed. Create a new code instead.", nameof(Code));

        if (dto.UsageLimit is { } limit && limit < TimesRedeemed)
            throw Invalid($"The usage limit cannot be below the {TimesRedeemed} uses already made.", nameof(UsageLimit));

        Apply(dto, isNew: false);
        Raise(dto.UserContext, dto.CorrelationId);
    }

    public void SetActivation(bool isActive, UserContext userContext, Guid correlationId)
    {
        IsActive = isActive;
        Raise(userContext, correlationId);
    }

    /// <summary>Counts one use. Refused at the overall limit (<c>DiscountUsageLimitReached</c>). Runs on the <b>tracked</b> code in the placement
    /// save, so a concurrent redemption of the last use collides on <c>xmin</c>.</summary>
    public void Redeem()
    {
        if (UsageLimit is { } limit && TimesRedeemed >= limit)
            throw new DomainRuleViolationException(BusinessViolationCodes.DiscountUsageLimitReached, "This code has reached its usage limit.", nameof(TimesRedeemed));

        TimesRedeemed++;
        UpdateVersion();
    }

    /// <summary>Gives a use back when an order is cancelled or expires before dispatch. Never below zero.</summary>
    public void ReleaseRedemption()
    {
        if (TimesRedeemed == 0) return;

        TimesRedeemed--;
        UpdateVersion();
    }

    /// <summary>The pure rules' view of this code.</summary>
    public DiscountRuleSet ToRuleSet() =>
        new(Id, Code, Kind,
            Percent is { } percent ? MinorUnits.PercentToHundredths(percent) : null,
            Amount is { } amount ? MinorUnits.FromAmount(amount) : null,
            MinimumSpend is { } minimum ? MinorUnits.FromAmount(minimum) : null,
            Scope, ProductIds.ToHashSet(), CategoryIds.ToHashSet(), FirstOrderOnly, UsageLimit, TimesRedeemed, PerCustomerLimit, ValidFrom, ValidTo, IsActive, CurrencyCode);

    private void Apply(DiscountDefinitionDto dto, bool isNew)
    {
        var code = Normalise(dto.Code);
        Validate(dto, code);

        if (isNew) Code = code;
        Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        Kind = dto.Kind;
        Percent = dto.Kind == DiscountKind.Percentage ? dto.Percent : null;
        Amount = dto.Kind == DiscountKind.FixedAmount ? dto.Amount : null;
        CurrencyCode = dto.CurrencyCode.ToUpperInvariant();
        MinimumSpend = dto.MinimumSpend;
        Scope = dto.Scope;

        ProductIds = dto.Scope == DiscountScope.Products ? dto.ProductIds.Distinct().ToList() : [];
        CategoryIds = dto.Scope == DiscountScope.Categories ? dto.CategoryIds.Distinct().ToList() : [];

        FirstOrderOnly = dto.FirstOrderOnly;
        UsageLimit = dto.UsageLimit;
        PerCustomerLimit = dto.PerCustomerLimit;
        ValidFrom = dto.ValidFrom?.ToUniversalTime();
        ValidTo = dto.ValidTo?.ToUniversalTime();
        IsActive = dto.IsActive;
    }

    private void Raise(UserContext userContext, Guid correlationId)
    {
        AddDomainEvent(new DiscountCodeChangedEvent { DiscountCodeId = Id, IsActive = IsActive, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    private static void Validate(DiscountDefinitionDto dto, string code)
    {
        if (code.Length < DiscountCodeRules.MinCodeLength || code.Length > DiscountCodeRules.MaxCodeLength || !CodePattern().IsMatch(code))
            throw Invalid($"A code is {DiscountCodeRules.MinCodeLength} to {DiscountCodeRules.MaxCodeLength} characters: letters, digits and hyphens.", nameof(Code));

        if (dto.Description is { Length: > DiscountCodeRules.MaxDescriptionLength })
            throw Invalid($"A description is up to {DiscountCodeRules.MaxDescriptionLength} characters.", nameof(Description));

        if (string.IsNullOrWhiteSpace(dto.CurrencyCode) || dto.CurrencyCode.Length != 3)
            throw Invalid("A currency code is required.", nameof(CurrencyCode));

        if (dto.Kind == DiscountKind.Percentage)
        {
            if (dto.Amount is not null) throw Invalid("A percentage code has no fixed amount.", nameof(Amount));
            if (dto.Percent is not { } percent || percent < 0.01m || percent > 100m || decimal.Round(percent, 2) != percent)
                throw Invalid("A percentage is 0.01 to 100 with at most two decimal places.", nameof(Percent));
        }
        else
        {
            if (dto.Percent is not null) throw Invalid("A fixed-amount code has no percentage.", nameof(Percent));
            RequireMoney(dto.Amount, "The amount", nameof(Amount), mustBePositive: true);
        }

        if (dto.MinimumSpend is not null) RequireMoney(dto.MinimumSpend, "The minimum spend", nameof(MinimumSpend), mustBePositive: false);

        if (dto.UsageLimit is < 1) throw Invalid("The usage limit must be at least 1.", nameof(UsageLimit));
        if (dto.PerCustomerLimit is < 1) throw Invalid("The per-customer limit must be at least 1.", nameof(PerCustomerLimit));

        if (dto.ValidFrom is { } from && dto.ValidTo is { } to && to < from)
            throw Invalid("The code cannot end before it starts.", nameof(ValidTo));

        if (dto.Scope == DiscountScope.Products)
        {
            if (dto.ProductIds.Count == 0) throw Invalid("Choose at least one product for a product-scoped code.", nameof(ProductIds));
            if (dto.ProductIds.Distinct().Count() > DiscountCodeRules.MaxScopeIds) throw Invalid($"A code lists at most {DiscountCodeRules.MaxScopeIds} products.", nameof(ProductIds));
        }
        else if (dto.ProductIds.Count > 0)
        {
            throw Invalid("Only a product-scoped code lists products.", nameof(ProductIds));
        }

        if (dto.Scope == DiscountScope.Categories)
        {
            if (dto.CategoryIds.Count == 0) throw Invalid("Choose at least one category for a category-scoped code.", nameof(CategoryIds));
            if (dto.CategoryIds.Distinct().Count() > DiscountCodeRules.MaxScopeIds) throw Invalid($"A code lists at most {DiscountCodeRules.MaxScopeIds} categories.", nameof(CategoryIds));
            if (dto.CategoryIds.Any(id => Enumeration.GetAll<ProductCategory>().All(c => c.Value != id)))
                throw Invalid("A category is not one of the shop's categories.", nameof(CategoryIds));
        }
        else if (dto.CategoryIds.Count > 0)
        {
            throw Invalid("Only a category-scoped code lists categories.", nameof(CategoryIds));
        }
    }

    private static void RequireMoney(decimal? amount, string what, string property, bool mustBePositive)
    {
        if (amount is not { } value || value < 0 || (mustBePositive && value == 0) || decimal.Round(value, 2) != value || value > 9_999_999_999.99m)
            throw Invalid($"{what} must be {(mustBePositive ? "above zero" : "zero or more")}, with at most two decimal places.", property);
    }

    private static DomainRuleViolationException Invalid(string message, string property) =>
        new(BusinessViolationCodes.InvalidDiscountDefinition, message, property);

    [GeneratedRegex(DiscountCodeRules.CodePattern)]
    private static partial Regex CodePattern();
}
