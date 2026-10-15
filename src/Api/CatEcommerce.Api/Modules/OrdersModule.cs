// Orders module -- HO-ORD-001 (Cart, Checkout and Orders).
//
// Program.cs already calls AddOrdersModule(builder.Configuration) once (ADR-341); this file is the only one that changes as each phase lands.
// Registered explicitly -- there is no assembly scanning: every command handler, validator (as IValidator<T>, never the base class), query
// handler and domain event handler, the Infrastructure ports, the rate-limit policies and (from Phase E) the OrderWorker.
//
// Phase A: the HTTP idempotency edge (ADR-202) and the currency guard (OQ-5).
// Phase B: delivery zones, couriers, the bank-transfer account, discount codes.
// Phase C: the cart (guest cookie, merge, retention sweep).
// Phase D: the checkout quote, the order aggregate and its tables, and placement (C9) in one database transaction (ADR-215, HO-ORD-001 §1 Amended (build)).
// Phase E: payment paths and cancellation (C10-C15, C27), reconciliation issues, the order reads, the OrderWorker, the merge / anonymise / audit event handlers.
// Phase F: fulfilment (packing, dispatch, ready-for-collection, tracking, delivery, delivery failure, reallocation), the staff reads and the courier tracking webhook.

using CatEcommerce.Api.Background;
using CatEcommerce.Api.Ordering;
using CatEcommerce.Api.RateLimiting;
using CatEcommerce.Application.CommandHandlers.Order;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Application.DomainEventHandlers.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Ordering;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Order;
using CatEcommerce.Read.QueryHandlers.Order;
using CatEcommerce.Read.ViewModels;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Events;
using Platform.Infrastructure.Core.Queries;
using RateLimitPartition = System.Threading.RateLimiting.RateLimitPartition;

