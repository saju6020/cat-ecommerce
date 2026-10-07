using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Domain.Events.Clinic;

/// <summary>HO-APT-001 §8.11/§11.5.</summary>
public sealed class WaitlistJoinedEvent : Event
{
    public required Guid WaitlistEntryId { get; init; }
    public required Guid BranchId { get; init; }
}

public sealed class WaitlistSlotOfferedEvent : Event
{
    public required Guid WaitlistEntryId { get; init; }
    public required Guid AppointmentId { get; init; }
}

public sealed class WaitlistOfferLapsedEvent : Event
{
    public required Guid WaitlistEntryId { get; init; }
}
