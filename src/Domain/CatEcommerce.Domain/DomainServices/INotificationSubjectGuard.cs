using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.7 -- one implementation per <see cref="NotificationSubjectKind"/>,
/// resolved by kind through <c>SubjectGuardResolver</c>. A guard reads; it never writes.</summary>
public interface INotificationSubjectGuard
{
    NotificationSubjectKind Kind { get; }

    /// <summary><paramref name="subjectStamp"/> -- HO-APT-001 §8.9 amendment: the request/notification's
    /// own copy of a producer-defined stamp (e.g. an appointment's <c>ScheduleStamp</c>), for a guard
    /// that needs to detect "the subject changed since this message was enqueued" beyond what
    /// <paramref name="subjectId"/>/<paramref name="subjectRef"/>/<paramref name="subjectDueOn"/> can
    /// express. Unused by a guard that does not need it (e.g. <c>CatVaccinationDueGuard</c>).</summary>
    Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default);
}
