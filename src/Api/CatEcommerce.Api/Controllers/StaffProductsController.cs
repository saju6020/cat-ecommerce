using System.Text;
using CatEcommerce.Api.Authorization;
using CatEcommerce.Api.Contracts.Product;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;

namespace CatEcommerce.Api.Controllers;

/// <summary>HO-SHP-001 section 12.4 -- staff product editor. Branch-independent (the catalogue is
/// not branch-scoped, section 12.4). Every write carries <c>ExpectedVersion</c>; a stale value maps
/// to 409 through <c>ConcurrencyExceptionFilter</c> (ADR-016).</summary>
[ApiController]
[Route("api/staff/shop/products")]
public sealed class StaffProductsController(IInMemoryDispatcher dispatcher, IUserContextProvider userContextProvider) : ControllerBase
{
    [HttpGet]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> Get([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] string? brand, [FromQuery] string? barcode, [FromQuery] string? kind, [FromQuery] bool? listed,
        [FromQuery] string? sort, [FromQuery] int? pageNumber, [FromQuery] int? pageSize)
    {
        var query = new GetStaffProductsQuery
        {
            Q = q, Status = status, Category = category, Brand = brand, Barcode = barcode, Kind = kind, Listed = listed, Sort = sort,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        var pagingError = PagingHelper.ApplyPaging(query, pageNumber, pageSize);
        if (pagingError is not null) return pagingError;

        var response = await dispatcher.GetResultAsync(query);
        return Ok(response.Result);
    }

    [HttpGet("export.csv")]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> ExportCsv([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? category,
        [FromQuery] string? brand, [FromQuery] string? kind, [FromQuery] bool? listed, [FromQuery] string? sort)
    {
        var query = new GetStaffProductsQuery
        {
            Q = q, Status = status, Category = category, Brand = brand, Kind = kind, Listed = listed, Sort = sort,
            PageNumber = 1, PageSize = 50_000,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid()
        };

        var response = await dispatcher.GetResultAsync(query);
        if (response.Result.TotalCount > 50_000) return StatusCode(413);

        Response.Headers.CacheControl = "no-store";
        var csv = BuildCsv(response.Result.Items);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "products.csv");
    }

    [HttpPost]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        var productId = Guid.NewGuid();
        var command = new CreateProductCommand
        {
            ProductId = productId, Kind = request.Kind, Name = request.Name, Brand = request.Brand,
            Category = request.Category, Subcategory = request.Subcategory, TaxClassId = request.TaxClassId,
            Slug = request.Slug, BundleSkuId = request.Kind == "Bundle" ? Guid.NewGuid() : null, BundleSkuCode = request.BundleSkuCode,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { productId }) : BadRequest(response.ValidationResult);
    }

    [HttpGet("{productId:guid}")]
    [RequireAction(Actions.Shop.ProductsView)]
    public async Task<IActionResult> GetDetail(Guid productId)
    {
        var response = await dispatcher.GetResultAsync(new GetStaffProductDetailQuery
        { ProductId = productId, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid() });

        return response.Result is null ? NotFound() : Ok(response.Result);
    }

