namespace CatEcommerce.Infrastructure.Persistence.ReportingViews;

// HO-RPT-001 section 8.4 -- the keyless row types of the phase B views. Properties map to the snake_case column of the same name (ReportViewConfiguration). No float or double anywhere (U-D4).

public sealed class RvClinicAppointmentRow
{
    public Guid AppointmentId { get; set; }
    public Guid BranchId { get; set; }
    public DateOnly? LocalDate { get; set; }
    public DateTimeOffset? StartsOn { get; set; }
    public DateTimeOffset? EndsOn { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid CatId { get; set; }
    public string? CatName { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int ServiceCategory { get; set; }
    public Guid? PerformerId { get; set; }
    public string? PerformerName { get; set; }
    public string? RoomName { get; set; }
    public int Channel { get; set; }
    public bool IsWalkIn { get; set; }
    public int TriagePriority { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? CheckedInOn { get; set; }
    public DateTimeOffset? StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public DateTimeOffset? CancelledOn { get; set; }
    public DateTimeOffset? NoShowRecordedOn { get; set; }
}

public sealed class RvCatVaccinationRow
{
    public Guid CatId { get; set; }
    public string CatName { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerMobile { get; set; }
    public string? OwnerEmail { get; set; }
    public int OwnerStatus { get; set; }
    public int CatLifeStatus { get; set; }
    public Guid VaccineId { get; set; }
    public string VaccineName { get; set; } = string.Empty;
    public bool VaccineActive { get; set; }
    public DateOnly AdministeredOn { get; set; }
    public DateOnly NextDueOn { get; set; }
}

public sealed class RvCatParasiteTreatmentRow
{
    public Guid CatId { get; set; }
    public string CatName { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerMobile { get; set; }
    public string? OwnerEmail { get; set; }
    public int OwnerStatus { get; set; }
    public int CatLifeStatus { get; set; }
    public int TreatmentType { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public DateOnly GivenOn { get; set; }
    public DateOnly NextDueOn { get; set; }
}

public sealed class RvCatLastBranchRow
{
    public Guid CatId { get; set; }
    public Guid BranchId { get; set; }
    public DateTimeOffset? LastAppointmentOn { get; set; }
}

public sealed class RvCareReminderSentRow
{
    public int SubjectKind { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? SubjectRef { get; set; }
    public DateOnly? SubjectDueOn { get; set; }
    public DateTimeOffset SentOn { get; set; }
}

public sealed class RvSurgeryRow
{
    public Guid SurgeryId { get; set; }
    public Guid BranchId { get; set; }
    public DateOnly LocalDate { get; set; }
    public DateTimeOffset OccurredOn { get; set; }
    public string ProcedureName { get; set; } = string.Empty;
    public int ProcedureCategory { get; set; }
    public bool IsSterilisation { get; set; }
    public Guid CatId { get; set; }
    public string? CatName { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid SurgeonId { get; set; }
    public string? SurgeonName { get; set; }
    public string? AssistantName { get; set; }
    public string? TheatreName { get; set; }
    public int? AsaGrade { get; set; }
    public int Status { get; set; }
    public int? Outcome { get; set; }
    public bool HasComplications { get; set; }
    public string? ComplicationsSummary { get; set; }
    public bool ConsentOverride { get; set; }
    public int ControlledDrugEntries { get; set; }
}

public sealed class RvVisitOpenRow
{
    public Guid VisitId { get; set; }
    public Guid BranchId { get; set; }
    public Guid VetId { get; set; }
    public int Status { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
    public DateTimeOffset? FinalisedOn { get; set; }
}
