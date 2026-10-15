using CatEcommerce.Domain.ValueObjects;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-SRG-001 §11.7 G1 -- the inputs of the four AppointmentAggregate methods that keep a surgery appointment in step with its surgery.

public sealed record SurgeryAppointmentDto(
    Guid SurgeryId, Guid BranchId, Guid CustomerId, Guid CatId, Guid ServiceId, ServiceSnapshot ServiceSnapshot,
    Guid SurgeonId, Guid AssistantId, Guid TheatreId, DateTimeOffset StartsOn, DateTimeOffset EndsOn, UserContext UserContext);

public sealed record SurgeryAppointmentMoveDto(
    Guid SurgeryId, Guid SurgeonId, Guid AssistantId, Guid TheatreId, DateTimeOffset StartsOn, DateTimeOffset EndsOn, UserContext UserContext);

public sealed record SurgeryAppointmentCancelDto(Guid SurgeryId, string Reason, DateTimeOffset Now, UserContext UserContext);

public sealed record SurgeryAppointmentCompleteDto(Guid SurgeryId, DateTimeOffset Now, UserContext UserContext);
