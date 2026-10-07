using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Auditing;

/// <summary>
/// HO-ACC-001 §11.9 (US-ACC-12). Every aggregate change is audited in the <b>same</b> transaction:
/// this interceptor adds one <see cref="AuditEntryAggregate"/> to the same <see cref="DbContext"/>
/// before <c>SaveChangesAsync</c> actually runs, so it commits atomically with whatever it audits
/// (`.ai/domain-events-and-outbox.md` §3 -- a handler-written audit would fail AFTER the change
/// committed, which SRS C-9 forbids for this category).
///
/// The actor and correlation id are read from the aggregate's own freshly-raised domain event
/// (<c>AggregateRoot.Events</c>, not yet cleared at this point in the repository's save sequence)
/// rather than from a separately-injected <c>IUserContextProvider</c> snapshot, so a DataChange row
/// and its sibling BusinessEvent/AuthenticationEvent row (written by an event handler off the same
/// event) always share the same <c>CorrelationId</c> (§11.6). Falls back to the ambient user
/// context only for the rare aggregate save that raised no event.
/// </summary>
public sealed class AuditChangeCaptureInterceptor(
    IClientIpAccessor clientIpAccessor, IUserContextProvider fallbackUserContextProvider, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    private static readonly HashSet<string> ExcludedRootProperties = new(StringComparer.Ordinal)
    {
        "Version", "LastUpdatedBy", "LastUpdatedDate", "Metadata", "Events", "xmin"
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext context)
    {
        // ACCEPTANCE gate, HIGH (A28, 2026-09-25): a Deleted entry must be seen too -- an owned
        // VALUE OBJECT that is wholesale replaced (LoginThrottle, EmailAddress, MobileNumber,
        // PasswordCredential) is tracked by EF as a Deleted instance (the OLD value) plus a
        // separate Added instance (the NEW value), never as one Modified instance in place. Roots
        // are never hard-deleted in this domain (accounts are anonymised, sessions are ended), so
        // filtering root candidates to Added/Modified below is still correct.
        var changed = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        var rootEntries = changed
            .Where(e => e.State is EntityState.Added or EntityState.Modified)
            .Where(e => e.Entity is AggregateRoot && !AuditCaptureExclusions.IsExcluded(e.Entity))
            .ToList();

        foreach (var rootEntry in rootEntries)
        {
            var root = (AggregateRoot)rootEntry.Entity;
            var domainEvent = root.Events.FirstOrDefault();
            var isAnonymisation = domainEvent is CustomerAccountAnonymisedEvent;
            var isInsert = rootEntry.State == EntityState.Added;

            var ownedEntries = changed.Where(e => e != rootEntry && e.Metadata.IsOwned() && BelongsTo(e, rootEntry)).ToList();

            // "Before" must never show a value that did not exist yet: exclude Added owned
            // entries (the NEW half of a replaced value object, or a brand-new collection member)
            // and, for a brand-new root, skip building a before-snapshot at all -- there is no
            // "before" for something that was just inserted.
            var beforeOwnedEntries = ownedEntries.Where(e => e.State != EntityState.Added).ToList();

            // "After" must never show a value that no longer exists: exclude Deleted owned
            // entries (the OLD half of a replaced value object, or a removed collection member).
            var afterOwnedEntries = ownedEntries.Where(e => e.State != EntityState.Deleted).ToList();

            var before = isAnonymisation || isInsert ? null : BuildSnapshot(rootEntry, beforeOwnedEntries, useCurrent: false);
            var after = BuildSnapshot(rootEntry, afterOwnedEntries, useCurrent: true);

            var actorUserContext = domainEvent?.UserContext ?? fallbackUserContextProvider.GetUserContext();
            var correlationId = domainEvent?.CorrelationId ?? Guid.NewGuid();
            var action = domainEvent?.GetType().Name ?? $"{root.GetType().Name}Changed";

            var auditEntry = new AuditEntryAggregate(Guid.NewGuid());
            auditEntry.Record(new AuditEntryDto(
                EntityType: root.GetType().Name,
                EntityId: root.Id,
                Action: action,
                Reason: action,
                ActorUserId: actorUserContext.UserId,
                OccurredOn: timeProvider.GetUtcNow(),
                BeforeValue: before,
                AfterValue: after,
                UserContext: actorUserContext,
                CorrelationId: correlationId,
                Category: AuditCategory.DataChange,
                IpAddress: clientIpAccessor.GetClientIpAddress()));

            context.Add(auditEntry);
        }
    }

    private static string BuildSnapshot(EntityEntry rootEntry, List<EntityEntry> ownedEntries, bool useCurrent)
    {
        var snapshot = new Dictionary<string, object?>();
        CollectProperties(rootEntry, snapshot, useCurrent, prefix: "");

        foreach (var owned in ownedEntries)
            CollectProperties(owned, snapshot, useCurrent, prefix: owned.Metadata.ClrType.Name + ".");

        return JsonSerializer.Serialize(snapshot);
    }

    private static void CollectProperties(EntityEntry entry, Dictionary<string, object?> snapshot, bool useCurrent, string prefix)
    {
        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (prefix.Length == 0 && ExcludedRootProperties.Contains(name)) continue;

            // ACCEPTANCE gate, HIGH (A28): the caller now filters WHICH entries reach a "before"
            // build at all -- an Added entry (root or owned) is never passed in with
            // useCurrent: false, so this can read OriginalValue unconditionally there and still be
            // correct; reading CurrentValue for an Added entry (the previous behaviour) is what
            // made "before" equal "after" for every insert and every replaced value object.
            var value = useCurrent ? property.CurrentValue : property.OriginalValue;

            snapshot[prefix + name] = AuditRedactionRules.IsSecret(name) ? "[redacted]" : value;
        }
    }

    /// <summary>Matches an owned entry to its root by comparing the ownership foreign key's
    /// current value to the root's primary key -- correct even when more than one aggregate root
    /// is saved in the same transaction (ADR-033's sign-in exception).</summary>
    private static bool BelongsTo(EntityEntry ownedEntry, EntityEntry rootEntry)
    {
        var ownership = ownedEntry.Metadata.FindOwnership();
        if (ownership is null || ownership.PrincipalEntityType != rootEntry.Metadata) return false;

        var fkProperties = ownership.Properties;
        var pkProperties = ownership.PrincipalKey.Properties;

        for (var i = 0; i < fkProperties.Count; i++)
        {
            var fkValue = ownedEntry.Property(fkProperties[i].Name).CurrentValue;
            var pkValue = rootEntry.Property(pkProperties[i].Name).CurrentValue;
            if (!Equals(fkValue, pkValue)) return false;
        }

        return true;
    }
}
