using CatEcommerce.Domain.Aggregates.Boarding;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-BRD-001 section 8.19 -- the send-time guard of <c>boarding.daily-update</c> (subject <c>BoardingStayInHouse</c>): valid only while the stay is <c>CheckedIn</c> and still the recipient's.
/// A stay checked out at 17:00 gets no 18:00 update. Anything unreadable is "not valid".
/// </summary>
public sealed class BoardingStayInHouseGuard(IAggregateRootRepository<BoardingStayAggregate> stayRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.BoardingStayInHouse;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null) return false;

        var stay = await stayRepository.GetByIdAsync(subjectId.Value);
        return stay is not null && stay.CustomerId == recipientId && stay.Status == BoardingStayStatus.CheckedIn;
    }
}
