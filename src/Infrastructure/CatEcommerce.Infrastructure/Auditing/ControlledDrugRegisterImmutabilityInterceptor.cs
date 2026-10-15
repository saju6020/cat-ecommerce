using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CatEcommerce.Infrastructure.Auditing;

/// <summary>A programming error, never an expected outcome: something tried to modify or delete a controlled-drug register entry or its seal. The register is
/// append-only (HO-PHM-001 ADR-144); a correction is a new, linked entry.</summary>
public sealed class ControlledDrugRegisterImmutableException() : Exception(PharmacyMessages.RegisterAppendOnly);

/// <summary>
/// HO-PHM-001 §8.9 layer 2 (ADR-144, V2) -- the application half of the register's append-only guarantee, beside HO-ACC-001's
/// <see cref="AuditEntryImmutabilityInterceptor"/>. Throws <see cref="ControlledDrugRegisterImmutableException"/> before any SQL is sent on any
/// <c>Modified</c> or <c>Deleted</c> <see cref="ControlledDrugRegisterEntryAggregate"/> or <see cref="ControlledDrugRegisterSealAggregate"/>. The database
/// triggers (layer 3) are what is left if this is bypassed; the aggregate having no mutator (layer 1) is what makes reaching this a bug.
/// </summary>
public sealed class ControlledDrugRegisterImmutabilityInterceptor : SaveChangesInterceptor
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

        var offending =
            context.ChangeTracker.Entries<ControlledDrugRegisterEntryAggregate>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || context.ChangeTracker.Entries<ControlledDrugRegisterSealAggregate>().Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (offending) throw new ControlledDrugRegisterImmutableException();
    }
}
