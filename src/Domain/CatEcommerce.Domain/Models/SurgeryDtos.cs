using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Domain.Models;

// HO-SRG-001 §11.3 -- the inputs of SurgeryAggregate's mutation methods. Immutable records; the command handler maps a command to one of these.

public sealed record SurgeryScheduleDto(
    Guid BranchId, Guid CatId, Guid CustomerId, Guid ServiceId, ProcedureSnapshot Procedure, SurgeryPriceSnapshot Price,
    Guid AppointmentId, Guid VisitId, Guid TheatreId, Guid SurgeonId, Guid AssistantId,
    DateTimeOffset StartsOn, DateTimeOffset EndsOn, DateTimeOffset AdmitBy, string FastingInstructions,
    DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

/// <param name="NewLocalDate">The new start in the branch zone -- the date the date-bound checklist items are judged against.</param>
public sealed record SurgeryRescheduleDto(
    Guid TheatreId, Guid SurgeonId, Guid AssistantId, DateTimeOffset StartsOn, DateTimeOffset EndsOn, DateTimeOffset AdmitBy, string FastingInstructions,
    DateOnly NewLocalDate, int WeightValidityDays, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record SurgeryCancelDto(string? Reason, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record SurgeryAdmitDto(DateTimeOffset Now, string TimeZoneId, int EarlyAdmissionHours, UserContext UserContext, Guid CorrelationId);

/// <param name="ConsentRecorded">Re-read from the consent registry by the handler -- the checklist flag is never consulted (ADR-161).</param>
/// <param name="OverrideReason">Given only by a caller using the emergency override; ignored when consent is recorded.</param>
/// <param name="OverriderIsVet">Defence in depth: the endpoint already requires <c>Surgery.OverrideConsent</c> (Vet).</param>
public sealed record SurgeryStartDto(
    bool ConsentRecorded, string? OverrideReason, bool OverriderIsVet, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record SurgeryEndProcedureDto(ProcedureOutcome Outcome, DateTimeOffset Now, string TimeZoneId, UserContext UserContext, Guid CorrelationId);

public sealed record RecoveryStatusDto(RecoveryStatus Status, string? Note, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record PlannedFollowUpInput(Guid Id, SurgeryFollowUpKind Kind, DateOnly DueOn, DateOnly WindowFrom, DateOnly WindowTo);

public sealed record SurgeryDischargeDto(
    string? PostOpInstructions, IReadOnlyList<PlannedFollowUpInput> FollowUps, bool CatIsDeceased, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId, Guid? OwnerAtDischargeId = null);

public sealed record ChecklistCompleteDto(PreOpChecklistItemKind Kind, string? Note, Guid? ReferenceId, DateOnly? ReferenceDate, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record ChecklistWaiveDto(PreOpChecklistItemKind Kind, string? Reason, bool WaiverIsVet, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record AsaGradeDto(AsaGrade Grade, bool IsEmergency, bool CallerIsVet, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record FollowUpCloseDto(Guid FollowUpId, FollowUpStatus Status, string? Reason, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record SurgicalReportDraftDto(
    Guid CatId, Guid VisitId, Guid BranchId, string ProcedurePerformed, string Findings, string Technique, string SutureMaterial, bool HadComplications, string? Complications,
    Guid SurgeonId, Guid AssistantId, UserContext UserContext, Guid CorrelationId);

public sealed record SurgicalReportFinaliseDto(bool CallerIsVet, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);

public sealed record SurgicalReportAddendumDto(Guid AddendumId, string? Text, bool CallerIsVet, DateTimeOffset Now, UserContext UserContext, Guid CorrelationId);
