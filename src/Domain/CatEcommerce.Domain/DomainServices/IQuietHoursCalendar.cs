namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.14 -- loads the <c>notifications.quietHours</c> setting for the given
/// branch (else the main branch) and the branch's IANA zone, and calls the pure <c>QuietHours</c>.
/// Implemented in Infrastructure (<c>QuietHoursCalendar</c>).</summary>
public interface IQuietHoursCalendar
{
    Task<DateTimeOffset?> NextAllowedInstantAsync(DateTimeOffset now, Guid? branchId, CancellationToken ct = default);
}
