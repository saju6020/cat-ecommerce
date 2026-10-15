using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing.ItemSources;

/// <summary>
/// HO-BIL-001 Phase J -- the two small facts every Phase J item source needs and none may get wrong.
/// <list type="bullet">
/// <item><b>Service date</b> = the date of the instant in the <b>branch's own zone</b> (Asia/Dhaka for the Bangladesh branches), never UTC and never the
/// server's zone. A branch that no longer exists is a data fault and throws -- falling back to UTC would put an evening visit on the wrong day.</item>
/// <item><b>The customer to bill</b> = the customer the source recorded, or, if that customer was since merged into another (ADR-039), the survivor.
/// Visits are immutable records and keep the id they were finalised with, so the merge cannot have moved it; Billing follows the chain instead.</item>
/// </list>
/// </summary>
internal static class BillableSourceSupport
{
    private const int MaxMergeHops = 10;

    public static async Task<DateOnly> ServiceDateAsync(CatEcommerceDbContext db, Guid branchId, DateTimeOffset instant, CancellationToken ct)
    {
        var timeZoneId = await db.Set<BranchAggregate>().AsNoTracking()
            .Where(b => b.Id == branchId).Select(b => b.TimeZoneId).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The source's branch no longer exists, so its local service date cannot be computed.");

        return BranchCalendar.LocalDateOf(instant, timeZoneId);
    }

    public static async Task<Guid> BillableCustomerAsync(CatEcommerceDbContext db, Guid customerId, CancellationToken ct)
    {
        var current = customerId;
        for (var hop = 0; hop < MaxMergeHops; hop++)
        {
            var next = await db.Set<CustomerAccountAggregate>().AsNoTracking()
                .Where(c => c.Id == current).Select(c => c.MergedIntoCustomerId).FirstOrDefaultAsync(ct);
            if (next is null) return current;
            current = next.Value;
        }

        return current;
    }
}
