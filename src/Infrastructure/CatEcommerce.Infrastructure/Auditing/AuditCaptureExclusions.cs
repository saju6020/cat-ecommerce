using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Aggregates.Visit;

namespace CatEcommerce.Infrastructure.Auditing;

/// <summary>
/// HO-NTF-001 §12.1 / ADR-066 -- the aggregate types <see cref="AuditChangeCaptureInterceptor"/>
/// never writes a <c>DataChange</c> row for, as data rather than an inline <c>if</c>. Amends
/// HO-ACC-001 §11.9 step 7 ("Never audit <c>AuditEntryAggregate</c>"): the audit root itself must
/// never be audited (it would recurse), and <see cref="NotificationRequestAggregate"/> /
/// <see cref="NotificationAggregate"/> must never be audited because the notification log IS the
/// record and, for a secret-bearing template, carries a live credential in flight (ADR-063) that
/// must never be copied into the immutable audit table, where it could never be removed. Template
/// edits ARE audited normally -- only these three roots are exempt.
/// </summary>
public static class AuditCaptureExclusions
{
    public static readonly IReadOnlyCollection<Type> Types =
    [
        typeof(AuditEntryAggregate),
        typeof(NotificationRequestAggregate),
        typeof(NotificationAggregate),
        // HO-XCT-001 section 5.3 / ADR-303 -- a retention run is itself an audit-grade log; one BusinessEvent summary row per run (RetentionRunFinishedEvent) replaces roughly seven DataChange rows a night.
        typeof(CatEcommerce.Domain.Aggregates.Operations.RetentionRunAggregate),
        // HO-XCT-001 section 5.3 / ADR-309 -- one row per page view must not write one audit row per page view; the batch is anonymous and holds no identity.
        typeof(CatEcommerce.Domain.Aggregates.Operations.WebVitalBatchAggregate),

        // HO-EMR-001 §8.4 / ADR-122 -- the per-author auto-saved working copy is overwritten every few
        // seconds and is not the record; the record's changes are audited when the note is saved or the
        // visit finalised. Never VisitAggregate (U-X2).
        typeof(ConsultationDraftAggregate),
        // HO-IMP-001 ADR-098 -- every ledger row already records who, what, when and why; auditing it twice would double the busiest table.
        typeof(StockLedgerEntryAggregate),
        typeof(StockReservationEntryAggregate),
    ];

    public static bool IsExcluded(object entity) => Types.Contains(entity.GetType());
}
