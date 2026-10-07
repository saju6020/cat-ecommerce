namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §12.2 -- added beyond §8.13's own port list (recorded as a deviation in the
/// implementation report) so C5 can read <c>Worker:SendTimeoutSeconds</c> without depending on
/// Infrastructure's own <c>NotificationOptions</c>. Implemented by Infrastructure, backed by
/// <c>IOptions&lt;NotificationOptions&gt;</c>.
/// </summary>
public interface INotificationDeliverySettings
{
    int SendTimeoutSeconds { get; }

    /// <summary>Gate fix 2026-09-27 (SECURITY/CODE_REVIEW LOW). <c>DeliverNotificationCommandHandler</c>
    /// used to hard-code the claim lease to 120 seconds, ignoring <c>Notifications:Worker:LeaseSeconds</c>
    /// entirely -- a deployment that raised <c>SendTimeoutSeconds</c> above 120 could then have the
    /// lease expire while a send was still in flight, letting a second poll re-claim and re-send.</summary>
    int LeaseSeconds { get; }
}
