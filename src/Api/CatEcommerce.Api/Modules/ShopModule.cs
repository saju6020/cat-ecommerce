// Shop module -- HO-SHP-001.
//
// Registers every command handler, validator, query handler and domain event handler this area
// needs, plus its own rate-limit policies and (from Phase G) the Shop worker. Program.cs already
// calls AddShopModule(builder.Configuration) once (ADR-341) -- this file is the only one that
// needs to change as each phase lands.

using CatEcommerce.Api.Background;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Product;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DomainEventHandlers.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Infrastructure.Hosting;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.QueryHandlers.Product;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;

namespace CatEcommerce.Api.Modules;

public static class ShopModule
{
    public static IServiceCollection AddShopModule(this IServiceCollection services, IConfiguration configuration)
    {
        // FAKE until HO-ORD-001 lands -- replace this registration (CONTRACTS.md pattern).
        // Fail-closed (ADR-086): denies every review submission until the real reader lands.
        services.AddScoped<IPurchaseHistoryReader, NoPurchaseHistoryReader>();

        services.AddSingleton<IValidateOptions<ShopOptions>, ShopOptionsValidator>();
        services.AddOptions<ShopOptions>()
            .Bind(configuration.GetSection(ShopOptions.SectionName))
            .Validate(o => o.Worker.IntervalSeconds >= 1, "Shop:Worker:IntervalSeconds must be at least 1.")
            .ValidateOnStart();

        // Ports and read helpers (section 8.7/11.6/12.2), each on its own line.
        services.AddScoped<ISkuAvailabilityReader, SkuAvailabilityReader>();
        services.AddScoped<IShopCustomerDataFinder, ShopCustomerDataFinder>();
        services.AddScoped<IBundleMembershipReader, BundleMembershipReader>();

        // Content locales (section 8.14): the default is the first entry; validated on start.
        services.AddSingleton<IValidateOptions<LocalisationOptions>, LocalisationOptionsValidator>();
        services.AddOptions<LocalisationOptions>()
            .Bind(configuration.GetSection(LocalisationOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IContentLocaleSettings, ConfiguredContentLocales>();

        // HO-NTF-001's SubjectGuardResolver resolves IEnumerable<INotificationSubjectGuard> -- this
        // adds the StockAlert guard alongside the other areas', additively.
        services.AddScoped<INotificationSubjectGuard, StockAlertGuard>();

        // HO-XCT-001 C-ALL-2: Shop holds only customer ids -- exempted from erasure, not given an
        // eraser (see ErasureExemptions.Shop.cs). No IPersonalDataEraser is registered here.

        // Worker (section 12.2).
        services.AddScoped<ShopWorkCycle>();
        services.AddHostedService<ShopWorker>();

        // The facet cache (GetCatalogueFiltersQueryHandler uses IMemoryCache): registered here rather
        // than relying on another area having done so.
        services.AddMemoryCache();

        RegisterCommands(services);
        RegisterQueries(services);
        RegisterDomainEventHandlers(services);

        // ── Rate limiting (section 12.3) -- composes with every other area's own AddRateLimiter
        // call; each area adds only its own named policies. ─────────────────────────────────────
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(ShopRateLimitPolicies.Catalogue, context =>
            {
                // From ShopOptions (not a literal) so a test factory can lower/raise it (I-E10/I-E11).
                var limits = context.RequestServices.GetRequiredService<IOptions<ShopOptions>>().Value.Catalogue;
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
            });

            options.AddPolicy(ShopRateLimitPolicies.Reviews, context =>
            {
                var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
                var partitionKey = caller is { Kind: ResolvedCallerKind.Customer } ? $"customer:{caller.AccountId}" : $"ip:{context.Connection.RemoteIpAddress}";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 });
            });

            options.AddPolicy(ShopRateLimitPolicies.StockAlerts, context =>
            {
                var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
                var partitionKey = caller is { Kind: ResolvedCallerKind.Customer } ? $"customer:{caller.AccountId}" : $"ip:{context.Connection.RemoteIpAddress}";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                { PermitLimit = 30, Window = TimeSpan.FromHours(1), QueueLimit = 0 });
            });

