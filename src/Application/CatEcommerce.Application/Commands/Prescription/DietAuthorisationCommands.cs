using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Prescription;

// HO-PHM-001 §11.1 C17-C19 (US-PHM-08, BR-2, BR-6, ADR-149). Ids are minted in the controller; every command is dispatched with ValidateCommand = true (F3).

/// <summary>C17 -- a vet authorises a named therapeutic food for ONE cat from an OPEN visit, for a period of branch-local dates. Vet only (BR-6). The owner is told.</summary>
public sealed class AuthoriseDietCommand : Command
{
    public required Guid AuthorisationId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid VisitId { get; init; }
    public required Guid ProductId { get; init; }
    public required DateOnly ValidFrom { get; init; }
    public required DateOnly ValidTo { get; init; }
    public string? Notes { get; init; }

    /// <summary>The caller's active branch (a visit of another branch is "not found").</summary>
    public required Guid BranchId { get; init; }
}

/// <summary>C18 -- extend an active authorisation, or one that lapsed within the last 30 days.</summary>
public sealed class ExtendDietAuthorisationCommand : Command
{
    public required Guid AuthorisationId { get; init; }
    public required DateOnly ValidTo { get; init; }
}

/// <summary>C18 -- revoke an active authorisation with a reason.</summary>
public sealed class RevokeDietAuthorisationCommand : Command
{
    public required Guid AuthorisationId { get; init; }
    public required string Reason { get; init; }
}

/// <summary>C19 -- the worker records that an authorisation's last day has passed, raises the event and tells the owner (the cat's CURRENT owner). Idempotent. The gate locked the
/// product the moment the date passed, whether or not this ran (ADR-149).</summary>
public sealed class ExpireDietAuthorisationCommand : Command
{
    public required Guid AuthorisationId { get; init; }
}

/// <summary>The answer to a successful authorisation.</summary>
public sealed record AuthorisedDiet(Guid AuthorisationId);
