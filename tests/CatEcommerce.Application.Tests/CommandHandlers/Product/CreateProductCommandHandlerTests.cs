using CatEcommerce.Application.CommandHandlers.Product;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Product;

/// <summary>HO-SHP-001 §16 I-A3, I-A7 (the handler-level slice of the barcode/slug pre-checks).</summary>
public sealed class CreateProductCommandHandlerTests
{
    private static UserContext ShopManagerContext() => new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["ShopManager"] };

    private static TaxClassAggregate ActiveTaxClass()
    {
        var taxClass = new TaxClassAggregate(Guid.NewGuid());
        taxClass.Create(new TaxClassCreateDto("STD", "Standard", ShopManagerContext(), Guid.NewGuid()));
        return taxClass;
    }

    private static CreateProductCommand ValidCommand(Guid taxClassId, string slug = "royal-canin-kitten") => new()
    {
        ProductId = Guid.NewGuid(), Kind = "Standard", Name = "Royal Canin Kitten", Brand = "Royal Canin",
        Category = "CatFood", Subcategory = "Kitten", TaxClassId = taxClassId, Slug = slug,
        UserContext = ShopManagerContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
    };

    [Fact]
    public async Task Create_Valid_Succeeds()
    {
        var productRepository = new FakeAggregateRootRepository<ProductAggregate>();
        var taxClassRepository = new FakeAggregateRootRepository<TaxClassAggregate>();
        var taxClass = ActiveTaxClass();
        taxClassRepository.Seed(taxClass);

        var handler = new CreateProductCommandHandler(NullLogger<CreateProductCommandHandler>.Instance, productRepository, taxClassRepository);
        var response = await handler.HandleAsync(ValidCommand(taxClass.Id));

        Assert.True(response.IsSuccess);
        Assert.Single(productRepository.Saved);
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReturnsSlugAlreadyInUse()
    {
        var productRepository = new FakeAggregateRootRepository<ProductAggregate>();
        var taxClassRepository = new FakeAggregateRootRepository<TaxClassAggregate>();
        var taxClass = ActiveTaxClass();
        taxClassRepository.Seed(taxClass);

        var existing = new ProductAggregate(Guid.NewGuid());
        existing.Create(new ProductCreateDto(CatEcommerce.Shared.Common.Enums.ProductKind.Standard, "Existing", "Brand",
            CatEcommerce.Shared.Common.Enums.ProductCategory.ToysAndFurniture, null, taxClass.Id, "royal-canin-kitten",
            null, null, ShopManagerContext(), Guid.NewGuid()));
        productRepository.Seed(existing);

        var handler = new CreateProductCommandHandler(NullLogger<CreateProductCommandHandler>.Instance, productRepository, taxClassRepository);
        var response = await handler.HandleAsync(ValidCommand(taxClass.Id));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SlugAlreadyInUse);
    }

    [Fact]
    public async Task Create_TaxClassNotFound_ReturnsTaxClassNotFound()
    {
        var productRepository = new FakeAggregateRootRepository<ProductAggregate>();
        var taxClassRepository = new FakeAggregateRootRepository<TaxClassAggregate>();

        var handler = new CreateProductCommandHandler(NullLogger<CreateProductCommandHandler>.Instance, productRepository, taxClassRepository);
        var response = await handler.HandleAsync(ValidCommand(Guid.NewGuid()));

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.TaxClassNotFound);
    }
}
