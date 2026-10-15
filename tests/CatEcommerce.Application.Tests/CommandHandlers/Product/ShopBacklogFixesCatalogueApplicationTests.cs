using CatEcommerce.Application.CommandHandlers.Product;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DomainEventHandlers.Product;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence.Views;
using CatEcommerce.Read.QueryHandlers.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Product;

/// <summary>HO-SHP-001 backlog row 37 at read-model level.</summary>
public sealed class ShopBacklogFixesCatalogueApplicationTests
{
    private static UserContext Ctx() => new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["ShopManager"] };

    private static ProductAggregate NewProduct(string slug = "scratch-post")
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), slug, null, null, Ctx(), Guid.NewGuid()));
        return product;
    }

    private static ProductAggregate ProductWithSku(out Guid skuId, bool publish = false, bool listed = true)
    {
        var product = NewProduct();
        skuId = Guid.NewGuid();
        product.AddSku(new SkuAddDto(skuId, "SP-1", null, null, null, null, null), Ctx(), Guid.NewGuid());
        if (!listed)
            product.UpdateDetails(new ProductDetailsUpdateDto(product.Name, product.Brand, product.Description, product.Category, null, product.Tags,
                product.TaxClassId, null, false, false, [], Ctx(), Guid.NewGuid()));
        if (publish) product.Publish(new PublicationFacts([skuId], true, []), DateTimeOffset.UtcNow, Ctx(), Guid.NewGuid());
        return product;
    }

    // ── Row 37 ──────────────────────────────────────────────────────────────────────────────────

    private static StorefrontCatalogueEntry Entry(decimal? minPrice, string? currency) => new()
    {
        ProductId = Guid.NewGuid(), Slug = "s", Name = "N", Brand = "B", Category = ProductCategory.CatFood.Value, Kind = ProductKind.Standard.Value,
        SearchText = "n b", MinPrice = minPrice, MaxPrice = minPrice, CurrencyCode = currency
    };

    [Fact]
    public void CatalogueItem_ShowsTheStoredCurrency_NotAHardCodedOne()
    {
        var item = GetCatalogueQueryHandler.ToCatalogueItem(Entry(1500m, "USD"), inStock: false);

        Assert.Equal("USD", item.FromPrice!.CurrencyCode);
        Assert.Equal("1500.00", item.FromPrice.Amount);
    }

    [Fact]
    public void CatalogueItem_PriceWithNoStoredCurrency_IsNotShownUnderAnInventedOne()
    {
        var item = GetCatalogueQueryHandler.ToCatalogueItem(Entry(1500m, null), inStock: false);

        Assert.Null(item.FromPrice);
    }
}
