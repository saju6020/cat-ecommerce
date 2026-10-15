namespace CatEcommerce.Api.Contracts.Boarding;

// HO-BRD-001 section 12.4 (Phase G) request bodies.

public sealed class ReferCatRequest
{
    public Guid CatId { get; init; }
    public string Reason { get; init; } = default!;

    /// <summary>A <c>TriagePriority</c> name: Routine, Urgent or Emergency.</summary>
    public string Urgency { get; init; } = default!;
}

public sealed class RequestVetApprovalRequest
{
    public Guid CatId { get; init; }
    public Guid? ReferralId { get; init; }

    /// <summary>Money as text, e.g. <c>"4500.00"</c>.</summary>
    public string Amount { get; init; } = default!;
    public string Description { get; init; } = default!;
}

/// <summary>The owner's answer (portal) or the answer staff record by phone (the note is then required).</summary>
public sealed class VetApprovalAnswerRequest
{
    public bool Approve { get; init; }
    public string? Note { get; init; }
}

public sealed class ExtendStayRequest
{
    public int ExpectedVersion { get; init; }
    public DateOnly NewDepartureOn { get; init; }
}

public sealed class RelocateStayRequest
{
    public int ExpectedVersion { get; init; }
    public Guid TargetUnitId { get; init; }
    public DateOnly EffectiveFromLocalDate { get; init; }
    public string Reason { get; init; } = default!;
    public DateOnly? NewDepartureOn { get; init; }
}

public sealed class BelongingReturnRequest
{
    public Guid BelongingId { get; init; }
    public bool Returned { get; init; }
    public string? Note { get; init; }
}

public sealed class CheckOutCatRequest
{
    public Guid CatId { get; init; }
    public decimal? WeightKg { get; init; }
    public IReadOnlyList<BelongingReturnRequest> Belongings { get; init; } = [];
    public SignatureRequest? Signature { get; init; }
}

public sealed class CheckOutRequest
{
    public IReadOnlyList<CheckOutCatRequest> Cats { get; init; } = [];
}

public sealed class ClearAttentionRequest
{
    public string Note { get; init; } = default!;
}
