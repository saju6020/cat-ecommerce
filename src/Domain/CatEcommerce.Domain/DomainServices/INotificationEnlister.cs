using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Notification;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.1/ADR-056 -- added beyond §8.13's own port list (recorded as a deviation in
/// HO-NTF-001 §1 Amended). <c>IAggregateRootRepository{T}.SaveAsync</c>/<c>UpdateAsync</c> always
/// call <c>SaveChangesAsync</c> immediately (`.ai/persistence-ef-core.md` §2); ADR-056's "expansion
/// and fallback commit several roots in one <c>SaveChangesAsync</c>" therefore needs one more root
/// added to the same scoped context WITHOUT saving -- exactly <c>INotificationOutbox.Enqueue</c>'s
/// own trick (F23), generalised so C4/C5/C7 can enlist a second (or third) new root -- a fallback
/// <see cref="NotificationAggregate"/>, a resend's audit <see cref="AuditEntryAggregate"/> -- ahead
/// of the one call that actually commits (the request's or the original notification's own
/// <c>UpdateAsync</c>/<c>SaveAsync</c>). Never call <c>SaveChangesAsync</c> from an implementation.
///
/// Gate fix 2026-09-27 (ARCHITECTURE MEDIUM). <c>Enlist(AggregateRoot)</c> was untyped -- an
/// unbounded unit-of-work back door any handler could use to enlist any root into someone else's
/// commit, widening ADR-056's own recorded exception far beyond what §11.4 C7 actually names.
/// Narrowed to exactly the two types this slice ever enlists.
/// </summary>
public interface INotificationEnlister
{
    void Enlist(NotificationAggregate notification);
    void Enlist(AuditEntryAggregate auditEntry);
}
