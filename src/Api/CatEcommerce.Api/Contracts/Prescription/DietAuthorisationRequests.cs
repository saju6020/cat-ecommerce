namespace CatEcommerce.Api.Contracts.Prescription;

// HO-PHM-001 §12.4 -- diet authorisation bodies. Dates are branch-local calendar dates (DateOnly): there is no instant, so no offset can reach a timestamptz column.
// The controller mints the authorisation id and takes the caller and branch from the session.

public sealed class AuthoriseDietRequest
{
    public Guid VisitId { get; init; }
    public Guid ProductId { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly ValidTo { get; init; }
    public string? Notes { get; init; }
}

public sealed class ExtendDietAuthorisationRequest
{
    public DateOnly ValidTo { get; init; }
}

public sealed class RevokeDietAuthorisationRequest
{
    public string Reason { get; init; } = "";
}
