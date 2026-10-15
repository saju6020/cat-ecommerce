using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.DataMappers;

/// <summary>HO-SHP-001 section 11.3. command.UserContext and command.CorrelationId are always
/// passed through -- how the audit trail attributes a change. Enum-typed fields are parsed by the
/// caller (the handler, via <c>EnumerationParsing</c>) before reaching these mappers, since a
/// mapper failure has nowhere good to report a 400 from.</summary>
public static class ProductMapper
{
    public static ProductCreateDto ToDomainDto(this CreateProductCommand command, ProductKind kind, ProductCategory category, ProductSubcategory? subcategory) =>
        new(kind, command.Name, command.Brand, category, subcategory, command.TaxClassId, command.Slug,
            command.BundleSkuId, command.BundleSkuCode, command.UserContext, command.CorrelationId);

    public static ProductDetailsUpdateDto ToDomainDto(this UpdateProductDetailsCommand command, ProductCategory category, ProductSubcategory? subcategory, IReadOnlyList<HealthFocus> healthFocus) =>
        new(command.Name, command.Brand, command.Description ?? "", category, subcategory, command.Tags,
            command.TaxClassId, command.CountryOfOriginCode, command.ListedOnStorefront, command.IsExpiryTracked,
            healthFocus, command.UserContext, command.CorrelationId);

    public static SkuAddDto ToDomainDto(this AddSkuCommand command) =>
        new(command.SkuId, command.Code, command.Barcode, command.SizeOrWeight, command.Flavour, command.Colour, command.PackQuantity, command.ShippingWeightGrams);

    public static SkuUpdateDto ToDomainDto(this UpdateSkuCommand command) =>
        new(command.SkuId, command.SizeOrWeight, command.Flavour, command.Colour, command.PackQuantity, command.ShippingWeightGrams);

    public static IReadOnlyList<ProductImageInputDto> ToDomainDto(this SetProductImagesCommand command) =>
        command.Images.Select(i => new ProductImageInputDto(i.ImageId, i.SkuId, i.FileId, i.AltText, i.SortOrder, i.IsPrimary)).ToList();

    public static SkuPriceCreateDto ToDomainDto(this ScheduleSkuPriceCommand command, DateTimeOffset now) =>
        new(command.SkuId, new Money(command.Amount, command.CurrencyCode), command.EffectiveFrom, command.UserContext.UserId, now, command.UserContext, command.CorrelationId);

    public static FoodDetails ToDomainDto(this SetFoodDetailsCommand command, LifeStage? lifeStage, ProteinSource? proteinSource)
    {
        var analysis = command.GuaranteedAnalysis is null
            ? null
            : new GuaranteedAnalysis(command.GuaranteedAnalysis.ProteinPercent, command.GuaranteedAnalysis.FatPercent,
                command.GuaranteedAnalysis.FibrePercent, command.GuaranteedAnalysis.MoisturePercent);

        return new FoodDetails(command.Manufacturer, lifeStage, proteinSource, command.IngredientList, analysis,
            command.FeedingGuide, command.StorageInstructions);
    }

    public static SourcingOriginCreateDto ToDomainDto(this CreateSourcingOriginCommand command) =>
        new(command.CountryCode, command.SourcingNotes, command.IsFeatured, command.SortOrder, command.UserContext, command.CorrelationId);

    public static SourcingOriginUpdateDto ToDomainDto(this UpdateSourcingOriginCommand command) =>
        new(command.SourcingNotes, command.IsFeatured, command.SortOrder, command.UserContext, command.CorrelationId);
}
