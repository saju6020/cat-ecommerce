using System.Text.RegularExpressions;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.1/8.3 -- the Product bounded context root (ADR-077). One aggregate mutated
/// per command, with no exception (section 8.1). Owns SKUs, images, bundle components, related
/// product links and translations (all bounded, owned collections); prices, reviews, wishlists and
/// stock alerts are separate roots because they accumulate without bound (bounded-contexts.md rule
/// 3, ADR-082).
/// </summary>
public sealed partial class ProductAggregate : AggregateRoot
{
    private readonly List<Sku> _skus = [];
    private readonly List<ProductImage> _images = [];
    private readonly List<BundleComponent> _bundleComponents = [];
    private readonly List<RelatedProductLink> _relatedProducts = [];
    private readonly List<ProductTranslation> _translations = [];
    private readonly List<string> _tags = [];
    private readonly List<HealthFocus> _healthFocus = [];

    public ProductKind Kind { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Brand { get; private set; } = default!;
    public string Description { get; private set; } = "";
    public ProductCategory Category { get; private set; } = default!;
    public ProductSubcategory? Subcategory { get; private set; }
    public string Slug { get; private set; } = default!;
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public DateTimeOffset? FirstPublishedOn { get; private set; }
    public Guid TaxClassId { get; private set; }
    public string? CountryOfOriginCode { get; private set; }
    public bool PrescriptionRequired { get; private set; }
    public bool IsExpiryTracked { get; private set; }
    public bool ListedOnStorefront { get; private set; } = true;
    public string SearchText { get; private set; } = "";
    public FoodDetails? FoodDetails { get; private set; }

    public IReadOnlyList<Sku> Skus => _skus;
    public IReadOnlyList<ProductImage> Images => _images;
    public IReadOnlyList<BundleComponent> BundleComponents => _bundleComponents;
    public IReadOnlyList<RelatedProductLink> RelatedProducts => _relatedProducts;
    public IReadOnlyList<ProductTranslation> Translations => _translations;
    public new IReadOnlyList<string> Tags => _tags;
    public IReadOnlyList<HealthFocus> HealthFocus => _healthFocus;

    public bool IsFood => Category == ProductCategory.CatFood;

    private ProductAggregate() { }

    public ProductAggregate(Guid id) : base(id) { }

    public void Create(ProductCreateDto dto)
    {
        ValidateCategory(dto.Category, dto.Subcategory);
        ValidateSlugFormat(dto.Slug);

        Kind = dto.Kind;
        Name = ValidateName(dto.Name);
        Brand = ValidateBrand(dto.Brand);
        Category = dto.Category;
        Subcategory = dto.Subcategory;
        Slug = dto.Slug;
        TaxClassId = dto.TaxClassId;
        Status = ProductStatus.Draft;
        ListedOnStorefront = true;
        IsExpiryTracked = IsFood;

        if (dto.Kind == ProductKind.Bundle)
        {
            if (dto.BundleSkuId is null || string.IsNullOrWhiteSpace(dto.BundleSkuCode))
                throw new DomainRuleViolationException(BusinessViolationCodes.SkuLimitReached, "A bundle needs its own SKU id and code.", nameof(dto.BundleSkuCode));

            _skus.Add(new Sku(dto.BundleSkuId.Value, dto.BundleSkuCode, barcode: null, sizeOrWeight: null, flavour: null, colour: null, packQuantity: null));
        }

        RefreshSearchText();
        SetDefaultValue(dto.UserContext);

        AddDomainEvent(new ProductCreatedEvent
        {
            ProductId = Id,
            Kind = Kind.Value,
            Category = Category.Value,
            CorrelationId = dto.CorrelationId,
            UserContext = dto.UserContext
        });

        UpdateVersion();
    }

    public void UpdateDetails(ProductDetailsUpdateDto dto)
    {
        if (Status == ProductStatus.Active && dto.Category != Category)
            throw new DomainRuleViolationException(BusinessViolationCodes.CategoryLockedWhileActive, "The category cannot change while the product is active.", nameof(dto.Category));

        if (Status != ProductStatus.Active)
            ValidateCategory(dto.Category, dto.Subcategory);
        else if (dto.Subcategory is not null && dto.Subcategory.Category != Category)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductCategory, "The subcategory does not belong to this product's category.", nameof(dto.Subcategory));

        var normalisedCountry = NormaliseCountryCode(dto.CountryOfOriginCode);

        if (Status == ProductStatus.Active && IsFood && normalisedCountry is null)
            throw new DomainRuleViolationException(BusinessViolationCodes.CountryOfOriginRequired, "An active food product must keep a country of origin.", nameof(dto.CountryOfOriginCode));

        Name = ValidateName(dto.Name);
        Brand = ValidateBrand(dto.Brand);
        Description = ValidateDescription(dto.Description);
        if (Status != ProductStatus.Active)
        {
            var leavingFood = IsFood && dto.Category != ProductCategory.CatFood;
            Category = dto.Category;

            // Row 31: food-only state cannot outlive the category. Left in place it would make a non-food product
            // "prescription required" with no way to clear the flag (SetPrescriptionRequired refuses non-food).
            if (leavingFood) ClearFoodOnlyState(dto.UserContext, dto.CorrelationId);
        }
        Subcategory = dto.Subcategory;
        TaxClassId = dto.TaxClassId;
        CountryOfOriginCode = normalisedCountry;
        ListedOnStorefront = dto.ListedOnStorefront;
        IsExpiryTracked = IsFood || dto.IsExpiryTracked;

        SetTags(dto.Tags);
        SetHealthFocusInternal(dto.HealthFocus);

        RefreshSearchText();

        AddDomainEvent(new ProductDetailsUpdatedEvent { ProductId = Id, CorrelationId = dto.CorrelationId, UserContext = dto.UserContext });

        UpdateVersion();
    }

