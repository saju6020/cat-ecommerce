using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>HO-SHP-001 section 8.3.2 -- the whole image set is replaced at once. Content type and
/// size are <c>StoredFileAggregate</c>'s own state (HO-FIL-001), checked by the handler through
/// file eligibility before this call -- not this aggregate's concern.</summary>
public sealed partial class ProductAggregate
{
    public void SetImages(IReadOnlyList<ProductImageInputDto> images, UserContext userContext, Guid correlationId)
    {
        if (images.Count > ProductRules.MaxImages)
            throw new DomainRuleViolationException(BusinessViolationCodes.ImageLimitReached, $"A product may have at most {ProductRules.MaxImages} images.", nameof(images));

        var skuIds = _skus.Select(s => s.Id).ToHashSet();
        var newImages = new List<ProductImage>();

        foreach (var image in images)
        {
            if (string.IsNullOrWhiteSpace(image.AltText) || image.AltText.Length > ProductRules.AltTextMaxLength)
                throw new DomainRuleViolationException(BusinessViolationCodes.ImageAltTextRequired, "Alt text is required, up to 200 characters.", nameof(image.AltText));

            if (image.SkuId is not null && !skuIds.Contains(image.SkuId.Value))
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidImage, "An image's SKU must belong to this product.", nameof(image.SkuId));

            newImages.Add(new ProductImage(image.ImageId, image.SkuId, image.FileId, image.AltText.Trim(), image.SortOrder, image.IsPrimary));
        }

        var productLevelPrimaryCount = newImages.Count(i => i.SkuId is null && i.IsPrimary);
        var productLevelCount = newImages.Count(i => i.SkuId is null);
        if (productLevelCount > 0 && productLevelPrimaryCount != 1)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidImage, "Exactly one product-level image must be marked primary.", nameof(images));

        _images.Clear();
        _images.AddRange(newImages);

        AddDomainEvent(new ProductImagesChangedEvent { ProductId = Id, CorrelationId = correlationId, UserContext = userContext });
        UpdateVersion();
    }
}
