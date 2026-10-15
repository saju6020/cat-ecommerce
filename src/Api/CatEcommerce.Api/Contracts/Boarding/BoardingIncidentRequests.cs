namespace CatEcommerce.Api.Contracts.Boarding;

// HO-BRD-001 section 12.4 (Phase F) request bodies.

public sealed class AddStayMediaRequest
{
    public Guid FileId { get; init; }
    public Guid? CatId { get; init; }
    public Guid? IncidentId { get; init; }
    public string? Caption { get; init; }

    /// <summary>Branch-local date the photo or video was taken.</summary>
    public DateOnly CapturedOnLocalDate { get; init; }
}

public sealed class HideStayMediaRequest
{
    public string Reason { get; init; } = default!;
}

public sealed class RecordIncidentRequest
{
    public Guid CatId { get; init; }
    public string Kind { get; init; } = default!;
    public string Severity { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string ImmediateAction { get; init; } = default!;
    public DateTimeOffset OccurredOn { get; init; }
}

public sealed class RecordIncidentPhoneCallRequest
{
    public string Outcome { get; init; } = default!;
    public bool OwnerReached { get; init; }
}

public sealed class ResolveIncidentRequest
{
    public string Resolution { get; init; } = default!;
}