    private void ClearFoodOnlyState(Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        FoodDetails = null;

        if (!PrescriptionRequired) return;

        PrescriptionRequired = false;
        AddDomainEvent(new ProductPrescriptionRequirementChangedEvent
        { ProductId = Id, PrescriptionRequired = false, CorrelationId = correlationId, UserContext = userContext });
    }

    public void ChangeSlug(string slug, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (FirstPublishedOn is not null)
            throw new DomainRuleViolationException(BusinessViolationCodes.SlugLockedAfterPublication, "The slug is locked after first publication.", nameof(Slug));

        ValidateSlugFormat(slug);
        Slug = slug;

        AddDomainEvent(new ProductSlugChangedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public IReadOnlyList<PublicationChecklistItem> Publish(PublicationFacts facts, DateTimeOffset now, Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (Status != ProductStatus.Draft && Status != ProductStatus.Archived)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductTransition, $"Cannot publish from {Status.DisplayName}.", nameof(Status));

        var checklist = ProductPublicationRules.Evaluate(this, facts);
        var blocking = checklist.Where(i => i.IsBlocking && !i.IsMet).ToList();
        if (blocking.Count > 0)
        {
            var message = "This product can't be published yet: " + string.Join("; ", blocking.Select(i => i.Message));
            throw new DomainRuleViolationException(BusinessViolationCodes.ProductNotPublishable, message, nameof(Status));
        }

        var firstPublication = FirstPublishedOn is null;
        Status = ProductStatus.Active;
        FirstPublishedOn ??= now;

        AddDomainEvent(new ProductPublishedEvent { ProductId = Id, FirstPublication = firstPublication, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();

        return checklist;
    }

    public void Unpublish(Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (Status != ProductStatus.Active)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductTransition, $"Cannot unpublish from {Status.DisplayName}.", nameof(Status));

        Status = ProductStatus.Draft;

        AddDomainEvent(new ProductUnpublishedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    public void Archive(Platform.Infrastructure.Common.Security.UserContext userContext, Guid correlationId)
    {
        if (Status != ProductStatus.Active)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductTransition, $"Cannot archive from {Status.DisplayName}.", nameof(Status));

        Status = ProductStatus.Archived;

        AddDomainEvent(new ProductArchivedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }

    private void SetTags(IReadOnlyList<string> tags)
    {
        if (tags.Count > ProductRules.MaxTags)
            throw new DomainRuleViolationException(BusinessViolationCodes.TagLimitExceeded, $"A product may have at most {ProductRules.MaxTags} tags.", nameof(Tags));

        var normalised = new List<string>();
        foreach (var raw in tags)
        {
            // U-A13: "Grain Free" normalises to "grain-free" -- internal whitespace becomes a
            // hyphen before the pattern is checked, not just lower-casing and trimming.
            var tag = CollapseWhitespace().Replace((raw ?? "").Trim().ToLowerInvariant(), "-");
            if (tag.Length == 0 || tag.Length > ProductRules.TagMaxLength || !TagPattern().IsMatch(tag))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidTag, $"'{raw}' is not a valid tag.", nameof(Tags));

            if (!normalised.Contains(tag)) normalised.Add(tag);
        }

        _tags.Clear();
        _tags.AddRange(normalised);
    }

    private void SetHealthFocusInternal(IReadOnlyList<HealthFocus> focuses)
    {
        if (!IsFood)
        {
            _healthFocus.Clear();
            return;
        }

        if (focuses.Count > ProductRules.MaxHealthFocus)
            throw new DomainRuleViolationException(BusinessViolationCodes.TagLimitExceeded, $"A product may have at most {ProductRules.MaxHealthFocus} health focus values.", nameof(HealthFocus));

        _healthFocus.Clear();
        _healthFocus.AddRange(focuses.Distinct());
    }

    /// <summary>Section 8.3 -- lower-cased, whitespace-collapsed concatenation of name, brand,
    /// tags, ingredient list, every SKU flavour and every translation name. Recomputed by the
    /// aggregate on every mutation that touches those fields; never set from outside (ADR-083).</summary>
    private void RefreshSearchText()
    {
        var parts = new List<string> { Name, Brand };
        parts.AddRange(_tags);
        if (FoodDetails?.IngredientList is { Length: > 0 } ingredients) parts.Add(ingredients);
        parts.AddRange(_skus.Where(s => !string.IsNullOrWhiteSpace(s.Flavour)).Select(s => s.Flavour!));
        parts.AddRange(_translations.Where(t => !string.IsNullOrWhiteSpace(t.Name)).Select(t => t.Name!));

        var joined = string.Join(" ", parts).ToLowerInvariant();
        SearchText = CollapseWhitespace().Replace(joined, " ").Trim();
    }

    private static string ValidateName(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > ProductRules.NameMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductName, "Name must be 1-200 characters.", nameof(Name));
        return trimmed;
    }

    private static string ValidateBrand(string brand)
    {
        var trimmed = (brand ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > ProductRules.BrandMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidBrand, "Brand must be 1-100 characters.", nameof(Brand));
        return trimmed;
    }

    private static string ValidateDescription(string? description)
    {
        var value = description ?? "";
        if (value.Length > ProductRules.DescriptionMaxLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductDescription, $"Description cannot exceed {ProductRules.DescriptionMaxLength} characters.", nameof(Description));

        var markupError = Shared.Common.Helpers.LimitedMarkup.Validate(value);
        if (markupError is not null)
            throw new DomainRuleViolationException(markupError, "Description failed markup validation.", nameof(Description));

        return value;
    }

    private static void ValidateCategory(ProductCategory category, ProductSubcategory? subcategory)
    {
        if (subcategory is not null && subcategory.Category != category)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidProductCategory, "The subcategory does not belong to this category.", nameof(subcategory));
    }

    private static void ValidateSlugFormat(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length > ProductRules.SlugMaxLength || !SlugPattern().IsMatch(slug))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidSlug, "Slug must be lower-case letters, digits and single hyphens, up to 120 characters.", nameof(slug));
    }

    private static string? NormaliseCountryCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        if (!CountryCodes.IsValid(code))
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidCountryCode, $"'{code}' is not a known country code.", nameof(CountryOfOriginCode));

        return CountryCodes.Normalise(code);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespace();
}

/// <summary>HO-SHP-001 section 8.4 -- one row of the publication checklist
/// <c>ProductPublicationRules.Evaluate</c> returns, shared by the publish command and the staff
/// detail read.</summary>
public sealed record PublicationChecklistItem(string Item, bool IsBlocking, bool IsMet, string Message);
