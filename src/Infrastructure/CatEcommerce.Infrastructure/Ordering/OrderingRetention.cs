using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 / HO-XCT-001 C-ALL-2. An order is a financial record (D-9, retained per tax law) and carries the customer's delivery name, phone and address
/// as a snapshot. This eraser <b>mutates nothing</b>: it reports <see cref="ErasureOutcome.Held"/> with the earliest date the retained data may be reviewed,
/// or <see cref="ErasureOutcome.Completed"/> when the customer has no orders (or every order is past its period -- a review flag only, never an automatic
/// delete, ADR-301). Same fixed six-year default as Billing until <c>retention.financialYears</c> lands.
/// </summary>
public sealed class OrderingPersonalDataEraser(CatEcommerceDbContext db) : IPersonalDataEraser
{
    public const int DefaultRetentionYears = 6;

    public string ContextKey => "orders";

    public async Task<ErasureOutcome> EraseAsync(Guid customerAccountId, DateTimeOffset asOf, UserContext actor, CancellationToken ct)
    {
        var latest = await db.Set<OrderAggregate>().AsNoTracking()
            .Where(o => o.CustomerId == customerAccountId)
            .MaxAsync(o => (DateTimeOffset?)o.PlacedOn, ct);
        if (latest is null) return new ErasureOutcome.Completed();

        var until = DateOnly.FromDateTime(latest.Value.UtcDateTime).AddYears(DefaultRetentionYears);
        var today = DateOnly.FromDateTime(asOf.UtcDateTime);
        return until > today ? new ErasureOutcome.Held(until, "D-9 financial record") : new ErasureOutcome.Completed();
    }
}

/// <summary>HO-XCT-001 C-ALL-3 -- how many orders are past the retention period (D-9). A review count only (ADR-301).</summary>
public sealed class OrderingRetentionReviewSource(CatEcommerceDbContext db) : IRetentionReviewSource
{
    public string SourceKey => "orders-financial";

    public async Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct)
    {
        var cutoff = new DateTimeOffset(asOf.AddYears(-OrderingPersonalDataEraser.DefaultRetentionYears).ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var past = db.Set<OrderAggregate>().AsNoTracking().Where(o => o.PlacedOn <= cutoff);
        var count = await past.CountAsync(ct);
        if (count == 0) return [];

        var oldest = await past.MinAsync(o => o.PlacedOn, ct);
        return [new RetentionReviewCount(RecordClass.Financial, "sales_order", count, DateOnly.FromDateTime(oldest.UtcDateTime))];
    }
}
