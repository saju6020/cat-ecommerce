namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §11.4 C7/§12.6 -- added beyond §8.13's own port list (recorded as a deviation in the
/// implementation report). The platform's <c>IAggregateRootRepository&lt;T&gt;</c> has no
/// count-by-filter method (only <c>ExistAsync</c>, which proves "at least one"), so counting a
/// resend chain's prior resends needs a small dedicated read. Shared by C7 and the read-side
/// <c>cannotResendReason</c> computation so the two cannot disagree (§12.6).
/// </summary>
public interface INotificationResendCounter
{
    Task<int> CountResendsAsync(Guid chainRootId, CancellationToken ct = default);
}
