using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;

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
    ];

    public static bool IsExcluded(object entity) => Types.Contains(entity.GetType());
}
