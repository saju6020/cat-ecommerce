using System.Text.Json;

namespace CatEcommerce.Api.Contracts.Boarding;

// HO-BRD-001 section 12.4 (Phase E) request bodies.

public sealed class BelongingRequest
{
    public string Kind { get; init; } = default!;
    public string Description { get; init; } = default!;
    public IReadOnlyList<Guid> FileIds { get; init; } = [];
}

/// <summary><c>strokes</c> is a JSON array of strokes, each a list of <c>[x, y]</c> integer pairs (0..10000, at most 32 KB) for a Drawn signature; omitted for Typed.</summary>
public sealed class SignatureRequest
{
    public string SignerName { get; init; } = default!;
    public string Method { get; init; } = default!;
    public JsonElement? Strokes { get; init; }
    public bool ConfirmedStatement { get; init; }
}

public sealed class CheckInCatRequest
{
    public Guid CatId { get; init; }
    public bool IdentityConfirmed { get; init; }
    public bool VaccinationProofVerified { get; init; }
    public decimal? WeightKg { get; init; }
    public string? ArrivalHealthObservation { get; init; }
    public IReadOnlyList<BelongingRequest> Belongings { get; init; } = [];
    public bool NoBelongings { get; init; }
    public SignatureRequest? Signature { get; init; }
}

public sealed class CheckInRequest
{
    public IReadOnlyList<CheckInCatRequest> Cats { get; init; } = [];
}

public sealed class CompleteCareTaskRequest
{
    public string? Note { get; init; }
}

public sealed class SkipCareTaskRequest
{
    public string Reason { get; init; } = default!;
}

public sealed class RecordObservationRequest
{
    public Guid CatId { get; init; }
    public string PortionEaten { get; init; } = default!;
    public string WaterIntake { get; init; } = default!;
    public string LitterOutput { get; init; } = default!;
    public string Mood { get; init; } = default!;
    public string? Notes { get; init; }
}

public sealed class FlagInfectiousRequest
{
    public string Note { get; init; } = default!;
    public Guid? TargetIsolationUnitId { get; init; }
}

public sealed class ClearInfectiousRequest
{
    public string? Note { get; init; }
}

public sealed class IsolationEntryRequest
{
    public string Purpose { get; init; } = default!;
}
