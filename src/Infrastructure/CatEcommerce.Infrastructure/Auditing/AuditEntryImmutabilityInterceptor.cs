using CatEcommerce.Domain.Aggregates.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CatEcommerce.Infrastructure.Auditing;

/// <summary>
/// HO-ACC-001 §11.9. Application-layer half of audit immutability -- the database trigger
/// (<c>AddAuditTrailIntegrity</c> migration) is the authoritative layer. Throws before SQL is
/// sent, on any <c>Modified</c> or <c>Deleted</c> <see cref="AuditEntryAggregate"/>.
/// </summary>
public sealed class AuditEntryImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Check(DbContext? context)
    {
        if (context is null) return;

        var offending = context.ChangeTracker.Entries<AuditEntryAggregate>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (offending)
            throw new AuditEntryImmutableException();
    }
}