    [HttpPut("{productId:guid}")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> UpdateDetails(Guid productId, [FromBody] UpdateProductDetailsRequest request)
    {
        var command = new UpdateProductDetailsCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, Name = request.Name, Brand = request.Brand,
            Description = request.Description, Category = request.Category, Subcategory = request.Subcategory,
            Tags = request.Tags, TaxClassId = request.TaxClassId, CountryOfOriginCode = request.CountryOfOriginCode,
            ListedOnStorefront = request.ListedOnStorefront, IsExpiryTracked = request.IsExpiryTracked, HealthFocus = request.HealthFocus,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/slug")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> ChangeSlug(Guid productId, [FromBody] ChangeProductSlugRequest request)
    {
        var command = new ChangeProductSlugCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, Slug = request.Slug,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/food")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> SetFoodDetails(Guid productId, [FromBody] SetFoodDetailsRequest request)
    {
        var command = new SetFoodDetailsCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, Manufacturer = request.Manufacturer,
            LifeStage = request.LifeStage, ProteinSource = request.ProteinSource, IngredientList = request.IngredientList,
            GuaranteedAnalysis = request.GuaranteedAnalysis is null ? null : new GuaranteedAnalysisInput
            {
                ProteinPercent = request.GuaranteedAnalysis.ProteinPercent, FatPercent = request.GuaranteedAnalysis.FatPercent,
                FibrePercent = request.GuaranteedAnalysis.FibrePercent, MoisturePercent = request.GuaranteedAnalysis.MoisturePercent
            },
            FeedingGuide = request.FeedingGuide, StorageInstructions = request.StorageInstructions,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/prescription")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> SetPrescriptionRequired(Guid productId, [FromBody] SetPrescriptionRequiredRequest request)
    {
        var command = new SetPrescriptionRequiredCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, PrescriptionRequired = request.PrescriptionRequired,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    /// <summary>US-SHP-14 AC3 -- a shop manager enters content per enabled language. A locale that
    /// is not enabled is <c>400 LocaleNotEnabled</c>; the default locale is
    /// <c>400 DefaultLocaleNotTranslatable</c>; a translation with all four fields empty removes
    /// the locale.</summary>
    [HttpPut("{productId:guid}/translations/{locale}")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> SetTranslation(Guid productId, string locale, [FromBody] SetProductTranslationRequest request)
    {
        var command = new SetProductTranslationCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, Locale = locale, Name = request.Name,
            Description = request.Description, IngredientList = request.IngredientList, FeedingGuide = request.FeedingGuide,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    /// <summary>The version travels in the query string -- a DELETE carries no body.</summary>
    [HttpDelete("{productId:guid}/translations/{locale}")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> RemoveTranslation(Guid productId, string locale, [FromQuery] int expectedVersion)
    {
        var command = new RemoveProductTranslationCommand
        {
            ProductId = productId, ExpectedVersion = expectedVersion, Locale = locale,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/bundle-components")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> SetBundleComponents(Guid productId, [FromBody] SetBundleComponentsRequest request)
    {
        var command = new SetBundleComponentsCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion,
            Components = request.Components.Select(c => new BundleComponentInput { ComponentSkuId = c.ComponentSkuId, Quantity = c.Quantity }).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/related-products")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> SetRelatedProducts(Guid productId, [FromBody] SetRelatedProductsRequest request)
    {
        var command = new SetRelatedProductsCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion,
            Links = request.Links.Select(l => new RelatedProductInput { RelatedProductId = l.RelatedProductId, Kind = l.Kind, SortOrder = l.SortOrder }).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/images")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> SetImages(Guid productId, [FromBody] SetProductImagesRequest request)
    {
        var command = new SetProductImagesCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion,
            Images = request.Images.Select(i => new ProductImageInput
            { ImageId = Guid.NewGuid(), SkuId = i.SkuId, FileId = i.FileId, AltText = i.AltText, SortOrder = i.SortOrder, IsPrimary = i.IsPrimary }).ToList(),
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPost("{productId:guid}/skus")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> AddSku(Guid productId, [FromBody] AddSkuRequest request)
    {
        var skuId = Guid.NewGuid();
        var command = new AddSkuCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, SkuId = skuId, Code = request.Code,
            Barcode = request.Barcode, SizeOrWeight = request.SizeOrWeight, Flavour = request.Flavour, Colour = request.Colour,
            PackQuantity = request.PackQuantity, ShippingWeightGrams = request.ShippingWeightGrams, UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? StatusCode(201, new { skuId }) : ToErrorResult(response);
    }

    [HttpPut("{productId:guid}/skus/{skuId:guid}")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> UpdateSku(Guid productId, Guid skuId, [FromBody] UpdateSkuRequest request)
    {
        var command = new UpdateSkuCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, SkuId = skuId, SizeOrWeight = request.SizeOrWeight,
            Flavour = request.Flavour, Colour = request.Colour, PackQuantity = request.PackQuantity, ShippingWeightGrams = request.ShippingWeightGrams,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPost("{productId:guid}/skus/{skuId:guid}/discontinue")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> DiscontinueSku(Guid productId, Guid skuId, [FromBody] SkuVersionRequest request)
    {
        var command = new DiscontinueSkuCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, SkuId = skuId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPost("{productId:guid}/skus/{skuId:guid}/reactivate")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> ReactivateSku(Guid productId, Guid skuId, [FromBody] SkuVersionRequest request)
    {
        var command = new ReactivateSkuCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion, SkuId = skuId,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPost("{productId:guid}/publish")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> Publish(Guid productId, [FromBody] SkuVersionRequest request)
    {
        var command = new PublishProductCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPost("{productId:guid}/unpublish")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> Unpublish(Guid productId, [FromBody] SkuVersionRequest request)
    {
        var command = new UnpublishProductCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    [HttpPost("{productId:guid}/archive")]
    [RequireAction(Actions.Shop.ProductsManage)]
    public async Task<IActionResult> Archive(Guid productId, [FromBody] SkuVersionRequest request)
    {
        var command = new ArchiveProductCommand
        {
            ProductId = productId, ExpectedVersion = request.ExpectedVersion,
            UserContext = userContextProvider.GetUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
        };

        var response = await dispatcher.SendAsync(command);
        return response.IsSuccess ? NoContent() : ToErrorResult(response);
    }

    /// <summary>ADR-016 / §12.4 I-A11 -- a stale <c>ExpectedVersion</c> is 409, the same
    /// code-to-status mapping HO-NTF-001's <c>AdminNotificationTemplatesController</c> already
    /// uses for its own <c>TemplateVersionConflict</c>.</summary>
    private IActionResult ToErrorResult(Platform.Infrastructure.Core.Commands.CommandResponse response)
    {
        var isVersionConflict = response.ValidationResult?.Errors?.Any(e => e.ErrorCode == BusinessViolationCodes.ProductVersionConflict) ?? false;
        return isVersionConflict ? Conflict(response.ValidationResult) : BadRequest(response.ValidationResult);
    }

    /// <summary>ADR-051 -- a cell beginning = + - @ (optionally after leading whitespace) is
    /// prefixed with ' (CSV-injection guard, U-X3).</summary>
    private static string BuildCsv(IReadOnlyList<StaffProductSummaryViewModel> items)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ProductId,Name,Brand,Category,Status,Kind,SkuCount,ListedOnStorefront,LastUpdatedOn");

        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",",
                Escape(item.ProductId.ToString()), Escape(item.Name), Escape(item.Brand), Escape(item.Category),
                Escape(item.Status), Escape(item.Kind), Escape(item.SkuCount.ToString()), Escape(item.ListedOnStorefront.ToString()),
                Escape(item.LastUpdatedOn.ToString("O"))));
        }

        return sb.ToString();
    }

    private static string Escape(string value)
    {
        var firstSignificant = value.AsSpan().TrimStart("\t\r\n ");
        var guarded = firstSignificant.Length > 0 && "=+-@".Contains(firstSignificant[0]) ? "'" + value : value;
        var quoted = guarded.Replace("\"", "\"\"");
        return $"\"{quoted}\"";
    }
}
