using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Boarding;

// HO-BRD-001 section 11.1 C18-C19 (Phase F).

public sealed class AddStayMediaCommand : Command
{
    public required Guid MediaId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public Guid? CatId { get; init; }
    public Guid? IncidentId { get; init; }

    /// <summary>The HO-FIL-001 stored file (purpose BoardingMedia), uploaded by the caller moments ago.</summary>
    public required Guid FileId { get; init; }
    public string? Caption { get; init; }
    public required DateOnly CapturedOnLocalDate { get; init; }
}

public sealed class HideStayMediaCommand : Command
{
    public required Guid MediaId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}

public sealed class RecordIncidentCommand : Command
{
    public required Guid IncidentId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }
    public required string Severity { get; init; }
    public required string Description { get; init; }
    public required string ImmediateAction { get; init; }
    public required DateTimeOffset OccurredOn { get; init; }
}

/// <summary>The owner acknowledges from the portal (own incident only).</summary>
public sealed class AcknowledgeIncidentCommand : Command
{
    public required Guid IncidentId { get; init; }
    public required Guid StayId { get; init; }
    public required Guid CustomerId { get; init; }
}

/// <summary>Staff record that the owner acknowledged by phone.</summary>
public sealed class RecordIncidentAcknowledgementCommand : Command
{
    public required Guid IncidentId { get; init; }
    public required Guid BranchId { get; init; }
}

/// <summary>Worker only.</summary>
public sealed class EscalateIncidentCommand : Command
{
    public required Guid IncidentId { get; init; }
}

public sealed class RecordIncidentPhoneCallCommand : Command
{
    public required Guid IncidentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Outcome { get; init; }
    public bool OwnerReached { get; init; }
}

public sealed class ResolveIncidentCommand : Command
{
    public required Guid IncidentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Resolution { get; init; }
}