namespace CatEcommerce.Api.Modules;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        // ── Edge infrastructure (ADR-202): claims commit on their own, through a separate short-lived context. ──
        services.AddSingleton<IDbContextFactory<CatEcommerceDbContext>, OpsDbContextFactory>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();

        // U-A4 / OQ-5: a currency other than a two-decimal one is refused at start-up.
        services.AddHostedService<CurrencyStartupGuard>();

        // ── Ports (Infrastructure) ──
        services.AddScoped<ICatalogueLookup, CatalogueLookup>();
        services.AddSingleton<IGuestCartTokens, GuestCartTokens>();
        services.AddScoped<ICartFinder, CartFinder>();
        services.AddScoped<GuestCartCookieWriter>();
        services.AddScoped<ICheckoutInputsLoader, CheckoutInputsLoader>();
        services.AddScoped<ICustomerDirectory, CustomerDirectory>();
        services.AddScoped<ICounterBuyerRegistrar, AccountsCounterBuyerRegistrar>(); // ADR-216 amended 2026-10-10: find-or-register in its own short transaction, before the sale scope
        services.AddScoped<ICounterCustomerFinder, AccountsCounterCustomerFinder>(); // ADR-216: HO-ACC-001's FindCustomersQuery, adapted in the Api project
        services.AddScoped<ICustomerPolicyStatus, CustomerPolicyStatus>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<ITransactionalScope, TransactionalScope>();
        services.AddScoped<IOrderBillingReader, OrderBillingReader>();
        services.AddScoped<IReconciliationIssueRecorder, ReconciliationIssueRecorder>();
        services.AddScoped<IOrderFinder, OrderFinder>();
        services.AddScoped<TransferDependencies>();
        services.AddScoped<IWebhookInbox, WebhookInbox>();
        services.AddScoped<IOrderingStaging, OrderingStaging>();
        services.AddScoped<IReturnFinder, ReturnFinder>();
        services.AddOptions<CouriersOptions>().BindConfiguration(CouriersOptions.SectionName); // Couriers:Webhooks:{provider}:Secret -- secrets live outside committed files (§20.8)

        // C-ALL-2 / C-ALL-3: orders are financial records, held for review (never auto-deleted).
        services.AddScoped<IPersonalDataEraser, OrderingPersonalDataEraser>();
        services.AddScoped<IRetentionReviewSource, OrderingRetentionReviewSource>();

        // ── The order worker (§12.2): options bound from "Orders", one scoped work cycle, one hosted service. ──
        services.AddOptions<OrdersOptions>()
            .BindConfiguration(OrdersOptions.SectionName)
            .Validate(o => o.Worker.IntervalSeconds >= 1, "Orders:Worker:IntervalSeconds must be at least 1.")
            .ValidateOnStart();
        services.AddScoped<OrderWorkCycle>();
        services.AddHostedService<OrderWorker>();

        RegisterCommands(services);
        RegisterEventHandlers(services);
        RegisterQueries(services);

        // ── Rate limiting (§12.3) -- composes with every other area's own AddRateLimiter call; each area adds only its own named policies. ──
        services.AddRateLimiter(options =>
        {
            var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>() ?? new RateLimitingOptions();

            AddIpPolicy(options, RateLimitPolicies.Cart, limits.Cart);
            AddIpPolicy(options, RateLimitPolicies.Checkout, limits.Checkout);
            AddIpPolicy(options, RateLimitPolicies.CourierWebhook, limits.CourierWebhook);
        });

        return services;
    }

    private static void AddIpPolicy(RateLimiterOptions options, string name, RateLimitPolicyOptions policy) =>
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            { PermitLimit = policy.PermitLimit, Window = TimeSpan.FromMinutes(policy.WindowMinutes), QueueLimit = 0 }));

    private static void RegisterCommands(IServiceCollection services)
    {
        // ── Phase B: configuration (C1-C4) ──
        services.AddScoped<ICommandHandlerAsync<CreateDeliveryZoneCommand>, CreateDeliveryZoneCommandHandler>();
        services.AddScoped<IValidator<CreateDeliveryZoneCommand>, CreateDeliveryZoneCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateDeliveryZoneCommand>, UpdateDeliveryZoneCommandHandler>();
        services.AddScoped<IValidator<UpdateDeliveryZoneCommand>, UpdateDeliveryZoneCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetDeliveryZoneActivationCommand>, SetDeliveryZoneActivationCommandHandler>();
        services.AddScoped<IValidator<SetDeliveryZoneActivationCommand>, SetDeliveryZoneActivationCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateCourierCommand>, CreateCourierCommandHandler>();
        services.AddScoped<IValidator<CreateCourierCommand>, CreateCourierCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateCourierCommand>, UpdateCourierCommandHandler>();
        services.AddScoped<IValidator<UpdateCourierCommand>, UpdateCourierCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetBankTransferAccountCommand>, SetBankTransferAccountCommandHandler>();
        services.AddScoped<IValidator<SetBankTransferAccountCommand>, SetBankTransferAccountCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CreateDiscountCodeCommand>, CreateDiscountCodeCommandHandler>();
        services.AddScoped<IValidator<CreateDiscountCodeCommand>, CreateDiscountCodeCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateDiscountCodeCommand>, UpdateDiscountCodeCommandHandler>();
        services.AddScoped<IValidator<UpdateDiscountCodeCommand>, UpdateDiscountCodeCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<SetDiscountCodeActivationCommand>, SetDiscountCodeActivationCommandHandler>();
        services.AddScoped<IValidator<SetDiscountCodeActivationCommand>, SetDiscountCodeActivationCommandValidator>();

        // ── Phase D: placement (C9) ──
        services.AddScoped<ICommandHandlerAsync<PlaceOrderCommand>, PlaceOrderCommandHandler>();
        services.AddScoped<IValidator<PlaceOrderCommand>, PlaceOrderCommandValidator>();

        // ── Phase E: payment paths, cancellation, re-order, reconciliation (C10-C15, C27) ──
        services.AddScoped<ICommandHandlerAsync<SubmitTransferReferenceCommand>, SubmitTransferReferenceCommandHandler>();
        services.AddScoped<IValidator<SubmitTransferReferenceCommand>, SubmitTransferReferenceCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ConfirmBankTransferCommand>, ConfirmBankTransferCommandHandler>();
        services.AddScoped<IValidator<ConfirmBankTransferCommand>, ConfirmBankTransferCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ApproveHeldPaymentCommand>, ApproveHeldPaymentCommandHandler>();
        services.AddScoped<IValidator<ApproveHeldPaymentCommand>, ApproveHeldPaymentCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RejectHeldPaymentCommand>, RejectHeldPaymentCommandHandler>();
        services.AddScoped<IValidator<RejectHeldPaymentCommand>, RejectHeldPaymentCommandValidator>();

        // Sent by OrderWorker only; no controller dispatches it.
        services.AddScoped<ICommandHandlerAsync<ExpireUnpaidOrderCommand>, ExpireUnpaidOrderCommandHandler>();
        services.AddScoped<IValidator<ExpireUnpaidOrderCommand>, ExpireUnpaidOrderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<CancelOrderCommand>, CancelOrderCommandHandler>();
        services.AddScoped<IValidator<CancelOrderCommand>, CancelOrderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReorderCommand>, ReorderCommandHandler>();
        services.AddScoped<IValidator<ReorderCommand>, ReorderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ResolveReconciliationIssueCommand>, ResolveReconciliationIssueCommandHandler>();
        services.AddScoped<IValidator<ResolveReconciliationIssueCommand>, ResolveReconciliationIssueCommandValidator>();

        // ── Phase F: fulfilment (C16-C21) ──
        services.AddScoped<ICommandHandlerAsync<StartPackingCommand>, StartPackingCommandHandler>();
        services.AddScoped<IValidator<StartPackingCommand>, StartPackingCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<DispatchOrderCommand>, DispatchOrderCommandHandler>();
        services.AddScoped<IValidator<DispatchOrderCommand>, DispatchOrderCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<MarkReadyForCollectionCommand>, MarkReadyForCollectionCommandHandler>();
        services.AddScoped<IValidator<MarkReadyForCollectionCommand>, MarkReadyForCollectionCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordTrackingNumberCommand>, RecordTrackingNumberCommandHandler>();
        services.AddScoped<IValidator<RecordTrackingNumberCommand>, RecordTrackingNumberCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordDeliveryCommand>, RecordDeliveryCommandHandler>();
        services.AddScoped<IValidator<RecordDeliveryCommand>, RecordDeliveryCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RecordDeliveryFailureCommand>, RecordDeliveryFailureCommandHandler>();
        services.AddScoped<IValidator<RecordDeliveryFailureCommand>, RecordDeliveryFailureCommandValidator>();

        // Sent by the courier webhook controller only, after the signature was verified.
        services.AddScoped<ICommandHandlerAsync<ApplyCourierTrackingEventCommand>, ApplyCourierTrackingEventCommandHandler>();
        services.AddScoped<IValidator<ApplyCourierTrackingEventCommand>, ApplyCourierTrackingEventCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReallocateOrderLineCommand>, ReallocateOrderLineCommandHandler>();
        services.AddScoped<IValidator<ReallocateOrderLineCommand>, ReallocateOrderLineCommandValidator>();

        // ── Phase G: returns and refunds (C22-C25) ──
        services.AddScoped<ICommandHandlerAsync<RequestReturnCommand>, RequestReturnCommandHandler>();
        services.AddScoped<IValidator<RequestReturnCommand>, RequestReturnCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ApproveReturnCommand>, ApproveReturnCommandHandler>();
        services.AddScoped<IValidator<ApproveReturnCommand>, ApproveReturnCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RejectReturnCommand>, RejectReturnCommandHandler>();
        services.AddScoped<IValidator<RejectReturnCommand>, RejectReturnCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<ReceiveReturnCommand>, ReceiveReturnCommandHandler>();
        services.AddScoped<IValidator<ReceiveReturnCommand>, ReceiveReturnCommandValidator>();


        services.AddScoped<ICommandHandlerAsync<CompleteCounterSaleCommand>, CompleteCounterSaleCommandHandler>();
        services.AddScoped<IValidator<CompleteCounterSaleCommand>, CompleteCounterSaleCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<IssueRefundCommand>, IssueRefundCommandHandler>();
        services.AddScoped<IValidator<IssueRefundCommand>, IssueOrderRefundCommandValidator>();

        // ── Phase C: the cart (C5-C8) ──
        services.AddScoped<ICommandHandlerAsync<AddCartLineCommand>, AddCartLineCommandHandler>();
        services.AddScoped<IValidator<AddCartLineCommand>, AddCartLineCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<UpdateCartLineCommand>, UpdateCartLineCommandHandler>();
        services.AddScoped<IValidator<UpdateCartLineCommand>, UpdateCartLineCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<RemoveCartLineCommand>, RemoveCartLineCommandHandler>();
        services.AddScoped<IValidator<RemoveCartLineCommand>, RemoveCartLineCommandValidator>();

        services.AddScoped<ICommandHandlerAsync<MergeGuestCartCommand>, MergeGuestCartCommandHandler>();
        services.AddScoped<IValidator<MergeGuestCartCommand>, MergeGuestCartCommandValidator>();

        // Sent by OrderWorker only; no controller dispatches it.
        services.AddScoped<ICommandHandlerAsync<PurgeStaleGuestCartsCommand>, PurgeStaleGuestCartsCommandHandler>();
        services.AddScoped<IValidator<PurgeStaleGuestCartsCommand>, PurgeStaleGuestCartsCommandValidator>();
    }

    private static void RegisterQueries(IServiceCollection services)
    {
        // ── Phase B: configuration reads ──
        services.AddScoped<IQueryHandlerAsync<GetDeliveryZonesQuery, IReadOnlyList<DeliveryZoneSummaryViewModel>>, GetDeliveryZonesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDeliveryZoneQuery, DeliveryZoneViewModel?>, GetDeliveryZoneQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<ResolveDeliveryZoneQuery, ResolvedDeliveryZoneViewModel>, ResolveDeliveryZoneQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCouriersQuery, IReadOnlyList<CourierViewModel>>, GetCouriersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetBankTransferAccountQuery, BankTransferAccountViewModel>, GetBankTransferAccountQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetDiscountCodesQuery, IReadOnlyList<DiscountCodeViewModel>>, GetDiscountCodesQueryHandler>();

        // ── Phase C: the cart read ──
        services.AddScoped<IQueryHandlerAsync<GetCartQuery, CartViewModel>, GetCartQueryHandler>();

        // ── Phase G: returns and refunds reads ──
        services.AddScoped<IQueryHandlerAsync<GetCustomerReturnQuery, CustomerReturnViewModel?>, GetCustomerReturnQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReturnsQuery, PagedResult<StaffReturnRowViewModel>>, GetReturnsQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReturnDetailQuery, StaffReturnDetailViewModel?>, GetReturnDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPosSkuQuery, PosSkuViewModel?>, GetPosSkuQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<SearchPosSkusQuery, IReadOnlyList<PosSkuViewModel>>, SearchPosSkusQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCounterQuoteQuery, CounterQuoteViewModel>, GetCounterQuoteQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReceiptQuery, ReceiptViewModel?>, GetReceiptQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRefundOptionsQuery, RefundOptionsViewModel?>, GetRefundOptionsQueryHandler>();

        // ── Phase F: the staff reads ──
        services.AddScoped<IQueryHandlerAsync<GetStaffOrderQueueQuery, PagedResult<StaffOrderRowViewModel>>, GetStaffOrderQueueQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffCustomerOrdersQuery, PagedResult<StaffCustomerOrderRowViewModel>?>, GetStaffCustomerOrdersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetStaffOrderDetailQuery, StaffOrderDetailViewModel?>, GetStaffOrderDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPickListQuery, PickListViewModel>, GetPickListQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetPackingSlipQuery, PackingSlipViewModel?>, GetPackingSlipQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCourierManifestQuery, IReadOnlyList<ManifestRowViewModel>>, GetCourierManifestQueryHandler>();

        // ── Phase E: the order reads ──
        services.AddScoped<IQueryHandlerAsync<GetCustomerOrdersQuery, PagedResult<CustomerOrderSummaryViewModel>>, GetCustomerOrdersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetCustomerOrderDetailQuery, CustomerOrderDetailViewModel?>, GetCustomerOrderDetailQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetAwaitingPaymentOrdersQuery, IReadOnlyList<AwaitingPaymentOrderViewModel>>, GetAwaitingPaymentOrdersQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetReconciliationIssuesQuery, PagedResult<ReconciliationIssueViewModel>>, GetReconciliationIssuesQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetRefundsDueQuery, PagedResult<RefundDueOrderViewModel>>, GetRefundsDueQueryHandler>();
        services.AddScoped<IQueryHandlerAsync<GetExpirableOrdersQuery, IReadOnlyList<Guid>>, GetExpirableOrdersQueryHandler>();

        // ── Phase D: the checkout quote ──
        services.AddScoped<IQueryHandlerAsync<GetCheckoutQuoteQuery, CheckoutQuoteViewModel>, GetCheckoutQuoteQueryHandler>();
    }

    private static void RegisterEventHandlers(IServiceCollection services)
    {
        // HO-ACC-001 ADR-039 / ADR-214: a merge moves carts and orders to the survivor; an anonymisation redacts or flags them. Neither enqueues a message.
        services.AddScoped<IEventHandlerAsync<CustomerMergedEvent>, ReassignOrderingOnCustomerMergedHandler>();
        services.AddScoped<IEventHandlerAsync<CustomerAccountAnonymisedEvent>, RedactOrdersOnCustomerAnonymisedHandler>();

        // BR-17: the actor of every money-relevant order event, as a BusinessEvent audit row.
        services.AddScoped<IEventHandlerAsync<OrderConfirmedEvent>, AuditOrderMoneyEventsHandler>();
        services.AddScoped<IEventHandlerAsync<OrderPaymentHeldForApprovalEvent>, AuditOrderMoneyEventsHandler>();
        services.AddScoped<IEventHandlerAsync<OrderDeliveredEvent>, AuditOrderMoneyEventsHandler>();
        services.AddScoped<IEventHandlerAsync<OrderLineReallocatedEvent>, AuditOrderMoneyEventsHandler>();
        services.AddScoped<IEventHandlerAsync<CounterSaleCompletedEvent>, AuditOrderMoneyEventsHandler>();
        services.AddScoped<IEventHandlerAsync<RefundIssuedEvent>, AuditOrderMoneyEventsHandler>();
    }
}
