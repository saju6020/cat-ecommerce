using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

/// <summary>HO-EMR-001 §8.2 -- the handler passes the appointment's own status and the cat's life
/// status (cross-aggregate facts the visit cannot see), and the service snapshot from the
/// appointment's <c>ServiceSnapshot</c>.</summary>
public sealed record VisitOpeningDto(
    Guid BranchId,
    Guid AppointmentId,
    Guid CatId,
    AppointmentStatus AppointmentStatus,
    CatLifeStatus CatLifeStatus,
    Guid ClinicServiceId,
    string ServiceName,
    ServiceCategory ServiceCategory,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>HO-EMR-001 §8.3. Every measurement is optional but at least one must be present.
/// <see cref="ConfirmedOutOfRange"/> lists the signs the caller has seen the plausible-range warning
/// for and confirmed (ADR-120).</summary>
public sealed record VitalsDto(
    Guid VitalsEntryId,
    DateTimeOffset MeasuredOn,
    decimal? WeightKg,
    decimal? TemperatureC,
    int? HeartRateBpm,
    int? RespiratoryRatePerMin,
    int? BodyConditionScore,
    int? PainScore,
    IReadOnlyList<VitalSign> ConfirmedOutOfRange,
    string? Note,
    Guid RecordedBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>One problem of a note save. <see cref="Code"/>/<see cref="Name"/>/<see cref="CodeIsActive"/>
/// are the handler's snapshot of the catalogue entry (U-C2).</summary>
public sealed record ProblemDto(
    Guid ProblemId,
    Guid DiagnosisCodeId,
    string Code,
    string Name,
    bool CodeIsActive,
    ProblemCertainty Certainty,
    string? Note);

public sealed record SoapNoteDto(
    string Subjective,
    string Objective,
    string Assessment,
    string Plan,
    string DischargeInstructions,
    IReadOnlyList<ProblemDto> Problems,
    Guid? AppliedTemplateId,
    int? AppliedTemplateVersion,
    Guid SavedBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary><see cref="FinaliserRoles"/> are the caller's staff roles, read by the handler from the
/// caller's own staff account -- the aggregate re-checks them (ADR-119). <see cref="CustomerId"/> is the cat's
/// owner <b>at this moment</b> (HO-BIL-001 Phase J, owner decision 2026-10-14): the handler reads it from the
/// cat, the visit stores it, and it is the customer Billing invoices.</summary>
public sealed record VisitFinalisationDto(
    IReadOnlyList<string> FinaliserRoles,
    Guid FinalisedBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId,
    Guid CustomerId);

public sealed record ClinicalPhotoDto(
    Guid PhotoId,
    Guid FileId,
    DateOnly TakenOn,
    string? Note,
    Guid AddedBy,
    DateOnly Today,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary><see cref="Today"/> is the branch-local date (the handler computes it with
/// <c>BranchCalendar.LocalDateOf</c>); <see cref="AppointmentValid"/> is the handler's verdict on a
/// linked appointment (Booked, in the future, this cat's).</summary>
public sealed record FollowUpDto(
    FollowUpKind Kind,
    DateOnly DueOn,
    Guid? AppointmentId,
    bool AppointmentValid,
    DateOnly Today,
    Guid SetBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

public sealed record FollowUpClearDto(DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <summary><see cref="VisitStatus"/> is the visit's own status, passed by the handler (§8.4).</summary>
public sealed record VisitAddendumDto(
    Guid VisitId,
    Guid CatId,
    VisitStatus VisitStatus,
    string Text,
    string? Reason,
    Guid AuthorStaffId,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

public sealed record ConsultationDraftDto(
    Guid VisitId,
    Guid AuthorStaffId,
    VisitStatus VisitStatus,
    string Subjective,
    string Objective,
    string Assessment,
    string Plan,
    string DischargeInstructions,
    IReadOnlyList<Guid> ProblemCodeIds,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);

/// <summary>
/// HO-EMR-001 §8.6 -- a treatment as the handler has resolved it: the SKU, batch, deducted and shortfall quantities and the price snapshots are
/// the handler's (cross-aggregate facts the visit cannot see); the aggregate checks their coherence.
/// </summary>
public sealed record TreatmentDto(
    Guid LineId,
    TreatmentKind Kind,
    string Description,
    Guid? ClinicServiceId,
    Guid? SkuId,
    string? SkuName,
    string? StockUnit,
    Guid? BatchId,
    string? LotNumber,
    DateOnly? BatchExpiresOn,
    Guid? LocationId,
    decimal Quantity,
    decimal DeductedQuantity,
    decimal ShortfallQuantity,
    string? Dose,
    string? Route,
    bool IsBillable,
    Money? UnitPrice,
    Guid? TaxClassId,
    string? BatchOverrideReason,
    Guid RecordedBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId,
    int Position = 0);

public sealed record TreatmentReversalDto(
    Guid LineId,
    string Reason,
    Guid ReversedBy,
    DateTimeOffset Now,
    UserContext UserContext,
    Guid CorrelationId);
