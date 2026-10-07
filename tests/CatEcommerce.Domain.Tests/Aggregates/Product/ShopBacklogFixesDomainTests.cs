using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 / HO-SHP-002 backlog rows 21, 31, 32, 36 and S5 (server half) -- aggregate and
/// helper level. Each case fails without the corresponding fix.</summary>
public sealed class ShopBacklogFixesDomainTests
{
    // ── Reviews: rows 21 and 36 ─────────────────────────────────────────────────────────────────

    private static ProductReviewAggregate Submitted(string? title = "Great", string body = "Loved it.")
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        review.Submit(new ProductReviewSubmitDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, title, body, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
        return review;
    }

    private static ProductReviewAggregate InStatus(ProductReviewStatus status)
    {
        var review = Submitted();
        var moderator = Guid.NewGuid();
        var ctx = TestUserContext.Create();

        if (status == ProductReviewStatus.Published) review.Publish(moderator, DateTimeOffset.UtcNow, ctx, Guid.NewGuid());
        if (status == ProductReviewStatus.Hidden)
        {
            review.Publish(moderator, DateTimeOffset.UtcNow, ctx, Guid.NewGuid());
            review.Hide(moderator, DateTimeOffset.UtcNow, ctx, Guid.NewGuid());
        }

        if (status == ProductReviewStatus.Rejected)
            review.Reject(new ProductReviewRejectDto(ReviewRejectionReason.Spam, "mentions her phone number", moderator, DateTimeOffset.UtcNow, ctx, Guid.NewGuid()));
        if (status == ProductReviewStatus.Withdrawn) review.Withdraw(ctx, Guid.NewGuid());
        return review;
    }

    // Row 21
    [Fact]
    public void ClearTextForAnonymisation_PendingReview_ClearsTitleAndBody()
    {
        var review = InStatus(ProductReviewStatus.PendingModeration);

        Assert.True(review.ClearTextForAnonymisation());
        Assert.Null(review.Title);
        Assert.Equal("", review.Body);
    }

