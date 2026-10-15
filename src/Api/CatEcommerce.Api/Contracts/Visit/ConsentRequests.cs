namespace CatEcommerce.Api.Contracts.Visit;

/// <summary>HO-EMR-001 §12.4 -- <c>POST /api/staff/consents</c>. <c>SignedOn</c> is the server's time. An on-screen consent carries
/// <see cref="Signature"/> (strokes of [x, y] points in [0, 1]) and no file; a signed paper form carries <see cref="FileId"/> and
/// no signature.</summary>
public sealed class RecordConsentRequest
{
    public required Guid CatId { get; init; }
    public required Guid AppointmentId { get; init; }
    public required string Kind { get; init; }
    public required string ProcedureDescription { get; init; }
    public required string Method { get; init; }
    public decimal[][][]? Signature { get; init; }
    public Guid? FileId { get; init; }
    public required string SignedByName { get; init; }
    public string SignerRelationship { get; init; } = "Owner";
    public Guid? SignedByCustomerId { get; init; }
    public required int PolicyVersionNumber { get; init; }
    public Guid? WitnessStaffId { get; init; }
}

public sealed class WithdrawConsentRequest
{
    public required string Reason { get; init; }
}