            // Row 20: the food recommendations read the candidate pool and the diet gate, so they are
            // limited per signed-in customer (not per IP), the same partitioning as the policies above.
            options.AddPolicy(ShopRateLimitPolicies.FoodRecommendations, context =>
            {
                var caller = context.Items[ResolvedCaller.HttpContextItemsKey] as ResolvedCaller;
                var partitionKey = caller is { Kind: ResolvedCallerKind.Customer } ? $"customer:{caller.AccountId}" : $"ip:{context.Connection.RemoteIpAddress}";
                var limits = context.RequestServices.GetRequiredService<IOptions<ShopOptions>>().Value.FoodRecommendations;
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                { PermitLimit = limits.PermitLimit, Window = TimeSpan.FromMinutes(limits.WindowMinutes), QueueLimit = 0 });
            });
        });

        return services;
    }

    private static void RegisterCommands(IServiceCollection services)
    {
        services.AddScoped<ICommandHandlerAsync<CreateProductCommand>, CreateProductCommandHandler>();
        services.AddScoped<IValidator<CreateProductCommand>, CreateProductCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateProductDetailsCommand>, UpdateProductDetailsCommandHandler>();
        services.AddScoped<IValidator<UpdateProductDetailsCommand>, UpdateProductDetailsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ChangeProductSlugCommand>, ChangeProductSlugCommandHandler>();
        services.AddScoped<IValidator<ChangeProductSlugCommand>, ChangeProductSlugCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<AddSkuCommand>, AddSkuCommandHandler>();
        services.AddScoped<IValidator<AddSkuCommand>, AddSkuCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateSkuCommand>, UpdateSkuCommandHandler>();
        services.AddScoped<IValidator<UpdateSkuCommand>, UpdateSkuCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<DiscontinueSkuCommand>, DiscontinueSkuCommandHandler>();
        services.AddScoped<IValidator<DiscontinueSkuCommand>, DiscontinueSkuCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReactivateSkuCommand>, ReactivateSkuCommandHandler>();
        services.AddScoped<IValidator<ReactivateSkuCommand>, ReactivateSkuCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetProductImagesCommand>, SetProductImagesCommandHandler>();
        services.AddScoped<IValidator<SetProductImagesCommand>, SetProductImagesCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ScheduleSkuPriceCommand>, ScheduleSkuPriceCommandHandler>();
        services.AddScoped<IValidator<ScheduleSkuPriceCommand>, ScheduleSkuPriceCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<PublishProductCommand>, PublishProductCommandHandler>();
        services.AddScoped<IValidator<PublishProductCommand>, PublishProductCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UnpublishProductCommand>, UnpublishProductCommandHandler>();
        services.AddScoped<IValidator<UnpublishProductCommand>, UnpublishProductCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ArchiveProductCommand>, ArchiveProductCommandHandler>();
        services.AddScoped<IValidator<ArchiveProductCommand>, ArchiveProductCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetFoodDetailsCommand>, SetFoodDetailsCommandHandler>();
        services.AddScoped<IValidator<SetFoodDetailsCommand>, SetFoodDetailsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetPrescriptionRequiredCommand>, SetPrescriptionRequiredCommandHandler>();
        services.AddScoped<IValidator<SetPrescriptionRequiredCommand>, SetPrescriptionRequiredCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateSourcingOriginCommand>, CreateSourcingOriginCommandHandler>();
        services.AddScoped<IValidator<CreateSourcingOriginCommand>, CreateSourcingOriginCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateSourcingOriginCommand>, UpdateSourcingOriginCommandHandler>();
        services.AddScoped<IValidator<UpdateSourcingOriginCommand>, UpdateSourcingOriginCommandValidator>();

        // Inbound only -- HO-ORD-001 (and tests) dispatch this in-process; no controller sends it.
        services.AddScoped<ICommandHandlerAsync<RecordProductSalesStatsCommand>, RecordProductSalesStatsCommandHandler>();
        services.AddScoped<IValidator<RecordProductSalesStatsCommand>, RecordProductSalesStatsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SubmitProductReviewCommand>, SubmitProductReviewCommandHandler>();
        services.AddScoped<IValidator<SubmitProductReviewCommand>, SubmitProductReviewCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<EditProductReviewCommand>, EditProductReviewCommandHandler>();
        services.AddScoped<IValidator<EditProductReviewCommand>, EditProductReviewCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<WithdrawProductReviewCommand>, WithdrawProductReviewCommandHandler>();
        services.AddScoped<IValidator<WithdrawProductReviewCommand>, WithdrawProductReviewCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<PublishProductReviewCommand>, PublishProductReviewCommandHandler>();
        services.AddScoped<IValidator<PublishProductReviewCommand>, PublishProductReviewCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RejectProductReviewCommand>, RejectProductReviewCommandHandler>();
        services.AddScoped<IValidator<RejectProductReviewCommand>, RejectProductReviewCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<HideProductReviewCommand>, HideProductReviewCommandHandler>();
        services.AddScoped<IValidator<HideProductReviewCommand>, HideProductReviewCommandValidator>();

        // ── Phase F: bundles and curated related products (section 11.1 C13). ──
        services.AddScoped<ICommandHandlerAsync<SetBundleComponentsCommand>, SetBundleComponentsCommandHandler>();
        services.AddScoped<IValidator<SetBundleComponentsCommand>, SetBundleComponentsCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetRelatedProductsCommand>, SetRelatedProductsCommandHandler>();
        services.AddScoped<IValidator<SetRelatedProductsCommand>, SetRelatedProductsCommandValidator>();

        // ── Phase H: per-locale product content (section 11.1 C17). ──
        services.AddScoped<ICommandHandlerAsync<SetProductTranslationCommand>, SetProductTranslationCommandHandler>();
        services.AddScoped<IValidator<SetProductTranslationCommand>, SetProductTranslationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RemoveProductTranslationCommand>, RemoveProductTranslationCommandHandler>();
        services.AddScoped<IValidator<RemoveProductTranslationCommand>, RemoveProductTranslationCommandValidator>();

        // ── Phase G: wishlist, stock alerts and the worker's notify command (section 11.1 C14-C16). ──
        services.AddScoped<ICommandHandlerAsync<AddWishlistItemCommand>, AddWishlistItemCommandHandler>();
        services.AddScoped<IValidator<AddWishlistItemCommand>, AddWishlistItemCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RemoveWishlistItemCommand>, RemoveWishlistItemCommandHandler>();
        services.AddScoped<IValidator<RemoveWishlistItemCommand>, RemoveWishlistItemCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RequestStockAlertCommand>, RequestStockAlertCommandHandler>();
        services.AddScoped<IValidator<RequestStockAlertCommand>, RequestStockAlertCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelStockAlertCommand>, CancelStockAlertCommandHandler>();
        services.AddScoped<IValidator<CancelStockAlertCommand>, CancelStockAlertCommandValidator>();

        // Sent only by ShopWorker (ShopWorkCycle); no controller dispatches it.
        services.AddScoped<ICommandHandlerAsync<NotifyStockAlertCommand>, NotifyStockAlertCommandHandler>();
        services.AddScoped<IValidator<NotifyStockAlertCommand>, NotifyStockAlertCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        services.AddScoped<IQueryHandlerAsync<GetStaffProductsQuery, PagedResult<StaffProductSummaryViewModel>>, GetStaffProductsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffProductDetailQuery, StaffProductDetailViewModel?>, GetStaffProductDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSkuPriceHistoryQuery, SkuPriceHistoryViewModel?>, GetSkuPriceHistoryQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<FindSkuByBarcodeQuery, SkuByBarcodeViewModel?>, FindSkuByBarcodeQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCountriesQuery, IReadOnlyList<CountryViewModel>>, GetCountriesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffOriginsQuery, IReadOnlyList<StaffOriginViewModel>>, GetStaffOriginsQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetProductReviewsQuery, PagedResult<ProductReviewViewModel>>, GetProductReviewsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReviewEligibilityQuery, ReviewEligibilityViewModel?>, GetReviewEligibilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetMyReviewsQuery, IReadOnlyList<MyReviewViewModel>>, GetMyReviewsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReviewModerationQueueQuery, PagedResult<ReviewModerationItemViewModel>>, GetReviewModerationQueueQueryHandler>();

        services.AddScoped<IQueryHandlerAsync<GetCatalogueQuery, CatalogueResult>, GetCatalogueQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCatalogueFiltersQuery, CatalogueFiltersViewModel>, GetCatalogueFiltersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCategoriesQuery, IReadOnlyList<CategoryViewModel>>, GetCategoriesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetOriginsQuery, IReadOnlyList<OriginViewModel>>, GetOriginsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetOriginQuery, OriginViewModel?>, GetOriginQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetProductDetailQuery, ProductDetailViewModel?>, GetProductDetailQueryHandler>();

        // ── Phase F: prescription gating, bundles, recommendations and the contract queries. ──
        services.AddScoped<IQueryHandlerAsync<GetPurchasabilityQuery, PurchasabilityViewModel?>, GetPurchasabilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<CheckPrescriptionEligibilityQuery, PrescriptionEligibilityViewModel?>, CheckPrescriptionEligibilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBundleCompositionQuery, IReadOnlyList<BundleCompositionLineViewModel>?>, GetBundleCompositionQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetProductRecommendationsQuery, RecommendationsViewModel?>, GetProductRecommendationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSkuSaleFactsQuery, IReadOnlyList<SkuSaleFactViewModel>>, GetSkuSaleFactsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetSkuCatalogueFactsQuery, IReadOnlyList<SkuCatalogueFactViewModel>>, GetSkuCatalogueFactsQueryHandler>();

        // ── Phase G: wishlist, alerts, deliverability and the worker's due-alert read. ──
        services.AddScoped<IQueryHandlerAsync<GetWishlistQuery, WishlistViewModel>, GetWishlistQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetMyStockAlertsQuery, IReadOnlyList<StockAlertViewModel>>, GetMyStockAlertsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStockAlertDeliverabilityQuery, StockAlertDeliverabilityViewModel?>, GetStockAlertDeliverabilityQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDueStockAlertsQuery, IReadOnlyList<Guid>>, GetDueStockAlertsQueryHandler>();

        // ── Phase H: food recommendations for a cat and the enabled locales. ──
        services.AddScoped<IQueryHandlerAsync<GetFoodRecommendationsQuery, FoodRecommendationsViewModel?>, GetFoodRecommendationsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetLocalesQuery, LocalesViewModel>, GetLocalesQueryHandler>();
    }

    private static void RegisterDomainEventHandlers(IServiceCollection services)
    {
        services.AddScoped<IEventHandlerAsync<ProductPrescriptionRequirementChangedEvent>, AuditPrescriptionFlagHandler>();

        services.AddScoped<IEventHandlerAsync<ProductReviewPublishedEvent>, AuditReviewModerationHandler>();
        services.AddScoped<IEventHandlerAsync<ProductReviewRejectedEvent>, AuditReviewModerationHandler>();
        services.AddScoped<IEventHandlerAsync<ProductReviewHiddenEvent>, AuditReviewModerationHandler>();

        // Phase G (section 11.6, ADR-096). Idempotent; none of them enqueues (HO-NTF-001 ADR-057).
        services.AddScoped<IEventHandlerAsync<SkuDiscontinuedEvent>, CloseStockAlertsOnSkuDiscontinuedHandler>();
        services.AddScoped<IEventHandlerAsync<ProductArchivedEvent>, CloseStockAlertsOnProductArchivedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignShopDataOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, ClearShopDataOnCustomerAnonymisedHandler>();
    }
}
