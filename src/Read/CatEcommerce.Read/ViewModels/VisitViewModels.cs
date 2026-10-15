using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Read.ViewModels;

// HO-EMR-001 §12.4 -- the contract HO-EMR-002 consumes. JSON camelCase; instants ISO-8601 with offset;
// local dates yyyy-MM-dd; every measurement travels as { value, unit } (UI-8, value a string at the stated
// precision); every view carries branchTimeZoneId. All text is plain text.

public sealed record VisitAppointmentViewModel(Guid Id, string Status, DateTimeOffset? StartsOn, string? PerformerName);

public sealed record VisitServiceViewModel(Guid Id, string Name, string Category);

public sealed record VisitNoteViewModel(
    string Subjective, string Objective, string Assessment, string Plan, string DischargeInstructions,
    DateTimeOffset? SavedOn, string? SavedBy, Guid? AppliedTemplateId, int? AppliedTemplateVersion);

public sealed record VisitProblemViewModel(
    Guid Id, Guid DiagnosisCodeId, string Code, string Name, string Certainty, string? Note);

public sealed record VitalsEntryViewModel(
    Guid Id, DateTimeOffset MeasuredOn,
    Measurement? Weight, Measurement? Temperature, Measurement? HeartRate, Measurement? RespiratoryRate,
    Measurement? BodyConditionScore, Measurement? PainScore,
    IReadOnlyList<string> ConfirmedOutOfRange, string? Note, string RecordedBy, DateTimeOffset RecordedOn);

/// <summary>§12.4: a photograph carries a short-lived signed <c>url</c> and <c>thumbnailUrl</c> (no server-side thumbnail in v1, so
/// the two are the same signed file).</summary>
public sealed record VisitPhotoViewModel(
    Guid Id, DateOnly TakenOn, string? Note, DateTimeOffset AddedOn, string? Url, string? ThumbnailUrl, string? ContentType, DateTimeOffset? UrlExpiresOn);

public sealed record VisitFollowUpViewModel(string Kind, DateOnly DueOn, Guid? AppointmentId, DateTimeOffset SetOn);

public sealed record VisitAddendumViewModel(Guid Id, string Text, string? Reason, string Author, DateTimeOffset AddedOn);

public sealed record VisitDiagnosticSummaryViewModel(Guid Id, string Kind, string Status, DateTimeOffset OrderedOn, int ResultSetCount, bool HasAbnormal);

public sealed record VisitTemplateViewModel(
    Guid Id, int Version, string Name, string Subjective, string Objective, string Assessment, string Plan, string DischargeInstructions);

/// <summary>The caller's own auto-saved working copy (§8.4), returned only when newer than the saved note
/// and only while the visit is open.</summary>
public sealed record VisitDraftViewModel(
    string Subjective, string Objective, string Assessment, string Plan, string DischargeInstructions,
    IReadOnlyList<Guid> ProblemCodeIds, DateTimeOffset SavedOn);

/// <summary>§12.4 <c>GET /api/staff/visits/{visitId}</c>. <see cref="Treatments"/> are the visit treatment lines (US-CLN-15), reversed ones
/// included and marked, oldest first.</summary>
public sealed record VisitDetailViewModel(
    Guid Id, int Version, string Status, Guid CatId, string CatName,
    VisitAppointmentViewModel Appointment, VisitServiceViewModel Service,
    DateTimeOffset OpenedOn, string OpenedBy, string BranchTimeZoneId,
    IReadOnlyList<string> AllowedActions,
    VisitNoteViewModel Note,
    IReadOnlyList<VisitProblemViewModel> Problems,
    IReadOnlyList<VitalsEntryViewModel> Vitals,
    IReadOnlyList<VisitTreatmentViewModel> Treatments,
    IReadOnlyList<VisitPhotoViewModel> Photos,
    VisitFollowUpViewModel? FollowUp,
    IReadOnlyList<VisitAddendumViewModel> Addenda,
    IReadOnlyList<VisitDiagnosticSummaryViewModel> Diagnostics,
    VisitTemplateViewModel? Template,
    VisitDraftViewModel? Draft,
    DateTimeOffset? FinalisedOn, string? FinalisedBy);

/// <summary>§12.4 <c>GET /api/staff/visits</c> -- the open-visit list of the active branch.</summary>
public sealed record VisitListCatViewModel(Guid Id, string Name, bool IsHighRisk);

public sealed record VisitListItemViewModel(
    Guid Id, Guid AppointmentId, VisitListCatViewModel Cat, string Service,
    DateTimeOffset OpenedOn, string Status, bool HasVitals, bool HasNote);

/// <summary>§12.4 <c>GET /api/staff/appointments/{id}/visit</c>.</summary>
public sealed record VisitReferenceViewModel(Guid? VisitId, string? Status);

/// <summary>§8.3 -- one row of the server's range table, so a client warns from the server's numbers.</summary>
public sealed record VitalSignRuleViewModel(
    string Sign, string Unit, string AbsoluteMin, string AbsoluteMax, string? PlausibleMin, string? PlausibleMax, int DecimalPlaces);
