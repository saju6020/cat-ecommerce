using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Persistence;

/// <summary>Ordering (HO-ORD-001) DbSets -- one partial per area (ADR-341). The model is discovered from the configurations; these properties
/// are the typed entry points. The two plain entities live in schema <c>ops</c> (generic edge infrastructure, ADR-202/ADR-209).</summary>
public sealed partial class CatEcommerceDbContext
{
    // ── Phase D: orders ──
    public DbSet<OrderAggregate> Orders => Set<OrderAggregate>();

    // ── Phase C: the cart ──
    public DbSet<CartAggregate> Carts => Set<CartAggregate>();

    // ── Phase B: configuration roots ──
    public DbSet<DeliveryZoneAggregate> DeliveryZones => Set<DeliveryZoneAggregate>();
    public DbSet<CourierAggregate> Couriers => Set<CourierAggregate>();
    public DbSet<BankTransferAccountAggregate> BankTransferAccounts => Set<BankTransferAccountAggregate>();
    public DbSet<DiscountCodeAggregate> DiscountCodes => Set<DiscountCodeAggregate>();

    // ── Phase G: returns and refunds ──
    public DbSet<ReturnRequestAggregate> ReturnRequests => Set<ReturnRequestAggregate>();
    public DbSet<OrderRefundAggregate> OrderRefunds => Set<OrderRefundAggregate>();

    // ── Phase E: reconciliation issues ──
    public DbSet<OrderReconciliationIssueAggregate> ReconciliationIssues => Set<OrderReconciliationIssueAggregate>();

    // ── Phase A: edge infrastructure (schema ops) ──
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<InboundWebhookReceipt> InboundWebhookReceipts => Set<InboundWebhookReceipt>();
}
