using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Application.Commands.Visit;

// HO-EMR-001 §11.1 C19-C20 (Phase H). A consent is data -- an on-screen signature is stroke coordinates, not an image -- so it
// needs no upload and no scan; a signed paper form is a stored file referenced by id (§4.4: never bytes in a command).

/// <summary>C19 -- records consent for a procedure. <c>SignedOn</c> is the server's now. <see cref="PolicyVersionNumber"/> is the
/// consent-form version the signer was shown; it must be the current one.</summary>
public sealed class RecordConsentCommand : Command
{
    public required Guid ConsentId { get; init; }
    public required Guid CatId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Kind { get; init; }
    public required string ProcedureDescription { get; init; }
    public required string Method { get; init; }

    /// <summary><c>strokes[stroke][point][x, y]</c>, coordinates in [0, 1]; bounds are the value object's, not the validator's.</summary>
    public decimal[][][]? Signature { get; init; }
    public Guid? FileId { get; init; }
    public required string SignedByName { get; init; }
    public required string SignerRelationship { get; init; }
    public Guid? SignedByCustomerId { get; init; }
    public required int PolicyVersionNumber { get; init; }
    public Guid? WitnessStaffId { get; init; }
}

/// <summary>C20 -- withdraws a recorded consent. The record stays; its status changes and who withdrew it, when and why is kept.</summary>
public sealed class WithdrawConsentCommand : Command
{
    public required Guid ConsentId { get; init; }
    public required Guid BranchId { get; init; }
    public required string Reason { get; init; }
}