    // BSB1: a pending review must leave the moderation queue, or a moderator could publish an empty one.
    [Fact]
    public void ClearTextForAnonymisation_PendingReview_IsWithdrawn_AndCannotBePublished()
    {
        var review = InStatus(ProductReviewStatus.PendingModeration);

        review.ClearTextForAnonymisation();

        Assert.Equal(ProductReviewStatus.Withdrawn, review.Status);
        Assert.Throws<DomainRuleViolationException>(() => review.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));
    }

    [Fact]
    public void ClearTextForAnonymisation_RejectedReview_ClearsTitleBodyAndModerationNote()
    {
        var review = InStatus(ProductReviewStatus.Rejected);
        Assert.NotNull(review.ModerationNote);

        Assert.True(review.ClearTextForAnonymisation());
        Assert.Null(review.Title);
        Assert.Equal("", review.Body);
        Assert.Null(review.ModerationNote);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ClearTextForAnonymisation_WithdrawnOrHiddenReview_ClearsText(int which)
    {
        var review = InStatus(which == 1 ? ProductReviewStatus.Withdrawn : ProductReviewStatus.Hidden);

        Assert.True(review.ClearTextForAnonymisation());
        Assert.Null(review.Title);
        Assert.Equal("", review.Body);
    }

    [Fact]
    public void ClearTextForAnonymisation_PublishedReview_IsKept_PerADR096()
    {
        var review = InStatus(ProductReviewStatus.Published);

        Assert.False(review.ClearTextForAnonymisation());
        Assert.Equal("Great", review.Title);
        Assert.Equal("Loved it.", review.Body);
    }

    [Fact]
    public void ClearTextForAnonymisation_IsIdempotent()
    {
        var review = InStatus(ProductReviewStatus.PendingModeration);
        Assert.True(review.ClearTextForAnonymisation());
        Assert.False(review.ClearTextForAnonymisation());
    }

    // Row 36
    [Theory]
    [InlineData("bad\u0000title")]
    [InlineData("bell\u0007")]
    public void Submit_TitleWithAControlCharacter_Throws(string title)
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => review.Submit(
            new ProductReviewSubmitDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4, title, "fine", DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidReviewText, ex.ErrorCode);
        Assert.Equal(nameof(ProductReviewAggregate.Title), ex.PropertyName);
    }

    [Fact]
    public void Edit_TitleWithAControlCharacter_Throws()
    {
        var review = Submitted();
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            review.Edit(new ProductReviewEditDto(4, "bad\u0000title", "fine", TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(nameof(ProductReviewAggregate.Title), ex.PropertyName);
    }

    [Fact]
    public void Edit_AfterARejection_ClearsTheStaleModerationNote()
    {
        var review = InStatus(ProductReviewStatus.Rejected);
        Assert.Equal("mentions her phone number", review.ModerationNote);

        review.Edit(new ProductReviewEditDto(4, "Better", "Reworded", TestUserContext.Create(), Guid.NewGuid()));

        Assert.Null(review.ModerationNote);
        Assert.Equal(ProductReviewStatus.PendingModeration, review.Status);
    }

    // ── Products: rows 31 and 32 ────────────────────────────────────────────────────────────────

    private static ProductAggregate NewProduct(ProductCategory category)
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Test Product", "Acme", category, null,
            Guid.NewGuid(), "test-product", null, null, TestUserContext.Create(), Guid.NewGuid()));
        return product;
    }

    private static ProductDetailsUpdateDto Details(ProductAggregate product, ProductCategory category) =>
        new(product.Name, product.Brand, product.Description, category, null, product.Tags,
            product.TaxClassId, product.CountryOfOriginCode, product.ListedOnStorefront, false, [], TestUserContext.Create(), Guid.NewGuid());

    // Row 31
    [Fact]
    public void UpdateDetails_DraftFoodProductMovedToAnotherCategory_ClearsPrescriptionFlagAndFoodDetails()
    {
        var product = NewProduct(ProductCategory.CatFood);
        product.SetFoodDetails(new FoodDetails("Royal Canin", LifeStage.Adult, ProteinSource.Chicken, "Chicken", null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid());
        Assert.True(product.PrescriptionRequired);

        product.UpdateDetails(Details(product, ProductCategory.ToysAndFurniture));

        Assert.False(product.PrescriptionRequired);
        Assert.Null(product.FoodDetails);
        Assert.DoesNotContain("chicken", product.SearchText);
    }

    [Fact]
    public void UpdateDetails_LeavingFood_WithAPrescriptionFlag_RecordsTheFlagChange()
    {
        var product = NewProduct(ProductCategory.CatFood);
        product.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid());
        var before = product.Events.Count;

        product.UpdateDetails(Details(product, ProductCategory.ToysAndFurniture));

        Assert.Contains(product.Events.Skip(before), e => e is CatEcommerce.Domain.Events.Product.ProductPrescriptionRequirementChangedEvent { PrescriptionRequired: false });
    }

    [Fact]
    public void UpdateDetails_StayingInFood_KeepsFoodDetailsAndFlag()
    {
        var product = NewProduct(ProductCategory.CatFood);
        product.SetFoodDetails(new FoodDetails("Royal Canin", LifeStage.Adult, ProteinSource.Chicken, "Chicken", null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.SetPrescriptionRequired(true, TestUserContext.Create(), Guid.NewGuid());

        product.UpdateDetails(Details(product, ProductCategory.CatFood));

        Assert.True(product.PrescriptionRequired);
        Assert.NotNull(product.FoodDetails);
    }

    private static ProductAggregate ActiveProduct(out Guid skuId)
    {
        var product = NewProduct(ProductCategory.ToysAndFurniture);
        skuId = Guid.NewGuid();
        product.AddSku(new SkuAddDto(skuId, "SKU-A", null, "Small", null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.Publish(new PublicationFacts([skuId], true, []), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        return product;
    }

    // Row 32
    [Fact]
    public void AddSku_OnAnActiveProduct_Succeeds_ButTheNewSkuIsInactive()
    {
        var product = ActiveProduct(out var first);
        var added = Guid.NewGuid();

        product.AddSku(new SkuAddDto(added, "SKU-B", null, "Large", null, null, null), TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(2, product.Skus.Count);
        Assert.Equal(SkuStatus.Discontinued, product.Skus.Single(s => s.Id == added).Status);
        Assert.Equal(SkuStatus.Active, product.Skus.Single(s => s.Id == first).Status);
        // Inactive, so the PricedSkus rule (active SKUs only) still holds for a re-publication.
        Assert.All(ProductPublicationRules.Evaluate(product, new PublicationFacts([first], true, [])).Where(i => i.Item == ProductPublicationRules.PricedSkus), i => Assert.True(i.IsMet));
    }

    [Fact]
    public void AddSku_OnAnActiveProduct_ThenReactivate_NeedsAPriceNow()
    {
        var product = ActiveProduct(out _);
        var added = Guid.NewGuid();
        product.AddSku(new SkuAddDto(added, "SKU-B", null, "Large", null, null, null), TestUserContext.Create(), Guid.NewGuid());

        Assert.Throws<DomainRuleViolationException>(() => product.ReactivateSku(added, [], TestUserContext.Create(), Guid.NewGuid()));
        product.ReactivateSku(added, [added], TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(SkuStatus.Active, product.Skus.Single(s => s.Id == added).Status);
    }

    [Fact]
    public void AddSku_OnADraftProduct_StillWorks()
    {
        var product = NewProduct(ProductCategory.ToysAndFurniture);
        product.AddSku(new SkuAddDto(Guid.NewGuid(), "SKU-A", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        Assert.Single(product.Skus);
    }

    private static (ProductAggregate Product, Guid DiscontinuedSkuId) ActiveProductWithADiscontinuedSku()
    {
        var product = NewProduct(ProductCategory.ToysAndFurniture);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        product.AddSku(new SkuAddDto(first, "SKU-A", null, "Small", null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.AddSku(new SkuAddDto(second, "SKU-B", null, "Large", null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.DiscontinueSku(second, TestUserContext.Create(), Guid.NewGuid());
        product.Publish(new PublicationFacts([first], true, []), DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());
        return (product, second);
    }

    [Fact]
    public void ReactivateSku_OnAnActiveProduct_WithNoPriceNow_IsRefused()
    {
        var (product, skuId) = ActiveProductWithADiscontinuedSku();

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            product.ReactivateSku(skuId, [], TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(BusinessViolationCodes.ProductNotPublishable, ex.ErrorCode);
        Assert.Equal(SkuStatus.Discontinued, product.Skus.Single(s => s.Id == skuId).Status);
    }

    [Fact]
    public void ReactivateSku_OnAnActiveProduct_WithAPriceNow_Succeeds()
    {
        var (product, skuId) = ActiveProductWithADiscontinuedSku();

        product.ReactivateSku(skuId, [skuId], TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(SkuStatus.Active, product.Skus.Single(s => s.Id == skuId).Status);
    }

    [Fact]
    public void ReactivateSku_OnADraftProduct_NeedsNoPrice()
    {
        var product = NewProduct(ProductCategory.ToysAndFurniture);
        var skuId = Guid.NewGuid();
        product.AddSku(new SkuAddDto(skuId, "SKU-A", null, null, null, null, null), TestUserContext.Create(), Guid.NewGuid());
        product.DiscontinueSku(skuId, TestUserContext.Create(), Guid.NewGuid());

        product.ReactivateSku(skuId, [], TestUserContext.Create(), Guid.NewGuid());

        Assert.Equal(SkuStatus.Active, product.Skus.Single().Status);
    }

    // ── S5 (server half): LimitedMarkup site-relative links ─────────────────────────────────────

    [Theory]
    [InlineData("[home](/)")]
    [InlineData("[cats](/shop/products?category=CatFood)")]
    [InlineData("[ext](https://example.com/a)")]
    public void LimitedMarkup_AcceptsHttpsAndSiteRelativeLinks(string markup)
    {
        Assert.Null(LimitedMarkup.Validate(markup));
    }

    [Theory]
    [InlineData("[x](//evil.example)")]
    [InlineData("[x](/\\evil.example)")]
    [InlineData("[x](/\t/evil.example)")]
    [InlineData("[x](/\r/evil.example)")]
    [InlineData("[x](/\n/evil.example)")]
    [InlineData("[x](/ok\tpath)")]
    [InlineData("[x](evil.example)")]
    [InlineData("[x](http://evil.example)")]
    public void LimitedMarkup_RefusesProtocolRelativeBackslashAndWhitespaceTrickedLinks(string markup)
    {
        // The C0 tab/CR/LF cases are legitimate text elsewhere in the body, so they reach the link check.
        Assert.Equal(BusinessViolationCodes.InvalidContentLink, LimitedMarkup.Validate(markup));
    }
}
