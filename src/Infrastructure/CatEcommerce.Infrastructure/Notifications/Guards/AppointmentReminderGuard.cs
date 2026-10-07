using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-APT-001 §8.9 -- valid while the appointment (<paramref name="subjectId"/>) is
/// <see cref="AppointmentStatus.Booked"/> and its <c>ScheduleStamp</c> equals the request's own
/// <paramref name="subjectStamp"/>. A reschedule, move or cancellation changes or clears the stamp,
/// so a stale reminder fails this guard and is never sent (ADR-074). Re-checked at expansion and
/// again immediately before each delivery attempt (§8.7).
/// </summary>
public sealed class AppointmentReminderGuard(IAggregateRootRepository<AppointmentAggregate> appointmentRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.AppointmentReminder;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectStamp is null) return false;

        var appointment = await appointmentRepository.GetByIdAsync(subjectId.Value);
        if (appointment is null || appointment.CustomerId != recipientId) return false;

        return appointment.Status == AppointmentStatus.Booked && string.Equals(appointment.ScheduleStamp, subjectStamp, StringComparison.Ordinal);
    }
}
