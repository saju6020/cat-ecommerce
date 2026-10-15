using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-EMR-001 §8.9. The handler supplies the cross-aggregate facts: the appointment's cat and status,
/// the cat's life status, and the <b>current published</b> version of the consent form for the kind
/// (<see cref="CurrentPolicyVersionNumber"/>, <c>null</c> when none is published). The aggregate
/// checks the evidence (<see cref="Signature"/> xor <see cref="FileId"/>) and that the version signed
/// is the current one.
/// </summary>
public sealed record ConsentRecordingDto(
    Guid CatId,
    Guid AppointmentId,
    Guid AppointmentCatId,
    AppointmentStatus AppointmentStatus,
    Guid BranchId,
    CatLifeStatus CatLifeStatus,
    ConsentKind Kind,
    string ProcedureDescription,
    ConsentMethod Method,
    SignatureStrokes? Signature,
    Guid? FileId,
    string SignedByName,
    SignerRelationship SignerRelationship,
    Guid? SignedByCustomerId,
    string PolicyKey,
    int PolicyVersionNumber,
    int? CurrentPolicyVersionNumber,
    Guid CapturedBy,
    Guid? WitnessStaffId,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

public sealed record ConsentWithdrawalDto(string Reason, Guid WithdrawnBy, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);
